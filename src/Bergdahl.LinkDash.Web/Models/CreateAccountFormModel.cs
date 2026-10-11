using System.ComponentModel.DataAnnotations;
using Bergdahl.LinkDash.Web.Authentication;

namespace Bergdahl.LinkDash.Web.Models;

public class CreateAccountFormModel
{
    [Required(ErrorMessage = "User name is required.")]
    [StringLength(100)]
    [Display(Name = "User name")]
    public string Username { get; set; } = "admin";

    [Required(ErrorMessage = "Password is required.")]
    [StringLength(256, MinimumLength = AdminCredentials.MinPasswordLength, ErrorMessage = "The password must be at least {2} characters.")]
    [DataType(DataType.Password)]
    public string Password { get; set; } = string.Empty;

    [Required(ErrorMessage = "Repeat the password.")]
    [Compare(nameof(Password), ErrorMessage = "The passwords do not match.")]
    [DataType(DataType.Password)]
    [Display(Name = "Repeat password")]
    public string ConfirmPassword { get; set; } = string.Empty;

    /// <summary>
    /// The page to return to after creating the account. Only local URLs are followed.
    /// </summary>
    public string? ReturnUrl { get; set; }
}
