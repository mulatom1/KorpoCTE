using Microsoft.Extensions.DependencyInjection;

namespace App01.Shared.Abstractions;

public static class SharedAbstractionsDI
{
    public static IServiceCollection AddSharedAbstractionsServices(this IServiceCollection services)
    {
        return services;
    }
}

