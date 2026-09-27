namespace App01.Shared.Application.Interfaces;


public interface IJwtService
{
    string GenerateToken(long userId, string email, bool isAdmin, out DateTime expiresAt);

    Task<long> GetUserIdFromJwt();
    Task<string> GetEmailFromJwt();
    Task<bool> GetIsAdminFromJwt();
}