using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using RegMan.Backend.API.Common;
using RegMan.Backend.API.Hubs;
using RegMan.Backend.API.Middleware;
using RegMan.Backend.API.Seeders;
using RegMan.Backend.BusinessLayer;
using RegMan.Backend.BusinessLayer.Services;
using RegMan.Backend.DAL;
using RegMan.Backend.DAL.DataContext;
using RegMan.Backend.DAL.Entities;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using static System.Net.WebRequestMethods;

namespace RegMan.Backend.API
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

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
            // =========================
            // Database + Business Layer
            // =========================
            builder.Services.AddDataBaseLayer(builder.Configuration);
            builder.Services.AddBusinessServices();

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

                options.User.RequireUniqueEmail = true;
            })
            .AddEntityFrameworkStores<AppDbContext>()
            .AddDefaultTokenProviders();

            // =================
            // JWT Authentication
            // =================
            var key = Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"]!);

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

                    NameClaimType = JwtRegisteredClaimNames.Sub,
                    RoleClaimType = ClaimTypes.Role
                };
                options.Events = new JwtBearerEvents
                {
                    OnMessageReceived = context =>
                    {
                        var accessToken = context.Request.Query["access_token"];
                        var path = context.HttpContext.Request.Path;

                        if (!string.IsNullOrEmpty(accessToken) &&
                            path.StartsWithSegments("/hubs/chat"))
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

                await RoleSeeder.SeedRolesAsync(roleManager);
                await UserSeeder.SeedAdminAsync(userManager);
                await AcademicPlanSeeder.SeedDefaultAcademicPlanAsync(dbContext);
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
            app.MapControllers();

            app.Run();
        }
    }
}
