using UserManagementApp.Infrastructure.Data;

namespace UserManagementApp.Features.Users.List;

//     View model for a single user row in the management table.
public class UserRowViewModel
{
    public Guid Id { get; set; }
    public string Name { get; set; } = null!;
    public string Email { get; set; } = null!;
    public string Status { get; set; } = null!;
    public DateTime? LastLoginTime { get; set; }
    public bool IsBlocked { get; set; }
    public DateTime RegistrationTime { get; set; }
}

//     View model for the user management list page.
public class ListViewModel
{
    public List<UserRowViewModel> Users { get; set; } = new();
    public string? StatusMessage { get; set; }
    public string? StatusMessageType { get; set; } // "success", "danger", "info", "warning"
}
