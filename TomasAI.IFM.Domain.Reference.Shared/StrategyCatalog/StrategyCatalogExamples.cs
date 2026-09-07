using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace TomasAI.IFM.Domain.Reference.Shared.StrategyCatalog;

/// <summary>Engineering authoring examples only. They do not declare execution capabilities implemented.</summary>
public static class StrategyCatalogExamples
{
    public static Guid StableId(string code) => new(SHA256.HashData(Encoding.UTF8.GetBytes("IFM.StrategyCatalog.V1/" + code)).AsSpan(0, 16));
    public static StrategyCatalogDefinition New(StrategyCatalogKind kind, string code, string name) => new()
    { Key = new(kind, StableId(code), 1), Code = code, Name = name };

    public static StrategyCatalogDefinition[] Create()
    {
        var family = New(StrategyCatalogKind.Family, "Directional", "Directional strategies");
        var future = Structure("Future", "Outright future", [new("Future", "Futures", "Buy", "None", 1, "Front")]);
        var call = Structure("CallVertical", "Call vertical", [new("Lower", "FuturesOption", "Buy", "Call", 1, "Front"), new("Upper", "FuturesOption", "Sell", "Call", 1, "Front")]);
        var put = Structure("PutVertical", "Put vertical", [new("Lower", "FuturesOption", "Buy", "Put", 1, "Front"), new("Upper", "FuturesOption", "Sell", "Put", 1, "Front")]);
        var condor = Structure("IronCondor", "Iron condor", [new("LowerPut", "FuturesOption", "Buy", "Put", 1, "Front"), new("UpperPut", "FuturesOption", "Sell", "Put", 1, "Front"), new("LowerCall", "FuturesOption", "Sell", "Call", 1, "Front"), new("UpperCall", "FuturesOption", "Buy", "Call", 1, "Front")]);
        var strategy = New(StrategyCatalogKind.Strategy, "RegimeAligned", "Regime-aligned strategy") with
        {
            Families = [family.Key], Structures = [future.Key, call.Key, put.Key, condor.Key],
            Description = "Engineering draft. Configure selection rules and qualified capabilities before publication.",
            Capabilities = [new("evaluator", "RegimeAligned", 1), new("data", "AcceptedMarketAssessment", 1)]
        };
        var result = new List<StrategyCatalogDefinition> { family, future, call, put, condor, strategy };
        result.Add(Variant(future, "LongFuture", "Long future", "Long", "Bullish", "None", false));
        result.Add(Variant(future, "ShortFuture", "Short future", "Short", "Bearish", "None", true));
        result.Add(Variant(call, "BullCallDebit", "Bullish call debit spread", "Long", "Bullish", "Debit", false));
        result.Add(Variant(call, "BearCallCredit", "Bearish call credit spread", "Short", "Bearish", "Credit", true));
        result.Add(Variant(put, "BullPutCredit", "Bullish put credit spread", "Short", "Bullish", "Credit", false));
        result.Add(Variant(put, "BearPutDebit", "Bearish put debit spread", "Long", "Bearish", "Debit", true));
        foreach (var side in new[] { "Short", "Long" })
            foreach (var bias in new[] { "Balanced", "Bullish", "Bearish" })
                result.Add(Variant(condor, side + bias + "IronCondor", $"{side} {bias.ToLowerInvariant()} iron condor", side, bias, side == "Short" ? "Credit" : "Debit", side == "Long"));
        return result.ToArray();
    }

    static StrategyCatalogDefinition Structure(string code, string name, CatalogLeg[] legs) => New(StrategyCatalogKind.Structure, code, name) with
    {
        ExpiryGroups = [new("Front")], Legs = legs,
        Capabilities = [new("builder", code, 1), new("risk", code, 1)]
    };
    static StrategyCatalogDefinition Variant(StrategyCatalogDefinition structure, string code, string name, string side, string bias, string premium, bool invert) =>
        New(StrategyCatalogKind.Variant, code, name) with
        {
            Parent = structure.Key, Side = side, Bias = bias, PremiumMode = premium,
            Capabilities = [new("validator", "StructureVariant", 1)],
            VariantLegs = structure.Legs.Select(l => new CatalogVariantLeg(l.Key, invert ? (l.Side == "Buy" ? "Sell" : "Buy") : l.Side, l.Ratio)).ToArray(),
            Settings = JsonSerializer.SerializeToElement(new
            {
                TargetNetDelta = bias == "Balanced" ? 0m : bias == "Bullish" ? .15m : -.15m,
                BalanceTolerance = .05m, SymmetricWings = true, MinimumWingWidth = 0m, MaximumWingWidth = 0m
            })
        };
}
