using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using UserManagementApp.Infrastructure.Data;
using UserManagementApp.Infrastructure.Extensions;

namespace UserManagementApp.Features.Users.Delete;

/// <summary>
/// Hard-deletes selected users via AJAX.
/// </summary>
[Authorize]
public class DeleteController : Controller
{
    private readonly ApplicationDbContext _dbContext;

    public DeleteController(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Index([FromBody] DeleteRequestModel model)
    {
        if (model?.Ids == null || !model.Ids.Any())
        {
            return Json(new { success = false, message = "No users selected." });
        }

        var users = await _dbContext.Users
            .Where(u => model.Ids.Contains(u.Id))
            .ToListAsync();

        // Determine whether the currently signed-in user is among the users
        // being deleted. If so, the UserStatusMiddleware will sign them out on
        // their next request; tell the client so it can show a clear message and
        // a clean sign-out flow instead of reloading and tripping the middleware
        // (which used to surface a confusing "operation failed" toast here).
        var currentUserId = User.GetUserId();
        var selfImpact = currentUserId.HasValue && users.Any(u => u.Id == currentUserId);

        _dbContext.Users.RemoveRange(users);
        await _dbContext.SaveChangesAsync();

        return Json(new
        {
            success = true,
            message = selfImpact
                ? "You were logged out because your account was deleted."
                : $"Successfully deleted {users.Count} user(s).",
            deletedCount = users.Count,
            redirectToLogin = selfImpact
        });
    }
}

public class DeleteRequestModel
{
    public List<Guid> Ids { get; set; } = new();
}
