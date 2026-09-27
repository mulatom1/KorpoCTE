namespace App01.Shared.Application.Exceptions;


public class ApiException : Exception
{
    public ApiException(string message) : base(message) { }
}