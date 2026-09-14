using System.Security.Cryptography;
using System.Text;
using TomasAI.IFM.Application.MarketData.OperationsHealth;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesItiSignal.Realtime.Actor;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesItiSignal.Realtime.Logging;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.ServiceApi;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.FuturesMarketPrice.Events;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesItiSignal.Realtime;

/// <summary>Maps one eligible current ES trade-price update to a Daily Futures ITI Generate command.</summary>
public static class FuturesMarketPriceUpdated
{
    const string Scope = "ES";

    /// <summary>Processes a normalized market-price update through the minimal Daily ITI ingress boundary.</summary>
    /// <param name="event">The routed normalized futures market-price event.</param>
    /// <param name="context">The typed realtime actor context.</param>
    /// <returns><see langword="true"/> when the event was handled or intentionally ignored.</returns>
    public static ValueTask<bool> ExecuteAsync(
        this FuturesMarketPriceUpdatedRealtimeEvent @event,
        IFuturesItiSignalRealtimeContext context)
    {
        ArgumentNullException.ThrowIfNull(@event);
        ArgumentNullException.ThrowIfNull(context);

        var telemetry = context.Telemetry;
        var trade = @event.Price.Trade;
        var eventTime = trade?.EventTimestamp.UtcDateTime ?? @event.ReceivedOn;
        telemetry.RecordMarketPriceReceived(eventTime);

        if (context.GenerationGate.IsBusy)
        {
            telemetry.RecordBusySkipped();
            return ValueTask.FromResult(true);
        }

        try
        {
            if (!IsUsableTradeEvent(@event, out var esTrade, out var filterReason))
                return ValueTask.FromResult(Filter(filterReason));
            if (!StringComparer.Ordinal.Equals(@event.EntityId.ContractId, @event.Price.ContractId)
                || @event.EntityId.ValueDate != @event.Price.ValueDate
                || @event.EntityId.AssetTypeId != @event.Price.AssetTypeId)
            {
                return ValueTask.FromResult(
                    Reject("The market-price event entity and price snapshot identities do not match."));
            }
            if (!context.MarketDataApi.TryGetOnTheRunFuturesContract("ES", out var esContract)
                || !StringComparer.Ordinal.Equals(esContract.ContractId, @event.Price.ContractId))
                return ValueTask.FromResult(Filter("Trade is not for the current on-the-run ES contract."));

            telemetry.RecordEligibleEsTrade(esTrade.EventTimestamp.UtcDateTime);

            if (!TryGetCurrentVxPrice(context, out var vxPrice, out var unavailableReason))
            {
                _ = telemetry.RecordInputUnavailable(unavailableReason);
                context.HealthEvidence.Record("ITI", Scope, "Degraded", unavailableReason, eventTime);
                return ValueTask.FromResult(true);
            }

            var commandId = CreateCommandId(@event, esTrade);
            var sourceEventId = @event.Id;
            var contractId = @event.Price.ContractId;
            var valueDate = @event.Price.ValueDate;
            var futuresPrice = Convert.ToDouble(esTrade.LastPrice);
            var tradeTimestamp = esTrade.EventTimestamp.UtcDateTime;
            if (!context.GenerationGate.TryStart(
                    () =>
                    {
                        telemetry.RecordCommandRequested();
                        FuturesItiSignalRealtimeLogging.CommandGenerated(
                            context.Logger,
                            sourceEventId,
                            commandId,
                            contractId,
                            valueDate);
                    },
                    () => GenerateAsync(
                        context,
                        sourceEventId,
                        commandId,
                        contractId,
                        valueDate,
                        tradeTimestamp,
                        futuresPrice,
                        vxPrice,
                        eventTime)))
            {
                telemetry.RecordBusySkipped();
                return ValueTask.FromResult(true);
            }
            return ValueTask.FromResult(true);
        }
        catch (Exception exception)
        {
            telemetry.RecordFailure(exception.Message);
            context.HealthEvidence.Record("ITI", Scope, "Unhealthy", exception.Message, eventTime);
            FuturesItiSignalRealtimeLogging.IngressFailed(
                context.Logger,
                exception,
                @event.Id,
                @event.CommandId,
                @event.EntityId.ContractId,
                nameof(FuturesMarketPriceUpdated),
                exception.GetType().Name);
            throw;
        }

        bool Filter(string reason)
        {
            telemetry.RecordFiltered(reason);
            return true;
        }


        bool Reject(string reason)
        {
            telemetry.RecordFailure(reason);
            context.HealthEvidence.Record("ITI", Scope, "Unhealthy", reason, eventTime);
            FuturesItiSignalRealtimeLogging.IngressRejected(
                context.Logger,
                @event.Id,
                @event.CommandId,
                @event.EntityId.ContractId,
                nameof(FuturesMarketPriceUpdated),
                reason);
            return false;
        }
    }

