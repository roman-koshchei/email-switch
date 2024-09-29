namespace Api;

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