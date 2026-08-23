using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using UserManagementApp.Infrastructure.Data;
using UserManagementApp.Infrastructure.Extensions;

namespace UserManagementApp.Features.Users.DeleteUnverified;

/// <summary>
/// Bulk-deletes all unverified users. No selection needed.
/// </summary>
[Authorize]
public class DeleteUnverifiedController : Controller
{
    private readonly ApplicationDbContext _dbContext;

    public DeleteUnverifiedController(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Index()
    {
        var unverifiedUsers = await _dbContext.Users
            .Where(u => u.Status == UserStatus.Unverified)
            .ToListAsync();

        if (!unverifiedUsers.Any())
        {
            return Json(new { success = true, message = "No unverified users found.", deletedCount = 0 });
        }

        // If the currently signed-in user was unverified, they're being deleted
        // right along with everyone else. The UserStatusMiddleware will sign them
        // out on their next request; flag it so the client shows a clear message
        // and a clean sign-out flow.
        var currentUserId = User.GetUserId();
        var selfImpact = currentUserId.HasValue && unverifiedUsers.Any(u => u.Id == currentUserId);

        _dbContext.Users.RemoveRange(unverifiedUsers);
        await _dbContext.SaveChangesAsync();

        return Json(new
        {
            success = true,
            message = selfImpact
                ? "You were logged out because your account was removed."
                : $"Successfully deleted {unverifiedUsers.Count} unverified user(s).",
            deletedCount = unverifiedUsers.Count,
            redirectToLogin = selfImpact
        });
    }
}
