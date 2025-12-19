using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using StudentManagementSystem.API.Common;
using StudentManagementSystem.API.Middleware;
using StudentManagementSystem.API.Seeders;
using StudentManagementSystem.BusinessLayer;
using StudentManagementSystem.BusinessLayer.Services;
using StudentManagementSystem.DAL;
using StudentManagementSystem.DAL.DataContext;
using StudentManagementSystem.DAL.Entities;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace StudentManagementSystem.API
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
            // Seed Roles + Admin
            // ==================
            using (var scope = app.Services.CreateScope())
            {
                var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
                var userManager = scope.ServiceProvider.GetRequiredService<UserManager<BaseUser>>();

                await RoleSeeder.SeedRolesAsync(roleManager);
                await UserSeeder.SeedAdminAsync(userManager);
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

            app.UseHttpsRedirection();

            app.UseAuthentication();
            app.UseAuthorization();

            app.MapControllers();

            app.Run();
        }
    }
}
