namespace Api.Senders;

/// <summary>Care only about sending email. Limits are controlled by ILimiter.</summary>
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