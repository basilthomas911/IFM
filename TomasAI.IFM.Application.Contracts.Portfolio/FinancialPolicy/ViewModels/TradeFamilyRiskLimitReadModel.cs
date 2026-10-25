using TomasAI.IFM.Domain.Reference.Shared.StrategyCatalog;
using System.Text.Json.Serialization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using MessagePack;
using TomasAI.IFM.Domain.Portfolio.Shared.Contracts;

namespace TomasAI.IFM.Domain.Portfolio.Shared.ViewModels;

[MessagePackObject(AllowPrivate = true)]
public sealed record TradeFamilyRiskLimitReadModel
{
    [Key(0)] public int TradeStrategyFamilyId { get; init; }
    [Key(1)] public long DefinitionVersion { get; init; }
    [Key(2)] public bool Enabled { get; init; }
    [Key(3)] public decimal MaximumRiskPerTrade { get; init; }
    [Key(4)] public decimal MaximumAggregateRisk { get; init; }
    [Key(5)] public decimal MaximumMargin { get; init; }
    [Key(6)] public decimal MaximumGrossNotional { get; init; }
    [Key(7)] public int MaximumOpenPositions { get; init; }
    [Key(8), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public CatalogKey? CatalogDeployment { get; init; }

    public IReadOnlyList<string> Validate()
    {
        List<string> errors = [];
        if (CatalogDeployment is { } key ? key.Kind != StrategyCatalogKind.Deployment || key.Id == Guid.Empty || key.Version <= 0 || TradeStrategyFamilyId != 0 || DefinitionVersion != 0 : TradeStrategyFamilyId <= 0 || DefinitionVersion <= 0) errors.Add("An exact deployment reference is required (legacy identities are replay-only).");
        if (MaximumRiskPerTrade < 0 || MaximumAggregateRisk < 0 || MaximumMargin < 0 || MaximumGrossNotional < 0 || MaximumOpenPositions < 0)
            errors.Add("Trade-family limits cannot be negative.");
        if (MaximumRiskPerTrade > MaximumAggregateRisk) errors.Add("Family MaximumRiskPerTrade cannot exceed MaximumAggregateRisk.");
        return errors;
    }
}
