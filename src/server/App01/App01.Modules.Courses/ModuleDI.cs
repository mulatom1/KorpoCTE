using System.Reflection;

using FluentValidation;

using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace App01.Modules.Courses;

public static class ModuleDI
{
    public static IServiceCollection AddModuleCoursesServices(this IServiceCollection services)
    {
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(Assembly.GetExecutingAssembly()));
        services.AddValidatorsFromAssembly(Assembly.GetExecutingAssembly());
        return services;
    }

    public static WebApplication UseModuleCoursesEndpoints(this WebApplication app)
    {
        Features.ModuleHello.Endpoint.AddEndpoint(app);

        return app;
    }
}