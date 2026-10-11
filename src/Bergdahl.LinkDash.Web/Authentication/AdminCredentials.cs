using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace Bergdahl.LinkDash.Web.Authentication;

/// <summary>
/// Checks sign-ins against the administrator account: the one in <see cref="AdminOptions"/> when configured,
/// otherwise the one created on first sign-in (<see cref="AdminAccountStore"/>). Looks the account up on
/// every call, so a changed password takes effect without a restart.
/// </summary>
public class AdminCredentials(IOptionsMonitor<AdminOptions> options, AdminAccountStore store)
{
    /// <summary>
    /// Claim holding a fingerprint of the credentials the cookie was issued for. Changing the user name
    /// or password changes the fingerprint, which signs out every existing session.
    /// </summary>
    public const string StampClaimType = "linkdash:stamp";

    public const int MinPasswordLength = 6;

    private static readonly PasswordHasher<AdminOptions> Hasher = new();

    // Verified against when the user name is wrong, so a failed sign-in takes as long either way.
    private static readonly string DummyHash = Hasher.HashPassword(new AdminOptions(), Guid.NewGuid().ToString());

    public static string HashPassword(string password) => Hasher.HashPassword(new AdminOptions(), password);

    /// <summary>
    /// True once there is an account to sign in with; until then the create account page is shown.
    /// </summary>
    public bool HasAccount => CurrentAccount() is not null;

    /// <summary>
    /// Creates the account on first sign-in. Returns null when an account already exists.
    /// </summary>
    public ClaimsPrincipal? CreateAccount(string username, string password)
    {
        if (HasAccount)
        {
            return null;
        }

        var account = new AdminOptions { Username = username.Trim(), PasswordHash = HashPassword(password) };
        return store.TryCreate(account) ? CreatePrincipal(account) : null;
    }

    /// <summary>
    /// Returns the principal to sign in, or null when the user name or password is wrong.
    /// </summary>
    public ClaimsPrincipal? Authenticate(string? username, string? password)
    {
        var account = CurrentAccount();
        if (account is null || string.IsNullOrEmpty(password))
        {
            return null;
        }

        var usernameMatches = string.Equals(username?.Trim(), account.Username.Trim(), StringComparison.OrdinalIgnoreCase);
        var result = Hasher.VerifyHashedPassword(account, usernameMatches ? account.PasswordHash! : DummyHash, password);
        return usernameMatches && result != PasswordVerificationResult.Failed ? CreatePrincipal(account) : null;
    }

    /// <summary>
    /// False when the credentials changed after <paramref name="principal"/> signed in, or the account was removed.
    /// </summary>
    public bool IsCurrent(ClaimsPrincipal? principal) =>
        CurrentAccount() is { } account && principal?.FindFirst(StampClaimType)?.Value == Stamp(account);

    private AdminOptions? CurrentAccount()
    {
        var configured = options.CurrentValue;
        if (IsComplete(configured))
        {
            return configured;
        }

        var stored = store.Load();
        return IsComplete(stored) ? stored : null;
    }

    private static ClaimsPrincipal CreatePrincipal(AdminOptions account) =>
        new(new ClaimsIdentity(
            [new Claim(ClaimTypes.Name, account.Username.Trim()), new Claim(StampClaimType, Stamp(account))],
            CookieAuthenticationDefaults.AuthenticationScheme));

    private static string Stamp(AdminOptions account) =>
        Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes($"{account.Username.Trim().ToUpperInvariant()}\n{account.PasswordHash}")));

    private static bool IsComplete(AdminOptions? account) =>
        account is not null && !string.IsNullOrWhiteSpace(account.Username) && IsWellFormedHash(account.PasswordHash);

    private static bool IsWellFormedHash(string? hash) =>
        !string.IsNullOrWhiteSpace(hash) && Convert.TryFromBase64String(hash, new byte[hash.Length], out var length) && length > 0;
}
