namespace Api.Senders;

/// <summary>
/// Send emails with Brevo service: <see href="https://www.brevo.com">brevo.com</see>
/// </summary>
public class Brevo(string token) : IEmailSender
{
    private const string url = "https://api.brevo.com/v3/smtp/email";
    private readonly HttpClient httpClient = Http.JsonClient(_ => _.Add("api-key", token));

    public async Task<bool> TrySend(
        string fromEmail, string fromName, IEnumerable<string> to,
        string subject, string text, string html
    )
    {
        string jsonBody = $@"
        {{
            ""sender"":{{""name"":""{fromName}"",""email"":""{fromEmail}""}},
            ""to"":[{string.Join(",", to.Select(x => $@"{{""email"":""{x}""}}"))}],
            ""subject"":""{subject}"",
            ""htmlContent"":""{html}"",
            ""textContent"":""{text}""
        }}";

        var response = await Http.JsonPost(httpClient, url, jsonBody);
        if (response == null) return false;

        return response.IsSuccessStatusCode;
    }
}