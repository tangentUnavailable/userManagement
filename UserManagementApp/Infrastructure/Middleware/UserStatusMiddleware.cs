using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using UserManagementApp.Infrastructure.Data;
using UserManagementApp.Infrastructure.Extensions;

namespace UserManagementApp.Infrastructure.Middleware;

/// <summary>
/// Checks the authenticated user against the database on every request.
/// Signs them out and bounces them to the login page if their account is
/// blocked or has been deleted. Placed between UseAuthentication and
/// UseAuthorization in the middleware pipeline.
/// </summary>
public class UserStatusMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<UserStatusMiddleware> _logger;

    // Paths that skip the user-status check entirely.
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
        // Let static files, login, register, etc. through without checking.
        if (ExemptPaths.Any(path => context.Request.Path.StartsWithSegments(path, StringComparison.OrdinalIgnoreCase)))
        {
            await _next(context);
            return;
        }

        // For authenticated users, make sure the account still exists and is not blocked.
        if (context.User?.Identity?.IsAuthenticated == true)
        {
            var userId = context.User.GetUserId();
            if (userId.HasValue)
            {
                var user = await dbContext.Users
                    .AsNoTracking()
                    .FirstOrDefaultAsync(u => u.Id == userId);

                // Account deleted or blocked: sign out and send them to login.
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

        // Unauthenticated users are handled by [Authorize] on the controllers.
        await _next(context);
    }
}

public static class UserStatusMiddlewareExtensions
{
    public static IApplicationBuilder UseUserStatus(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<UserStatusMiddleware>();
    }
}
