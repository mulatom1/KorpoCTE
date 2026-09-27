using FluentValidation;

using MediatR;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;


namespace App01.Modules.Portal.Features.GetApiVersion;


public class ApiVersionHandler : IRequestHandler<Contracts.Request, Contracts.Response>
{
    private readonly ILogger<ApiVersionHandler> _logger;
    private readonly IValidator<Contracts.Request> _validator;
    private readonly IConfiguration _configuration;


    public ApiVersionHandler(
        ILogger<ApiVersionHandler> logger,
        IValidator<Contracts.Request> validator, IConfiguration configuration)
    {
        _logger = logger;
        _validator = validator;
        _configuration = configuration;
    }

    public async Task<Contracts.Response> Handle(Contracts.Request request, CancellationToken cancellationToken)
    {
        var validationResult = await _validator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
        {
            throw new ValidationException(validationResult.Errors);
        }

        var apiVersion = _configuration.GetValue<string>("ApiVersion");
        _logger.LogDebug("Retrieved API version: {ApiVersion}", apiVersion);

        return await Task.FromResult(new Contracts.Response(apiVersion ?? "Version not found"));
    }
}