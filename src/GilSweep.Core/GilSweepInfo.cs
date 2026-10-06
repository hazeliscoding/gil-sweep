using System.Reflection;

namespace GilSweep.Core;

public static class GilSweepInfo
{
    public const string RepositoryUrl = "https://github.com/hazeliscoding/gil-sweep";

    /// <summary>Sent with every market request, so the API owners can see who is calling.</summary>
    public static string UserAgent => $"GilSweep/{Version} (+{RepositoryUrl})";

    public static string Version { get; } = ReadVersion();

    private static string ReadVersion()
    {
        var informational = typeof(GilSweepInfo).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        if (string.IsNullOrEmpty(informational))
        {
            return typeof(GilSweepInfo).Assembly.GetName().Version?.ToString(3) ?? "0.0.0";
        }

        // The SDK appends "+<commit>" to the informational version; users only need the release number.
        var plus = informational.IndexOf('+');
        return plus < 0 ? informational : informational[..plus];
    }
}
