using System.Security.Claims;

namespace UserManagementApp.Infrastructure.Extensions;

public static class UniqueIdExtensions
{
    // Short hex string used for unique HTML element IDs.
    public static string GetUniqIdValue()
    {
        return Guid.NewGuid().ToString("N")[..8];
    }

    public static Guid? GetUserId(this ClaimsPrincipal user)
    {
        var id = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(id, out var result) ? result : null;
    }

    public static string? GetUserEmail(this ClaimsPrincipal user)
    {
        return user.FindFirst(ClaimTypes.Email)?.Value;
    }

    public static string? GetUserName(this ClaimsPrincipal user)
    {
        return user.FindFirst(ClaimTypes.Name)?.Value;
    }
}
