using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using System.Text;


namespace App01.Bootstrapper.Api;


public static class ServerDI
{
    public static IServiceCollection AddServerSwagger(this IServiceCollection services)
    {
        services.AddSwaggerGen(c =>
        {
            c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
            {
                Description = "JWT Authorization header using the Bearer scheme. Example: \"Authorization: Bearer {token}\"",
                Name = "Authorization",
                In = ParameterLocation.Header,
                Type = SecuritySchemeType.Http,
                Scheme = "bearer",
                BearerFormat = "JWT"
            });
            c.AddSecurityDefinition("X-TOKEN", new OpenApiSecurityScheme
            {
                Description = "Custom token header. Example: \"X-TOKEN: {token}\"",
                Name = "X-TOKEN",
                In = ParameterLocation.Header,
                Type = SecuritySchemeType.ApiKey
            });
            c.AddSecurityRequirement(new OpenApiSecurityRequirement()
            {
                {
                    new OpenApiSecurityScheme
                    {
                        Reference = new OpenApiReference
                        {
                            Type = ReferenceType.SecurityScheme,
                            Id = "Bearer"
                        }
                    },
                    new string[] {}
                },
                {
                    new OpenApiSecurityScheme
                    {
                        Reference = new OpenApiReference
                        {
                            Type = ReferenceType.SecurityScheme,
                            Id = "X-TOKEN"
                        }
                    },
                    new string[] {}
                }
            });
            c.CustomSchemaIds(type => type.FullName?.Replace("+", "."));
        });

        return services;
    }

    public static IServiceCollection AddServerAuthentication(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddJwtBearer(options =>
        {
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                ClockSkew = TimeSpan.Zero,
                ValidIssuer = configuration["Jwt:Issuer"],
                ValidAudience = configuration["Jwt:Audience"],
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(configuration["Jwt:Key"] ?? throw new InvalidOperationException("JWT Key not configured")))
            };

            options.Events = new JwtBearerEvents
            {
                // Domyślny challenge dokłada nagłówek WWW-Authenticate, przez który
                // przeglądarka pokazuje natywne okno logowania. Przechwytujemy go i
                // zwracamy czysty 401 (JSON), aby UI mogło samo przekierować na /login.
                OnChallenge = async context =>
                {
                    context.HandleResponse();

                    // Nagłówek WWW-Authenticate (Bearer, a na IIS dodatkowo Negotiate/NTLM)
                    // sprawia, że przeglądarka pokazuje własne okno logowania.
                    context.Response.Headers.Remove("WWW-Authenticate");

                    context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                    context.Response.ContentType = "application/json";
                    await context.Response.WriteAsJsonAsync(new
                    {
                        status = StatusCodes.Status401Unauthorized,
                        title = "Unauthorized",
                        detail = "Token jest nieważny lub wygasł."
                    });
                }
            };
        });

        return services;
    }

    public static IServiceCollection AddServerCors(this IServiceCollection services)
    {
        services.AddCors(options =>
        {
            options.AddDefaultPolicy(builder =>
            {
                builder.AllowAnyOrigin()
                       .AllowAnyMethod()
                       .AllowAnyHeader();
            });
        });

        return services;
    }

}
