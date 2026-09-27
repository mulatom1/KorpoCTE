
using App01.Shared.Application.Interfaces;

using Microsoft.AspNetCore.Http;


namespace App01.Shared.Application.Filters;


public class XTokenFilter : IEndpointFilter
{
    private readonly IXTokenService _xTokenService;

    public XTokenFilter(IXTokenService xTokenService)
    {
        _xTokenService = xTokenService;
    }

    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var token = context.HttpContext.Request.Headers["X-TOKEN"].FirstOrDefault();
        _xTokenService.ValidateToken(token);
        return await next(context);
    }
}