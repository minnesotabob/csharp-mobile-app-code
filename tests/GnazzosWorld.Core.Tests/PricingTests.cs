using GnazzosWorld.Core.Orders;
using Xunit;

namespace GnazzosWorld.Core.Tests;

public sealed class PricingTests
{
    [Theory]
    [InlineData(1, false, 49)] [InlineData(1, true, 79)]
    [InlineData(2, false, 79)] [InlineData(2, true, 129)]
    [InlineData(3, false, 109)] [InlineData(3, true, 179)]
    [InlineData(4, false, 139)] [InlineData(4, true, 229)]
    [InlineData(6, false, 199)] [InlineData(6, true, 319)]
    [InlineData(8, false, 259)] [InlineData(8, true, 399)]
    public void Single_song_prices_match_the_source_snapshot(
        int minutes, bool voice, int expected)
        => Assert.Equal((decimal)expected, SongPricing.Calculate(minutes, voice, 1));

    [Fact]
    public void Second_song_gets_ten_percent_discount()
        => Assert.Equal(340.10m, SongPricing.Calculate(3, true, 2));

    [Fact]
    public void Third_song_gets_twenty_percent_discount_in_addition_to_second()
        => Assert.Equal(483.30m, SongPricing.Calculate(3, true, 3));

    [Theory]
    [InlineData(0)] [InlineData(5)] [InlineData(-1)]
    public void Unsupported_length_is_rejected(int minutes)
        => Assert.Throws<ArgumentOutOfRangeException>(() =>
            SongPricing.Calculate(minutes, true, 1));

    [Theory]
    [InlineData(0)] [InlineData(4)] [InlineData(-1)]
    public void Unsupported_quantity_is_rejected(int quantity)
        => Assert.Throws<ArgumentOutOfRangeException>(() =>
            SongPricing.Calculate(3, true, quantity));
}
