using MessagePack;
using TomasAI.IFM.Domain.Reference.Shared.StrategyCatalog;

namespace TomasAI.IFM.Domain.Portfolio.Shared.Financial;

/// <summary>Versioned financial contract: CapacityRequirements; numeric keys are append-only.</summary>
[MessagePackObject]
public sealed record CapacityRequirements
{
    [Key(0)] public string Currency { get; init; } = string.Empty;
    [Key(1)] public decimal SettlementCash { get; init; }
    [Key(2)] public decimal MarginFunding { get; init; }
    [Key(3)] public decimal FeeReserve { get; init; }
    [Key(4)] public decimal VariationReserve { get; init; }
    [Key(5)] public decimal LossCharge { get; init; }
    [Key(6)] public decimal MarginRequirement { get; init; }
    [Key(7)] public decimal GrossNotional { get; init; }
    [Key(8)] public int GrossContracts { get; init; }
    [Key(9)] public int PositionSlots { get; init; }
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1819", Justification = "Preserve the existing serialized array payload and public financial model contract.")]
    [Key(10)] public CapacityExposure[] Exposures { get; init; } = [];
    [Key(11)] public int AccountingMethodVersion { get; init; }
    [Key(12)] public string ContentHash { get; init; } = string.Empty;
}
