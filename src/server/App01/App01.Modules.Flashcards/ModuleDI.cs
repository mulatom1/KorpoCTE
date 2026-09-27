using System.Reflection;

using FluentValidation;

using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace App01.Modules.Flashcards;

public static class ModuleDI
{
    public static IServiceCollection AddModuleFlashcardsServices(this IServiceCollection services)
    {
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(Assembly.GetExecutingAssembly()));
        services.AddValidatorsFromAssembly(Assembly.GetExecutingAssembly());
        return services;
    }

    public static WebApplication UseModuleFlashcardsEndpoints(this WebApplication app)
    {
        Features.Generate.Endpoint.AddEndpoint(app);

        return app;
    }
}