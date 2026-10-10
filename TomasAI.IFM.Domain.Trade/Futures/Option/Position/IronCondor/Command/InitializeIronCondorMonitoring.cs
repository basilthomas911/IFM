using TomasAI.IFM.Domain.Trade.Futures.Option.Position.IronCondor.Command.State;
using TomasAI.IFM.Domain.Trade.Futures.Option.Position.IronCondor.Plan.Model;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Futures.Option.Position;
using TomasAI.IFM.Domain.Trade.Futures.Option.Position.IronCondor.Command.Model;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Futures.Option.Position.IronCondor.Command;

/// <summary>Computes legacy monitoring limits from captured business observations, then applies their source event.</summary>
public static class InitializeIronCondorMonitoring
{
    /// <summary>Checks failure guards before the single event application; only state.Apply mutates authoritative limits.</summary>
    /// <param name="command">The captured initialization intent.</param><param name="state">The position and monitoring-limit owner.</param>
    /// <returns>Accepted command identity or a descriptive business failure.</returns>
    public static ServiceResult<GuidResult> Execute(this InitializeIronCondorMonitoringCommand command, IronCondorPositionCommandState state)
    {
        var errorMsg = "IronCondorMonitoring.STATE.APPLY_FAILED: unable to apply initialized limits";
        var updated = command.Compute(state.PositionSnapshot, out var monitoringLimits) switch
        {
            _ when monitoringLimits.RejectionReason is not null => command.UpdateFailed(ref errorMsg, monitoringLimits.RejectionReason),
            _ when state.TradeLimits is not null => command.UpdateFailed(ref errorMsg, "IronCondorMonitoring.LIMITS.ALREADY_INITIALIZED"),
            _ => state.Update(command.CreateIronCondorMonitoringInitializedEvent(monitoringLimits), command)
        };
        return updated ? new ServiceOk<GuidResult>(new GuidResult(command.CommandId)) : command.UpdateFailed(errorMsg);
    }

    /// <summary>Computes immutable limits from actual weighted opening prices, quantity, multiplier, cash and accepted capital.</summary>
    /// <param name="command">The captured trade, cash and order evidence.</param><param name="position">The matching authoritative position.</param>
    /// <param name="monitoringLimits">The proposed limits or validation failure.</param><returns>True when all required business evidence is valid.</returns>
    internal static bool Compute(this InitializeIronCondorMonitoringCommand command, StrategyPositionSnapshot? position, out IronCondorMonitoringLimits monitoringLimits)
    {
        try
        {
            var input = command.MonitoringInitialization;
            var trade = input.IronCondorTrade;
            if (position is null || position.Id != command.EntityId || trade.Id != command.EntityId.Trade
                || trade.StrategyKind != TradeStrategyKind.IronCondor || position.Legs.Length != 4
                || input.FundFinancialRevision < 0 || input.TradeOrderRevision <= 0 || input.FundCashAsOfUtc.Kind != DateTimeKind.Utc
                || input.InitializedAtUtc.Kind != DateTimeKind.Utc || input.InitializedAtUtc == default
                || input.ValueDate == default || input.FundCashAsOfUtc > input.InitializedAtUtc
                || input.InitializedAtUtc - input.FundCashAsOfUtc > TimeSpan.FromSeconds(30))
                throw new ArgumentException("Matching position, established trade, accepted order and ledger evidence required.");
            var type = IronCondorMonitoringInputReader.IdentifyTradeType(trade.Legs);
            var multipliers = trade.Legs.Select(leg => leg.CashMultiplier).Distinct().ToArray();
            if (multipliers.Length != 1 || multipliers[0] <= 0 || trade.Legs.Any(leg => !position.Legs.Any(p => p.TradeLegId == leg.TradeLegId && p.ContractId == leg.ContractId
                    && p.SignedQuantity == leg.SignedQuantity && p.PutCall == leg.PutCall && p.Strike == leg.Strike)))
                throw new ArgumentException("Exact four legs and common positive contract multiplier required.");
            var expiry = trade.MaturityDate ?? throw new ArgumentException("Expiry required for each leg.");
            var limits = IronCondorTradeLimitInitialization.Initialize(trade.Id.TradeId, type, OpeningSpreadPrice(position, 2), OpeningSpreadPrice(position, 1),
                Math.Abs(trade.Legs[0].SignedQuantity), multipliers[0], input.FundAvailableCash, input.RequiredCapital,
                trade.OpeningCommission, expiry.DayNumber - input.ValueDate.DayNumber, input.InitializedAtUtc, "IronCondorMonitoring");
            monitoringLimits = new(limits.TradeLimit, limits.SpreadLimits, null);
        }
        catch (ArgumentException error) { monitoringLimits = new(null, [], $"IronCondorMonitoring.INPUTS.INVALID: {error.Message}"); }
        catch (InvalidOperationException error) { monitoringLimits = new(null, [], $"IronCondorMonitoring.LEGS.INVALID: {error.Message}"); }
        return monitoringLimits.RejectionReason is null;
    }

    /// <summary>Calculates the opening spread magnitude from actual weighted opening executions: abs(short price - long price).</summary>
    /// <param name="position">The exact four-leg opening execution basis.</param><param name="putCall">Two for puts or one for calls.</param>
    /// <returns>The per-strategy opening premium in points, without quantity or multiplier scaling.</returns>
    static decimal OpeningSpreadPrice(StrategyPositionSnapshot position, byte putCall)
        => Math.Abs(position.Legs.Single(leg => leg.PutCall == putCall && leg.SignedQuantity < 0).OpeningPrice
            - position.Legs.Single(leg => leg.PutCall == putCall && leg.SignedQuantity > 0).OpeningPrice);

    /// <summary>Creates the accepted source event with explicit command identity and business-named payloads.</summary>
    /// <param name="command">The originating initialization command.</param><param name="monitoringLimits">The validated calculation.</param>
    /// <returns>The event that exclusively updates state and supplies persisted limit projections.</returns>
    internal static IronCondorMonitoringInitializedEvent CreateIronCondorMonitoringInitializedEvent(this InitializeIronCondorMonitoringCommand command, IronCondorMonitoringLimits monitoringLimits)
        => new()
        {
            CommandId = command.CommandId, EntityId = command.EntityId, ReceivedOn = command.MonitoringInitialization.InitializedAtUtc,
            Subject = new(ActorType.Event, "FuturesIronCondorTradePositionEvent", IronCondorMonitoringInitializedEvent.Verb, command.EntityId.Format()),
            MonitoringInitialization = command.MonitoringInitialization, TradeLimits = monitoringLimits.TradeLimits!, SpreadLimits = monitoringLimits.SpreadLimits
        };

}
