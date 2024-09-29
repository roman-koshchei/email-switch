using System.Net.Http.Headers;
using System.Text.Json;

namespace Api.Senders;

/// <summary>
/// Send emails with Resend service: <see href="https://resend.com">resend.com</see>
/// </summary>
public class ResendSender(string token) : IEmailProvider
{
    private const string url = "https://api.resend.com/emails";

    private readonly HttpClient httpClient = Http.JsonClient(_ =>
    {
        _.Authorization = new AuthenticationHeaderValue("Bearer", token);
    });

    public static string Id => "resend";

    public static IEmailSender Parse(JsonElement jsonObject)
    {
        var token = jsonObject.GetProperty("token").GetString()!;
        return new ResendSender(token);
    }

    public async Task<bool> TrySend(
        string fromEmail, string fromName,
        IEnumerable<string> toEmails, string subject,
        string text, string html
    )
    {
        string jsonBody = $@"
        {{
            ""from"":""{fromName} <{fromEmail}>"",
            ""to"":[{string.Join(",", toEmails.Select(x => $@"""{x}"""))}],
            ""subject"":""{subject}"",
            ""text"":""{text}"",
            ""html"":""{html}""
        }}";

        var response = await Http.JsonPost(httpClient, url, jsonBody);
        if (response == null) return false;

        return response.IsSuccessStatusCode;
    }
}