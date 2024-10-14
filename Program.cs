using System.Net.Http.Headers;
using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Cryptography;
using System.Net.Mail;

Env.LoadFile("./.env");
var ROOT_API_KEY = Env.GetRequired<string>("ROOT_API_KEY");

#region providers

JsonDocument providersJson;
var PROVIDERS_VALUE = Env.GetOptionalRef<string>("PROVIDERS_VALUE");
if (string.IsNullOrWhiteSpace(PROVIDERS_VALUE) is false)
{
    providersJson = JsonDocument.Parse(PROVIDERS_VALUE);
}
else
{
    var PROVIDERS_FILE = Env.GetOptionalRef<string>("PROVIDERS_FILE") ?? "./providers.json";
    providersJson = JsonDocument.Parse(await File.ReadAllBytesAsync(PROVIDERS_FILE));
}

// I know, "Factories" wtf. I have no other name for this functions.
(string, Func<JsonElement, IEmailSender>)[] providersFactories = [
    ("resend", ResendSender.Parse),
    ("smtp", SmtpSender.Parse),
    ("sendgrid", SendGridSender.Parse),
    ("test", TestEmailSender.Parse),
];

List<IEmailSender> senders = [];
foreach (var obj in providersJson.RootElement.EnumerateArray())
{
    var id = obj.GetProperty("id").GetString();
    var providerFactory = providersFactories.FirstOrDefault(x => x.Item1 == id).Item2;
    if (providerFactory is not null)
    {
        senders.Add(providerFactory(obj));
    }
    else
    {
        var prevColor = Console.ForegroundColor;
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine($"No provider was found with id: {id}");
        Console.ForegroundColor = prevColor;
    }
}

#endregion providers

var builder = WebApplication.CreateBuilder(args);

builder.Logging.SetMinimumLevel(LogLevel.Information);
builder.Logging.AddFilter("Microsoft.AspNetCore", LogLevel.Warning);

builder.Services.AddScoped((_) => new EmailSwitch([.. senders]));

var app = builder.Build();

// Configure the HTTP request pipeline.

app.UseHttpsRedirection();

var api = app.MapGroup("/api");
api.MapPost("/emails", async (
    [FromHeader(Name = "Authorization")] string authHeader,
    [FromBody] EmailInput input,
    [FromServices] EmailSwitch email
) =>
{
    if (IsAuthorized(authHeader) is false) return Results.Forbid();

    if (input.IsValid() is false) return Results.BadRequest("Body has wrong shape");

    var sent = await email.TrySend(
        fromEmail: input.FromEmail,
        fromName: input.FromName ?? input.FromEmail,
        to: input.To,
        subject: input.Subject,
        text: input.Text,
        html: input.Html
    );

    if (sent) { return Results.Ok(); }
    return Results.Problem();
});

// #region qstash
// QStash queue integration for fun

var QSTASH = Env.GetOptionalVal<bool>("QSTASH") ?? false;
Console.WriteLine($"Qstash is enabled: {QSTASH}");
if (QSTASH)
{
    var QSTASH_CURRENT_SIGNING_KEY = Env.GetRequired<string>("QSTASH_CURRENT_SIGNING_KEY");
    var QSTASH_NEXT_SIGNING_KEY = Env.GetRequired<string>("QSTASH_NEXT_SIGNING_KEY");

    api.MapPost("/qstash", async (
        [FromHeader(Name = "Upstash-Signature")] string signature,
        [FromBody] EmailInput input,
        [FromServices] EmailSwitch email,
        HttpRequest req
    ) =>
    {
        req.EnableBuffering();

        using var reader = new StreamReader(req.Body, Encoding.UTF8);
        var body = await reader.ReadToEndAsync();
        req.Body.Seek(0, SeekOrigin.Begin);

        var isLegit = VerifyQstashRequestWithKey(QSTASH_CURRENT_SIGNING_KEY, signature, body);
        if (isLegit is false)
        {
            isLegit = VerifyQstashRequestWithKey(QSTASH_NEXT_SIGNING_KEY, signature, body);
        }

        if (isLegit is false) { return Results.BadRequest(); }

        if (input.IsValid() is false) return Results.BadRequest("Body has wrong shape");

        var sent = await email.TrySend(
            fromEmail: input.FromEmail,
            fromName: input.FromName ?? input.FromEmail,
            to: input.To,
            subject: input.Subject,
            text: input.Text,
            html: input.Html
        );

        if (sent) { return Results.Ok(); }
        return Results.Problem();
    });
}

bool VerifyQstashRequestWithKey(string key, string token, string body)
{
    try
    {
        // Console.WriteLine($"KEY USED: {key}");
        var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key));

        var validations = new TokenValidationParameters
        {
            ValidateIssuer = false,
            ValidIssuer = "Upstash",
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = securityKey,
            ValidateLifetime = true,
            // RequireSignedTokens = true,
            ConfigurationManager = null,
            ValidAlgorithms = new[] { SecurityAlgorithms.HmacSha256 },
            // ClockSkew = TimeSpan.FromSeconds(1),
            ValidateAudience = false
        };

        // TODO: validate url

        var tokenHandler = new JwtSecurityTokenHandler();
        var principal = tokenHandler.ValidateToken(token, validations, out var _);


        var jwtBodyHash = principal.Claims.FirstOrDefault(x => x.Type == "body")?.ToString();
        Console.WriteLine($"JWT BODY HASH: {jwtBodyHash}");
        jwtBodyHash = jwtBodyHash?.Replace("=", "");
        Console.WriteLine($"JWT BODY HASH: {jwtBodyHash}");
        if (jwtBodyHash is null)
        {
            Console.WriteLine("Body claim isn't found");
            return false;
        }

        var bodyHash = SHA256.HashData(Convert.FromBase64String(body));
        var base64Hash = Convert.ToBase64String(bodyHash);
        Console.WriteLine($"REAL BODY HASH: {base64Hash}");
        base64Hash = base64Hash.Replace("=", "");
        Console.WriteLine($"REAL BODY HASH: {base64Hash}");
        if (jwtBodyHash != base64Hash)
        {
            Console.WriteLine("Hashes aren't equal");
            return false;
        }

        return true;
    }
    catch (Exception)
    {
        return false;
    }
}

