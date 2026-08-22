using System.ComponentModel.DataAnnotations;

namespace UserManagementApp.Features.Account.Register;

//     View model for the registration form.
public class RegisterViewModel
{
    [Required(ErrorMessage = "Name is required")]
    [StringLength(100, ErrorMessage = "Name must be at most 100 characters")]
    [Display(Name = "Name")]
    public string Name { get; set; } = null!;

    [Required(ErrorMessage = "Email is required")]
    [EmailAddress(ErrorMessage = "Please enter a valid email address")]
    [Display(Name = "E-mail")]
    public string Email { get; set; } = null!;

    [Required(ErrorMessage = "Password is required")]
    [DataType(DataType.Password)]
    [StringLength(100, MinimumLength = 1, ErrorMessage = "Password must be at least 1 character")]
    [Display(Name = "Password")]
    public string Password { get; set; } = null!;

    public string? ReturnUrl { get; set; }
}
