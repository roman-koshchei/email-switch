using System.Text.Json;

namespace Api.Senders;

public interface IEmailProvider : IEmailSender
{
  public static abstract string Id { get; }

  public static abstract IEmailSender Parse(JsonElement jsonObject);
}