using TomasAI.IFM.Domain.Trade.Shared.ViewModels;

namespace TomasAI.IFM.Domain.Trade.Futures.Option.Position.IronCondor.Command.Model;

/// <summary>Immutable proposed monitoring limits, with a business rejection when computation cannot proceed.</summary>
internal sealed record IronCondorMonitoringLimits(TradeLimitReadModel? TradeLimits,
    TradeTypeLimitReadModel[] SpreadLimits, string? RejectionReason);
