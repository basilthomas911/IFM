using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Nodes;
using TomasAI.IFM.Application.MarketData.Rust.Delivery;

internal static class CatalogPagePerformance
{
    const string Dataset = "GLBX.MDP3";
    const int RowCount = 2048;
    const int SampleCount = 7;

    internal static async Task Run(string path, string seed)
    {
        await using var runtime = new NativeMarketDataRuntime(path);
        runtime.ConfigureCatalog(RowCount, 8 * 1024 * 1024);
        await runtime.HydrateCatalogAsync(Lines(seed));
        using var snapshot = runtime.OpenCatalogSnapshot(Dataset);
        if (snapshot.Count != RowCount)
            throw new InvalidDataException($"Expected {RowCount} immutable rows, received {snapshot.Count}.");

        string[] ReadSources() => Enumerable.Range(0, snapshot.Count).Select(snapshot.ReadSource).ToArray();
        NativeContractDefinition[] ReadDefinitions() => Enumerable.Range(0, snapshot.Count).Select(snapshot.ReadDefinition).ToArray();

        var expectedSources = ReadSources();
        var expectedDefinitions = ReadDefinitions();
        var sources = Task.Run(() => snapshot.ReadSources().ToArray());
        var definitions = Task.Run(() => snapshot.ReadDefinitions().ToArray());
        var actualSources = await sources;
        var actualDefinitions = await definitions;
        if (!expectedSources.SequenceEqual(actualSources) || !expectedDefinitions.SequenceEqual(actualDefinitions))
            throw new InvalidDataException("Paged results differ from the legacy rows or order.");
        using var cancellation = new CancellationTokenSource();
        using var reader = snapshot.ReadSources(cancellation.Token).GetEnumerator();
        if (!reader.MoveNext()) throw new InvalidDataException("Catalog page was empty.");
        cancellation.Cancel();
        try
        {
            reader.MoveNext();
            throw new InvalidDataException("Cancellation was ignored between rows.");
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
        }

        Benchmark("source", () => ReadSources().Length, () => snapshot.ReadSources().ToArray().Length, 25);
        Benchmark("projection", () => ReadDefinitions().Length, () => snapshot.ReadDefinitions().ToArray().Length, 10);
    }

    static void Benchmark(string format, Func<int> legacy, Func<int> paged, int iterations)
    {
        Measure(legacy, 2);
        Measure(paged, 2);
        for (var index = 0; index < SampleCount; index++)
        {
            var before = index % 2 == 0 ? Measure(legacy, iterations) : default;
            var after = Measure(paged, iterations);
            if (index % 2 != 0) before = Measure(legacy, iterations);
            Console.WriteLine(JsonSerializer.Serialize(new { format, rows = RowCount, iterations, sample = index + 1, before, after }));
        }
    }

    static Measurement Measure(Func<int> read, int iterations)
    {
        var startBytes = GC.GetAllocatedBytesForCurrentThread();
        var startTime = Stopwatch.GetTimestamp();
        for (var index = 0; index < iterations; index++)
            if (read() != RowCount) throw new InvalidDataException("Catalog result count changed during measurement.");
        return new(Stopwatch.GetElapsedTime(startTime).TotalMilliseconds / iterations,
            (GC.GetAllocatedBytesForCurrentThread() - startBytes) / iterations);
    }

    static async IAsyncEnumerable<CatalogSourceLine> Lines(string seed, [EnumeratorCancellation] CancellationToken token = default)
    {
        var source = JsonNode.Parse(seed)!.AsObject();
        var header = source["hd"]!.AsObject();
        await Task.Yield();
        for (var index = 0; index < RowCount; index++)
        {
            token.ThrowIfCancellationRequested();
            header["instrument_id"] = index + 100;
            source["raw_symbol"] = $"ESPERF{index}";
            yield return new(Dataset, source.ToJsonString());
        }
    }

    readonly record struct Measurement(double Milliseconds, long ManagedBytes);
}
