using System.Net.Http.Headers;

namespace Api.Senders;

/// <summary>
/// Send emails with SendGrid service: <see href="https://sendgrid.com/">sendgrid.com</see>
/// </summary>
public class SendGridSender(string apiKey) : IEmailSender
{
    private const string url = "https://api.sendgrid.com/v3/mail/send";

    private readonly HttpClient httpClient = Http.JsonClient(_ =>
    {
        _.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
    });

    public async Task<bool> TrySend(
        string fromEmail, string fromName, IEnumerable<string> toEmails,
        string subject, string text, string html
    )
    {
        string jsonBody = @$"{{
            ""personalizations"":[{{
                ""to"":[{string.Join(",", toEmails.Select(x => $@"{{""email"":""{x}""}}"))}]
            }}],
            ""from"":{{
                ""email"":""{fromEmail}"",
                ""name"":""{fromName}""
            }},
            ""reply_to"":{{
                ""email"":""{fromEmail}"",
                ""name"":""{fromName}""
            }},
            ""subject"":""{subject}"",
            ""content"": [
                {{
                    ""type"": ""text/plain"",
                    ""value"": ""{text}""
                }},
                {{
                    ""type"": ""text/html"",
                    ""value"": ""{html}""
                }}
            ]
        }}";

        var response = await Http.JsonPost(httpClient, url, jsonBody);
        if (response == null) return false;

        return response.IsSuccessStatusCode;
        //if (response.IsSuccessStatusCode) return EmailStatus.Success;
        //if (response.StatusCode == HttpStatusCode.TooManyRequests) return EmailStatus.LimitReached;
        //return EmailStatus.Failed;
    }
}