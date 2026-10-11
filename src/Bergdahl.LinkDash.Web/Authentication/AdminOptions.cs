namespace Bergdahl.LinkDash.Web.Authentication;

/// <summary>
/// The single administrator account that may use the settings pages. Bound from "LinkDash:Admin", which is optional:
/// when it has no password hash, the account created on first sign-in (<see cref="AdminAccountStore"/>) is used.
/// </summary>
public class AdminOptions
{
    public const string SectionName = "LinkDash:Admin";

    public string Username { get; set; } = "admin";

    /// <summary>
    /// Password hash created with the "hash-password" command.
    /// </summary>
    public string? PasswordHash { get; set; }
}
