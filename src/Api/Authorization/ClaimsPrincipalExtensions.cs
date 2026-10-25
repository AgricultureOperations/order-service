using System.Security.Claims;

namespace Api.Authorization;

public static class ClaimsPrincipalExtensions
{
    /// <summary>The auth-service user id from the "sub" claim. Never taken from a request body.</summary>
    public static Guid? UserId(this ClaimsPrincipal user) =>
        Guid.TryParse(user.FindFirst("sub")?.Value, out var id) ? id : null;
}
