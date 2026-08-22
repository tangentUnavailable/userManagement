using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using UserManagementApp.Infrastructure.Data;

namespace UserManagementApp.Features.Users.Delete;

/// <summary>
/// Hard-deletes selected users via AJAX.
/// </summary>
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

        _dbContext.Users.RemoveRange(users);
        await _dbContext.SaveChangesAsync();

        return Json(new
        {
            success = true,
            message = $"Successfully deleted {users.Count} user(s).",
            deletedCount = users.Count
        });
    }
}

public class DeleteRequestModel
{
    public List<Guid> Ids { get; set; } = new();
}
