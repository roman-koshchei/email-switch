using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Api;

public static class QStash
{
    public static RouteHandlerBuilder MapQStash(
        IEndpointRouteBuilder builder,
        string qstashCurrentSigningKey, string qstashNextSigningKey
    )
    {
        return builder.MapPost("/qstash",
        async (
            [FromHeader(Name = "Upstash-Signature")] string signature,
            [FromServices] EmailSwitch email,
            HttpRequest req
        ) =>
        {
            using var reader = new StreamReader(req.Body, Encoding.UTF8);
            var body = await reader.ReadToEndAsync();

            var isLegit = VerifyRequestWithKey(qstashCurrentSigningKey, signature, body);
            if (isLegit is false)
            {
                isLegit = VerifyRequestWithKey(qstashNextSigningKey, signature, body);
            }
            if (isLegit is false) { return Results.BadRequest(); }

            EmailInput input;
            try
            {
                input = JsonSerializer.Deserialize(body, AppJsonSerializerContext.Default.EmailInput)!;
            }
            catch (Exception)
            {
                return Results.BadRequest("Can't deserialize body");
            }
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

    private static bool VerifyRequestWithKey(string key, string token, string body)
    {
        try
        {
            var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key));

            var validations = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = "Upstash",
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = securityKey,
                ValidateLifetime = true,
                ClockSkew = TimeSpan.FromSeconds(1),
                ValidateAudience = false
            };

            // TODO: validate url

            var tokenHandler = new JwtSecurityTokenHandler();
            var principal = tokenHandler.ValidateToken(token, validations, out var _);

            var jwtBodyHash = principal.Claims.FirstOrDefault(x => x.Type == "body")?.Value?.Replace("=", "");
            if (jwtBodyHash is null) { return false; }

            var bodyHash = SHA256.HashData(Encoding.UTF8.GetBytes(body));
            var base64Hash = WebEncoders.Base64UrlEncode(bodyHash).Replace("=", "");
            if (jwtBodyHash.Equals(base64Hash) is false) { return false; }

            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }
}