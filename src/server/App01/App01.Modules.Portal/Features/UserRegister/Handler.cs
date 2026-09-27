using App01.Shared.Application.Entities.Portal;
using App01.Shared.Infrastructure.Repositories;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;


namespace App01.Modules.Portal.Features.UserRegister;


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

        var existingUser = await _dbContext.Users
            .FirstOrDefaultAsync(u => u.Email == request.Email, cancellationToken);

        if (existingUser != null)
        {
            throw new ValidationException("User with this email already exists");
        }

        var user = new User
        {
            Email = request.Email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
            IsAdmin = false,
            CreatedAt = DateTime.Now
        };

        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogDebug("User registered with Id: {UserId}", user.Id);

        return new Contracts.Response(user.Id, user.Email, user.CreatedAt);
    }
}
