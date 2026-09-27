using App01.Shared.Application.Exceptions;
using App01.Shared.Application.Interfaces;
using App01.Shared.Infrastructure.Repositories;

using FluentValidation;

using MediatR;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;


namespace App01.Modules.Portal.Features.UserList;

public class UserListHandler : IRequestHandler<Contracts.Request, Contracts.Response>
{
    private readonly ILogger<UserListHandler> _logger;
    private readonly IValidator<Contracts.Request> _validator;
    private readonly AppDbContext _dbContext;
    private readonly IJwtService _jwtService;


    public UserListHandler(
        ILogger<UserListHandler> logger,
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
            throw new ForbiddenException("Only administrators can view user list");
        }

        var query = _dbContext.Users.AsQueryable();

        if (!string.IsNullOrWhiteSpace(request.Email))
        {
            query = query.Where(u => u.Email.Contains(request.Email));
        }

        if (request.IsAdmin.HasValue)
        {
            query = query.Where(u => u.IsAdmin == request.IsAdmin.Value);
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var totalPages = (int)Math.Ceiling(totalCount / (double)request.PageSize);

        var users = await query
            .OrderBy(u => u.Email)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(u => new Contracts.UserDto(
                u.Id,
                u.Email,
                u.IsAdmin,
                u.CreatedAt
            ))
            .ToListAsync(cancellationToken);

        _logger.LogDebug("Retrieved {Count} users (page {Page}/{TotalPages})", users.Count, request.Page, totalPages);

        return new Contracts.Response(users, totalCount, request.Page, request.PageSize, totalPages);
    }
}