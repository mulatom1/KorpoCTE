using FluentValidation;

using MediatR;

using Microsoft.Extensions.Logging;


namespace App01.Modules.Courses.Features.ModuleHello;

public class ModuleHelloHandler : IRequestHandler<Contracts.Request, Contracts.Response>
{
    private readonly ILogger<ModuleHelloHandler> _logger;
    private readonly IValidator<Contracts.Request> _validator;


    public ModuleHelloHandler(
        ILogger<ModuleHelloHandler> logger,
        IValidator<Contracts.Request> validator)
    {
        _logger = logger;
        _validator = validator;
    }

    public async Task<Contracts.Response> Handle(Contracts.Request request, CancellationToken cancellationToken)
    {
        var validationResult = await _validator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
        {
            throw new ValidationException(validationResult.Errors);
        }

        _logger.LogDebug("Module Courses hello requested");

        return new Contracts.Response("Hello from module Courses!");
    }
}