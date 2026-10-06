using GilSweep.Core.Platform;

namespace GilSweep.Tests.Support;

/// <summary>A throwaway data folder (and a v1 folder beside it) under the temp directory.</summary>
public sealed class TestEnvironment : IAppEnvironment, IDisposable
{
    public TestEnvironment()
    {
        Root = Path.Combine(Path.GetTempPath(), "gil-sweep-tests-" + Guid.NewGuid().ToString("N")[..10]);
        DataDirectory = Path.Combine(Root, "GilSweep");
        LegacyDataDirectory = Path.Combine(Root, "gil-sweep");
        Directory.CreateDirectory(DataDirectory);
    }

    public string Root { get; }

    public string DataDirectory { get; }

    public string LegacyDataDirectory { get; }

    public void Dispose()
    {
        try
        {
            Directory.Delete(Root, recursive: true);
        }
        catch (IOException)
        {
        }
    }
}

/// <summary>A clock tests move by hand.</summary>
public sealed class TestClock(DateTimeOffset now) : TimeProvider
{
    public DateTimeOffset Now { get; set; } = now;

    public override DateTimeOffset GetUtcNow() => Now.ToUniversalTime();

    public void Advance(TimeSpan time) => Now += time;
}
