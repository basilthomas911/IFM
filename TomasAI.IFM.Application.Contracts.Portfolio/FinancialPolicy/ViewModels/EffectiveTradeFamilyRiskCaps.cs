using TomasAI.IFM.Domain.Reference.Shared.StrategyCatalog;
using System.Text.Json.Serialization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using MessagePack;
using TomasAI.IFM.Domain.Portfolio.Shared.Contracts;

namespace TomasAI.IFM.Domain.Portfolio.Shared.ViewModels;

[MessagePackObject(AllowPrivate = true)]
public sealed record EffectiveTradeFamilyRiskCaps
{
    [Key(0)] public int TradeStrategyFamilyId { get; init; }
    [Key(1)] public long DefinitionVersion { get; init; }
    [Key(2)] public bool PermitsNewExposure { get; init; }
    [Key(3)] public decimal MaximumRiskPerTrade { get; init; }
    [Key(4)] public decimal MaximumAggregateRisk { get; init; }
    [Key(5)] public decimal MaximumMargin { get; init; }
    [Key(6)] public decimal MaximumGrossNotional { get; init; }
    [Key(7)] public int MaximumOpenPositions { get; init; }
    [Key(8), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public CatalogKey? CatalogDeployment { get; init; }
}
