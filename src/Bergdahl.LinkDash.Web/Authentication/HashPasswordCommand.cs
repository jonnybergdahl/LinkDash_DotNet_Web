using System.Text;

namespace Bergdahl.LinkDash.Web.Authentication;

/// <summary>
/// "hash-password" command line mode: asks for a password and prints the hash to put in
/// <c>LinkDash:Admin:PasswordHash</c>, instead of starting the web server.
/// </summary>
public static class HashPasswordCommand
{
    public const string Name = "hash-password";

    public static int Run()
    {
        var password = ReadPassword("Password: ");
        if (string.IsNullOrEmpty(password))
        {
            Console.Error.WriteLine("The password cannot be empty.");
            return 1;
        }

        if (!Console.IsInputRedirected && ReadPassword("Repeat password: ") != password)
        {
            Console.Error.WriteLine("The passwords do not match.");
            return 1;
        }

        Console.Error.WriteLine("Put this hash in LinkDash:Admin:PasswordHash:");
        Console.WriteLine(AdminCredentials.HashPassword(password));
        return 0;
    }

    /// <summary>
    /// Reads a line without echoing it, or a plain line when the input is piped.
    /// </summary>
    private static string? ReadPassword(string prompt)
    {
        if (Console.IsInputRedirected)
        {
            return Console.ReadLine();
        }

        Console.Error.Write(prompt);
        var password = new StringBuilder();
        while (true)
        {
            var key = Console.ReadKey(intercept: true);
            if (key.Key == ConsoleKey.Enter)
            {
                Console.Error.WriteLine();
                return password.ToString();
            }

            if (key.Key == ConsoleKey.Backspace)
            {
                if (password.Length > 0)
                {
                    password.Length--;
                }
            }
            else if (!char.IsControl(key.KeyChar))
            {
                password.Append(key.KeyChar);
            }
        }
    }
}
