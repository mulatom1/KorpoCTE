using System.Reflection;

using App01.Modules.Courses.Content;

using FluentValidation;

using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace App01.Modules.Courses;

public static class ModuleDI
{
    public static IServiceCollection AddModuleCoursesServices(this IServiceCollection services)
    {
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(Assembly.GetExecutingAssembly()));
        services.AddValidatorsFromAssembly(Assembly.GetExecutingAssembly());
        services.AddSingleton<ICourseFrontmatterReader, CourseFrontmatterReader>();
        services.TryAddSingleton(TimeProvider.System);
        return services;
    }

    public static WebApplication UseModuleCoursesEndpoints(this WebApplication app)
    {
        Features.ModuleHello.Endpoint.AddEndpoint(app);
        Features.CourseTiles.Endpoint.AddEndpoint(app);
        Features.CourseContent.Endpoint.AddEndpoint(app);
        Features.HangarTasks.Endpoint.AddEndpoint(app);

        return app;
    }
}