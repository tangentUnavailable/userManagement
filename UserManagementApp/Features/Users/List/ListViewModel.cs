using UserManagementApp.Infrastructure.Data;

namespace UserManagementApp.Features.Users.List;

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

public class ListViewModel
{
    public List<UserRowViewModel> Users { get; set; } = new();
    public string? StatusMessage { get; set; }
    public string? StatusMessageType { get; set; }
}
