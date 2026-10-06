namespace GilSweep.Core.Watchlist;

/// <summary>One watched item and which notifications it should send.</summary>
public sealed class WatchEntry
{
    public int ItemId { get; set; }

    /// <summary>Remind when the item's timed node is about to open.</summary>
    public bool NodeOpens { get; set; }

    /// <summary>Notify when the price rises past the spike threshold.</summary>
    public bool PriceSpike { get; set; }

    /// <summary>Notify when the price drops past the crash threshold.</summary>
    public bool PriceCrash { get; set; }

    /// <summary>Notify when someone lists below one of your retainers.</summary>
    public bool Undercut { get; set; }

    /// <summary>A farm you return to; marked wherever the item appears.</summary>
    public bool Favorite { get; set; }

    public WatchEntry Clone() => new()
    {
        ItemId = ItemId,
        NodeOpens = NodeOpens,
        PriceSpike = PriceSpike,
        PriceCrash = PriceCrash,
        Undercut = Undercut,
        Favorite = Favorite,
    };
}
