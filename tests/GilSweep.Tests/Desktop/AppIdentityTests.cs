using System.Reflection;
using GilSweep.Desktop;
using GilSweep.Desktop.Services;

namespace GilSweep.Tests.Desktop;

/// <summary>Windows shows the app as "Gil Sweep" everywhere: Task Manager, Start menu, Apps, toasts.</summary>
public sealed class AppIdentityTests
{
    private static readonly Assembly Desktop = typeof(App).Assembly;

    [Fact]
    public void Task_manager_and_file_properties_read_gil_sweep()
    {
        // The exe's FileDescription and ProductName come from these.
        Assert.Equal("Gil Sweep", Desktop.GetCustomAttribute<AssemblyTitleAttribute>()?.Title);
        Assert.Equal("Gil Sweep", Desktop.GetCustomAttribute<AssemblyProductAttribute>()?.Product);
    }

    [Fact]
    public void Toasts_use_the_start_menu_shortcuts_app_id()
    {
        // Velopack names the shortcut's AppUserModelID velopack.<packId>; release.yml packs GilSweep.
        var release = File.ReadAllText(Path.Combine(RepoRoot(), ".github", "workflows", "release.yml"));
        Assert.Contains("--packId GilSweep ", release, StringComparison.Ordinal);
        Assert.Contains("--packTitle \"Gil Sweep\"", release, StringComparison.Ordinal);
        Assert.Equal("velopack.GilSweep", WindowsToastNotifier.AppId);
    }

    private static string RepoRoot()
    {
        var folder = new DirectoryInfo(AppContext.BaseDirectory);
        while (folder is not null && !File.Exists(Path.Combine(folder.FullName, "GilSweep.sln")))
        {
            folder = folder.Parent;
        }

        return folder?.FullName ?? throw new InvalidOperationException("Run the tests from inside the repository.");
    }
}
