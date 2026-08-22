using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using UserManagementApp.Infrastructure.Data;

namespace UserManagementApp.Features.Users.Unblock;

//     Controller for the "Unblock" toolbar action (icon button).
public class UnblockController : Controller
{
    private readonly ApplicationDbContext _dbContext;

    public UnblockController(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Index([FromBody] UnblockRequestModel model)
    {
        if (model?.Ids == null || !model.Ids.Any())
        {
            return Json(new { success = false, message = "No users selected." });
        }

        var users = await _dbContext.Users
            .Where(u => model.Ids.Contains(u.Id) && u.Status == UserStatus.Blocked)
            .ToListAsync();

        var unblockedCount = 0;
        foreach (var user in users)
        {
            // Restore the status that existed before the block.
            // If ConfirmationToken is still set, the email was never confirmed → Unverified.
            // If ConfirmationToken was cleared by ConfirmEmailController, email was confirmed → Active.
            user.Status = string.IsNullOrEmpty(user.ConfirmationToken) ? UserStatus.Active : UserStatus.Unverified;
            _dbContext.Users.Update(user);
            unblockedCount++;
        }

        await _dbContext.SaveChangesAsync();

        return Json(new
        {
            success = true,
            message = $"Successfully unblocked {unblockedCount} user(s).",
            unblockedCount
        });
    }
}

public class UnblockRequestModel
{
    public List<Guid> Ids { get; set; } = new();
}
