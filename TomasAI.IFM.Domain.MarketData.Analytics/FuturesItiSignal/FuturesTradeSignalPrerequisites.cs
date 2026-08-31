using TomasAI.IFM.Domain.MarketData.Analytics.FuturesItiSignal.Event.Extensions;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Events;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.ViewModels;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesItiSignal;

/// <summary>
/// Resolves the canonical inputs for the temporary Market Outlook trade signal.
/// Keeping this in one place prevents the durable and realtime completion paths
/// from drifting to indicator identities that are not activated.
/// </summary>
internal static class FuturesTradeSignalPrerequisites
{
    internal const TimeFrameType TriggerTimePeriod = TimeFrameType.Daily;
    internal const TimeFrameType SignalTimePeriod = TimeFrameType.FifteenSeconds;

    internal static bool ShouldGenerate(FuturesItiSignalGeneratedCompleteEvent source)
        => source.EntityId.TimePeriod == TriggerTimePeriod;

    internal static async ValueTask<FuturesTradeSignalPrerequisiteResult> LoadAsync<TActor>(
        FuturesItiSignalGeneratedCompleteEvent source,
        IEventActorContext<TActor> context)
        where TActor : IActor
    {
        var contractId = source.EntityId.ContractId;
        var valueDate = source.FuturesItiSignal?.ValueDate ?? source.EntityId.ValueDate;

        var futuresEodDataTask = context.GetFuturesEodDataAsync(contractId, valueDate).AsTask();
        var futuresRsiSignalTask = context.GetFuturesRsiSignalAsync(
            contractId,
            valueDate,
            SignalTimePeriod,
            FuturesIntradaySignalActivationProfile.RsiPeriodLength).AsTask();
        var futuresTdiSignalTask = context.GetFuturesTdiSignalAsync(
            contractId,
            valueDate,
            SignalTimePeriod).AsTask();
        var futuresItiSignalDataTask = context.GetFuturesItiSignalDataAsync(
            contractId,
            valueDate,
            TriggerTimePeriod).AsTask();
        var vixFuturesPriceTask = source.VixFuturesPrice > 0
            ? Task.FromResult(Convert.ToDecimal(source.VixFuturesPrice))
            : context.GetVixFuturesEodDataClosePriceAsync(valueDate).AsTask();

        await Task.WhenAll(
            futuresEodDataTask,
            futuresRsiSignalTask,
            futuresTdiSignalTask,
            futuresItiSignalDataTask,
            vixFuturesPriceTask).ConfigureAwait(false);

        var futuresEodData = await futuresEodDataTask.ConfigureAwait(false);
        var futuresRsiSignal = await futuresRsiSignalTask.ConfigureAwait(false);
        var futuresTdiSignal = await futuresTdiSignalTask.ConfigureAwait(false);
        var futuresItiSignalData = await futuresItiSignalDataTask.ConfigureAwait(false);
        var vixFuturesPrice = await vixFuturesPriceTask.ConfigureAwait(false);

        List<string> missing = [];
        if (futuresEodData is null)
            missing.Add("EOD");
        if (futuresRsiSignal is null)
            missing.Add($"RSI {SignalTimePeriod}/{FuturesIntradaySignalActivationProfile.RsiPeriodLength}");
        if (futuresTdiSignal is null)
            missing.Add($"TDI {SignalTimePeriod}/{FuturesTdiConfiguration.StandardConfigurationId}");
        if (futuresItiSignalData is null)
            missing.Add($"ITI {TriggerTimePeriod}");
        if (vixFuturesPrice <= 0)
            missing.Add("VX price");

        // EOD is the calculation base. All analytics enrichments are independent optional
        // components and therefore use OR admission rather than an all-or-nothing barrier.
        if (futuresEodData is null)
            return new FuturesTradeSignalPrerequisiteResult(null, string.Join(", ", missing));

        return new FuturesTradeSignalPrerequisiteResult(
            new FuturesTradeSignalInputs(
                futuresEodData,
                futuresRsiSignal,
                futuresTdiSignal,
                futuresItiSignalData,
                vixFuturesPrice),
            missing.Count == 0 ? null : string.Join(", ", missing));
    }
}

internal sealed record FuturesTradeSignalPrerequisiteResult(
    FuturesTradeSignalInputs? Inputs,
    string? MissingInputs);

internal sealed record FuturesTradeSignalInputs(
    FuturesEodDataV2ReadModel FuturesEodData,
    FuturesRsiSignalReadModel? FuturesRsiSignal,
    FuturesTdiSignalReadModel? FuturesTdiSignal,
    FuturesItiSignalDataReadModel? FuturesItiSignalData,
    decimal VixFuturesPrice);
