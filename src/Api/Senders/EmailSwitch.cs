namespace Api.Senders;

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