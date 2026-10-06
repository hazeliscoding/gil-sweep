using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace GilSweep.Tests.Support;

/// <summary>
/// Stands in for Universalis and Saddlebag Exchange: answers from the recorded responses in
/// Fixtures/, the same way the v1 characterization harness did, and can be told to fail.
/// </summary>
public sealed partial class FakeMarketHandler : HttpMessageHandler
{
    private readonly List<(Func<Uri, bool> Match, Func<HttpResponseMessage> Respond, int Times)> _overrides = [];
    private readonly Lock _gate = new();

    public FakeMarketHandler()
    {
        Aggregated = ((JsonArray)Fixture.Json("Universalis", "aggregated-cactuar.json")["results"]!)
            .ToDictionary(node => (int)node!["itemId"]!, node => node!);
        Current = Fixture.Json("Universalis", "current-cactuar.json")["items"]!.AsObject();
        History = Fixture.Json("Universalis", "history-cactuar.json")["items"]!.AsObject();
        Saddlebag = Fixture.Json("Saddlebag", "marketshare-cactuar.json");
        Worlds = Fixture.Json("Universalis", "worlds.json");
    }

    public Dictionary<int, JsonNode> Aggregated { get; }

    public JsonObject Current { get; }

    public JsonObject History { get; }

    public JsonNode Saddlebag { get; set; }

    public JsonNode Worlds { get; }

    public List<Uri> Requests { get; } = [];

    /// <summary>Answers matching requests with a status code instead, <paramref name="times"/> times (or always).</summary>
    public void Fail(Func<Uri, bool> match, HttpStatusCode status, int times = int.MaxValue) =>
        _overrides.Add((match, () => new HttpResponseMessage(status), times));

    public void Respond(Func<Uri, bool> match, string json, int times = int.MaxValue) =>
        _overrides.Add((match, () => Json(json), times));

    public void Throw(Func<Uri, bool> match, int times = int.MaxValue) =>
        _overrides.Add((match, () => throw new HttpRequestException("connection refused"), times));

    // HttpClientFactory disposes handlers when it rotates them; this one is shared by the test.
    protected override void Dispose(bool disposing)
    {
    }

    /// <summary>While set, every request waits for it: a sweep caught in the middle.</summary>
    public TaskCompletionSource? Hold { get; set; }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var uri = request.RequestUri!;
        if (Hold is { } hold)
        {
            await hold.Task.WaitAsync(cancellationToken);
        }

        lock (_gate)
        {
            Requests.Add(uri);
            for (var i = 0; i < _overrides.Count; i++)
            {
                var (match, respond, times) = _overrides[i];
                if (times > 0 && match(uri))
                {
                    _overrides[i] = (match, respond, times - 1);
                    return respond();
                }
            }
        }

        return Route(uri);
    }

    private HttpResponseMessage Route(Uri uri)
    {
        if (uri.Host == "api.saddlebagexchange.com")
        {
            return Json(Saddlebag.ToJsonString());
        }

        if (uri.Host == "v2.xivapi.com")
        {
            var query = Uri.UnescapeDataString(uri.Query.Split("query=")[1].Split('&')[0]);
            var name = SearchName().Match(query).Groups[1].Value.Replace(' ', '_');
            var file = Fixture.PathOf("Verify", (query.Contains('~', StringComparison.Ordinal) ? "search-fuzzy-" : "search-") + name + ".json");
            return Json(File.Exists(file) ? File.ReadAllText(file) : """{"results":[]}""");
        }

        if (uri.Host == "garlandtools.org")
        {
            var file = uri.AbsolutePath.Contains("/core/", StringComparison.Ordinal)
                ? Fixture.PathOf("Verify", "garland-core-locations.json")
                : Fixture.PathOf("Verify", "garland-" + Path.GetFileName(uri.AbsolutePath));
            return File.Exists(file) ? Json(File.ReadAllText(file)) : new HttpResponseMessage(HttpStatusCode.NotFound);
        }

        var path = uri.AbsolutePath;
        if (path == "/api/v2/worlds")
        {
            return Json(Worlds.ToJsonString());
        }

        Match m;
        if ((m = AggregatedPath().Match(path)).Success)
        {
            var results = new JsonArray([.. Ids(m).Where(Aggregated.ContainsKey).Select(id => Aggregated[id].DeepClone())]);
            return Json(new JsonObject { ["results"] = results }.ToJsonString());
        }

        if ((m = HistoryPath().Match(path)).Success)
        {
            return Lookup(History, Ids(m));
        }

        if ((m = CurrentPath().Match(path)).Success)
        {
            return Lookup(Current, Ids(m));
        }

        return new HttpResponseMessage(HttpStatusCode.NotFound);
    }

    private static HttpResponseMessage Lookup(JsonObject source, List<int> ids)
    {
        if (ids.Count == 1)
        {
            return source[ids[0].ToString(System.Globalization.CultureInfo.InvariantCulture)] is { } one
                ? Json(one.ToJsonString())
                : new HttpResponseMessage(HttpStatusCode.NotFound);
        }

        var items = new JsonObject();
        foreach (var id in ids)
        {
            var key = id.ToString(System.Globalization.CultureInfo.InvariantCulture);
            if (source[key] is { } item)
            {
                items[key] = item.DeepClone();
            }
        }

        return Json(new JsonObject { ["items"] = items }.ToJsonString());
    }

    private static List<int> Ids(Match match) => [.. match.Groups["ids"].Value.Split(',').Select(int.Parse)];

    private static HttpResponseMessage Json(string json) =>
        new(HttpStatusCode.OK) { Content = new StringContent(json, Encoding.UTF8, "application/json") };

    [GeneratedRegex("Name[=~]\"(.*)\"")]
    private static partial Regex SearchName();

    [GeneratedRegex(@"^/api/v2/aggregated/[^/]+/(?<ids>[\d,]+)$")]
    private static partial Regex AggregatedPath();

    [GeneratedRegex(@"^/api/v2/history/[^/]+/(?<ids>[\d,]+)$")]
    private static partial Regex HistoryPath();

    [GeneratedRegex(@"^/api/v2/[^/]+/(?<ids>[\d,]+)$")]
    private static partial Regex CurrentPath();
}
