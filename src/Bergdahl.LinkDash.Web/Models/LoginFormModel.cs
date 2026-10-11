using System.ComponentModel.DataAnnotations;

namespace Bergdahl.LinkDash.Web.Models;

public class LoginFormModel
{
    [Required(ErrorMessage = "User name is required.")]
    [StringLength(100)]
    [Display(Name = "User name")]
    public string Username { get; set; } = string.Empty;

    [Required(ErrorMessage = "Password is required.")]
    [StringLength(256)]
    [DataType(DataType.Password)]
    public string Password { get; set; } = string.Empty;

    [Display(Name = "Keep me signed in")]
    public bool RememberMe { get; set; }

    /// <summary>
    /// The page to return to after signing in. Only local URLs are followed.
    /// </summary>
    public string? ReturnUrl { get; set; }
}
