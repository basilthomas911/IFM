using TomasAI.IFM.Domain.Trade.Model;
using TomasAI.IFM.Domain.Trade.Order.Model;
using TomasAI.IFM.Domain.Trade.Shared;

namespace TomasAI.IFM.Domain.Trade.Futures.Position.Model;

/// <summary>Resident whole-strategy state used by concrete strategy position command actors.</summary>
public sealed class StrategyPositionActorStateMachine
{
    readonly Dictionary<Guid, StrategyPositionLeg> _legs = [];
    Guid[] _orderedLegIds = [];
    public StrategyPositionSnapshot? Current { get; private set; }

    public TradeDecision<StrategyPositionSnapshot> Open(
        EstablishedTradeDefinition trade,
        Guid positionId,
        DateTime openedAtUtc)
    {
        if (Current is not null)
        {
            if (Current.Id.Trade == trade.Id) return TradeDecision<StrategyPositionSnapshot>.Accept(Current);
            return Reject("POSITION.ALREADY_EXISTS", "A different Trade already owns this position stream.");
        }
        if (positionId == Guid.Empty || openedAtUtc.Kind != DateTimeKind.Utc || trade.Status is EstablishedTradeStatus.Closed)
            return Reject("POSITION.INVALID_OPEN", "Open position requires a valid identity, UTC time, and open Trade.");

        foreach (var leg in trade.Legs)
        {
            var fills = trade.OriginalFills.Where(value => value.TradeLegId == leg.TradeLegId).ToArray();
            var absoluteQuantity = fills.Sum(static value => Math.Abs(value.SignedQuantity));
            if (absoluteQuantity == 0) return Reject("POSITION.MISSING_FILL", $"Leg {leg.TradeLegId} has no opening fill.");
            var openingPrice = fills.Sum(value => value.Price * Math.Abs(value.SignedQuantity)) / absoluteQuantity;
            var filledQuantity = fills.Sum(static value => value.SignedQuantity);
            _legs.Add(leg.TradeLegId, new StrategyPositionLeg
            {
                TradeLegId = leg.TradeLegId,
                ContractId = leg.ContractId,
                SignedQuantity = filledQuantity,
                OpeningPrice = openingPrice,
                CurrentPrice = openingPrice,
                LastPriceAtUtc = openedAtUtc,
                AssetFamily = leg.AssetFamily,
                ContractKey = leg.ContractKey,
                Expiry = leg.Expiry,
                Strike = leg.Strike,
                PutCall = leg.PutCall
            });
        }
        _orderedLegIds = _legs.Keys.Order().ToArray();
        Current = Build(new StrategyPositionId(trade.Id, positionId), trade.StrategyKind,
            StrategyPositionPhase.Open, 1, 1, openedAtUtc, true, 0);
        return TradeDecision<StrategyPositionSnapshot>.Accept(Current);
    }

    public TradeDecision<StrategyPositionSnapshot> UpdateLeg(
        Guid tradeLegId,
        decimal price,
        long sourceSequence,
        DateTime occurredAtUtc,
        long routeGeneration)
        => UpdateLeg(tradeLegId, null, price, sourceSequence, occurredAtUtc, routeGeneration);

    public TradeDecision<StrategyPositionSnapshot> UpdateLeg(
        Guid tradeLegId,
        string? contractId,
        decimal price,
        long sourceSequence,
        DateTime occurredAtUtc,
        long routeGeneration)
    {
        if (Current is null) return Reject("POSITION.NOT_FOUND", "Position does not exist.");
        if (!Current.IsOpen) return Reject("POSITION.CLOSED", "Position is closed.");
        if (routeGeneration != Current.RouteGeneration)
            return Reject("POSITION.STALE_ROUTE", "Route generation is stale.");
        if (!_legs.TryGetValue(tradeLegId, out var leg))
            return Reject("POSITION.UNKNOWN_LEG", "Trade leg is not part of this position.");
        if (contractId is not null && !string.Equals(contractId, leg.ContractId, StringComparison.Ordinal))
            return Reject("POSITION.CONTRACT_MISMATCH", "The routed ContractId does not identify this trade leg.");
        if (price <= 0 || occurredAtUtc.Kind != DateTimeKind.Utc)
            return Reject("POSITION.INVALID_TICK", "A positive price and UTC timestamp are required.");
        if (sourceSequence <= leg.LastSourceSequence)
            return Reject("POSITION.DUPLICATE_OR_OUT_OF_ORDER", "Tick sequence is not newer than the current leg sequence.");

        _legs[tradeLegId] = leg with
        {
            CurrentPrice = price,
            LastSourceSequence = sourceSequence,
            LastPriceAtUtc = occurredAtUtc
        };
        Current = Build(Current.Id, Current.StrategyKind, StrategyPositionPhase.MarkToMarket,
            checked(Current.PositionSequence + 1), Current.RouteGeneration, occurredAtUtc, true, Current.RealizedPnl);
        return TradeDecision<StrategyPositionSnapshot>.Accept(Current);
    }

