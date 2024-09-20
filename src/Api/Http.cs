using System.Net.Http.Headers;
using System.Net;
using System.Text.Json;
using System.Text;

namespace Api;

/// <summary>Helper to work with Http.</summary>
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

    /// <summary>Read json from <paramref name="response"/>.</summary>
    /// <returns>Null if error otherwise data of type <typeparamref name="T"/></returns>
    public static async Task<T?> JsonResponse<T>(HttpResponseMessage response) where T : class
    {
        try
        {
            var jsonStream = await response.Content.ReadAsStreamAsync();
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var data = await JsonSerializer.DeserializeAsync<T>(jsonStream, options);
            return data;
        }
        catch
        {
            return null;
        }
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