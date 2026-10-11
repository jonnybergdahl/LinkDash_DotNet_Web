using System.Text.RegularExpressions;

namespace Bergdahl.LinkDash.Tests.Web;

public static partial class HttpClientFormExtensions
{
    /// <summary>
    /// Loads <paramref name="pageUrl"/> to obtain an antiforgery token and cookie, then posts the form.
    /// </summary>
    public static async Task<HttpResponseMessage> PostFormAsync(this HttpClient client, string pageUrl, string postUrl, Dictionary<string, string> fields)
    {
        var page = await client.GetAsync(pageUrl);
        page.EnsureSuccessStatusCode();
        var html = await page.Content.ReadAsStringAsync();
        var token = AntiforgeryTokenRegex().Match(html).Groups[1].Value;

        // The test client keeps the antiforgery cookie from the GET between requests.
        return await client.PostAsync(postUrl,
            new FormUrlEncodedContent(fields.Append(new("__RequestVerificationToken", token))));
    }

    /// <summary>
    /// Signs in through the login page; the client keeps the authentication cookie.
    /// </summary>
    public static Task<HttpResponseMessage> SignInAsync(this HttpClient client,
        string username = LinkDashWebApplicationFactory.AdminUsername, string password = LinkDashWebApplicationFactory.AdminPassword) =>
        client.PostFormAsync("/Account/Login", "/Account/Login", new()
        {
            ["Username"] = username,
            ["Password"] = password
        });

    [GeneratedRegex("name=\"__RequestVerificationToken\" type=\"hidden\" value=\"([^\"]+)\"")]
    private static partial Regex AntiforgeryTokenRegex();
}
