using App01.Shared.Application.Exceptions;
using App01.Shared.Application.Interfaces;
using App01.Shared.Infrastructure.Repositories;

using MediatR;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace App01.Modules.Portal.Features.MailFromClientGet;

public class MailFromClientGetHandler : IRequestHandler<Contracts.Request, Contracts.Response>
{
    private readonly ILogger<MailFromClientGetHandler> _logger;
    private readonly AppDbContext _dbContext;
    private readonly IJwtService _jwtService;

    public MailFromClientGetHandler(
        ILogger<MailFromClientGetHandler> logger,
        AppDbContext dbContext,
        IJwtService jwtService)
    {
        _logger = logger;
        _dbContext = dbContext;
        _jwtService = jwtService;
    }

    public async Task<Contracts.Response> Handle(Contracts.Request request, CancellationToken cancellationToken)
    {
        var isCallerAdmin = await _jwtService.GetIsAdminFromJwt();
        if (!isCallerAdmin)
        {
            throw new ForbiddenException("Only administrators can view mail details");
        }

        var mail = await _dbContext.Mails
            .FirstOrDefaultAsync(m => m.Id == request.Id, cancellationToken);

        if (mail == null)
        {
            throw new NotFoundException($"Mail with Id {request.Id} not found");
        }

        _logger.LogDebug("Retrieved mail with Id: {MailId}", mail.Id);

        return new Contracts.Response(mail.Id, mail.Email, mail.Topic, mail.Body, mail.CreatedAt);
    }
}