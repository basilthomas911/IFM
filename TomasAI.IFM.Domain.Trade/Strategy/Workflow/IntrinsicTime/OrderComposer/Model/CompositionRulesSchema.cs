using System.Text.Json;
using TomasAI.IFM.Domain.Reference.Shared.StrategyCatalog;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.OrderComposition;

namespace TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.OrderComposer.Model;

/// <summary>Authors the complete strict catalog schema, including bounded optional adjustment arrays.</summary>
public static class CompositionRulesSchema
{
    public static CatalogParameterShape Create()
    {
        var parameters = Enum.GetValues<CompositionParameter>().ToDictionary(x => x.ToString(), x =>
            Number(x is CompositionParameter.LoadingMilliseconds or CompositionParameter.ExecutionMilliseconds
                or CompositionParameter.CandidateLifetimeMilliseconds or CompositionParameter.MaximumQuoteAgeMilliseconds
                or CompositionParameter.MaximumQuoteSkewMilliseconds or CompositionParameter.MaximumAdverseMoveTicks));
        var key = Object(("Kind", Number(true)), ("Id", Text()), ("Version", Number(true)));
        var bounds = Object(("Parameter", Number(true)), ("Minimum", Number()), ("Maximum", Number()), ("Grid", Number()));
        var adjustment = Object(("Code", Text()), ("Priority", Number(true)), ("Predicate", Predicate(1)),
            ("Parameter", Number(true)), ("Operation", Number(true)), ("Operand", Number()));
        var variant = Object(("VariantKey", key), ("StructureKey", key), ("BaseParameters", new()
            { Type = CatalogValueType.Object, Properties = parameters, Required = parameters.Keys.ToArray() }),
            ("HardBounds", Array(bounds, 64)), ("AdjustmentRules", Array(adjustment, 64)),
            ("AllowedWidths", Array(Number(), 64)), ("RequireSymmetricWings", Boolean()),
            ("DeltaUnits", Text()), ("RankingVersion", Text()));
        return Object(("SchemaVersion", Number(true)), ("AlgorithmVersion", Text()), ("PricerVersion", Text()),
            ("SupportedHorizon", Number(true)), ("InstrumentRoot", Text()), ("Currency", Text()), ("VariantRules", Array(variant, 12)));
    }
    public static JsonElement Settings() => JsonSerializer.SerializeToElement(Create(), new JsonSerializerOptions { MaxDepth = 96 });
    static CatalogParameterShape Predicate(int depth) => Object(("Comparison", Number(true)), ("Feature", Number(true)),
        ("Values", Array(Number(), 64)), ("Children", depth == 8 ? Array(Object(), 0) : Array(Predicate(depth + 1), 64)), ("Required", Boolean()));
    static CatalogParameterShape Object(params (string Name, CatalogParameterShape Shape)[] properties) => new()
        { Type = CatalogValueType.Object, Properties = properties.ToDictionary(x => x.Name, x => x.Shape, StringComparer.Ordinal), Required = properties.Select(x => x.Name).ToArray() };
    static CatalogParameterShape Array(CatalogParameterShape items, int maximum) => new() { Type = CatalogValueType.Array, Items = items, MaxLength = maximum };
    static CatalogParameterShape Number(bool integer = false) => new() { Type = integer ? CatalogValueType.Integer : CatalogValueType.Decimal };
    static CatalogParameterShape Text() => new() { Type = CatalogValueType.String, MaxLength = 128 };
    static CatalogParameterShape Boolean() => new() { Type = CatalogValueType.Boolean };
}
