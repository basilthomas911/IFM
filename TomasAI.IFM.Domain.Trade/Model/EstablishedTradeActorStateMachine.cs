using TomasAI.IFM.Domain.Trade.Order.Model;
using TomasAI.IFM.Domain.Trade.Shared.Model;

namespace TomasAI.IFM.Domain.Trade.Model;

/// <summary>Common low-frequency state model for OptionTrade and FuturesTrade command actors.</summary>
public sealed class EstablishedTradeActorStateMachine
{
    readonly HashSet<Guid> _amendmentIds = [];
    public EstablishedTradeDefinition? Current { get; private set; }

    public TradeDecision<EstablishedTradeDefinition> Create(EstablishedTradeDefinition trade)
    {
        if (Current is not null)
        {
            if (Current.Id == trade.Id && Current.ExecutionAttemptId == trade.ExecutionAttemptId)
                return TradeDecision<EstablishedTradeDefinition>.Accept(Current);
            return Reject("TRADE.ALREADY_EXISTS", "A different established Trade already owns this identity.");
        }
        if (!trade.Id.IsValid || trade.SourceComponentId == Guid.Empty || trade.ExecutionAttemptId == Guid.Empty ||
            trade.Legs.Length == 0 || trade.OriginalFills.Length == 0 || trade.EstablishedAtUtc.Kind != DateTimeKind.Utc)
            return Reject("TRADE.INVALID", "Trade identity, legs, original fills, source component, execution, and UTC establishment time are required.");
        if (trade.AssetFamily == TradeAssetFamily.Futures && trade.StrategyKind != TradeStrategyKind.FuturesOutright)
            return Reject("TRADE.TYPE_MISMATCH", "FuturesTrade requires FuturesOutright strategy.");
        if (trade.AssetFamily == TradeAssetFamily.FuturesOption && trade.StrategyKind == TradeStrategyKind.FuturesOutright)
            return Reject("TRADE.TYPE_MISMATCH", "OptionTrade cannot use FuturesOutright strategy.");
        Current = trade;
        return TradeDecision<EstablishedTradeDefinition>.Accept(Current);
    }

    public TradeDecision<EstablishedTradeDefinition> AmendEvidence(Guid amendmentId, decimal commissionDelta)
    {
        if (Current is null) return Reject("TRADE.NOT_FOUND", "Trade does not exist.");
        if (amendmentId == Guid.Empty) return Reject("TRADE.INVALID_AMENDMENT", "Amendment ID is required.");
        if (!_amendmentIds.Add(amendmentId)) return TradeDecision<EstablishedTradeDefinition>.Accept(Current);
        Current = Current with
        {
            OpeningCommission = Current.OpeningCommission + commissionDelta,
            EvidenceRevision = checked(Current.EvidenceRevision + 1),
            Status = EstablishedTradeStatus.Corrected
        };
        return TradeDecision<EstablishedTradeDefinition>.Accept(Current);
    }

    public TradeDecision<EstablishedTradeDefinition> BeginClose()
    {
        if (Current is null) return Reject("TRADE.NOT_FOUND", "Trade does not exist.");
        if (Current.Status is EstablishedTradeStatus.Closing or EstablishedTradeStatus.Closed)
            return Reject("TRADE.INVALID_TRANSITION", $"Cannot begin close in {Current.Status} state.");
        Current = Current with { Status = EstablishedTradeStatus.Closing };
        return TradeDecision<EstablishedTradeDefinition>.Accept(Current);
    }

    public TradeDecision<EstablishedTradeDefinition> Close()
    {
        if (Current is null) return Reject("TRADE.NOT_FOUND", "Trade does not exist.");
        if (Current.Status != EstablishedTradeStatus.Closing)
            return Reject("TRADE.INVALID_TRANSITION", $"Cannot close in {Current.Status} state.");
        Current = Current with { Status = EstablishedTradeStatus.Closed };
        return TradeDecision<EstablishedTradeDefinition>.Accept(Current);
    }

    public void Replay(EstablishedTradeDefinition state) => Current = state;

    static TradeDecision<EstablishedTradeDefinition> Reject(string code, string detail) =>
        TradeDecision<EstablishedTradeDefinition>.Reject(code, detail);
}

