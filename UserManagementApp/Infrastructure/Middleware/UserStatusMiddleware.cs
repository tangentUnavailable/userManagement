using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using UserManagementApp.Infrastructure.Data;
using UserManagementApp.Infrastructure.Extensions;

namespace UserManagementApp.Infrastructure.Middleware;

/// <summary>
///     Middleware that checks if the authenticated user exists in the database
///     and is not blocked. If the user account is blocked or deleted, the request
///     is redirected to the login page.
///     Runs AFTER authentication (UseAuthentication) and BEFORE authorization (UseAuthorization).
/// </summary>
public class UserStatusMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<UserStatusMiddleware> _logger;

    // Paths that bypass the user-status check (login, register, static files, etc.)
    private static readonly HashSet<string> ExemptPaths = new(StringComparer.OrdinalIgnoreCase)
    {
        "/Login",
        "/Register",
        "/ConfirmEmail",
        "/favicon.ico",
        "/css/",
        "/js/",
        "/lib/",
        "/images/"
    };

    public UserStatusMiddleware(RequestDelegate next, ILogger<UserStatusMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, ApplicationDbContext dbContext)
    {
        // Skip the check for exempt paths (login, register, static files, etc.)
        if (ExemptPaths.Any(path => context.Request.Path.StartsWithSegments(path, StringComparison.OrdinalIgnoreCase)))
        {
            await _next(context);
            return;
        }

        // If the user is authenticated, verify their database record
        if (context.User?.Identity?.IsAuthenticated == true)
        {
            var userId = context.User.GetUserId();
            if (userId.HasValue)
            {
                var user = await dbContext.Users
                    .AsNoTracking()
                    .FirstOrDefaultAsync(u => u.Id == userId);

                // User is deleted (not found) or blocked → sign out and redirect
                if (user == null || user.Status == UserStatus.Blocked)
                {
                    _logger.LogWarning(
                        "User {UserId} is blocked or deleted. Signing out and redirecting to login.",
                        userId);

                    await context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                    context.Response.Redirect("/Login");
                    return;
                }
            }
        }

        // For non-authenticated users, [Authorize] on controllers handles the redirect
        await _next(context);
    }
}

/// <summary>
///     Extension method for registering the middleware in Program.cs.
/// </summary>
public static class UserStatusMiddlewareExtensions
{
    public static IApplicationBuilder UseUserStatus(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<UserStatusMiddleware>();
    }
}
