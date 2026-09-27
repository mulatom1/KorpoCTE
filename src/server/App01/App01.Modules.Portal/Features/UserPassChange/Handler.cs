using App01.Shared.Application.Exceptions;
using App01.Shared.Infrastructure.Repositories;

using FluentValidation;

using MediatR;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;


namespace App01.Modules.Portal.Features.UserPassChange;


public class Handler : IRequestHandler<Contracts.Request, Contracts.Response>
{
    private readonly ILogger<Handler> _logger;
    private readonly IValidator<Contracts.Request> _validator;
    private readonly AppDbContext _dbContext;

    public Handler(
        ILogger<Handler> logger,
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

        var user = await _dbContext.Users
            .FirstOrDefaultAsync(u => u.Email == request.Login, cancellationToken);

        if (user == null)
        {
            throw new NotFoundException($"User not found");
        }

        bool passwordValid;
        try
        {
            passwordValid = BCrypt.Net.BCrypt.Verify(request.Password1, user.PasswordHash);
        }
        catch (Exception)
        {
            passwordValid = false;
        }

        if (!passwordValid)
        {
            throw new UnauthorizedAccessException("Current password is incorrect");
        }

        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password2);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogDebug("Password changed for user: {UserId}", user.Id);

        return new Contracts.Response(user.Id, user.Email);
    }
}