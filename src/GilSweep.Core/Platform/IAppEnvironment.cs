namespace GilSweep.Core.Platform;

/// <summary>Where Gil Sweep keeps its files. Tests point it at a temp folder.</summary>
public interface IAppEnvironment
{
    /// <summary>Settings, snapshots, the alert log and logs: %APPDATA%\GilSweep.</summary>
    string DataDirectory { get; }

    /// <summary>Where v1 (the Electron app) kept its files: %APPDATA%\gil-sweep.</summary>
    string LegacyDataDirectory { get; }
}

public sealed class SystemAppEnvironment : IAppEnvironment
{
    private static readonly string RoamingAppData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);

    public string DataDirectory { get; } = Path.Combine(RoamingAppData, "GilSweep");

    public string LegacyDataDirectory { get; } = Path.Combine(RoamingAppData, "gil-sweep");
}
