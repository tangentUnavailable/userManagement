using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using UserManagementApp.Infrastructure.Data;

namespace UserManagementApp.Features.Users.List;

/// <summary>
/// Shows the user management table, sorted by most recent login.
/// </summary>
public class ListController : Controller
{
    private readonly ApplicationDbContext _dbContext;

    public ListController(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    [HttpGet]
    [Authorize]
    public async Task<IActionResult> Index()
    {
        var users = await _dbContext.Users
            .AsNoTracking()
            .Select(u => new UserRowViewModel
            {
                Id = u.Id,
                Name = u.Name,
                Email = u.Email,
                Status = u.Status.ToString(),
                LastLoginTime = u.LastLoginTime,
                IsBlocked = u.Status == UserStatus.Blocked,
                RegistrationTime = u.RegistrationTime
            })
            .OrderByDescending(u => u.LastLoginTime)
            .ToListAsync();

        var model = new ListViewModel
        {
            Users = users,
            StatusMessage = TempData["StatusMessage"] as string,
            StatusMessageType = TempData["StatusMessageType"] as string
        };

        return View("Index", model);
    }
}
