using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;

namespace Api.Authorization;

/// <summary>
/// A valid token without the required permission gets 403 with <c>{ message }</c>, never 401: the frontend logs
/// the user out on any 401, so 401 is reserved for a missing, invalid or expired token.
/// </summary>
public class JsonAuthorizationResultHandler : IAuthorizationMiddlewareResultHandler
{
    public const string ForbiddenMessage = "You do not have permission to perform this action.";

    private readonly AuthorizationMiddlewareResultHandler _default = new();

    public async Task HandleAsync(RequestDelegate next, HttpContext context, AuthorizationPolicy policy, PolicyAuthorizationResult authorizeResult)
    {
        if (authorizeResult.Forbidden)
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            await context.Response.WriteAsJsonAsync(new { message = ForbiddenMessage });
            return;
        }
        await _default.HandleAsync(next, context, policy, authorizeResult);
    }
}
