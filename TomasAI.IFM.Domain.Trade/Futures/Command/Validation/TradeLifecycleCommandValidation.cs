using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Validation;
using TomasAI.IFM.Domain.Trade.Shared.Trade;
using TomasAI.IFM.Domain.Trade.Shared.Trade.Position;
using TomasAI.IFM.Domain.Trade.Shared.Futures;
using TomasAI.IFM.Domain.Trade.Shared.Futures.Option;
using TomasAI.IFM.Domain.Trade.Shared.Futures.Position;
using TomasAI.IFM.Domain.Trade.Shared.Futures.Option.Position;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Validation;

namespace TomasAI.IFM.Domain.Trade.Futures.Command.Validation;

/// <summary>Pure ingress checks shared by established Trade and strategy Position commands.</summary>
public static class TradeLifecycleCommandValidation
{
    /// <summary>Validates immutable trade evidence and its owning actor identity.</summary>
    public static List<ValidationError> ValidateEstablishedTradeCommand(this List<ValidationError> errors,
        ICommand<TradeEntityId> command, TradeAssetFamily assetFamily, TradeStrategyKind? strategy)
    {
        if (command is CreateEstablishedTradeCommand create)
        {
            errors.ValidateEstablishedTrade(create.Trade);
            if (create.Trade is not null)
            {
                if (create.Trade.Id != command.EntityId) errors.Add(new("Trade.Id must match EntityId."));
                if (create.Trade.AssetFamily != assetFamily || strategy is not null && create.Trade.StrategyKind != strategy)
                    errors.Add(new("Trade asset family and strategy must match the owning actor."));
            }
        }
        if (command is AmendEstablishedTradeEvidenceCommand amend && amend.AmendmentId == Guid.Empty)
            errors.Add(new("AmendmentId is required."));
        var closingFills = command switch
        {
            CloseFuturesTradeCommand close => close.ClosingFills,
            CloseOptionTradeCommand close => close.ClosingFills,
            _ => null
        };
        if (command is CloseFuturesTradeCommand or CloseOptionTradeCommand)
        {
            errors.ValidateClosingFills(closingFills, true);
            var closedAt = command is CloseFuturesTradeCommand close ? close.ClosedAtUtc : ((CloseOptionTradeCommand)command).ClosedAtUtc;
            if (closedAt.Kind != DateTimeKind.Utc) errors.Add(new("ClosedAtUtc must be UTC."));
        }
        if (command.Subject.EntityId != command.EntityId.Format()) errors.Add(new("Subject.EntityId must match EntityId."));
        return errors;
    }

    /// <summary>Validates position inputs without changing position state or routing subscriptions.</summary>
    public static List<ValidationError> ValidatePositionCommand(this List<ValidationError> errors,
        ICommand<StrategyPositionId> command, TradeAssetFamily assetFamily, TradeStrategyKind? strategy)
    {
        var trade = command switch
        {
            OpenFuturesPositionCommand open => open.Trade,
            OpenPositionCommand open => open.Trade,
            _ => null
        };
        if (command is OpenFuturesPositionCommand or OpenPositionCommand)
        {
            errors.ValidateEstablishedTrade(trade);
            if (trade is not null && (trade.Id != command.EntityId.Trade || trade.AssetFamily != assetFamily || trade.StrategyKind != strategy))
                errors.Add(new("Opening Trade identity, asset family and strategy must match the Position."));
        }
        var effectiveAt = command switch
        {
            OpenFuturesPositionCommand value => value.EffectiveAtUtc,
            OpenPositionCommand value => value.EffectiveAtUtc,
            UpdateFuturesPositionMarketPriceCommand value => value.EffectiveAtUtc,
            UpdatePositionLegMarketPriceCommand value => value.EffectiveAtUtc,
            CorrectFuturesPositionBasisCommand value => value.EffectiveAtUtc,
            CorrectPositionBasisCommand value => value.EffectiveAtUtc,
            EndOfDayFuturesPositionCommand value => value.EffectiveAtUtc,
            CloseFuturesPositionCommand value => value.EffectiveAtUtc,
            TimedPositionCommand value => value.EffectiveAtUtc,
            ChangeTradeLegDataCommand value => value.EffectiveAtUtc,
            _ => (DateTime?)null
        };
        if (effectiveAt is not null && effectiveAt.Value.Kind != DateTimeKind.Utc) errors.Add(new("EffectiveAtUtc must be UTC."));
        var market = command switch
        {
            UpdateFuturesPositionMarketPriceCommand value => (TradeLegId: value.TradeLegId, Price: value.Price, SourceSequence: value.SourceSequence, RouteGeneration: value.RouteGeneration),
            UpdatePositionLegMarketPriceCommand value => (TradeLegId: value.TradeLegId, Price: value.Price, SourceSequence: value.SourceSequence, RouteGeneration: value.RouteGeneration),
            ChangeTradeLegDataCommand value => (TradeLegId: value.TradeLegId, Price: value.Price, SourceSequence: value.SourceSequence, RouteGeneration: value.RouteGeneration),
            _ => ((Guid TradeLegId, decimal Price, long SourceSequence, long RouteGeneration)?)null
        };
        if (market is { } quote)
        {
            if (quote.TradeLegId == Guid.Empty) errors.Add(new("TradeLegId is required."));
            if (quote.Price <= 0) errors.Add(new("Price must be positive."));
            if (quote.SourceSequence <= 0) errors.Add(new("SourceSequence must be positive."));
            if (quote.RouteGeneration <= 0) errors.Add(new("RouteGeneration must be positive."));
        }
        if (command is ChangeTradeLegDataCommand routed && (routed.TradeType != strategy || string.IsNullOrWhiteSpace(routed.ContractId)))
            errors.Add(new("Routed ContractId and TradeType must match the strategy Position."));
        var correction = command switch
        {
            CorrectFuturesPositionBasisCommand value => (TradeLegId: value.TradeLegId, Price: value.Price),
            CorrectPositionBasisCommand value => (TradeLegId: value.TradeLegId, Price: value.Price),
            _ => ((Guid TradeLegId, decimal Price)?)null
        };
        if (correction is { } basis && (basis.TradeLegId == Guid.Empty || basis.Price <= 0)) errors.Add(new("Correction requires TradeLegId and a positive Price."));
        var closingFills = command switch
        {
            CloseFuturesPositionCommand value => value.ClosingFills,
            CloseIronCondorPositionCommand value => value.ClosingFills,
            CloseVerticalSpreadPositionCommand value => value.ClosingFills,
            _ => null
        };
        if (command is CloseFuturesPositionCommand or CloseIronCondorPositionCommand or CloseVerticalSpreadPositionCommand)
            errors.ValidateClosingFills(closingFills, false);
        if (command.Subject.EntityId != command.EntityId.Format()) errors.Add(new("Subject.EntityId must match EntityId."));
        return errors;
    }

    /// <summary>Validates supplied close executions; legacy position closure may have an empty array.</summary>
    private static List<ValidationError> ValidateClosingFills(this List<ValidationError> errors, ExecutionFillEvidence[]? fills, bool required)
    {
        if (fills is null || required && fills.Length == 0) errors.Add(new("ClosingFills are required."));
        foreach (var fill in fills ?? []) errors.ValidateExecutionFill(fill);
        return errors;
    }
}