    public TradeDecision<StrategyPositionSnapshot> EndOfDay(DateTime asOfUtc)
    {
        if (Current is null || !Current.IsOpen) return Reject("POSITION.NOT_OPEN", "An open position is required.");
        if (asOfUtc.Kind != DateTimeKind.Utc) return Reject("POSITION.INVALID_TIME", "EOD time must be UTC.");
        Current = Build(Current.Id, Current.StrategyKind, StrategyPositionPhase.EndOfDay,
            checked(Current.PositionSequence + 1), Current.RouteGeneration, asOfUtc, true, Current.RealizedPnl);
        return TradeDecision<StrategyPositionSnapshot>.Accept(Current);
    }

    public TradeDecision<StrategyPositionSnapshot> Close(DateTime closedAtUtc)
    {
        if (Current is null || !Current.IsOpen) return Reject("POSITION.NOT_OPEN", "An open position is required.");
        if (closedAtUtc.Kind != DateTimeKind.Utc) return Reject("POSITION.INVALID_TIME", "Close time must be UTC.");
        var realized = CalculatePnl();
        Current = Build(Current.Id, Current.StrategyKind, StrategyPositionPhase.Close,
            checked(Current.PositionSequence + 1), checked(Current.RouteGeneration + 1), closedAtUtc, false, realized);
        return TradeDecision<StrategyPositionSnapshot>.Accept(Current);
    }

    public TradeDecision<StrategyPositionSnapshot> CorrectBasis(Guid tradeLegId, decimal openingPrice, DateTime correctedAtUtc)
    {
        if (Current is null) return Reject("POSITION.NOT_FOUND", "Position does not exist.");
        if (!_legs.TryGetValue(tradeLegId, out var leg) || openingPrice <= 0 || correctedAtUtc.Kind != DateTimeKind.Utc)
            return Reject("POSITION.INVALID_CORRECTION", "Correction requires an existing leg, positive basis, and UTC time.");
        _legs[tradeLegId] = leg with { OpeningPrice = openingPrice };
        Current = Build(Current.Id, Current.StrategyKind, StrategyPositionPhase.Correction,
            checked(Current.PositionSequence + 1), checked(Current.RouteGeneration + 1), correctedAtUtc,
            Current.IsOpen, Current.IsOpen ? Current.RealizedPnl : CalculatePnl());
        return TradeDecision<StrategyPositionSnapshot>.Accept(Current);
    }

    public void Replay(StrategyPositionSnapshot snapshot)
    {
        Current = snapshot;
        _legs.Clear();
        foreach (var leg in snapshot.Legs) _legs.Add(leg.TradeLegId, leg);
        _orderedLegIds = snapshot.Legs.Select(static leg => leg.TradeLegId).ToArray();
    }

    StrategyPositionSnapshot Build(
        StrategyPositionId id,
        TradeStrategyKind strategy,
        StrategyPositionPhase phase,
        long sequence,
        long generation,
        DateTime asOfUtc,
        bool isOpen,
        decimal realizedPnl)
    {
        var legs = new StrategyPositionLeg[_orderedLegIds.Length];
        decimal value = 0;
        decimal pnl = 0;
        for (var index = 0; index < _orderedLegIds.Length; index++)
        {
            var leg = _legs[_orderedLegIds[index]];
            legs[index] = leg;
            value += leg.CurrentPrice * leg.SignedQuantity;
            pnl += (leg.CurrentPrice - leg.OpeningPrice) * leg.SignedQuantity;
        }
        return new StrategyPositionSnapshot
        {
            Id = id,
            StrategyKind = strategy,
            Phase = phase,
            PositionSequence = sequence,
            RouteGeneration = generation,
            Legs = legs,
            MarketValue = value,
            UnrealizedPnl = isOpen ? pnl : 0,
            RealizedPnl = realizedPnl,
            AsOfUtc = asOfUtc,
            IsOpen = isOpen
        };
    }

    decimal CalculatePnl() => _legs.Values.Sum(static leg =>
        (leg.CurrentPrice - leg.OpeningPrice) * leg.SignedQuantity);

    static TradeDecision<StrategyPositionSnapshot> Reject(string code, string detail) =>
        TradeDecision<StrategyPositionSnapshot>.Reject(code, detail);
}
