using System.Text.Json;
using TomasAI.IFM.Framework.MarketData.OptionChainCache;
using TomasAI.IFM.Framework.MarketData.ReferenceData;
using TomasAI.IFM.Shared.MarketData.Contracts;

// Invoke unchanged production functions with explicit values. This executable has
// no dependency on a running API, broker, database or provider subscription.
if (args.Length is not (1 or 2)) throw new ArgumentException("Pass the output fixture path and optional new native DLL path.");
var policy = UsTreasuryCurve.ConversionPolicy;
var retrieved = DateTimeOffset.Parse("2026-10-25T23:00:00Z", System.Globalization.CultureInfo.InvariantCulture);
var date = new DateOnly(2026, 10, 23);
var rates = new[] { -200m, -199.99m, -1m, 0m, 0.0000000001m, 1.25m, 4.25m, 5m, 100m, 200m }
    .Concat(Enumerable.Range(-100, 1101).Select(x => x / 10m)).Distinct().ToArray();
var treasury = rates.Select(rate =>
{
    var snapshot = new TreasuryCurveSnapshot(date, [new(TreasuryTenor.OneMonth, rate)], retrieved, UsTreasuryCurve.Source);
    var result = TreasuryRateConversion.Convert(snapshot, TreasuryTenor.OneMonth, policy);
    return new
    {
        RatePercent = rate.ToString(System.Globalization.CultureInfo.InvariantCulture),
        HalfYieldBits = BitConverter.DoubleToUInt64Bits((double)(rate / 200m)),
        result.Succeeded,
        result.Error,
        ContinuousRateBits = result.Value is { } value ? BitConverter.DoubleToUInt64Bits(value.AnnualContinuousRate) : (ulong?)null,
        Digest = TreasuryRateConversion.Digest(snapshot)
    };
}).ToArray();
var store = new OptionChainSnapshotStore<string>(1);
var generation = Guid.Parse("00000000-0000-0000-0000-000000000001");
var replacement = Guid.Parse("00000000-0000-0000-0000-000000000002");
var steps = new List<object>();
void Check(string name, object? value) => steps.Add(new { Name = name, Value = value });
Check("before-admit", store.Read(generation, "a"));
store.Admit(generation);
Check("publish-first", store.Publish(generation, "a", new(1, retrieved, "first")));
Check("duplicate-version", store.Publish(generation, "a", new(1, retrieved.AddSeconds(1), "duplicate")));
Check("capacity", store.Publish(generation, "b", new(1, retrieved, "overflow")));
Check("older-time", store.Publish(generation, "a", new(2, retrieved.AddSeconds(-1), "stale")));
Check("publish-newer", store.Publish(generation, "a", new(2, retrieved.AddSeconds(1), "second")));
Check("latest", store.Read(generation, "a")?.Snapshot);
store.Admit(replacement);
Check("old-read", store.Read(generation, "a"));
Check("old-publish", store.Publish(generation, "a", new(3, retrieved.AddSeconds(2), "old")));
Check("replacement-publish", store.Publish(replacement, "a", new(1, retrieved, "replacement")));
store.Fence(generation);
Check("old-fence-preserves-replacement", store.Read(replacement, "a")?.Snapshot);
store.Remove(generation, "a");
Check("old-remove-preserves-replacement", store.Read(replacement, "a")?.Snapshot);
store.Fence(replacement);
Check("fenced-read", store.Read(replacement, "a"));
var output = new
{
    Schema = 1,
    Baseline = "Unchanged Framework.MarketData production functions",
    Treasury = treasury,
    Tenors = new[] { -1, 0, 29, 30, 59, 60, 89, 90, 252 }.Select(days => new { Days = days, Tenor = (int?)TreasuryRateConversion.SelectTenor(days) }).ToArray(),
    SnapshotStore = steps
};
File.WriteAllText(args[0], JsonSerializer.Serialize(output, new JsonSerializerOptions { WriteIndented = true }) + Environment.NewLine);
if (args.Length == 2) NativeParity.Check(args[1], rates, policy, date, retrieved);
