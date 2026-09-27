using Microsoft.Extensions.DependencyInjection;

namespace App01.Shared.Application;

public static class SharedApplicationDI
{
    public static IServiceCollection AddSharedApplicationServices(this IServiceCollection services)
    {
        return services;
    }
}