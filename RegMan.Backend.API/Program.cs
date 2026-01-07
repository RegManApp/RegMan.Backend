using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using RegMan.Backend.API.Common;
using RegMan.Backend.API.Hubs;
using RegMan.Backend.API.Middleware;
using RegMan.Backend.API.Seeders;
using RegMan.Backend.API.Services;
using RegMan.Backend.BusinessLayer;
using RegMan.Backend.BusinessLayer.Contracts;
using RegMan.Backend.BusinessLayer.Helpers;
using RegMan.Backend.BusinessLayer.Services;
using RegMan.Backend.DAL;
using RegMan.Backend.DAL.DataContext;
using RegMan.Backend.DAL.Entities;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Xml.Linq;
using static System.Net.WebRequestMethods;

namespace RegMan.Backend.API
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // ==================
            // MonsterASP/IIS config fallback
            // ==================
            // Some shared IIS hosts do not expose "secrets" as real process environment variables.
            // They may instead write them into the generated web.config as either:
            // - <configuration><appSettings><add key="..." value="..."/></appSettings>
            // - <configuration><system.webServer><aspNetCore><environmentVariables>...</environmentVariables>
            // We ingest those values into IConfiguration early so DI services can read them.
            TryAddWebConfigSecrets(builder);

            // ==================
            // CORS Policy
            // ==================
            var allowedOrigins = new[]
            {
                "https://regman.app",
                "https://www.regman.app",
                "https://regman.pages.dev",
                "http://localhost:5173",
                "https://localhost:5173/",
                "http://localhost:5174",
                "https://localhost:5174",
                "http://localhost:5236",
                "http://localhost:3000",
                "https://localhost:7025"
            };
            builder.Services.AddCors(options =>
            {
                options.AddPolicy("AllowRegman", policy =>
                {
                    policy.WithOrigins(allowedOrigins)
                        .AllowAnyHeader()
                        .AllowAnyMethod()
                        .AllowCredentials();
                });
            });


            //Add Websocket SignalR 
            builder.Services.AddSignalR();

            // Realtime notifications publisher (BusinessLayer depends on interface only)
            builder.Services.AddScoped<INotificationRealtimePublisher, SignalRNotificationRealtimePublisher>();

            // Scheduled notification dispatcher (in-app reminders)
            builder.Services.AddHostedService<ScheduledNotificationDispatcherHostedService>();
            // =========================
            // Database + Business Layer
            // =========================
            builder.Services.AddDataBaseLayer(builder.Configuration);
            builder.Services.AddBusinessServices();

            // Institution metadata (used in transcript headers)
            builder.Services.Configure<InstitutionSettings>(builder.Configuration.GetSection("Institution"));

            // ==================
            // HttpContext Accessor (IMPORTANT for Audit Logs)
            // ==================
            builder.Services.AddHttpContextAccessor();

            // ========
            // Identity
            // ========
            builder.Services.AddIdentity<BaseUser, IdentityRole>(options =>
            {
                options.Password.RequireDigit = true;
                options.Password.RequireLowercase = true;
                options.Password.RequireUppercase = true;
                options.Password.RequireNonAlphanumeric = true;
                options.Password.RequiredLength = 8;

                // Minimal brute-force protection (DB-backed via Identity)
                options.Lockout.AllowedForNewUsers = true;
                options.Lockout.MaxFailedAccessAttempts = 5;
                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);

                options.User.RequireUniqueEmail = true;
            })
            .AddEntityFrameworkStores<AppDbContext>()
            .AddDefaultTokenProviders();

            // =================
            // JWT Authentication
            // =================
            var jwtKey = builder.Configuration["Jwt:Key"]; // supports env var: Jwt__Key
            if (string.IsNullOrWhiteSpace(jwtKey) || jwtKey.Contains("SUPER_SECRET_KEY", StringComparison.OrdinalIgnoreCase) || jwtKey.Length < 32)
            {
                throw new InvalidOperationException(
                    "JWT signing key is missing/weak. Configure a strong secret via environment variable 'Jwt__Key' (>= 32 chars)."
                );
            }

            var key = Encoding.UTF8.GetBytes(jwtKey);

            builder.Services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultScheme = JwtBearerDefaults.AuthenticationScheme;
            })
            .AddJwtBearer(options =>
            {
                options.SaveToken = true;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,

                    ValidIssuer = builder.Configuration["Jwt:Issuer"],
                    ValidAudience = builder.Configuration["Jwt:Audience"],
                    IssuerSigningKey = new SymmetricSecurityKey(key),

                    NameClaimType = ClaimTypes.NameIdentifier,
                    RoleClaimType = ClaimTypes.Role
                };
                options.Events = new JwtBearerEvents
                {
                    OnMessageReceived = context =>
                    {
                        var accessToken = context.Request.Query["access_token"];
                        var path = context.HttpContext.Request.Path;

                        if (!string.IsNullOrEmpty(accessToken) &&
                            (path.StartsWithSegments("/hubs/chat") || path.StartsWithSegments("/hubs/notifications")))
                        {
                            context.Token = accessToken;
                        }

                        return Task.CompletedTask;
                    }
                };
            });

            // ==================
            // Authorization Policies
            // ==================
            builder.Services.AddAuthorization(options =>
            {
                options.AddPolicy("AdminOnly", p => p.RequireRole("Admin"));
                options.AddPolicy("StudentOnly", p => p.RequireRole("Student"));
                options.AddPolicy("InstructorOnly", p => p.RequireRole("Instructor"));
            });

            // ==================
            // Token Service
            // ==================
            builder.Services.AddScoped<TokenService>();

            // ==================
            // Data Protection (used for integration token storage)
            // ==================
            builder.Services.AddDataProtection();

            // ==================
            // Controllers + Validation Wrapper
            // ==================
            builder.Services.AddControllers()
                .AddJsonOptions(options =>
                {
                    options.JsonSerializerOptions.Converters.Add(
                        new System.Text.Json.Serialization.JsonStringEnumConverter()
                    );
                });

            builder.Services.Configure<ApiBehaviorOptions>(options =>
            {
                options.InvalidModelStateResponseFactory = context =>
                {
                    var errors = context.ModelState
                        .Where(x => x.Value!.Errors.Count > 0)
                        .ToDictionary(
                            x => x.Key,
                            x => x.Value!.Errors.Select(e => e.ErrorMessage).ToArray()
                        );

                    var response = ApiResponse<object>.FailureResponse(
                        message: "Validation failed",
                        statusCode: StatusCodes.Status400BadRequest,
                        errors: errors
                    );

                    return new BadRequestObjectResult(response);
                };
            });

            // ==================
            // Swagger + JWT
            // ==================
            builder.Services.AddEndpointsApiExplorer();
            builder.Services.AddSwaggerGen(options =>
            {
                options.AddSecurityDefinition(JwtBearerDefaults.AuthenticationScheme,
                    new OpenApiSecurityScheme
                    {
                        BearerFormat = "JWT",
                        Name = "Authorization",
                        In = ParameterLocation.Header,
                        Type = SecuritySchemeType.Http,
                        Scheme = JwtBearerDefaults.AuthenticationScheme,
                        Description = "Enter JWT Bearer token only"
                    });

                options.CustomSchemaIds(type => type.FullName);

                options.AddSecurityRequirement(new OpenApiSecurityRequirement
                {
                    {
                        new OpenApiSecurityScheme
                        {
                            Reference = new OpenApiReference
                            {
                                Type = ReferenceType.SecurityScheme,
                                Id = JwtBearerDefaults.AuthenticationScheme
                            }
                        },
                        Array.Empty<string>()
                    }
                });
            });

            var app = builder.Build();

            // ==================
            // Seed Roles + Admin + Academic Plans
            // ==================
            using (var scope = app.Services.CreateScope())
            {
                var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
                var userManager = scope.ServiceProvider.GetRequiredService<UserManager<BaseUser>>();
                var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                // Keep production schema in sync with code (prevents runtime 500s from missing tables/columns)
                await dbContext.Database.MigrateAsync();

                await RoleSeeder.SeedRolesAsync(roleManager);
                await UserSeeder.SeedAdminAsync(userManager);
                await AcademicPlanSeeder.SeedDefaultAcademicPlanAsync(dbContext);
                await AcademicCalendarSeeder.EnsureDefaultRowAsync(dbContext);
            }

            // ==================
            // Middleware Pipeline
            // ==================
            // if (app.Environment.IsDevelopment())
            {
                app.UseSwagger();
                app.UseSwaggerUI();
            }

            app.UseMiddleware<GlobalExceptionMiddleware>();

            app.UseCors("AllowRegman");

            app.UseHttpsRedirection(); // Temporarily disabled for local testing

            app.UseAuthentication();
            app.UseAuthorization();

            app.MapHub<ChatHub>("/hubs/chat");
            app.MapHub<NotificationHub>("/hubs/notifications");
            app.MapControllers();

            // ==================
            // Startup diagnostics (MonsterASP)
            // ==================
            // Do NOT log secret values. Log presence and redirect URI only.
            var cfg = app.Configuration;

            string? Env(string k) => Environment.GetEnvironmentVariable(k);
            bool Has(string? v) => !string.IsNullOrWhiteSpace(v);

            var clientIdEnv = Env("GOOGLE_CLIENT_ID") ?? Env("Google__ClientId");
            var clientSecretEnv = Env("GOOGLE_CLIENT_SECRET") ?? Env("Google__ClientSecret");
            var redirectEnv = Env("GOOGLE_REDIRECT_URI") ?? Env("Google__RedirectUri");

            var clientIdCfg = cfg["GOOGLE_CLIENT_ID"] ?? cfg["Google:ClientId"];
            var clientSecretCfg = cfg["GOOGLE_CLIENT_SECRET"] ?? cfg["Google:ClientSecret"];
            var redirectCfg = cfg["GOOGLE_REDIRECT_URI"] ?? cfg["Google:RedirectUri"];

            app.Logger.LogInformation(
                "Startup Google OAuth config presence: ClientId Env={ClientIdEnv} Cfg={ClientIdCfg}; ClientSecret Env={ClientSecretEnv} Cfg={ClientSecretCfg}; RedirectUri Env={RedirectEnv} Cfg={RedirectCfg}; EffectiveRedirectUri={EffectiveRedirectUri}",
                Has(clientIdEnv),
                Has(clientIdCfg),
                Has(clientSecretEnv),
                Has(clientSecretCfg),
                Has(redirectEnv),
                Has(redirectCfg),
                (redirectCfg ?? redirectEnv)?.Trim() ?? "<missing>"
            );

            app.Run();
        }

        private static void TryAddWebConfigSecrets(WebApplicationBuilder builder)
        {
            try
            {
                var webConfigPath = System.IO.Path.Combine(builder.Environment.ContentRootPath, "web.config");
                if (!System.IO.File.Exists(webConfigPath))
                {
                    builder.Logging.AddFilter("Microsoft", LogLevel.Warning);
                    return;
                }

                var doc = XDocument.Load(webConfigPath);
                var secrets = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);

                // <appSettings><add key="GOOGLE_CLIENT_ID" value="..."/></appSettings>
                foreach (var add in doc.Descendants("appSettings").Descendants("add"))
                {
                    var key = add.Attribute("key")?.Value?.Trim();
                    var value = add.Attribute("value")?.Value;
                    if (!string.IsNullOrWhiteSpace(key))
                    {
                        secrets[key] = value;
                    }
                }

                // <aspNetCore><environmentVariables><environmentVariable name="GOOGLE_CLIENT_ID" value="..."/></environmentVariables>
                foreach (var envVar in doc.Descendants("environmentVariables").Descendants("environmentVariable"))
                {
                    var name = envVar.Attribute("name")?.Value?.Trim();
                    var value = envVar.Attribute("value")?.Value;
                    if (!string.IsNullOrWhiteSpace(name))
                    {
                        secrets[name] = value;
                    }
                }

                // Normalize into the keys we support (both env-style and Google:* style)
                string? Get(string k) => secrets.TryGetValue(k, out var v) ? v : null;

                var clientId = Get("GOOGLE_CLIENT_ID") ?? Get("Google__ClientId") ?? Get("Google:ClientId");
                var clientSecret = Get("GOOGLE_CLIENT_SECRET") ?? Get("Google__ClientSecret") ?? Get("Google:ClientSecret");
                var redirectUri = Get("GOOGLE_REDIRECT_URI") ?? Get("Google__RedirectUri") ?? Get("Google:RedirectUri");

                var injected = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
                {
                    ["GOOGLE_CLIENT_ID"] = clientId,
                    ["GOOGLE_CLIENT_SECRET"] = clientSecret,
                    ["GOOGLE_REDIRECT_URI"] = redirectUri,
                    ["Google:ClientId"] = clientId,
                    ["Google:ClientSecret"] = clientSecret,
                    ["Google:RedirectUri"] = redirectUri
                };

                builder.Configuration.AddInMemoryCollection(injected!);
            }
            catch
            {
                // Never block startup due to diagnostics.
            }
        }
    }
}
