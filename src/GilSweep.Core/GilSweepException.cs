namespace GilSweep.Core;

public enum GilSweepErrorKind
{
    Unexpected,
    InvalidSettings,
    MarketUnavailable,
    NoMarketData,
}

/// <summary>A failure with a message written for the user.</summary>
public sealed class GilSweepException(GilSweepErrorKind kind, string message, Exception? inner = null) : Exception(message, inner)
{
    public GilSweepErrorKind Kind { get; } = kind;
}
