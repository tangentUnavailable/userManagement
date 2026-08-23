using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using UserManagementApp.Infrastructure.Data;
using UserManagementApp.Infrastructure.Extensions;

namespace UserManagementApp.Features.Users.Block;

/// <summary>
/// Blocks selected users via AJAX.
/// </summary>
[Authorize]
public class BlockController : Controller
{
    private readonly ApplicationDbContext _dbContext;

    public BlockController(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Index([FromBody] BlockRequestModel model)
    {
        if (model?.Ids == null || !model.Ids.Any())
        {
            return Json(new { success = false, message = "No users selected." });
        }

        var users = await _dbContext.Users
            .Where(u => model.Ids.Contains(u.Id) && u.Status != UserStatus.Blocked)
            .ToListAsync();

        var blockedCount = 0;
        foreach (var user in users)
        {
            user.Status = UserStatus.Blocked;
            _dbContext.Users.Update(user);
            blockedCount++;
        }

        await _dbContext.SaveChangesAsync();

        // If the current user blocked their own account, they're about to be
        // signed out by the UserStatusMiddleware on their next request. Flag it
        // so the client can show a clear message and a clean sign-out flow
        // instead of reloading and tripping the middleware (which used to surface
        // a confusing "operation failed" toast in that situation).
        var currentUserId = User.GetUserId();
        var selfImpact = currentUserId.HasValue && users.Any(u => u.Id == currentUserId);

        return Json(new
        {
            success = true,
            message = selfImpact
                ? "You were logged out because your account was blocked."
                : $"Successfully blocked {blockedCount} user(s).",
            blockedCount,
            redirectToLogin = selfImpact
        });
    }
}

public class BlockRequestModel
{
    public List<Guid> Ids { get; set; } = new();
}
