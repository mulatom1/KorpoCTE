using App01.Modules.Lotto.Services.LottoOpenApi;
using App01.Modules.Lotto.Workers;
using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;

namespace App01.Modules.Lotto;

public static class ModuleDI
{
    public static IServiceCollection AddModuleLottoServices(this IServiceCollection services)
    {
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(Assembly.GetExecutingAssembly()));
        services.AddValidatorsFromAssembly(Assembly.GetExecutingAssembly());
        services.AddScoped<ILottoOpenApiService, LottoOpenApiService>();
        return services;
    }

    public static WebApplication UseModuleLottoEndpoints(this WebApplication app)
    {
        Features.DrawsGetList.Endpoint.AddEndpoint(app);
        Features.DrawsExport.Endpoint.AddEndpoint(app);
        Features.DrawsImport.Endpoint.AddEndpoint(app);
        Features.DrawsAdd.Endpoint.AddEndpoint(app);
        Features.DrawsUpdate.Endpoint.AddEndpoint(app);
        Features.DrawsDelete.Endpoint.AddEndpoint(app);
        Features.TicketsGetList.Endpoint.AddEndpoint(app);
        Features.TicketsAdd.Endpoint.AddEndpoint(app);
        Features.TicketsDelete.Endpoint.AddEndpoint(app);
        Features.TicketsExport.Endpoint.AddEndpoint(app);
        Features.TicketsImport.Endpoint.AddEndpoint(app);
        Features.WinningTicketsList.Endpoint.AddEndpoint(app);
        Features.TransformNumbers.Endpoint.AddEndpoint(app);
        Features.FileEdit01.Endpoint.AddEndpoint(app);
        Features.DrawsGetPrizesList.Endpoint.AddEndpoint(app);
        Features.DrawsNumbersStatsList.Endpoint.AddEndpoint(app);

        return app;
    }

    public static IServiceCollection AddModuleLottoWorkers(this IServiceCollection services)
    {
        services.AddHostedService<LottoWorker01>();
        services.AddHostedService<LottoWorker02>();
        services.AddHostedService<LottoWorker03>();

        return services;
    }
}
