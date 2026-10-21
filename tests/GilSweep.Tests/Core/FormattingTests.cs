using GilSweep.Core;

namespace GilSweep.Tests.Core;

public sealed class FormattingTests
{
    [Theory]
    [InlineData(999_950, "1M")]
    [InlineData(999_995_000, "1B")]
    [InlineData(-999_999, "-1M")]
    [InlineData(9_999.5, "10K")]
    [InlineData(980, "980")]
    [InlineData(9_999, "9,999")]
    [InlineData(12_400, "12.4K")]
    [InlineData(1_060_000, "1.06M")]
    [InlineData(2_500_000_000, "2.5B")]
    public void Compact_picks_the_unit_after_rounding(double value, string shown) =>
        Assert.Equal(shown, Formatting.Compact(value));

    [Theory]
    [InlineData(9.96, "10")]
    [InlineData(9.94, "9.9")]
    [InlineData(10, "10")]
    [InlineData(0.5, "0.5")]
    [InlineData(1_234.4, "1,234")]
    public void Rate_picks_the_decimals_after_rounding(double perDay, string shown) =>
        Assert.Equal(shown, Formatting.Rate(perDay));
}
