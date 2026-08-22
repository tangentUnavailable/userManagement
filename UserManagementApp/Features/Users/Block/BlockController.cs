using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using UserManagementApp.Infrastructure.Data;

namespace UserManagementApp.Features.Users.Block;

//     Controller for the "Block" toolbar action.
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

        return Json(new
        {
            success = true,
            message = $"Successfully blocked {blockedCount} user(s).",
            blockedCount
        });
    }
}

public class BlockRequestModel
{
    public List<Guid> Ids { get; set; } = new();
}
