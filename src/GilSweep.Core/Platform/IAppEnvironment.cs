namespace GilSweep.Core.Platform;

/// <summary>Where Gil Sweep keeps its files. Tests point it at a temp folder.</summary>
public interface IAppEnvironment
{
    /// <summary>Settings, snapshots, the alert log and logs: %APPDATA%\GilSweep.</summary>
    string DataDirectory { get; }

    /// <summary>Where v1 (the Electron app) kept its files: %APPDATA%\gil-sweep.</summary>
    string LegacyDataDirectory { get; }

    /// <summary>The data folder as shown on screen, without the Windows user name.</summary>
    string DataDirectoryLabel => DataDirectory;
}

public sealed class SystemAppEnvironment : IAppEnvironment
{
    /// <summary>Points Gil Sweep at another data folder, for a second profile or trying a build without touching your own.</summary>
    public const string HomeVariable = "GIL_SWEEP_HOME";

    public SystemAppEnvironment()
    {
        var roaming = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var home = Environment.GetEnvironmentVariable(HomeVariable);
        DataDirectory = string.IsNullOrWhiteSpace(home) ? Path.Combine(roaming, "GilSweep") : Path.GetFullPath(home);

        // With a home of its own, nothing is imported from v1.
        LegacyDataDirectory = string.IsNullOrWhiteSpace(home) ? Path.Combine(roaming, "gil-sweep") : Path.Combine(DataDirectory, "v1");
    }

    public string DataDirectory { get; }

    public string LegacyDataDirectory { get; }

    public string DataDirectoryLabel =>
        string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(HomeVariable)) ? @"%AppData%\GilSweep" : DataDirectory;
}
