using App01.Shared.Application.Exceptions;
using App01.Shared.Application.Interfaces;
using App01.Shared.Infrastructure.Repositories;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;


namespace App01.Modules.Portal.Features.UserDelete;

public class UserDeleteHandler : IRequestHandler<Contracts.Request, Contracts.Response>
{
    private readonly ILogger<UserDeleteHandler> _logger;
    private readonly IValidator<Contracts.Request> _validator;
    private readonly AppDbContext _dbContext;
    private readonly IJwtService _jwtService;


    public UserDeleteHandler(
        ILogger<UserDeleteHandler> logger,
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
            throw new ForbiddenException("Only administrators can delete users");
        }

        var callerEmail = await _jwtService.GetEmailFromJwt();
        if (callerEmail?.Equals(request.Email, StringComparison.OrdinalIgnoreCase) == true)
        {
            throw new ForbiddenException("Cannot delete your own account");
        }

        var user = await _dbContext.Users
            .FirstOrDefaultAsync(u => u.Email == request.Email, cancellationToken);

        if (user == null)
        {
            throw new NotFoundException($"User with email '{request.Email}' not found");
        }

        _dbContext.Users.Remove(user);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("User {UserId} ({Email}) deleted by admin", user.Id, user.Email);

        return new Contracts.Response(true, $"User '{request.Email}' has been deleted");
    }
}