    static async ValueTask GenerateAsync(
        IFuturesItiSignalRealtimeContext context,
        Guid sourceEventId,
        Guid commandId,
        string contractId,
        DateOnly valueDate,
        DateTime tradeTimestamp,
        double futuresPrice,
        double vxPrice,
        DateTime eventTime)
    {
        try
        {
            var result = await MarketDataAnalyticsCommandApiExtensions.GenerateFuturesItiSignalAsync(
                context,
                contractId,
                valueDate,
                TimeFrameType.Daily,
                tradeTimestamp,
                futuresPrice,
                vxPrice,
                commandId,
                valueDate).ConfigureAwait(false);

            if (result is ServiceFailed<GuidResult> failed)
            {
                var message = failed.ErrorMessage ?? "Generate command was rejected.";
                context.Telemetry.RecordFailure(message);
                context.HealthEvidence.Record("ITI", Scope, "Unhealthy", message, eventTime);
                FuturesItiSignalRealtimeLogging.CommandFailed(
                    context.Logger, sourceEventId, commandId, contractId, valueDate,
                    failed.ErrorCode, message);
                return;
            }

            context.Telemetry.RecordCommandAccepted();
            context.HealthEvidence.Record(
                "ITI", Scope, "Healthy", "Daily ITI command accepted; no signal change is a valid result.", eventTime);
        }
        catch (Exception exception)
        {
            context.Telemetry.RecordFailure(exception.Message);
            context.HealthEvidence.Record("ITI", Scope, "Unhealthy", exception.Message, eventTime);
            FuturesItiSignalRealtimeLogging.IngressFailed(
                context.Logger,
                exception,
                sourceEventId,
                commandId,
                contractId,
                nameof(FuturesMarketPriceUpdated),
                exception.GetType().Name);
        }
    }

    /// <summary>Applies the allocation-free eligibility checks that do not require market-data lookup.</summary>
    internal static bool IsUsableTradeEvent(
        FuturesMarketPriceUpdatedRealtimeEvent @event,
        out FuturesMarketTradeSnapshot trade,
        out string reason)
    {
        if (@event.Price.AssetTypeId != AssetTypeId.Futures)
        {
            trade = default;
            reason = "Asset is not Futures.";
            return false;
        }
        if (@event.UpdateSource != FuturesMarketPriceUpdateSource.Trade)
        {
            trade = default;
            reason = "Update is not a trade.";
            return false;
        }
        if (@event.Price.Trade is not { } currentTrade)
        {
            trade = default;
            reason = "Trade snapshot is absent.";
            return false;
        }
        if (currentTrade.NormalizedTradeAction is NormalizedTradeAction.Cancel or NormalizedTradeAction.Clear)
        {
            trade = default;
            reason = "Trade action does not provide a usable current price.";
            return false;
        }
        if ((currentTrade.NormalizedTradeConditionFlags & NormalizedTradeConditionFlags.UndefinedPrice) != 0
            || currentTrade.LastPrice <= 0)
        {
            trade = default;
            reason = "Trade price is undefined or non-positive.";
            return false;
        }

        trade = currentTrade;
        reason = string.Empty;
        return true;
    }

    static bool TryGetCurrentVxPrice(
        IFuturesItiSignalRealtimeContext context,
        out double price,
        out string reason)
    {
        price = 0;
        if (!context.MarketDataApi.TryGetOnTheRunFuturesContract("VX", out var vxContract))
        {
            reason = "Current on-the-run VX contract is unavailable.";
            return false;
        }
        if (!context.MarketDataApi.TryGetLastTickPrice(vxContract.ContractId, out var snapshot)
            || snapshot.Trade is not { } trade
            || trade.LastPrice <= 0
            || (trade.NormalizedTradeConditionFlags & NormalizedTradeConditionFlags.UndefinedPrice) != 0
            || trade.NormalizedTradeAction is NormalizedTradeAction.Cancel or NormalizedTradeAction.Clear)
        {
            reason = $"Current VX trade price is unavailable for {vxContract.ContractId}.";
            return false;
        }

        price = Convert.ToDouble(trade.LastPrice);
        reason = string.Empty;
        return true;
    }

    internal static Guid CreateCommandId(
        FuturesMarketPriceUpdatedRealtimeEvent @event,
        FuturesMarketTradeSnapshot trade)
    {
        if (@event.Id != Guid.Empty)
            return @event.Id;

        var identity = string.Create(
            System.Globalization.CultureInfo.InvariantCulture,
            $"FuturesItiSignal|Daily|{@event.Price.ContractId}|{@event.Price.ValueDate:yyyy-MM-dd}|{trade.StreamEpochId:N}|{trade.TradeOrdinal}|{trade.SourceSequence}|{trade.EventTimestamp.UtcTicks}");
        return new Guid(SHA256.HashData(Encoding.UTF8.GetBytes(identity)).AsSpan(0, 16));
    }
}
