using GilSweep.Core.Catalog;
using GilSweep.Core.Configuration;
using GilSweep.Core.History;
using GilSweep.Core.Sweep;
using GilSweep.Tests.Support;

namespace GilSweep.Tests.Core;

public sealed class HistoryReportTests : IDisposable
{
    private static readonly GilSweepSettings Endgame = new()
    {
        Levels = new GathererLevels { Miner = 100, Botanist = 100 },
        MsqExpansion = Expansion.DT,
    };

    private readonly CoreHost _host = new();

    public HistoryReportTests()
    {
        var folder = _host.Get<IMarketSnapshotStore>().Folder;
        Directory.CreateDirectory(folder);
        foreach (var file in Directory.EnumerateFiles(Fixture.PathOf("V1", "archive"), "*Cactuar.json"))
        {
            File.Copy(file, Path.Combine(folder, Path.GetFileName(file)));
        }
    }

    private IReadOnlyList<MarketSnapshot> Snapshots => _host.Get<IMarketSnapshotStore>().Load("Cactuar");

    [Fact]
    public void The_log_lists_sweeps_newest_first_with_what_topped_each()
    {
        var log = HistoryReport.Log(Snapshots, Endgame, limit: 5);

        Assert.Equal(5, log.Count);
        Assert.Equal(new DateTimeOffset(2026, 10, 5, 18, 30, 0, TimeSpan.Zero), log[0].At);
        Assert.True(log.Zip(log.Skip(1)).All(pair => pair.First.At > pair.Second.At));
        Assert.All(log, entry =>
        {
            Assert.NotNull(entry.BestName);
            Assert.Equal(104, entry.Items);
        });
    }

    [Fact]
    public void The_week_is_summed_up_in_facts_from_the_archive()
    {
        var snapshots = Snapshots;
        var digest = _host.Get<IHistoryService>().Digest("Cactuar");

        var summary = HistoryReport.Summarize(snapshots, digest, Endgame, _host.Get<IMarketSnapshotStore>().Stats(), _host.Clock.Now);

        Assert.Equal(10, summary.SweepsThisWeek);
        Assert.Equal(104, summary.ItemsTracked);
        Assert.Matches(@"^\S.* was the top farm in \d+ of 10 sweeps\.", summary.Text);
        Assert.Contains(" since Sep 30.", summary.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void A_quiet_week_says_so()
    {
        var summary = HistoryReport.Summarize(Snapshots, _host.Get<IHistoryService>().Digest("Cactuar"), Endgame, new SnapshotStats(0, 0, null), _host.Clock.Now.AddDays(30));
        Assert.Equal(0, summary.SweepsThisWeek);
        Assert.StartsWith("No sweeps this week", summary.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void The_csv_has_a_line_per_item_per_sweep()
    {
        var writer = new StringWriter();
        HistoryReport.WriteCsv(Snapshots, writer);

        var lines = writer.ToString().Split(writer.NewLine, StringSplitOptions.RemoveEmptyEntries);
        Assert.StartsWith("swept_at_utc,world,item_id,item,", lines[0], StringComparison.Ordinal);
        Assert.Equal(1 + 15 * 104, lines.Length);
        Assert.Contains(lines, line => line.Contains("\"Approved Grade 4 Skybuilders' Umbral Galewood Branch\"", StringComparison.Ordinal) || line.Contains(",Approved Grade 4 Skybuilders' Umbral Galewood Branch,", StringComparison.Ordinal));
    }

    [Fact]
    public void Commas_and_quotes_in_names_are_escaped()
    {
        var snapshot = new MarketSnapshot { World = "Cactuar", Timestamp = "2026-10-05T18:30:00.000Z", Rows = [new SnapshotRow { Id = 1, Name = "Salt, \"fine\"" }] };
        var writer = new StringWriter();

        HistoryReport.WriteCsv([snapshot], writer);

        Assert.Contains(",\"Salt, \"\"fine\"\"\",", writer.ToString(), StringComparison.Ordinal);
    }

    public void Dispose() => _host.Dispose();
}
