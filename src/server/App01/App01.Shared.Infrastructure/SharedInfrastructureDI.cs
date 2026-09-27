using System.Text;

using App01.Bootstrapper.Api.Services;
using App01.Shared.Application.Interfaces;
using App01.Shared.Infrastructure.Repositories;
using App01.Shared.Infrastructure.Services;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;


namespace App01.Shared.Infrastructure;


public static class SharedInfrastructureDI
{
    public static IServiceCollection AddSharedInfrastructureServices(this IServiceCollection services, IConfiguration configuration, IHostEnvironment environment)
    {
        var connStrBase64 = configuration.GetConnectionString("DefaultConnection");

        var isTestEnvironment = environment.IsEnvironment("Test");
        if (!isTestEnvironment && string.IsNullOrWhiteSpace(connStrBase64))
            throw new InvalidOperationException("Connection string not configured");

        if (!string.IsNullOrWhiteSpace(connStrBase64))
        {
            var connStr = connStrBase64.StartsWith("Server=", StringComparison.OrdinalIgnoreCase) ||
                          connStrBase64.StartsWith("Data Source=", StringComparison.OrdinalIgnoreCase)
                ? connStrBase64
                : Encoding.UTF8.GetString(Convert.FromBase64String(connStrBase64)).Replace("\\\\", "\\");

            services.AddDbContext<AppDbContext>(options => options.UseSqlServer(connStr,
                sql => sql.UseCompatibilityLevel(110)));
            //services.AddDbContext<AppDbContext>(options => options.UseMySQL(connStr));
        }


        services.AddSingleton<ICacheDataService, CacheDataService>();

        services.AddScoped<IJwtService, JwtService>();
        services.AddScoped<IXTokenService, XTokenService>();
        services.AddScoped<IOpenRouterService, OpenRouterService>();

        return services;
    }
}