using System.Text.Json;
using System.Text.Json.Nodes;
using Api;
using Api.Senders;
using Microsoft.AspNetCore.Mvc;

Env.LoadFile("./.env");
var ROOT_API_KEY = Env.GetRequired<string>("ROOT_API_KEY");



var email = new EmailSwitch(
    new ResendSender("resend-api-token"),
    new SmtpSender("tsrctcrs", 456, "user", "password")
);

// providers loading
JsonDocument providersJson = JsonDocument.Parse(await File.ReadAllBytesAsync("./providers.json"));
foreach (var obj in providersJson.RootElement.EnumerateArray())
{
    var id = obj.GetProperty("id").GetString();

}



var builder = WebApplication.CreateSlimBuilder(args);

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.TypeInfoResolverChain.Insert(0, AppJsonSerializerContext.Default);
});

var app = builder.Build();

bool IsAuthorized(string header)
{
    var parts = header.Split(' ', StringSplitOptions.RemoveEmptyEntries);
    if (parts.Length != 2) return false;

    var key = parts[1];
    return key == ROOT_API_KEY;
}



var api = app.MapGroup("/api");
api.MapPost("/emails", async (
    [FromHeader(Name = "Authorization")] string authHeader,
    [FromBody] EmailInput input
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

app.Run();

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