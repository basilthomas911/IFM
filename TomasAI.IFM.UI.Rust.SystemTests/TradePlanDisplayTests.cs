using TomasAI.IFM.UI.Rust.Bridge.Net;
using TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Plan;
public sealed class TradePlanDisplayTests
{
    [Theory]
    [InlineData("2026-01-15T12:34:56.123Z", "2026-01-15 07:34:56.123")]
    [InlineData("2026-07-15T12:34:56.123Z", "2026-07-15 08:34:56.123")]
    [InlineData("2026-07-15T02:00:00Z", "2026-07-14 22:00:00.000")]
    public void Action_time_uses_Eastern_time_with_exact_milliseconds(string timestamp,string expected)
    {
        var utc=DateTime.Parse(timestamp,null,System.Globalization.DateTimeStyles.RoundtripKind);
        var plan=new StrategyTradePlanSnapshot {CalculatedAtUtc=utc.AddDays(1),IronCondorTradePlanSnapshot=new(){ActionDateTime=utc}};
        Assert.Equal(expected,TradePlanDisplay.ActionTime(plan));
        Assert.Equal(utc,plan.IronCondorTradePlanSnapshot.ActionDateTime);
    }
    [Fact]
    public void Generic_plan_uses_calculation_time_and_missing_time_remains_unavailable()
    {
        Assert.Equal("2026-01-15 07:34:56.000",TradePlanDisplay.ActionTime(new(){CalculatedAtUtc=new DateTime(2026,1,15,12,34,56,DateTimeKind.Utc)}));
        Assert.Null(TradePlanDisplay.ActionTime(new()));
    }
    [Fact]
    public void History_document_keeps_valuation_fields_without_repeating_leg_graphs()
    {
        var plan = new StrategyTradePlanSnapshot { PlanRevision = 640, IronCondorTradePlanSnapshot = new()
            { MaxProfit = 572.50m, CurrentPositionValue = -542.50m, ContractMultiplier = 50, VolatilityPrice = 19.90m } };
        var document = MessagePack.MessagePackSerializer.ConvertToJson(TradePlanDisplay.EncodeHistory(plan));
        using var json = System.Text.Json.JsonDocument.Parse(document);
        Assert.False(json.RootElement.TryGetProperty("position", out _));
        var detail = json.RootElement.GetProperty("ironCondorTradePlanSnapshot");
        Assert.False(detail.TryGetProperty("position", out _));
        Assert.Equal("50", detail.GetProperty("contractMultiplier").GetString());
        Assert.Equal("-542.50", detail.GetProperty("currentPositionValue").GetString());
        Assert.Equal("19.90", detail.GetProperty("volatilityPrice").GetString());
    }
    internal static byte[] LegacyHistory(StrategyTradePlanSnapshot plan)
    {
        var fields = BridgeDocument.Map(plan, camelCase: true, enumNames: true);
        fields.Remove("position"); fields.Remove("parameters");
        if (fields.GetValueOrDefault("ironCondorTradePlanSnapshot") is Dictionary<string, object?> detail)
        { detail.Remove("position"); detail.Remove("legs"); detail.Remove("legacyLegs"); detail.Remove("strategyRiskParameterSet"); }
        return BridgeDocument.Encode(fields);
    }

    [Fact]
    public void Typed_history_preserves_existing_wire_bytes_and_ignores_excluded_evidence()
    {
        var minimal = new StrategyTradePlanSnapshot { PlanRevision = 7, Explanation = "unicode ? & <>",
            CurrentValue = -123.4500m, ForwardLoss = -700.00m,
            IronCondorTradePlanSnapshot = new() { CurrentPositionValue = -542.50m, ContractMultiplier = 50, VolatilityPrice = 19.90m } };
        var heavy = minimal with { Position = new() { Legs = Enumerable.Range(0, 10000)
            .Select(i => new TomasAI.IFM.Domain.Trade.Shared.StrategyPositionLeg { ContractId = $"ES-{i}", CurrentPrice = i }).ToArray() } };
        Assert.Equal(LegacyHistory(minimal), TradePlanDisplay.EncodeHistory(minimal));
        Assert.Equal(LegacyHistory(heavy), TradePlanDisplay.EncodeHistory(heavy));
        Assert.Equal(TradePlanDisplay.EncodeHistory(minimal), TradePlanDisplay.EncodeHistory(heavy));
        Assert.Equal(LegacyHistory(minimal with { IronCondorTradePlanSnapshot = null }),
            TradePlanDisplay.EncodeHistory(minimal with { IronCondorTradePlanSnapshot = null }));
        // Warming descriptors and pooled buffers prevents measuring initialization as evidence traversal.
        _ = TradePlanDisplay.EncodeHistory(heavy);
        var before = GC.GetAllocatedBytesForCurrentThread();
        _ = TradePlanDisplay.EncodeHistory(heavy);
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.True(allocated < 32_000, $"History encoding traversed excluded evidence: {allocated} bytes");
    }
}
