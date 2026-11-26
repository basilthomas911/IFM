using TomasAI.IFM.Domain.MarketData.Analytics.RegimeDiscovery;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Commands;
using TomasAI.IFM.Shared.EventModelActor;
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
    static readonly FuturesAtrHotCacheReader AtrInputs = new();
    const string Scope = "ES";
    internal const string FailureRecordedKey = "IFM.ITI.IngressFailureRecorded";

    /// <param name="event">
    /// The routed normalized futures market-price event.
    /// </param>
    extension(FuturesMarketPriceUpdatedRealtimeEvent @event)
    {
        /// <summary>Processes a normalized market-price update through the minimal Daily ITI ingress boundary.</summary>
        ///
        /// <param name="context">The typed realtime actor context.</param>
        /// <param name="cancellationToken">Stops ingress processing without recalling an accepted downstream command.</param>
        /// <returns><see langword="true"/> when the event was handled or intentionally ignored.</returns>
        public async ValueTask<bool> ExecuteAsync(
            IFuturesItiSignalRealtimeContext context,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(@event);
            ArgumentNullException.ThrowIfNull(context);
            cancellationToken.ThrowIfCancellationRequested();

            var telemetry = context.Telemetry;
            var trade = @event.Price.Trade;
            var eventTime = trade?.EventTimestamp.UtcDateTime ?? @event.ReceivedOn;
            telemetry.RecordMarketPriceReceived(eventTime);

            try
            {
                if (!IsUsableTradeEvent(@event, out var esTrade, out var filterReason))
                    return Filter(context, filterReason);
                if (!StringComparer.Ordinal.Equals(@event.EntityId.ContractId, @event.Price.ContractId)
                    || @event.EntityId.ValueDate != @event.Price.ValueDate
                    || @event.EntityId.AssetTypeId != @event.Price.AssetTypeId)
                {
                    return Reject(context, @event, eventTime, "The market-price event entity and price snapshot identities do not match.");
                }
                var commandId = CreateCommandId(@event, esTrade);
                var identity = string.Create(System.Globalization.CultureInfo.InvariantCulture,
                    $"{@event.Price.ContractId}|{@event.Price.ValueDate:yyyyMMdd}|{@event.Price.PublisherId}|{@event.Price.InstrumentId}|{esTrade.StreamEpochId:N}|{esTrade.TradeOrdinal}|{esTrade.SourceSequence}|{esTrade.EventTimestamp.UtcTicks}|{esTrade.LastPrice:G29}");
                var fingerprint = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(identity)));
                if (context.IngressRecovery is { } recovery)
                    return await recovery.ProcessAsync(commandId, fingerprint, esTrade.EventTimestamp.UtcDateTime,
                        () => Capture(@event, context, esTrade, commandId, eventTime),
                        (command, token) => DispatchAsync(context, command, token), cancellationToken, Cursor(@event, esTrade)).ConfigureAwait(false);
                // Explicitly volatile test contexts retain the same outcome contract.
                var captured = Capture(@event, context, esTrade, commandId, eventTime);
                return captured is null || await DispatchAsync(context, captured, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
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
                exception.Data[FailureRecordedKey] = true;
                throw;
            }

        }
    }


    static GenerateFuturesItiSignalCommand? Capture(FuturesMarketPriceUpdatedRealtimeEvent source,
        IFuturesItiSignalRealtimeContext context, FuturesMarketTradeSnapshot trade, Guid id, DateTime eventTime)
    {
        if (!context.MarketDataApi.TryGetOnTheRunFuturesContract("ES", out var esContract)
            || !StringComparer.Ordinal.Equals(esContract.ContractId, source.Price.ContractId))
        { _ = Filter(context, "Trade is not for the current on-the-run ES contract."); return null; }
        context.Telemetry.RecordEligibleEsTrade(trade.EventTimestamp.UtcDateTime);
        if (!TryGetCurrentVxPrice(context, out var vxPrice, out var reason))
        {
            _ = context.Telemetry.RecordInputUnavailable(reason);
            context.HealthEvidence.Record("ITI", Scope, "Degraded", reason, eventTime); return null;
        }
        DailyItiAtrThreshold? threshold = null;
        if (AtrInputs.TryGetLatest(source.Price.ContractId, TimeFrameType.FiveMinutes, out var atr)
            && atr is { IsWarm: true, PeriodLength: 14, AtrValue: > 0, Metadata.IsValid: true } && double.IsFinite(atr.AtrValue))
        {
            var input = new DailyItiAtrThreshold(source.Price.ContractId, atr.AtrValue,
                atr.Metadata.MarketDataAsOfUtc.UtcDateTime, atr.Metadata.CalculatedAtUtc.UtcDateTime, atr.Metadata.SourceSequence);
            if (input.IsUsableAt(source.Price.ContractId, trade.EventTimestamp.UtcDateTime)) threshold = input;
        }
        var command = new GenerateFuturesItiSignalCommand(source.Price.ContractId, source.Price.ValueDate, TimeFrameType.Daily,
            trade.EventTimestamp.UtcDateTime, (double)trade.LastPrice, vxPrice, source.Price.ValueDate)
        {
            CommandId = id, Subject = new(ActorType.Command, GenerateFuturesItiSignalCommand.Actor,
                GenerateFuturesItiSignalCommand.Verb, new FuturesItiSignalEntityId(source.Price.ContractId, source.Price.ValueDate, TimeFrameType.Daily).Format()),
            SourceCursor = Cursor(source, trade),
            ErrorCode = GenerateFuturesItiSignalCommand.ErrorId, RequireDailyAtr = true, DailyAtr = threshold, InputsFrozen = true
        };
        MessageOrigins.ForPublication(command, TimeProvider.System, causationId: source.Id == Guid.Empty ? null : source.Id);
        return command;
    }

    static FuturesItiSourceCursor? Cursor(FuturesMarketPriceUpdatedRealtimeEvent source, FuturesMarketTradeSnapshot trade)
        => trade.StreamEpochId != Guid.Empty && trade.TradeOrdinal > 0 && !string.IsNullOrWhiteSpace(source.SourceDataset)
            ? new(source.SourceDataset, source.Price.PublisherId, source.Price.InstrumentId, trade.StreamEpochId,
                trade.TradeOrdinal, trade.SourceSequence, trade.EventTimestamp.UtcDateTime, trade.ReceiveTimestamp.UtcDateTime) : null;

    static bool Filter(IFuturesItiSignalRealtimeContext context, string reason)
    {
        context.Telemetry.RecordFiltered(reason);
        return true;
    }


    static bool Reject(IFuturesItiSignalRealtimeContext context, FuturesMarketPriceUpdatedRealtimeEvent @event, DateTime eventTime, string reason)
    {
        context.Telemetry.RecordFailure(reason);
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

    /// <summary>Submits a frozen command once. Only a successful reply permits completion.</summary>
    public static async ValueTask<bool> DispatchAsync(IFuturesItiSignalRealtimeContext context,
        GenerateFuturesItiSignalCommand command, CancellationToken cancellationToken = default)
    {
        context.Telemetry.RecordCommandRequested();
        FuturesItiSignalRealtimeLogging.CommandGenerated(context.Logger,
            MessageOrigins.Get(command)?.CausationId ?? command.CommandId, command.CommandId, command.ContractId, command.ValueDate);
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            // Join the actual request before releasing the journal gate or frozen payload, including cancellation.
            var result = await context.RequestAsync<GenerateFuturesItiSignalCommand, FuturesItiSignalEntityId>(command).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            if (result is null || !result.Success)
            {
                var message = (result as ServiceFailed<GuidResult>)?.ErrorMessage ?? "Generate command did not return success.";
                context.Telemetry.RecordFailure(message);
                context.HealthEvidence.Record("ITI", Scope, "Unhealthy", message, command.Timestamp);
                FuturesItiSignalRealtimeLogging.CommandFailed(context.Logger, MessageOrigins.Get(command)?.CausationId ?? command.CommandId,
                    command.CommandId, command.ContractId, command.ValueDate,
                    (result as ServiceFailed<GuidResult>)?.ErrorCode ?? command.ErrorCode, message);
                return false;
            }
            context.Telemetry.RecordCommandAccepted();
            context.HealthEvidence.Record("ITI", Scope, "Healthy", "Daily ITI command accepted; no signal change is a valid result.", command.Timestamp);
            return true;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (Exception exception)
        {
            context.Telemetry.RecordFailure(exception.Message);
            context.HealthEvidence.Record("ITI", Scope, "Unhealthy", exception.Message, command.Timestamp);
            FuturesItiSignalRealtimeLogging.IngressFailed(context.Logger, exception,
                MessageOrigins.Get(command)?.CausationId ?? command.CommandId, command.CommandId, command.ContractId,
                nameof(FuturesMarketPriceUpdated), exception.GetType().Name);
            return false;
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
        if ((currentTrade.NormalizedTradeConditionFlags & NormalizedTradeConditionFlags.UndefinedPrice) != NormalizedTradeConditionFlags.None
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
            || (trade.NormalizedTradeConditionFlags & NormalizedTradeConditionFlags.UndefinedPrice) != NormalizedTradeConditionFlags.None
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
