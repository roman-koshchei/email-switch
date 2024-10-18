namespace Api;

public class EmailInput
{
    public string FromEmail { get; set; } = string.Empty;
    public string? FromName { get; set; } = string.Empty;
    public string[] To { get; set; } = [];
    public string Subject { get; set; } = string.Empty;
    public string Text { get; set; } = string.Empty;
    public string Html { get; set; } = string.Empty;

    public static bool IsEmail(string email)
    {
        int index = email.IndexOf('@');

        return (
            index > 0 &&
            index != email.Length - 1 &&
            index == email.LastIndexOf('@')
        );
    }

    public bool IsValid()
    {
        if (string.IsNullOrWhiteSpace(FromEmail) || !IsEmail(FromEmail)) return false;
        if (To is null || To.Length == 0) return false;
        if (string.IsNullOrWhiteSpace(Subject)) return false;
        if (string.IsNullOrWhiteSpace(Html) && string.IsNullOrWhiteSpace(Text)) return false;
        return true;
    }
}