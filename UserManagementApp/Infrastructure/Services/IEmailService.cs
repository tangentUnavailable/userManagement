namespace UserManagementApp.Infrastructure.Services;

public interface IEmailService
{
    Task SendConfirmationEmailAsync(string email, string name, string confirmationLink);
}
