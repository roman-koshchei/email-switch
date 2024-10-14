
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Api.Senders;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Logging;
using Microsoft.IdentityModel.Tokens;

Env.LoadFile("./.env");
var ROOT_API_KEY = Env.GetRequired<string>("ROOT_API_KEY");

IdentityModelEventSource.ShowPII = true;

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

#endregion

var builder = WebApplication.CreateSlimBuilder(args);

builder.Logging.SetMinimumLevel(LogLevel.Information);
builder.Logging.AddFilter("Microsoft.AspNetCore", LogLevel.Warning);

builder.Services.ConfigureHttpJsonOptions(_ =>
{
    _.SerializerOptions.TypeInfoResolverChain.Insert(0, AppJsonSerializerContext.Default);
});

builder.Services.AddScoped((_) => new EmailSwitch([.. senders]));

var app = builder.Build();

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
        using var memoryStream = new MemoryStream();
        await req.Body.CopyToAsync(memoryStream);
        byte[] body = memoryStream.ToArray();

        var isLegit = VerifyQstashRequestWithKey(QSTASH_CURRENT_SIGNING_KEY, signature, body);
        if (isLegit is false)
        {
            isLegit = VerifyQstashRequestWithKey(QSTASH_NEXT_SIGNING_KEY, signature, body);
        }

        if (isLegit is false) { return Results.Forbid(); }

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

bool VerifyQstashRequestWithKey(string key, string signature, byte[] body)
{
    try
    {
        var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key));

        var validations = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            ValidateIssuer = true,
            ValidIssuer = "Upstash",
            IssuerSigningKey = securityKey,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(1),
            ValidateAudience = false
        };

        // TODO: validate url

        var tokenHandler = new JwtSecurityTokenHandler();
        var principal = tokenHandler.ValidateToken(signature, validations, out var _);

        var jwtBodyHash = principal.Claims.FirstOrDefault(x => x.Type == "body")?.ToString()?.Replace("=", "");
        if (jwtBodyHash is null) { return false; }

        var bodyHash = SHA256.HashData(body);
        var base64Hash = Convert.ToBase64String(bodyHash).Replace("=", "");
        if (jwtBodyHash != base64Hash) { return false; }

        return true;
    }
    catch (Exception ex)
    {
        Console.WriteLine(ex.ToString());
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

#region json

[JsonSerializable(typeof(EmailInput))]
[JsonSerializable(typeof(ProblemDetails))]
internal partial class AppJsonSerializerContext : JsonSerializerContext
{
}

#endregion

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

#endregion

#region senders

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

#endregion