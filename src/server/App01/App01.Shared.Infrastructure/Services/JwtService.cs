using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

using App01.Shared.Application.Interfaces;

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace App01.Bootstrapper.Api.Services;

/// <summary>
/// Service for generating JWT tokens for authentication
/// </summary>
public class JwtService : IJwtService
{
    private readonly IConfiguration _configuration;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public JwtService(IConfiguration configuration, IHttpContextAccessor httpContextAccessor)
    {
        _configuration = configuration;
        _httpContextAccessor = httpContextAccessor;
    }

    /// <inheritdoc/>
    public string GenerateToken(long userId, string email, bool isAdmin, out DateTime expiresAt)
    {
        var jwtKey = _configuration["Jwt:Key"]
            ?? throw new InvalidOperationException("JWT Key not configured");
        var jwtIssuer = _configuration["Jwt:Issuer"];
        var jwtAudience = _configuration["Jwt:Audience"];
        var expiryMinutes = _configuration.GetValue<int>("Jwt:ExpiryInMinutes", 1440);

        var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey));
        var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

        var issuedAt = DateTime.UtcNow;
        expiresAt = issuedAt.AddMinutes(expiryMinutes);

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, userId.ToString()),
            new Claim(JwtRegisteredClaimNames.Email, email),
            new Claim("isAdmin", isAdmin.ToString().ToLower()),
            new Claim(JwtRegisteredClaimNames.Iat, new DateTimeOffset(issuedAt).ToUnixTimeSeconds().ToString()),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        var token = new JwtSecurityToken(
            issuer: jwtIssuer,
            audience: jwtAudience,
            claims: claims,
            expires: expiresAt,
            signingCredentials: credentials
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public async Task<long> GetUserIdFromJwt()
    {
        var userIdClaim = _httpContextAccessor.HttpContext?.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdClaim) || !long.TryParse(userIdClaim, out var userId))
        {
            throw new UnauthorizedAccessException("Nie mo¿na zidentyfikowaæ identyfikatora u¿ytkownika");
        }
        return await Task.FromResult(userId);
    }

    public async Task<string> GetEmailFromJwt()
    {
        var emailClaim = _httpContextAccessor.HttpContext?.User.FindFirst(ClaimTypes.Email)?.Value;
        if (string.IsNullOrEmpty(emailClaim))
        {
            throw new UnauthorizedAccessException("Nie mo¿na zidentyfikowaæ email u¿ytkownika");
        }
        return await Task.FromResult(emailClaim);
    }

    public async Task<bool> GetIsAdminFromJwt()
    {
        var isAdminClaim = _httpContextAccessor.HttpContext?.User.FindFirst("isAdmin")?.Value;
        if (string.IsNullOrEmpty(isAdminClaim) || !bool.TryParse(isAdminClaim, out var isAdmin))
        {
            throw new UnauthorizedAccessException("Nie mo¿na zidentyfikowaæ uprawnieñ u¿ytkownika");
        }
        return await Task.FromResult(isAdmin);
    }
}