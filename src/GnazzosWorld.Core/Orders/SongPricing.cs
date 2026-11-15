namespace GnazzosWorld.Core.Orders;

// Snapshot of the uploaded website, not a live price or checkout contract.
public static class SongPricing
{
    public const string Version = "website-snapshot-2026-11-14";

    public static decimal Calculate(int minutes, bool withVoice, int quantity)
    {
        if (quantity is < 1 or > 3)
            throw new ArgumentOutOfRangeException(nameof(quantity));
        decimal unit = (minutes, withVoice) switch
        {
            (1, false) => 49m, (1, true) => 79m,
            (2, false) => 79m, (2, true) => 129m,
            (3, false) => 109m, (3, true) => 179m,
            (4, false) => 139m, (4, true) => 229m,
            (6, false) => 199m, (6, true) => 319m,
            (8, false) => 259m, (8, true) => 399m,
            _ => throw new ArgumentOutOfRangeException(nameof(minutes))
        };
        // All songs have the same length/style; discounts apply per song.
        decimal factor = quantity switch { 1 => 1m, 2 => 1.9m, _ => 2.7m };
        return decimal.Round(unit * factor, 2, MidpointRounding.AwayFromZero);
    }
}
