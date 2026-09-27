using App01.Shared.Application.Exceptions;
using App01.Shared.Application.Interfaces;
using Microsoft.Extensions.Configuration;


namespace App01.Bootstrapper.Api.Services;


public class XTokenService : IXTokenService
{
    private readonly string? _expectedToken;

    public XTokenService(IConfiguration configuration)
    {
        _expectedToken = configuration["Tokens:X-TOKEN"];
    }

    public void ValidateToken(string? token)
    {
        if (string.IsNullOrEmpty(_expectedToken))
        {
            throw new ForbiddenException("X-TOKEN is not configured");
        }

        if (!string.Equals(token, _expectedToken, StringComparison.Ordinal))
        {
            throw new ForbiddenException("Invalid X-TOKEN");
        }
    }
}
