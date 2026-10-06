using GilSweep.Core.Catalog;
using GilSweep.Core.Configuration;

namespace GilSweep.Core.Watchlist;

/// <summary>Watched items and their notification switches, stored in the settings.</summary>
public interface IWatchlistService
{
    IReadOnlyList<WatchEntry> Entries { get; }

    event EventHandler? Changed;

    bool IsWatched(int itemId);

    WatchEntry? Get(int itemId);

    /// <summary>Starts watching with the usual switches: node reminders for timed nodes, spikes and crashes.</summary>
    void Watch(int itemId);

    void Unwatch(int itemId);

    /// <summary>Watches the item if it isn't, stops if it is. Returns whether it is watched now.</summary>
    bool Toggle(int itemId);

    void Set(int itemId, Action<WatchEntry> change);
}

public sealed class WatchlistService : IWatchlistService
{
    private readonly ISettingsService _settings;
    private readonly IItemCatalog _catalog;

    public WatchlistService(ISettingsService settings, IItemCatalog catalog)
    {
        _settings = settings;
        _catalog = catalog;
        _settings.Changed += (_, _) => Changed?.Invoke(this, EventArgs.Empty);
    }

    public IReadOnlyList<WatchEntry> Entries => _settings.Current.Watchlist;

    public event EventHandler? Changed;

    public bool IsWatched(int itemId) => Get(itemId) is not null;

    public WatchEntry? Get(int itemId) => _settings.Current.WatchOf(itemId);

    public void Watch(int itemId)
    {
        if (IsWatched(itemId))
        {
            return;
        }

        var timed = _catalog.Find(itemId)?.IsTimed ?? false;
        _settings.Update(settings => settings.Watchlist.Add(new WatchEntry
        {
            ItemId = itemId,
            NodeOpens = timed,
            PriceSpike = true,
            PriceCrash = true,
        }));
    }

    public void Unwatch(int itemId) =>
        _settings.Update(settings => settings.Watchlist.RemoveAll(entry => entry.ItemId == itemId));

    public bool Toggle(int itemId)
    {
        if (IsWatched(itemId))
        {
            Unwatch(itemId);
            return false;
        }

        Watch(itemId);
        return true;
    }

    public void Set(int itemId, Action<WatchEntry> change) =>
        _settings.Update(settings =>
        {
            if (settings.WatchOf(itemId) is { } entry)
            {
                change(entry);
            }
        });
}
