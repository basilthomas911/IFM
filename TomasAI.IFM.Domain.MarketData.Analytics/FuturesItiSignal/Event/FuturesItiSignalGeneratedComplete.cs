using System.Buffers.Binary;
using System.Security.Cryptography;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Events;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.ServiceApi;
using TomasAI.IFM.Shared.StatusConsole;
using TomasAI.IFM.Shared.StatusConsole.ServiceApi;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesItiSignal.Event.Extensions;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesItiSignal.Event;

public static class FuturesItiSignalGeneratedComplete
{
    static FuturesItiSignalGeneratedComplete()
    {
        ServiceId = $"{LogSourceType.FuturesItiSignalEvent}";
    }
    static string ServiceId { get; } = default!;

    /// <summary>
    /// Handles the completion of the Futures ITI signal generation process. It retrieves necessary data, updates the trade signal, and logs any errors that occur during the process.
    /// </summary>
    /// <param name="e">The event instance containing details required for generating the futures trade signal, including the entity
    /// identifier.</param>
    /// <param name="context">The context in which the event is processed, supplying information necessary for asynchronous operations.</param>
    /// <param name="statusConsoleWriter">The writer used to output status messages to the console.</param>
    /// <param name="logger">The logger used to log error messages.</param>
    /// <returns>A value indicating whether the execution completed successfully. Returns <see langword="true"/> if the operation
    /// succeeded; otherwise, <see langword="false"/>.</returns>
    public static async ValueTask<bool> ExecuteAsync(
        this FuturesItiSignalGeneratedCompleteEvent e,
        IEventActorContext context,
        IActorMarketDataAnalyticsCommandApi commandApi,
        IStatusConsoleWriter statusConsoleWriter,
        ILogger logger)
    {
        var source = $"FuturesItiSignalGeneratedCompleteEvent for EntityId: {e.EntityId}";
        try
        {
            if (e.EntityId.TimePeriod == TimeFrameType.Daily && e.DeriveLongerPeriods)
                await e.GenerateLongerPeriodsAsync(commandApi).ConfigureAwait(false);

            var contractId = e.EntityId.ContractId;
            var valueDate = e.EntityId.ValueDate;
            var futuresEodDataTask = context.GetFuturesEodDataAsync(contractId, valueDate).AsTask();
            var futuresRsiSignalTask = context.GetFuturesRsiSignalAsync(contractId, valueDate, TimeFrameType.Daily, 14).AsTask();
            var futuresTdiSignalTask = context.GetFuturesTdiSignalAsync(
                contractId,
                valueDate,
                TimeFrameType.FifteenSeconds).AsTask();
            var futuresItiSignalDataTask = context.GetFuturesItiSignalDataAsync(contractId, valueDate, e.EntityId.TimePeriod).AsTask();
            var vixFuturesPriceTask = context.GetVixFuturesEodDataClosePriceAsync(valueDate).AsTask();
            await Task.WhenAll(
                futuresEodDataTask,
                futuresRsiSignalTask,
                futuresTdiSignalTask,
                futuresItiSignalDataTask,
                vixFuturesPriceTask);
            var futuresEodData = await futuresEodDataTask;
            var futuresRsiSignal = await futuresRsiSignalTask;
            var futuresTdiSignal = await futuresTdiSignalTask;
            var futuresItiSignalData = await futuresItiSignalDataTask;
            var vixFuturesPrice = await vixFuturesPriceTask;
            if (futuresEodData is null || futuresRsiSignal is null || futuresTdiSignal is null || futuresItiSignalData is null || vixFuturesPrice == 0)
                return false;
            await commandApi.UpdateFuturesTradeSignalAsync(futuresEodData!, futuresRsiSignal!, futuresTdiSignal!, futuresItiSignalData!, vixFuturesPrice, TimeFrameType.FifteenSeconds);
            return true;
        }
        catch (Exception ex)
        {
            await statusConsoleWriter.WriteConsoleAsync(LogSourceType.FuturesItiSignalEvent, FuturesItiSignalGeneratedCompleteEvent.ErrorCode, ex.GetErrorMessage());
            logger.LogErrorEvent(ServiceId, ex.GetErrorMessage(), "{Source}:  {ContractId} complete handler failed", source, e.EntityId.ContractId);
        }
        return false;
    }

    /// <summary>
    /// Derives the weekly and monthly durable commands exactly once from a daily
    /// completion. Stable command identifiers make a redelivered completion
    /// idempotent at the command boundary, while the period guard prevents
    /// weekly or monthly completions from recursively creating more commands.
    /// </summary>
    static async ValueTask GenerateLongerPeriodsAsync(
        this FuturesItiSignalGeneratedCompleteEvent e,
        IActorMarketDataAnalyticsCommandApi commandApi)
    {
        var signal = e.FuturesItiSignal
            ?? throw new InvalidOperationException(
                "A daily ITI completion requires its generated signal payload.");
        if (e.VixFuturesPrice <= 0)
        {
            throw new InvalidOperationException(
                "A daily ITI completion requires the source VIX futures price.");
        }

        foreach (var period in new[] { TimeFrameType.Weekly, TimeFrameType.Monthly })
        {
            _ = await commandApi.GenerateFuturesItiSignalAsync(
                e.EntityId.ContractId,
                e.EntityId.ValueDate,
                period,
                signal.IntrinsicTime,
                signal.IntrinsicPrice,
                e.VixFuturesPrice,
                CreateDerivedCommandId(e, period)).ConfigureAwait(false);
        }
    }

    /// <summary>Creates a stable command identifier for one source completion and target period.</summary>
    internal static Guid CreateDerivedCommandId(
        FuturesItiSignalGeneratedCompleteEvent e,
        TimeFrameType period)
    {
        Span<byte> input = stackalloc byte[20];
        var sourceId = e.Id != Guid.Empty ? e.Id : e.CommandId;
        if (sourceId == Guid.Empty)
            throw new InvalidOperationException("The ITI completion requires a stable event or command identifier.");

        sourceId.TryWriteBytes(input);
        BinaryPrimitives.WriteInt32LittleEndian(input[16..], (int)period);
        Span<byte> hash = stackalloc byte[32];
        SHA256.HashData(input, hash);
        return new Guid(hash[..16]);
    }
}
