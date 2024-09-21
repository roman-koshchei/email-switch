using System.Text.Json.Serialization;

[JsonSerializable(typeof(EmailInput))]
internal partial class AppJsonSerializerContext : JsonSerializerContext
{
}