using MessagePack;
using TomasAI.IFM.Domain.Portfolio.Shared.Contracts;

namespace TomasAI.IFM.Domain.Portfolio.Shared.ViewModels;

[MessagePackObject(AllowPrivate = true)]
public sealed record FundOrderTradeProjectionReadModel
{
    [Key(0)] public int PortfolioId { get; init; }
    [Key(1)] public int FundId { get; init; }
    [Key(2)] public int OrderId { get; init; }
    [Key(3)] public int TradeId { get; init; }
    [Key(4)] public string TradeFamily { get; init; } = string.Empty;
    [Key(5)] public string InstructionReference { get; init; } = string.Empty;
    [Key(6)] public int LegOrdinal { get; init; }
    [Key(7)] public long AggregateVersion { get; init; }
    [Key(8)] public string DirectionOrBias { get; init; } = string.Empty;
    [Key(9)] public string TradeAction { get; init; } = string.Empty;
    [Key(10)] public string UnderlyingRoot { get; init; } = string.Empty;
    [Key(11)] public DateOnly RequestedTradeDate { get; init; }
    [Key(12)] public DateOnly? RequestedMaturityDate { get; init; }
    [Key(13)] public string TradeType { get; init; } = string.Empty;
    [Key(14)] public string TradeState { get; init; } = string.Empty;
    [Key(15)] public bool PrimaryTrade { get; init; }
    [Key(16)] public string BaseContractSymbol { get; init; } = string.Empty;
    [Key(17)] public DateTime CreatedOnUtc { get; init; }
    [Key(18)] public string CreatedBy { get; init; } = string.Empty;
    /// <summary>The canonical, provider-independent identifier of the selected futures contract.</summary>
    [Key(19)] public string BaseContractId { get; init; } = string.Empty;
    /// <summary>The committed execution order created for this setup trade.</summary>
    [Key(20)] public int ExecutionOrderId { get; init; }
    /// <summary>The reserved established-trade identifier created by that execution.</summary>
    [Key(21)] public int ExecutionTradeId { get; init; }
    /// <summary>The actual trade date, unset until execution evidence is accepted.</summary>
    [Key(22)] public DateOnly? TradeDate { get; init; }
    /// <summary>The latest executed leg expiry, unset before execution.</summary>
    [Key(23)] public DateOnly? MaturityDate { get; init; }
    /// <summary>The backend attempt bound to this setup; fences delayed execution outcomes.</summary>
    [Key(24)] public Guid ExecutionAttemptId { get; init; }
    /// <summary>The intended effective date; the existing wire slot is retained for historical compatibility.</summary>
    [IgnoreMember] public DateOnly EffectiveDate { get => RequestedTradeDate; init => RequestedTradeDate = value; }
}
