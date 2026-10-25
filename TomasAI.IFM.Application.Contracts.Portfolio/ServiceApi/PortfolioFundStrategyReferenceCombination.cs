using MessagePack;
using TomasAI.IFM.Domain.Portfolio.Shared.Contracts;
using TomasAI.IFM.Domain.Portfolio.Shared.Identities;
using TomasAI.IFM.Domain.Portfolio.Shared.ViewModels;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Portfolio.Shared.ServiceApi;

[MessagePackObject(AllowPrivate = true)]
public sealed record PortfolioFundStrategyReferenceCombination
{
    [Key(0)] public int PortfolioId { get; init; }
    [Key(1)] public long PortfolioVersion { get; init; }
    [Key(2)] public int FundId { get; init; }
    [Key(3)] public long FundMandateVersion { get; init; }
    [Key(4)] public int TradingYear { get; init; }
    [Key(5)] public string DecisionHorizon { get; init; } = string.Empty;
    [Key(6)] public string UnderlyingRoot { get; init; } = string.Empty;
    [Key(7)] public string AssetType { get; init; } = string.Empty;
    [Key(8)] public string TradeFamily { get; init; } = string.Empty;
    [Key(9)] public Guid TradeTemplateId { get; init; }
    [Key(10)] public long TradeTemplateVersion { get; init; }
    [Key(11)] public Guid TradeSelectionHintProfileId { get; init; }
    [Key(12)] public long TradeSelectionHintProfileVersion { get; init; }
    [Key(13)] public Guid OrderCompositionProfileId { get; init; }
    [Key(14)] public long OrderCompositionProfileVersion { get; init; }
    [Key(15)] public bool CurrentlyEligible { get; init; }
    [Key(16)] public string ReasonCode { get; init; } = string.Empty;
    [Key(17)] public TomasAI.IFM.Domain.Reference.Shared.ViewModels.TradeStrategyFamilyReference? TradeStrategyFamily { get; init; }
}
