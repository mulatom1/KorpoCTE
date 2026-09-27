using System.Reflection;

using App01.Modules.Portal.Workers;

using FluentValidation;

using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace App01.Modules.Portal;

public static class ModuleDI
{
    public static IServiceCollection AddModulePortalServices(this IServiceCollection services)
    {
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(Assembly.GetExecutingAssembly()));
        services.AddValidatorsFromAssembly(Assembly.GetExecutingAssembly());
        return services;
    }

    public static WebApplication UseModulePortalEndpoints(this WebApplication app)
    {
        Features.GetApiVersion.Endpoint.AddEndpoint(app);
        Features.MailFromClientAdd.Endpoint.AddEndpoint(app);
        Features.MailFromClientList.Endpoint.AddEndpoint(app);
        Features.MailFromClientGet.Endpoint.AddEndpoint(app);
        Features.MailFromClientDelete.Endpoint.AddEndpoint(app);
        Features.UserRegister.Endpoint.AddEndpoint(app);
        Features.UsersRegister.Endpoint.AddEndpoint(app);
        Features.UserLogin.Endpoint.AddEndpoint(app);
        Features.UserPassChange.Endpoint.AddEndpoint(app);
        Features.UserPassReset.Endpoint.AddEndpoint(app);
        Features.UserSet.Endpoint.AddEndpoint(app);
        Features.UserList.Endpoint.AddEndpoint(app);
        Features.UserDelete.Endpoint.AddEndpoint(app);

        return app;
    }

    public static IServiceCollection AddModulePortalWorkers(this IServiceCollection services)
    {
        services.AddHostedService<PortalWorker01>();
        return services;
    }
}