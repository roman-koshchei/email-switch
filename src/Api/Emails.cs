using System.Net;
using System.Net.Mail;

namespace Api;

/// <summary>Care only about sending email. Limits are controlled by ILimiter.</summary>
public interface IEmailSender
{
    /// <param name="fromEmail">Email adress from what you send.</param>
    /// <param name="fromName">Name of a sender.</param>
    /// <param name="to">List of email adresses of destination.</param>
    /// <param name="subject">Subject of email.</param>
    /// <param name="text">Text content.</param>
    /// <param name="html">Html content.</param>
    public Task<bool> Send(
        string fromEmail, string fromName,
        IEnumerable<string> to, string subject,
        string text, string html
    );
}

/// <summary>
/// Send emails with Resend service: <see href="https://resend.com">resend.com</see>
/// </summary>
public class ResendSender(string token) : IEmailSender
{
    private const string url = "https://api.resend.com/emails";

    private readonly HttpClient httpClient = Http.JsonClient(_ =>
    {
        _.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
    });

    public async Task<bool> Send(
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

public class SmtpSender(string host, int port, string user, string password) : IEmailSender
{
    private readonly SmtpClient client = new()
    {
        Host = host,
        Port = port,
        Credentials = new NetworkCredential(user, password)
    };

    public async Task<bool> Send(string fromEmail, string fromName, IEnumerable<string> to, string subject, string text, string html)
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