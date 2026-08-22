using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using UserManagementApp.Infrastructure.Data;

namespace UserManagementApp.Features.Users.DeleteUnverified;

/// <summary>
/// Bulk-deletes all unverified users. No selection needed.
/// </summary>
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

        _dbContext.Users.RemoveRange(unverifiedUsers);
        await _dbContext.SaveChangesAsync();

        return Json(new
        {
            success = true,
            message = $"Successfully deleted {unverifiedUsers.Count} unverified user(s).",
            deletedCount = unverifiedUsers.Count
        });
    }
}
