using System.Diagnostics;
using GnazzosWorld.Core.Catalog;
using GnazzosWorld.Core.Performance;

namespace GnazzosWorld.PerformanceLab;

internal sealed record Sample(double Milliseconds, long AllocatedBytes, int Gen0, int Gen1, int Gen2, int Results);
internal sealed record Summary(double MedianMs, double P95Ms, long MedianAllocatedBytes, Sample[] Samples);
internal static class SearchMeasurements
{
    private const int Repetitions = 21;
    private static readonly string?[] Queries = ["synthetic", "cafe", "beret", "物語", "no-match"];
    internal static object Run()
    {
        var items = PerformanceCatalogFixture.Create();
        CatalogSearchSnapshot? snapshot = null;
        var construction = Measure(() => { snapshot = new(items); return snapshot.Items.Count; });
        var prepared = snapshot!;
        int Reference()
        { var count = 0; foreach (var q in Queries) count += CatalogSearch.Find(items, q).Count; return count; }
        int Indexed()
        { var count = 0; foreach (var q in Queries) count += prepared.Find(q).Count; return count; }
        foreach (var q in Queries)
            if (!CatalogSearch.Find(items, q).Select(x => x.Id).SequenceEqual(prepared.Find(q).Select(x => x.Id)))
                throw new InvalidDataException("Search implementations disagree.");
        for (var i = 0; i < 3; i++) { _ = Reference(); _ = Indexed(); }
        var before = new Sample[Repetitions]; var after = new Sample[Repetitions];
        for (var i = 0; i < Repetitions; i++)
        {
            if (i % 2 == 0) { before[i] = Measure(Reference); after[i] = Measure(Indexed); }
            else { after[i] = Measure(Indexed); before[i] = Measure(Reference); }
        }
        if (before.Any(s => s.Results != after[0].Results) || after.Any(s => s.Results != before[0].Results))
            throw new InvalidDataException("Measurement result count changed.");
        return new { Fixture = PerformanceCatalogFixture.Version, Items = items.Count, QueriesPerSample = Queries.Length,
            WarmupPairs = 3, PairedSamples = Repetitions, Construction = construction, Before = Summarize(before), After = Summarize(after),
            Scope = "Release desktop synchronous search batches; not native scrolling, first-frame startup or a capacity threshold." };
    }
    private static Sample Measure(Func<int> action)
    {
        var g0 = GC.CollectionCount(0); var g1 = GC.CollectionCount(1); var g2 = GC.CollectionCount(2);
        var bytes = GC.GetAllocatedBytesForCurrentThread(); var start = Stopwatch.GetTimestamp();
        var count = action(); var elapsed = Stopwatch.GetElapsedTime(start).TotalMilliseconds;
        return new(elapsed, GC.GetAllocatedBytesForCurrentThread() - bytes,
            GC.CollectionCount(0) - g0, GC.CollectionCount(1) - g1, GC.CollectionCount(2) - g2, count);
    }
    private static Summary Summarize(Sample[] values)
    {
        var time = values.Select(x => x.Milliseconds).Order().ToArray(); var allocated = values.Select(x => x.AllocatedBytes).Order().ToArray();
        return new(time[time.Length / 2], time[(int)Math.Ceiling(time.Length * .95) - 1], allocated[allocated.Length / 2], values);
    }
}
