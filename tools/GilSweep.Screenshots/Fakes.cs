using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using GilSweep.Core.Platform;
using GilSweep.Desktop.Services;

namespace GilSweep.Screenshots;

internal sealed class FakeEnvironment(string root) : IAppEnvironment
{
    public string DataDirectory { get; } = Path.Combine(root, "GilSweep");

    public string LegacyDataDirectory { get; } = Path.Combine(root, "gil-sweep");

    public string DataDirectoryLabel => @"%AppData%\GilSweep";
}

/// <summary>A settable clock, so sweeps can be made "in the past" and shown from a fixed "now".</summary>
internal sealed class FakeClock(DateTimeOffset now) : TimeProvider
{
    public DateTimeOffset Now { get; set; } = now;

    public override DateTimeOffset GetUtcNow() => Now.ToUniversalTime();
}

/// <summary>
/// Universalis and Saddlebag Exchange answered from the recorded fixtures (the tests' own), so
/// screenshots never depend on the live market. Can fail every request, or hold them.
/// </summary>
internal sealed partial class FixtureMarket : HttpMessageHandler
{
    private readonly Dictionary<int, JsonNode> _aggregated;
    private readonly JsonObject _current;
    private readonly JsonObject _history;
    private readonly string _saddlebag;
    private readonly string _worlds;

    public FixtureMarket(string fixtures)
    {
        JsonNode Read(params string[] parts) => JsonNode.Parse(File.ReadAllText(Path.Combine([fixtures, .. parts])))!;
        _aggregated = ((JsonArray)Read("Universalis", "aggregated-cactuar.json")["results"]!).ToDictionary(node => (int)node!["itemId"]!, node => node!);
        _current = Read("Universalis", "current-cactuar.json")["items"]!.AsObject();
        _history = Read("Universalis", "history-cactuar.json")["items"]!.AsObject();
        _saddlebag = File.ReadAllText(Path.Combine(fixtures, "Saddlebag", "marketshare-cactuar.json"));
        _worlds = File.ReadAllText(Path.Combine(fixtures, "Universalis", "worlds.json"));
    }

    public bool Down { get; set; }

    /// <summary>While set, requests wait here: the loading state.</summary>
    public TaskCompletionSource? Hold { get; set; }

    /// <summary>
    /// The recordings are from one moment; sale times shift by this much so "recent" sales stay
    /// recent relative to the screenshot clock.
    /// </summary>
    public TimeSpan Shift { get; set; }

    protected override void Dispose(bool disposing)
    {
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (Hold is { } hold)
        {
            await hold.Task.WaitAsync(cancellationToken);
        }

        if (Down)
        {
            return new HttpResponseMessage(HttpStatusCode.ServiceUnavailable);
        }

        var uri = request.RequestUri!;
        if (uri.Host == "api.saddlebagexchange.com")
        {
            return Json(_saddlebag);
        }

        if (uri.AbsolutePath == "/api/v2/worlds")
        {
            return Json(_worlds);
        }

        Match m;
        if ((m = AggregatedPath().Match(uri.AbsolutePath)).Success)
        {
            return Json(new JsonObject { ["results"] = new JsonArray([.. Ids(m).Where(_aggregated.ContainsKey).Select(id => _aggregated[id].DeepClone())]) }.ToJsonString());
        }

        if ((m = HistoryPath().Match(uri.AbsolutePath)).Success)
        {
            return Lookup(_history, Ids(m), "entries");
        }

        if ((m = CurrentPath().Match(uri.AbsolutePath)).Success)
        {
            return Lookup(_current, Ids(m), "recentHistory");
        }

        return new HttpResponseMessage(HttpStatusCode.NotFound);
    }

    private HttpResponseMessage Lookup(JsonObject source, List<int> ids, string sales)
    {
        JsonNode? Item(int id)
        {
            if (source[id.ToString(CultureInfo.InvariantCulture)] is not { } found)
            {
                return null;
            }

            var copy = found.DeepClone();
            foreach (var sale in copy[sales]?.AsArray() ?? [])
            {
                sale!["timestamp"] = (long)sale["timestamp"]! + (long)Shift.TotalSeconds;
            }

            if (copy["lastUploadTime"] is { } upload)
            {
                copy["lastUploadTime"] = (long)upload + (long)Shift.TotalMilliseconds;
            }

            return copy;
        }

        if (ids.Count == 1)
        {
            return Item(ids[0]) is { } one ? Json(one.ToJsonString()) : new HttpResponseMessage(HttpStatusCode.NotFound);
        }

        var items = new JsonObject();
        foreach (var id in ids)
        {
            if (Item(id) is { } item)
            {
                items[id.ToString(CultureInfo.InvariantCulture)] = item;
            }
        }

        return Json(new JsonObject { ["items"] = items }.ToJsonString());
    }

    private static List<int> Ids(Match match) => [.. match.Groups["ids"].Value.Split(',').Select(int.Parse)];

    private static HttpResponseMessage Json(string json) => new(HttpStatusCode.OK) { Content = new StringContent(json, Encoding.UTF8, "application/json") };

    [GeneratedRegex(@"^/api/v2/aggregated/[^/]+/(?<ids>[\d,]+)$")]
    private static partial Regex AggregatedPath();

    [GeneratedRegex(@"^/api/v2/history/[^/]+/(?<ids>[\d,]+)$")]
    private static partial Regex HistoryPath();

    [GeneratedRegex(@"^/api/v2/[^/]+/(?<ids>[\d,]+)$")]
    private static partial Regex CurrentPath();
}

internal sealed class StillMotion : IMotionSettings
{
    public bool ReduceMotion => true;
}

/// <summary>The clock never ticks by itself; the tool moves it.</summary>
internal sealed class ManualTicker : ITicker
{
    public event EventHandler? Tick;

    public void Beat() => Tick?.Invoke(this, EventArgs.Empty);
}

/// <summary>An installed copy that talks to no server; set <see cref="Latest"/> to offer an update.</summary>
internal sealed class FakeUpdater : IAppUpdater
{
    public bool IsInstalled => true;

    public string? Latest { get; set; }

    public Task<AvailableUpdate?> CheckAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(Latest is null ? null : new AvailableUpdate(Latest));

    public Task DownloadAsync(AvailableUpdate update, Action<int> progress, CancellationToken cancellationToken = default) => Task.CompletedTask;

    public void RestartToApply(AvailableUpdate update)
    {
    }
}

internal sealed class NoInstances : IAppInstances
{
    public bool OthersRunning => false;
}

internal sealed class NoShell : IShellService
{
    public void OpenUrl(string url)
    {
    }

    public void OpenFolder(string path)
    {
    }
}

internal sealed class NoNotifier : INotifier
{
    public void Show(string title, string body)
    {
    }
}
