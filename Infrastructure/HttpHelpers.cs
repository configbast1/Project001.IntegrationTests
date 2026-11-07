using System.Net;
using System.Text.RegularExpressions;

namespace Project001.IntegrationTests.Infrastructure;

/// <summary>Вспомогательные методы для HTTP-тестов: антифорджери-токен, формы, вход.</summary>
public static class HttpHelpers
{
    private static readonly Regex TokenRegex = new(
        "<input[^>]*name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"",
        RegexOptions.Compiled);

    /// <summary>
    /// Загружает страницу с формой и достаёт из неё антифорджери-токен.
    /// Cookie токена сохраняется в клиенте автоматически.
    /// </summary>
    public static async Task<string> GetAntiforgeryTokenAsync(this HttpClient client, string pageUrl = "/Account/Login")
    {
        var html = await client.GetStringAsync(pageUrl);
        var match = TokenRegex.Match(html);
        Assert.True(match.Success, $"На странице {pageUrl} нет антифорджери-токена");
        return WebUtility.HtmlDecode(match.Groups[1].Value);
    }

    /// <summary>Отправляет форму вместе с действующим антифорджери-токеном.</summary>
    public static async Task<HttpResponseMessage> PostFormWithTokenAsync(
        this HttpClient client, string url, IEnumerable<KeyValuePair<string, string>> fields)
    {
        var token = await client.GetAntiforgeryTokenAsync();
        var all = fields.ToList();
        all.Add(new("__RequestVerificationToken", token));
        return await client.PostAsync(url, new FormUrlEncodedContent(all));
    }

    /// <summary>HTML ответа с раскодированными сущностями (кириллица в Razor кодируется как &amp;#x...;).</summary>
    public static async Task<string> ReadHtmlAsync(this HttpResponseMessage response)
        => WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

    /// <summary>Вход через настоящую форму /Account/Login.</summary>
    public static Task<HttpResponseMessage> LoginAsync(
        this HttpClient client, string email = TestUser.Email, string password = TestUser.Password, bool remember = false)
        => client.PostFormWithTokenAsync("/Account/Login", new Dictionary<string, string>
        {
            ["Email"] = email,
            ["Password"] = password,
            ["RememberMe"] = remember ? "true" : "false"
        });

    /// <summary>Значение заголовка Set-Cookie для cookie с указанным именем (или null).</summary>
    public static string? GetSetCookie(this HttpResponseMessage response, string cookieName)
        => response.Headers.TryGetValues("Set-Cookie", out var values)
            ? values.FirstOrDefault(v => v.StartsWith(cookieName + "=", StringComparison.Ordinal))
            : null;

    public static string LocationOf(this HttpResponseMessage response)
        => response.Headers.Location?.OriginalString ?? "";
}
