using Api;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;
using System.Text.Json.Serialization;

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

var builder = WebApplication.CreateSlimBuilder(args);

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.TypeInfoResolverChain.Insert(0, AppJsonSerializerContext.Default);
    options.SerializerOptions.PropertyNameCaseInsensitive = true;
});

// Logging, I don't want to have appsettings.json
builder.Logging.SetMinimumLevel(LogLevel.Information);
builder.Logging.AddFilter("Microsoft.AspNetCore", LogLevel.Warning);

// Adding Email Switch, probably can be reused instead of creating new one each time
builder.Services.AddScoped((_) => new EmailSwitch([.. senders]));

var app = builder.Build();

var api = app.MapGroup("/api");

// Sending emails
api.MapPost("/emails",
async (
    [FromHeader(Name = "Authorization")] string authHeader,
    [FromBody] EmailInput input,
    [FromServices] EmailSwitch email
) =>
{
    if (IsAuthorized(authHeader) is false) return Results.Unauthorized();

    if (input.IsValid() is false) return Results.BadRequest("Body isn't valid");

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

// Optional QStash integration
var QSTASH = Env.GetOptionalVal<bool>("QSTASH") ?? false;
if (QSTASH)
{
    var QSTASH_CURRENT_SIGNING_KEY = Env.GetRequired<string>("QSTASH_CURRENT_SIGNING_KEY");
    var QSTASH_NEXT_SIGNING_KEY = Env.GetRequired<string>("QSTASH_NEXT_SIGNING_KEY");

    QStash.MapQStash(api, QSTASH_CURRENT_SIGNING_KEY, QSTASH_NEXT_SIGNING_KEY);
}

app.UseHttpsRedirection();

app.Run();

bool IsAuthorized(string header)
{
    var parts = header.Split(' ', StringSplitOptions.RemoveEmptyEntries);
    if (parts.Length != 2) return false;

    var key = parts[1];
    return key == ROOT_API_KEY;
}

[JsonSerializable(typeof(EmailInput))]
internal partial class AppJsonSerializerContext : JsonSerializerContext
{
}