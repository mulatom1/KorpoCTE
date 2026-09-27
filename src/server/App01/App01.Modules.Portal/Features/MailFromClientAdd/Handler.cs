using App01.Shared.Application.Entities.Portal;
using App01.Shared.Infrastructure.Repositories;

using FluentValidation;

using MediatR;

using Microsoft.Extensions.Logging;

namespace App01.Modules.Portal.Features.MailFromClientAdd;

public class MailFromClientAddHandler : IRequestHandler<Contracts.Request, Contracts.Response>
{
    private readonly ILogger<MailFromClientAddHandler> _logger;
    private readonly IValidator<Contracts.Request> _validator;
    private readonly AppDbContext _dbContext;

    public MailFromClientAddHandler(
        ILogger<MailFromClientAddHandler> logger,
        IValidator<Contracts.Request> validator,
        AppDbContext dbContext)
    {
        _logger = logger;
        _validator = validator;
        _dbContext = dbContext;
    }

    public async Task<Contracts.Response> Handle(Contracts.Request request, CancellationToken cancellationToken)
    {
        var validationResult = await _validator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
        {
            throw new ValidationException(validationResult.Errors);
        }

        var mail = new Mail
        {
            Id = 0,
            Email = request.Email,
            Topic = request.Topic,
            Body = request.Body,
            CreatedAt = DateTime.Now
        };

        _dbContext.Mails.Add(mail);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogDebug("Mail added with Id: {MailId}", mail.Id);

        return new Contracts.Response(mail.Id);
    }
}