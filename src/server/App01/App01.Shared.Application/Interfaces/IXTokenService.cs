namespace App01.Shared.Application.Interfaces;


public interface IXTokenService
{
    void ValidateToken(string? token);
}