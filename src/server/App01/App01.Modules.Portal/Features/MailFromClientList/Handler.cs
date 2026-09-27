using App01.Shared.Application.Exceptions;
using App01.Shared.Application.Interfaces;
using App01.Shared.Infrastructure.Repositories;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace App01.Modules.Portal.Features.MailFromClientList;

public class MailFromClientListHandler : IRequestHandler<Contracts.Request, Contracts.Response>
{
    private readonly ILogger<MailFromClientListHandler> _logger;
    private readonly IValidator<Contracts.Request> _validator;
    private readonly AppDbContext _dbContext;
    private readonly IJwtService _jwtService;

    public MailFromClientListHandler(
        ILogger<MailFromClientListHandler> logger,
        IValidator<Contracts.Request> validator,
        AppDbContext dbContext,
        IJwtService jwtService)
    {
        _logger = logger;
        _validator = validator;
        _dbContext = dbContext;
        _jwtService = jwtService;
    }

    public async Task<Contracts.Response> Handle(Contracts.Request request, CancellationToken cancellationToken)
    {
        var validationResult = await _validator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
        {
            throw new ValidationException(validationResult.Errors);
        }

        var isCallerAdmin = await _jwtService.GetIsAdminFromJwt();
        if (!isCallerAdmin)
        {
            throw new ForbiddenException("Only administrators can view mail list");
        }

        var totalCount = await _dbContext.Mails.CountAsync(cancellationToken);
        var totalPages = (int)Math.Ceiling(totalCount / (double)request.PageSize);

        var mails = await _dbContext.Mails
            .OrderByDescending(m => m.CreatedAt)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(m => new Contracts.MailDto(m.Id, m.Email, m.Topic, m.CreatedAt))
            .ToListAsync(cancellationToken);

        _logger.LogDebug("Retrieved {Count} mails (page {Page}/{TotalPages})", mails.Count, request.Page, totalPages);

        return new Contracts.Response(mails, totalCount, request.Page, request.PageSize, totalPages);
    }
}
