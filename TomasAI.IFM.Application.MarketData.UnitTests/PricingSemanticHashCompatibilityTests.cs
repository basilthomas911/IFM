using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using TomasAI.IFM.Application.MarketData.Pricing;
using Xunit.Abstractions;

namespace TomasAI.IFM.Application.MarketData.UnitTests;

public sealed class PricingSemanticHashCompatibilityTests(ITestOutputHelper output)
{
    [Fact]
    public void Typed_hash_preserves_established_digests_for_numeric_and_contract_values()
    {
        object?[] values = [null, true, "?<&\n", 1.000m, -0.0, -0.0f, decimal.MaxValue, decimal.MinValue,
            0.0000000000000000000000000001m, long.MinValue, ulong.MaxValue,
            new { Z = 1.2500m, A = new { Price = 5000.00, Empty = (string?)null } },
            new Dictionary<string, object?> { ["Z"] = 1m, ["A"] = 2.000m },
            new Dictionary<int, decimal> { [10] = 1.200m, [2] = 2m },
            new OrderedProperties(), new byte[] { 251, 255, 0 },
            new DateTimeOffset(2026, 10, 21, 1, 2, 3, TimeSpan.FromHours(5.5)).AddTicks(1200),
            new DateTime(2026, 10, 21, 1, 2, 3, DateTimeKind.Utc),
            new DateTime(2026, 10, 21, 1, 2, 3, DateTimeKind.Unspecified).AddTicks(1200),
            JsonSerializer.SerializeToElement(new { Z = 1.000m, A = 0.25 }),
            new Dictionary<decimal, double> { [1.200m] = 1e-10, [0.3m] = 1.0000 },
            OrderCompositionPricingPrerequisiteTests.Context(),
            OrderCompositionPricingPrerequisiteTests.Calendar(),
            OrderCompositionPricingPrerequisiteTests.Publication()];
        foreach (var value in values) Assert.True(Legacy(value) == PricingSemanticHash.Compute(value), $"Mismatch for {value?.GetType()}: {value}");
        foreach (var value in new[] { 1e-28, 1e20, 1e100, double.Epsilon, double.MaxValue, -1e-8, 1.2345678901234567 })
            Assert.Equal(Legacy(value), PricingSemanticHash.Compute(value));
        foreach (var value in new[] { float.Epsilon, float.MaxValue, 1e-8f, 1.2345678f })
            Assert.Equal(Legacy(value), PricingSemanticHash.Compute(value));
    }

    [Fact]
    public async Task Typed_hash_preserves_real_snapshot_digest_and_detects_changed_prices()
    {
        var snapshot = await OptionChainCacheTests.Snapshot();
        Assert.Equal(Legacy(snapshot), PricingSemanticHash.Compute(snapshot));
        Assert.NotEqual(PricingSemanticHash.Compute(snapshot), PricingSemanticHash.Compute(snapshot with { Horizon = "Weekly" }));
    }

    [Fact]
    public void Canonical_hash_is_order_scale_and_culture_independent()
    {
        var before = CultureInfo.CurrentCulture;
        try
        {
            var first = new { Z = 1.000m, A = 2.500m };
            var expected = Legacy(first);
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-CA");
            Assert.Equal(expected, PricingSemanticHash.Compute(new { A = 2.5m, Z = 1m }));
        }
        finally { CultureInfo.CurrentCulture = before; }
    }

    [Fact]
    public void Large_typed_payload_preserves_digest_with_substantially_lower_allocation()
    {
        var value = Enumerable.Range(0, 2000).Select(i => new
        { Contract = $"ES-option-{i}", Bid = 12.500m, Ask = 13.2500m, Delta = 0.123456789, Context = OrderCompositionPricingPrerequisiteTests.Context() }).ToArray();
        Assert.Equal(Legacy(value), PricingSemanticHash.Compute(value));
        long Measure(Func<string> calculate)
        {
            calculate();
            var start = GC.GetAllocatedBytesForCurrentThread();
            calculate();
            return GC.GetAllocatedBytesForCurrentThread() - start;
        }
        var legacy = Measure(() => Legacy(value));
        var typed = Measure(() => PricingSemanticHash.Compute(value));
        output.WriteLine($"Legacy allocated bytes: {legacy:N0}; typed streaming: {typed:N0}; reduction: {100.0 * (legacy - typed) / legacy:F1}%");
        Assert.True(typed < legacy / 2, $"Streaming allocation {typed} should be less than half the legacy {legacy}.");
    }

    // Frozen pre-optimization algorithm: the compatibility oracle for persisted scope and snapshot identities.
    internal static string Legacy<T>(T value)
    {
        using var document = JsonSerializer.SerializeToDocument(value);
        using var buffer = new MemoryStream();
        using (var writer = new Utf8JsonWriter(buffer)) Write(document.RootElement, writer);
        return Convert.ToHexStringLower(SHA256.HashData(buffer.GetBuffer().AsSpan(0, (int)buffer.Length)));
    }
    static void Write(JsonElement value, Utf8JsonWriter writer)
    {
        switch (value.ValueKind)
        {
            case JsonValueKind.Object:
                writer.WriteStartObject();
                foreach (var property in value.EnumerateObject().OrderBy(x => x.Name, StringComparer.Ordinal))
                { writer.WritePropertyName(property.Name); Write(property.Value, writer); }
                writer.WriteEndObject(); break;
            case JsonValueKind.Array:
                writer.WriteStartArray(); foreach (var item in value.EnumerateArray()) Write(item, writer);
                writer.WriteEndArray(); break;
            case JsonValueKind.Number:
                var raw = value.GetRawText();
                if (!raw.Contains('e', StringComparison.OrdinalIgnoreCase) && value.TryGetDecimal(out var number))
                    writer.WriteRawValue(number.ToString("G29", CultureInfo.InvariantCulture));
                else writer.WriteRawValue(value.GetDouble().ToString("R", CultureInfo.InvariantCulture));
                break;
            default: value.WriteTo(writer); break;
        }
    }
    sealed class OrderedProperties
    {
        [JsonPropertyOrder(-1)] public int Z => 1;
        [JsonPropertyName("Alpha")] public decimal A => 2.00m;
        [JsonIgnore] public string Ignored => "not hashed";
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? Missing => null;
    }
}