// #endregion

app.Run();

bool IsAuthorized(string header)
{
    var parts = header.Split(' ', StringSplitOptions.RemoveEmptyEntries);
    if (parts.Length != 2) return false;

    var key = parts[1];
    return key == ROOT_API_KEY;
}

#region inputs

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

#endregion inputs

#region senders

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

#endregion senders

#region helpers

public static class Http
{
    /// <summary>Create HttpClient to work with json.</summary>
    /// <param name="setHeaders">Action to set additional headers to client.</param>
    public static HttpClient JsonClient(Action<HttpRequestHeaders> setHeaders)
    {
        HttpClient client = new();

        client.DefaultRequestHeaders.Add("Accept", "application/json");
        setHeaders(client.DefaultRequestHeaders);

        return client;
    }

    /// <summary>
    /// Send POST request to specified url with json body.
    /// </summary>
    /// <param name="body">JSON string</param>
    /// <returns>Null if exception appeared, response otherwise.</returns>
    public static async Task<HttpResponseMessage?> JsonPost(HttpClient client, string url, string body)
    {
        try
        {
            return await client.PostAsync(url, new StringContent(body, Encoding.UTF8, "application/json"));
        }
        catch
        {
            return null;
        }
    }

    /// <summary>Check if status code is successful.</summary>
    /// <returns>True if success, false if not.</returns>
    public static bool IsSuccessful(int statusCode)
    {
        return (statusCode >= 200) && (statusCode <= 299);
    }

    /// <summary>Check if status code is successful.</summary>
    /// <returns>True if success, false if not.</returns>
    public static bool IsSuccessful(HttpStatusCode statusCode)
    {
        return ((int)statusCode >= 200) && ((int)statusCode <= 299);
    }
}

/// <summary>
/// Helper to safely use Env variables
/// </summary>
public class Env
{
    private static IEnvStrategy strategy = new EnvThrowStrategy();

    /// <summary>
    /// Load environment variables from file if file exists.
    /// If file doesn't exist then it just do nothing.
    /// <param name="path">Path to environment file.</param>
    /// </summary>
    public static void LoadFile(string path)
    {
        if (!File.Exists(path)) return;

        foreach (var line in File.ReadAllLines(path))
        {
            int separatorIndex = line.IndexOf('=');

            var key = line[..separatorIndex].Trim();
            var value = line[(separatorIndex + 1)..].Trim();

            Environment.SetEnvironmentVariable(key, value);
        }
    }

    /// <summary>
    /// Gets the required environment variable. <para />
    /// If you used <see cref="Ensure"/> then exception will be collected
    /// into the list returned from <see cref="Ensure"/>. <para />
    /// Otherwise, the method will throw <see cref="KeyNotFoundException"/>.
    /// </summary>
    public static string GetRequired(string key)
    {
        return strategy.GetRequired(key);
    }

    /// <summary>Get optional environment variable.</summary>
    /// <returns>Null if not found, otherwise value</returns>
    public static string? GetOptional(string key)
    {
        return Environment.GetEnvironmentVariable(key);
    }

    public static T GetRequired<T>(string key) where T : IParsable<T>
    {
        return strategy.GetRequired<T>(key);
    }

    /// <summary>Get optional environment variable of class <typeparamref name="T"/>.</summary>
    /// <returns>Null if not found, otherwise value</returns>
    public static T? GetOptionalRef<T>(string key) where T : class, IParsable<T>
    {
        var value = GetOptional(key);
        if (value == null) return null;
        try
        {
            return T.Parse(value, null);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>Get optional environment variable of value struct <typeparamref name="T"/>.</summary>
    /// <returns>Null if not found, otherwise value</returns>
    public static T? GetOptionalVal<T>(string key) where T : struct, IParsable<T>
    {
        var value = GetOptional(key);
        if (value == null) return null;
        try
        {
            return T.Parse(value, null);
        }
        catch
        {
            return null;
        }
    }
}

internal interface IEnvStrategy
{
    public string GetRequired(string key);

    public T GetRequired<T>(string key) where T : IParsable<T>;
}

internal class EnvThrowStrategy : IEnvStrategy
{
    public T? GetOptionalVal<T>(string key) where T : struct, IParsable<T>
    {
        throw new NotImplementedException();
    }

    public string GetRequired(string key)
    {
        return Environment.GetEnvironmentVariable(key)
            ?? throw new KeyNotFoundException($"Env variable '{key}' isn't found");
    }

    public T GetRequired<T>(string key) where T : IParsable<T>
    {
        var value = GetRequired(key);
        try
        {
            return T.Parse(value, null);
        }
        catch (Exception ex)
        {
            throw new FormatException(
                $"Environment variable '{key}' can't be parsed into {nameof(T)} type!", ex
            );
        }
    }
}

#endregion helpers