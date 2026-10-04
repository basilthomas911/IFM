using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
namespace TomasAI.IFM.Framework.TradeBroker.InteractiveBrokers.Emulator.Engine;
public sealed record OfflineFillSimulationOptions
{
    public bool Enabled { get; init; } = true;
    public TimeSpan CompletionTime { get; init; } = TimeSpan.FromSeconds(30);
    public int MaximumUnitsPerFill { get; init; } = 3;
    public int RandomSeed { get; init; } = 1;
}
/// <summary>Timed development fills through the normal emulator ledger, never a real broker.</summary>
public sealed class OfflineFillSimulation : IDisposable
{
    readonly EmulatorLedger ledger;
    readonly OfflineFillSimulationOptions options;
    readonly Func<bool> marketClosed;
    readonly CancellationTokenSource lifetime = new();
    readonly ConcurrentDictionary<string, Lazy<Task>> runs = new(StringComparer.Ordinal);
    public OfflineFillSimulation(EmulatorLedger ledger, OfflineFillSimulationOptions options, Func<bool> marketClosed)
    {
        if (options.CompletionTime <= TimeSpan.Zero || options.MaximumUnitsPerFill <= 0)
            throw new ArgumentOutOfRangeException(nameof(options));
        this.ledger = ledger; this.options = options; this.marketClosed = marketClosed;
    }
    public bool Enabled => options.Enabled;
    public void Start(string id)
    {
        if (options.Enabled) _ = runs.GetOrAdd(id, key => new(() => RunAsync(key))).Value;
    }
    public Task CompletionAsync(string id) => runs.TryGetValue(id, out var run) ? run.Value : Task.CompletedTask;
    async Task RunAsync(string id)
    {
        try
        {
            var random = new Random(BitConverter.ToInt32(SHA256.HashData(Encoding.UTF8.GetBytes(id)), 0) ^ options.RandomSeed);
            var remaining = ledger.RemainingStrategyUnits(id);
            var quantities = new List<int>();
            while (remaining > 0)
            {
                var quantity = random.Next(1, Math.Min(remaining, options.MaximumUnitsPerFill) + 1);
                quantities.Add(quantity); remaining -= quantity;
            }
            if (quantities.Count == 0) return;
            var times = Enumerable.Range(0, quantities.Count - 1)
                .Select(_ => options.CompletionTime.TotalMilliseconds * (0.1 + random.NextDouble() * 0.8))
                .Append(options.CompletionTime.TotalMilliseconds).Order().ToArray();
            var started = System.Diagnostics.Stopwatch.StartNew();
            for (var i = 0; i < times.Length; i++)
            {
                var delay = TimeSpan.FromMilliseconds(times[i]) - started.Elapsed;
                if (delay > TimeSpan.Zero) await Task.Delay(delay, lifetime.Token).ConfigureAwait(false);
                lifetime.Token.ThrowIfCancellationRequested();
                if (ledger.RemainingStrategyUnits(id) == 0) return;
                if (!marketClosed() && ledger.HasFreshMarketQuotes(id)) continue;
                var quantity = i == times.Length - 1 ? ledger.RemainingStrategyUnits(id) : quantities[i];
                do
                {
                    if (!ledger.SimulateOfflineFill(id, quantity)) break;
                } while (i == times.Length - 1 && ledger.RemainingStrategyUnits(id) > 0);
            }
        }
        catch (OperationCanceledException) when (lifetime.IsCancellationRequested) { }
        catch (Exception exception) { System.Diagnostics.Trace.TraceError("Offline emulator fill failed for {0}: {1}", id, exception); }
    }
    public void Dispose() => lifetime.Cancel();
}
