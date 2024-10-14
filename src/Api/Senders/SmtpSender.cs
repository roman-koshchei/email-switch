using System.Net;
using System.Net.Mail;
using System.Text.Json;

namespace Api.Senders;

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