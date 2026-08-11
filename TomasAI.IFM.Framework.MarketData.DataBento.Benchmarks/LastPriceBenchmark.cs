using System.Diagnostics;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation;
using TomasAI.IFM.Framework.MarketData.Contracts.LastPrice;
using TomasAI.IFM.Framework.MarketData.DataBento.LastPrice;

internal static class LastPriceBenchmark
{
    internal static int Run(string[] args)
    {
        var operations = ReadOperations(args);
        var strict = args.Contains("--strict", StringComparer.OrdinalIgnoreCase);
        var valueDate = new DateOnly(2026, 8, 10);
        var timestamp = new DateTimeOffset(2026, 8, 10, 14, 30, 0, TimeSpan.Zero);
        using var store = new DatabentoLastPriceStore(valueDate, 2);
        store.RegisterContract("ESU6", AssetTypeId.Futures);
        store.RegisterContract("ESU6 C6500", AssetTypeId.FuturesOption);
        var reader = store.GetFuturesOptionReader("ESU6 C6500", valueDate);

        for (var index = 1; index <= 100_000; index++)
            store.TryUpdateQuote(CreateQuote(index, valueDate, timestamp));
        _ = reader.TryGetLastQuote(out _);

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        var allocationBefore = GC.GetAllocatedBytesForCurrentThread();
        var stopwatch = Stopwatch.StartNew();
        for (var index = 1; index <= operations; index++)
            store.TryUpdateQuote(CreateQuote(index + 100_000L, valueDate, timestamp));
        stopwatch.Stop();
        var updateAllocated = GC.GetAllocatedBytesForCurrentThread() - allocationBefore;
        var updateRate = operations / stopwatch.Elapsed.TotalSeconds;

        allocationBefore = GC.GetAllocatedBytesForCurrentThread();
        stopwatch.Restart();
        long checksum = 0;
        for (var index = 0; index < operations; index++)
        {
            if (reader.TryGetLastQuote(out var quote))
                checksum += quote.SourceSequence;
        }
        stopwatch.Stop();
        var readAllocated = GC.GetAllocatedBytesForCurrentThread() - allocationBefore;
        var readRate = operations / stopwatch.Elapsed.TotalSeconds;

        store.Invalidate();
        var postStopMiss = !reader.TryGetLastQuote(out _);
        Console.WriteLine("DataBento last-price benchmark");
        Console.WriteLine($"Runtime: {Environment.Version}");
        Console.WriteLine($"OS: {Environment.OSVersion}");
        Console.WriteLine($"CPU count: {Environment.ProcessorCount}");
        Console.WriteLine($"Operations: {operations:N0}");
        Console.WriteLine(
            $"Quote updates: {updateRate:N0}/s, {updateAllocated:N0} B allocated " +
            $"({(double)updateAllocated / operations:N6} B/op)");
        Console.WriteLine(
            $"Quote reads: {readRate:N0}/s, {readAllocated:N0} B allocated " +
            $"({(double)readAllocated / operations:N6} B/op)");
        Console.WriteLine($"Checksum: {checksum}");
        Console.WriteLine($"Post-stop reader miss: {postStopMiss}");

        if (!strict) return 0;
        if (updateRate < 1_000_000
            || readRate < 5_000_000
            || updateAllocated != 0
            || readAllocated != 0
            || !postStopMiss)
        {
            Console.Error.WriteLine("Strict last-price qualification failed.");
            return 4;
        }
        return 0;
    }

    private static LastQuoteTickSnapshot CreateQuote(
        long sequence,
        DateOnly valueDate,
        DateTimeOffset timestamp) => new(
        "ESU6 C6500",
        valueDate,
        10m + sequence / 1_000_000_000m,
        10,
        1,
        11m + sequence / 1_000_000_000m,
        11,
        1,
        sequence,
        timestamp.AddTicks(sequence),
        timestamp.AddTicks(sequence + 1));

    private static int ReadOperations(string[] args)
    {
        var argument = args.FirstOrDefault(static value =>
            value.StartsWith("--operations=", StringComparison.OrdinalIgnoreCase));
        return argument is null
            ? 5_000_000
            : int.Parse(argument.AsSpan(argument.IndexOf('=') + 1));
    }
}
