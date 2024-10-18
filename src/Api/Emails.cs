using System.Net.Http.Headers;
using System.Net.Mail;
using System.Net;
using System.Text.Json;

namespace Api;

public interface IEmailSender
{
    /// <param name="fromEmail">Email adress from what you send.</param>
    /// <param name="fromName">Name of a sender.</param>
    /// <param name="to">List of email adresses of destination.</param>
    /// <param name="subject">Subject of email.</param>
    /// <param name="text">Text content.</param>
    /// <param name="html">Html content.</param>
    public Task<bool> TrySend(
        string fromEmail, string fromName,
        IEnumerable<string> to, string subject,
        string text, string html
    );
}

public class EmailSwitch(params IEmailSender[] senders) : IEmailSender
{
    private readonly IEnumerable<IEmailSender> senders = senders;

    public async Task<bool> TrySend(string fromEmail, string fromName, IEnumerable<string> to, string subject, string text, string html)
    {
        foreach (var sender in senders)
        {
            var sent = await sender.TrySend(fromEmail, fromName, to, subject, text, html);
            if (sent) return true;
        }

        return false;
    }
}

public class TestEmailSender : IEmailSender
{
    public Task<bool> TrySend(
        string fromEmail, string fromName, IEnumerable<string> to,
        string subject, string text, string html
    )
    {
        Console.WriteLine("Test email sender:");
        Console.WriteLine($"- From Email: {fromEmail}");
        Console.WriteLine($"- From Name: {fromName}");
        Console.WriteLine($"- To: {to}");
        Console.WriteLine($"- Subject: {subject}");
        Console.WriteLine($"- Text: {text}");

        return Task.FromResult(true);
    }

    public static IEmailSender Parse(JsonElement _) => new TestEmailSender();
}

/// <summary>
/// Send emails with Brevo service: <see href="https://www.brevo.com">brevo.com</see>
/// </summary>
public class BrevoSender(string token) : IEmailSender
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

/// <summary>
/// Send emails with Resend service: <see href="https://resend.com">resend.com</see>
/// </summary>
public class ResendSender(string token) : IEmailSender
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

/// <summary>
/// Send emails with SendGrid service: <see href="https://sendgrid.com/">sendgrid.com</see>
/// </summary>
public class SendGridSender(string token) : IEmailSender
{
    private const string url = "https://api.sendgrid.com/v3/mail/send";

    private readonly HttpClient httpClient = Http.JsonClient(_ =>
    {
        _.Authorization = new AuthenticationHeaderValue("Bearer", token);
    });

    public static string Id => "sendgrid";

    public static IEmailSender Parse(JsonElement jsonObject)
    {
        var token = jsonObject.GetProperty("token").GetString()!;
        return new SendGridSender(token);
    }

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

public class SmtpSender(string host, int port, string user, string password) : IEmailSender
{
    private readonly SmtpClient client = new()
    {
        Host = host,
        Port = port,
        Credentials = new NetworkCredential(user, password)
    };

    public static string Id => "smtp";

    public static IEmailSender Parse(JsonElement jsonObject)
    {
        var host = jsonObject.GetProperty("host").GetString()!;
        var port = jsonObject.GetProperty("port").GetInt32()!;
        var user = jsonObject.GetProperty("user").GetString()!;
        var password = jsonObject.GetProperty("password").GetString()!;
        return new SmtpSender(host, port, user, password);
    }

    public async Task<bool> TrySend(string fromEmail, string fromName, IEnumerable<string> to, string subject, string text, string html)
    {
        // change interface?
        var isHtml = string.IsNullOrWhiteSpace(html) is false;

        MailMessage message = new()
        {
            From = new MailAddress(fromEmail, fromName),
            Subject = subject,
            Body = isHtml ? text : html,
            IsBodyHtml = isHtml,
        };
        foreach (var item in to) { message.To.Add(item); }

        try
        {
            await client.SendMailAsync(message);
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }
}