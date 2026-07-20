using System;
using System.Collections.Generic;
using System.Text;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.MarketData;
using TomasAI.IFM.Shared.MarketData.Queries;
using TomasAI.IFM.Shared.MarketData.QueryParameters;
using TomasAI.IFM.Shared.MarketData.ViewModels;
using TomasAI.IFM.Shared.MarketDataAnalytics;
using TomasAI.IFM.Shared.MarketDataAnalytics.Commands;
using TomasAI.IFM.Shared.MarketDataAnalytics.Queries;
using TomasAI.IFM.Shared.MarketDataAnalytics.QueryParameters;
using TomasAI.IFM.Shared.MarketDataAnalytics.ViewModels;
using TomasAI.IFM.Shared.MarketDataFeed.Queries;
using TomasAI.IFM.Shared.MarketDataFeed.QueryParameters;
using TomasAI.IFM.Shared.MarketDataFeed.ViewModels;
using TomasAI.IFM.Shared.PredictiveModel.FuturesItiTrend;
using TomasAI.IFM.Shared.PredictiveModel.FuturesItiTrend.Queries;
using TomasAI.IFM.Shared.PredictiveModel.FuturesItiTrend.ViewModels;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesItiSignal.Event.Extensions;

public static class FuturesItiSignalEventExtensions
{
    /// <summary>
    /// Retrieves the most recent end-of-day (EOD) futures data for a specified contract and value date.
    /// </summary>
    /// <remarks>This method performs an asynchronous request and may return null if the operation is
    /// unsuccessful or if no data is available for the specified parameters.</remarks>
    /// <param name="contractId">The unique identifier of the futures contract for which EOD data is requested.</param>
    /// <param name="valueDate">The date for which the most recent end-of-day data is retrieved.</param>
    /// <returns>A task representing the asynchronous operation. The result contains the EOD data view model,
    /// or null if no data is found.</returns>
    public static async ValueTask<FuturesEodDataV2ReadModel?> GetFuturesEodDataAsync(this IEventActorContext context, string contractId, DateOnly valueDate)
    {
        var futuresEodData = default(FuturesEodDataV2ReadModel);
        var entityId = new GetLastFuturesEodDataParameter(contractId, valueDate);
        GetLastFuturesEodDataQuery query = new(contractId, valueDate)
        {
            Subject = new ActorSubject(ActorType.Query, GetLastFuturesEodDataQuery.Actor, GetLastFuturesEodDataQuery.Verb, entityId.Format()),
            EntityId = entityId,
            ErrorCode = GetLastFuturesEodDataQuery.ErrorId
        };
        var serviceResult = await context.RequestAsync<FuturesEodDataV2ReadModel, GetLastFuturesEodDataQuery>(query);
        if (serviceResult.Success && serviceResult.Value is not null)
            futuresEodData = serviceResult.Value;
        return futuresEodData;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="context"></param>
    /// <param name="contractId"></param>
    /// <param name="valueDate"></param>
    /// <param name="timePeriod"></param>
    /// <param name="periodLength"></param>
    /// <returns></returns>
    public static async ValueTask<FuturesRsiSignalReadModel?> GetFuturesRsiSignalAsync(this IEventActorContext context,  string contractId , DateOnly valueDate,TradeTimePeriodType timePeriod, int periodLength)
    {
        var rsiSignal = default(FuturesRsiSignalReadModel);
        var entityId = new FuturesRsiSignalEntityId(contractId, valueDate, timePeriod, periodLength);
        GetFuturesRsiSignalQuery query = new(contractId, valueDate,timePeriod, periodLength)
        {
            Subject = new ActorSubject(ActorType.Query, GetFuturesRsiSignalQuery.Actor, GetFuturesRsiSignalQuery.Verb, entityId.Format()),
            EntityId = entityId,
            ErrorCode = GetFuturesRsiSignalQuery.ErrorId
        };
        var serviceResult = await context.RequestAsync<FuturesRsiSignalReadModel, GetFuturesRsiSignalQuery>(query);
        if (serviceResult.Success && serviceResult.Value is not null)
            rsiSignal = serviceResult.Value;
        return rsiSignal;
    }

    /// <summary>
    /// Retrieves the TDI (Trend Direction / Divergence Index) signal for a specified futures contract and value date.
    /// </summary>
    /// <remarks>This method performs an asynchronous request and may return null if the operation is
    /// unsuccessful or if no data is available for the specified parameters.</remarks>
    /// <param name="contractId">The unique identifier of the futures contract for which the TDI signal is requested.</param>
    /// <param name="valueDate">The date for which the TDI signal is retrieved.</param>
    /// <returns>A task representing the asynchronous operation. The result contains the TDI signal view model,
    /// or null if no data is found.</returns>
    public static async ValueTask<FuturesTdiSignalReadModel?> GetFuturesTdiSignalAsync(this IEventActorContext context, string contractId, DateOnly valueDate)
    {
        var tdiSignal = default(FuturesTdiSignalReadModel);
        var entityId = new FuturesTdiSignalEntityId(contractId, valueDate, TradeTimePeriodType.Daily);
        GetFuturesTdiSignalQuery query = new(contractId, valueDate)
        {
            Subject = new ActorSubject(ActorType.Query, GetFuturesTdiSignalQuery.Actor, GetFuturesTdiSignalQuery.Verb, entityId.Format()),
            EntityId = entityId,
            ErrorCode = GetFuturesTdiSignalQuery.ErrorId
        };
        var serviceResult = await context.RequestAsync<FuturesTdiSignalReadModel, GetFuturesTdiSignalQuery>(query);
        if (serviceResult.Success && serviceResult.Value is not null)
            tdiSignal = serviceResult.Value;
        return tdiSignal;
    }

    /// <summary>
    /// Retrieves ITI (Intrinsic Time Indicator) signal data for a specified futures contract and value date.
    /// </summary>
    /// <remarks>This method performs an asynchronous request and may return null if the operation is
    /// unsuccessful or if no data is available for the specified parameters.</remarks>
    /// <param name="contractId">The unique identifier of the futures contract for which ITI signal data is requested.</param>
    /// <param name="valueDate">The date for which the ITI signal data is retrieved.</param>
    /// <param name="timePeriod">The time period classification for the ITI signal data, such as daily or weekly.</param>
    /// <returns>A task representing the asynchronous operation. The result contains the ITI signal data view model,
    /// or null if no data is found.</returns>
    public static async ValueTask<FuturesItiSignalDataReadModel?> GetFuturesItiSignalDataAsync(this IEventActorContext context, string contractId, DateOnly valueDate, TradeTimePeriodType timePeriod)
    {
        var itiSignalData = default(FuturesItiSignalDataReadModel);
        var entityId = new GetFuturesItiSignalDataParameter(contractId, valueDate, timePeriod);
        GetFuturesItiSignalDataQuery query = new(contractId, valueDate, timePeriod)
        {
            Subject = new ActorSubject(ActorType.Query, GetFuturesItiSignalDataQuery.Actor, GetFuturesItiSignalDataQuery.Verb, entityId.Format()),
            EntityId = entityId,
            ErrorCode = GetFuturesItiSignalDataQuery.ErrorId
        };
        var serviceResult = await context.RequestAsync<FuturesItiSignalDataReadModel, GetFuturesItiSignalDataQuery>(query);
        if (serviceResult.Success && serviceResult.Value is not null)
            itiSignalData = serviceResult.Value;
        return itiSignalData;
    }

    /// <summary>
    /// Retrieves the ITI (Intrinsic Time Indicator) signal for a specified futures contract and value date.
    /// </summary>
    /// <remarks>This method performs an asynchronous request and may return null if the operation is
    /// unsuccessful or if no data is available for the specified parameters.</remarks>
    /// <param name="context">The event actor context used to perform the request and access actor capabilities.</param>
    /// <param name="contractId">The unique identifier of the futures contract for which ITI signal is requested.</param>
    /// <param name="valueDate">The date for which the ITI signal is retrieved.</param>
    /// <returns>A task representing the asynchronous operation. The result contains the ITI signal view model,
    /// or null if no data is found.</returns>
    public static async ValueTask<FuturesItiSignalV2ReadModel?> GetFuturesItiSignalAsync(this IEventActorContext context, string contractId, DateOnly valueDate, TradeTimePeriodType timePeriod)
    {
        var itiSignal = default(FuturesItiSignalV2ReadModel);
        var entityId = new GetFuturesItiSignalParameter(contractId, valueDate, timePeriod);
        GetFuturesItiSignalQuery query = new(contractId, valueDate, timePeriod)
        {
            Subject = new ActorSubject(ActorType.Query, GetFuturesItiSignalQuery.Actor, GetFuturesItiSignalQuery.Verb, entityId.Format()),
            EntityId = entityId,
            ErrorCode = GetFuturesItiSignalQuery.ErrorId
        };
        var serviceResult = await context.RequestAsync<FuturesItiSignalV2ReadModel, GetFuturesItiSignalQuery>(query);
        if (serviceResult.Success && serviceResult.Value is not null)
            itiSignal = serviceResult.Value;
        return itiSignal;
    }

    /// <summary>
    /// Retrieves the ITI (Intrinsic Time Indicator) MDI signal data by trend for a specified futures contract, value date, and group.
    /// </summary>
    /// <remarks>This method performs an asynchronous request and may return null if the operation is
    /// unsuccessful or if no data is available for the specified parameters.</remarks>
    /// <param name="context">The event actor context used to perform the request and access actor capabilities.</param>
    /// <param name="contractId">The unique identifier of the futures contract for which ITI MDI by trend data is requested.</param>
    /// <param name="valueDate">The date for which the ITI MDI by trend data is retrieved.</param>
    /// <param name="groupId">The intrinsic time group identifier used to filter the MDI data by trend.</param>
    /// <returns>A task representing the asynchronous operation. The result contains the ITI MDI by trend view model array,
    /// or null if no data is found.</returns>
    public static async ValueTask<FuturesItiSignalMDIV2ReadModel[]?> GetFuturesItiSignalMDIByTrendAsync(this IEventActorContext context, string contractId, DateOnly valueDate, int groupId)
    {
        var mdiByTrend = default(FuturesItiSignalMDIV2ReadModel[]);
        var entityId = new GetFuturesItiSignalMDIByTrendParameter(contractId, valueDate, groupId);
        GetFuturesItiSignalMDIByTrendQuery query = new(contractId, valueDate, groupId)
        {
            Subject = new ActorSubject(ActorType.Query, GetFuturesItiSignalMDIByTrendQuery.Actor, GetFuturesItiSignalMDIByTrendQuery.Verb, entityId.Format()),
            EntityId = entityId,
            ErrorCode = GetFuturesItiSignalMDIByTrendQuery.ErrorId
        };
        var serviceResult = await context.RequestAsync<FuturesItiSignalMDIV2ReadModel[], GetFuturesItiSignalMDIByTrendQuery>(query);
        if (serviceResult.Success && serviceResult.Value is not null)
            mdiByTrend = serviceResult.Value;
        return mdiByTrend;
    }

    /// <summary>
    /// Retrieves the symbol for a specified futures contract.
    /// </summary>
    /// <remarks>This method performs an asynchronous request and may return null if the operation is
    /// unsuccessful or if no data is available for the specified parameters.</remarks>
    /// <param name="context">The event actor context used to perform the request and access actor capabilities.</param>
    /// <param name="contractId">The unique identifier of the futures contract for which the symbol is requested.</param>
    /// <returns>A task representing the asynchronous operation. The result contains the futures contract symbol,
    /// or null if no data is found.</returns>
    public static async ValueTask<string?> GetFuturesContractSymbolAsync(this IEventActorContext context, string contractId)
    {
        var symbol = default(string);
        var entityId = new GetFuturesContractSymbolParameter(contractId);
        GetFuturesContractSymbolQuery query = new(contractId)
        {
            Subject = new ActorSubject(ActorType.Query, GetFuturesContractSymbolQuery.Actor, GetFuturesContractSymbolQuery.Verb, entityId.Format()),
            EntityId = entityId,
            ErrorCode = GetFuturesContractSymbolQuery.ErrorId
        };
        var serviceResult = await context.RequestAsync<string, GetFuturesContractSymbolQuery>(query);
        if (serviceResult.Success && serviceResult.Value is not null)
            symbol = serviceResult.Value;
        return symbol;
    }

    /// <summary>
    /// Retrieves the predicted trend delta for the specified trend data.
    /// </summary>
    /// <remarks>This method performs an asynchronous request and may return null if the operation is
    /// unsuccessful or if no data is available for the specified parameters.</remarks>
    /// <param name="context">The event actor context used to perform the request and access actor capabilities.</param>
    /// <param name="trendData">The trend delta data used as input for the prediction.</param>
    /// <returns>A task representing the asynchronous operation. The result contains the predicted trend delta,
    /// or null if no data is found.</returns>
    public static async ValueTask<double> GetPredictedTrendDeltaAsync(this IEventActorContext context, FuturesItiSignalV2ReadModel e)
    {
        var symbol = new FuturesContractIdParser(e.ContractId).Symbol;
        var trendData = new FuturesItiTrendDeltaDataReadModel(
               symbol: symbol,
               valueDate: e.ValueDate,
               timestamp: e.IntrinsicTime,
               sequenceId: 0,
               trendDelta: 0,
               trendDirection: e.IntrinsicTimeTrend == IntrinsicTimeTrendType.UpTrend ? 1 : 0,
               trendDirectionMode: GetTrendDirectionMode(e.IntrinsicTimeMode),
               futuresPrice: Convert.ToSingle(e.IntrinsicPrice),
               trendExtreme: Convert.ToSingle(e.TrendExtreme),
               futuresRsi:0
           );
        double predictedTrendDelta = 0.0;
        var entityId = new FuturesItiTrendEntityId(trendData.Symbol, trendData.ValueDate);
        GetPredictedTrendDeltaQuery query = new(trendData)
        {
            Subject = new ActorSubject(ActorType.Query, GetPredictedTrendDeltaQuery.Actor, GetPredictedTrendDeltaQuery.Verb, entityId.Format()),
            EntityId = entityId,
            ErrorCode = GetPredictedTrendDeltaQuery.ErrorId
        };
        var serviceResult = await context.RequestAsync<ScalarValue<double>, GetPredictedTrendDeltaQuery>(query);
        if (serviceResult.Success && serviceResult.Value is not null)
            predictedTrendDelta = serviceResult.Value.AsDouble;
        return predictedTrendDelta;

        static int GetTrendDirectionMode(IntrinsicTimeModeType e)
               => e switch
               {
                   IntrinsicTimeModeType.TrendDirectionChanged => 0,
                   IntrinsicTimeModeType.TrendExtremeChanged => 1,
                   IntrinsicTimeModeType.TrendReversalChanged => -1,
                                   _ => 0
                };
     }

    /// <summary>
    /// Retrieves the futures ITI trend coastline counters for a specified contract, value date, symbol, and predicted trend delta.
    /// </summary>
    /// <remarks>This method performs an asynchronous request and may return null if the operation is
    /// unsuccessful or if no data is available for the specified parameters.</remarks>
    /// <param name="context">The event actor context used to perform the request and access actor capabilities.</param>
    /// <param name="contractId">The unique identifier of the futures contract for which coastline counters are requested.</param>
    /// <param name="valueDate">The date for which the coastline counters are retrieved.</param>
    /// <param name="symbol">The ticker symbol for the futures contract.</param>
    /// <param name="predictedTrendDelta">The predicted trend delta value used as input for coastline counter retrieval.</param>
    /// <returns>A task representing the asynchronous operation. The result contains the coastline counters view model,
    /// or null if no data is found.</returns>
    public static async ValueTask<FuturesItiTrendCoastLineCountersReadModel?> GetFuturesItiTrendCoastLineCountersAsync(this IEventActorContext context, string contractId, DateOnly valueDate, string symbol, double predictedTrendDelta)
    {
        var coastLineCounters = default(FuturesItiTrendCoastLineCountersReadModel);
        var entityId = new FuturesItiTrendEntityId(symbol, valueDate);
        GetFuturesItiTrendCoastLineCountersQuery query = new(contractId, valueDate, symbol, predictedTrendDelta)
        {
            Subject = new ActorSubject(ActorType.Query, GetFuturesItiTrendCoastLineCountersQuery.Actor, GetFuturesItiTrendCoastLineCountersQuery.Verb, entityId.Format()),
            EntityId = entityId,
            ErrorCode = GetFuturesItiTrendCoastLineCountersQuery.ErrorId
        };
        var serviceResult = await context.RequestAsync<FuturesItiTrendCoastLineCountersReadModel, GetFuturesItiTrendCoastLineCountersQuery>(query);
        if (serviceResult.Success && serviceResult.Value is not null)
            coastLineCounters = serviceResult.Value;
        return coastLineCounters;
    }

    /// <summary>
    /// Updates the futures trade signal for a specified contract and value date using end-of-day data and associated
    /// technical signals.
    /// </summary>
    /// <remarks>This method sends an update command to the actor system, incorporating multiple technical
    /// signals and price data. All optional signal parameters may be null if not applicable. The operation is
    /// asynchronous and will throw an exception if the update is unsuccessful.</remarks>
    /// <param name="context">The event actor context used to process the update request and access actor capabilities.</param>
    /// <param name="futuresEodData">The end-of-day data for the futures contract, including contract identifier and value date.</param>
    /// <param name="futuresRsiSignal">An optional relative strength index signal that may influence the trade signal update. May be null if not
    /// available.</param>
    /// <param name="futuresTdiSignal">An optional TDI signal that may influence the trade signal update. May be null if not available.</param>
    /// <param name="futuresItiSignalData">An optional ITI signal data that may influence the trade signal update. May be null if not available.</param>
    /// <param name="vixFuturesPrice">The current price of the VIX futures, used as an input for updating the trade signal.</param>
    /// <returns>A ValueTask representing the asynchronous operation of updating the futures trade signal.</returns>
    /// <exception cref="InvalidOperationException">Thrown if the update operation fails or returns an unsuccessful result.</exception>
    public static async ValueTask UpdateFuturesTradeSignalAsync(
        this IEventActorContext context,
        FuturesEodDataV2ReadModel futuresEodData,
        FuturesRsiSignalReadModel? futuresRsiSignal,
        FuturesTdiSignalReadModel? futuresTdiSignal,
        FuturesItiSignalDataReadModel? futuresItiSignalData,
        decimal vixFuturesPrice,
        TradeTimePeriodType timePeriod)
    {
        var entityId = new FuturesTradeSignalEntityId(futuresEodData.ContractId ?? string.Empty, futuresEodData.ValueDate, timePeriod);
        UpdateFuturesTradeSignalCommand cmd = new(futuresEodData, futuresRsiSignal, futuresTdiSignal, futuresItiSignalData, vixFuturesPrice)
        {
            Subject = new ActorSubject(ActorType.Command, UpdateFuturesTradeSignalCommand.Actor, UpdateFuturesTradeSignalCommand.Verb, entityId.Format()),
            EntityId = entityId,
            ErrorCode = UpdateFuturesTradeSignalCommand.ErrorId
        };
        var serviceResult = await context.RequestAsync<UpdateFuturesTradeSignalCommand, FuturesTradeSignalEntityId>(cmd);
        if (serviceResult?.Success != true)
            throw new InvalidOperationException(serviceResult?.ErrorMessage);
    }

    /// <summary>
    /// Asynchronously generates a futures ITI signal for the specified contract, date, and time period using the
    /// provided futures price and timestamp.
    /// </summary>
    /// <param name="context">The event actor context used to process the command and interact with the event system. Cannot be null.</param>
    /// <param name="contractId">The unique identifier of the futures contract for which the ITI signal is generated. Cannot be null or empty.</param>
    /// <param name="valueDate">The value date for which the ITI signal is generated.</param>
    /// <param name="timePeriod">The time period type that specifies the duration or granularity of the ITI signal.</param>
    /// <param name="timestamp">The timestamp indicating when the futures price was recorded.</param>
    /// <param name="futuresPrice">The price of the futures contract at the specified timestamp.</param>
    /// <returns>A ValueTask that represents the asynchronous operation of generating the futures ITI signal.</returns>
    /// <exception cref="InvalidOperationException">Thrown if the operation fails to generate the futures ITI signal, such as when the underlying service returns an
    /// error.</exception>
    public static async ValueTask GenerateFuturesItiSignalAsync(
        this IEventActorContext context,
        string contractId,
        DateOnly valueDate,
        TradeTimePeriodType timePeriod,
        DateTime timestamp,
        double futuresPrice,
        double vixFuturesPrice)
    {
        var entityId = new FuturesItiSignalEntityId(contractId, valueDate, timePeriod);
        GenerateFuturesItiSignalCommand cmd = new(contractId, valueDate, timePeriod, timestamp, futuresPrice, vixFuturesPrice)
        {
            Subject = new ActorSubject(ActorType.Command, GenerateFuturesItiSignalCommand.Actor, GenerateFuturesItiSignalCommand.Verb, entityId.Format()),
            EntityId = entityId
        };
        var serviceResult = await context.RequestAsync<GenerateFuturesItiSignalCommand, FuturesItiSignalEntityId>(cmd);
        if (serviceResult?.Success != true)
            throw new InvalidOperationException(serviceResult?.ErrorMessage);
    }

    /// <summary>
    /// Asynchronously retrieves the end-of-day closing price for VIX futures on the specified date.
    /// </summary>
    /// <remarks>If there are no currently traded VIX futures contracts, the method will return 0. Ensure that
    /// the provided date is valid to retrieve accurate data.</remarks>
    /// <param name="context">The context in which the method is executed, providing access to event actor functionalities.</param>
    /// <param name="valueDate">The date for which the end-of-day closing price is requested. Must be a valid date.</param>
    /// <returns>The closing price of the VIX futures for the specified date. Returns 0 if no data is available.</returns>
    public static async ValueTask<decimal> GetVixFuturesEodDataClosePriceAsync(this IEventActorContext context, DateOnly valueDate)
    {
        var vixFuturesEodData = default(VixFuturesEodDataReadModel);
        var futuresContracts = await context.GetCurrentlyTradedFuturesContractsAsync("VX");
        if (futuresContracts is not null)
        {
            var vixContractId = futuresContracts.FirstOrDefault(x => x.Symbol == "VX" && x.CurrentlyTraded)?.ContractId;
            if (vixContractId is not null)
                vixFuturesEodData = await context.GetLastVixFuturesEodDataAsync(vixContractId, valueDate);
        }
        return vixFuturesEodData?.ClosePrice ?? 0m;
    }

    /// <summary>
    /// Asynchronously retrieves the end-of-day data for VIX futures for a specified date.
    /// </summary>
    /// <remarks>This method obtains the currently traded VIX futures contracts and retrieves the end-of-day
    /// data for the contract with the symbol 'VX' on the specified date. If no suitable contract or data is found, the
    /// method returns null.</remarks>
    /// <param name="context">The event actor context used to access market data and event-related operations. Cannot be null.</param>
    /// <param name="valueDate">The date for which to retrieve the VIX futures end-of-day data.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains a VixFuturesEodDataReadModel with
    /// the end-of-day data for the specified date, or null if no data is available.</returns>
    public static async ValueTask<VixFuturesEodDataReadModel> GetVixFuturesEodDataAsync(this IEventActorContext context, DateOnly valueDate)
    {
        var vixFuturesEodData = default(VixFuturesEodDataReadModel);
        var futuresContracts = await context.GetCurrentlyTradedFuturesContractsAsync("VX");
        if (futuresContracts is not null)
        {
            var vixContractId = futuresContracts.FirstOrDefault(x => x.Symbol == "VX" && x.CurrentlyTraded)?.ContractId;
            if (vixContractId is not null)
                vixFuturesEodData = await context.GetLastVixFuturesEodDataAsync(vixContractId, valueDate);
        }
        return vixFuturesEodData!;
    }

    /// <summary>
    /// Retrieves the Futures ITI MDI distribution for a specified contract and value date.
    /// </summary>
    /// <remarks>This method first retrieves the ITI signal for the specified contract and date, and then uses
    /// it to fetch the MDI distribution. If the ITI signal is not found, the intrinsic time group ID defaults to
    /// zero.</remarks>
    /// <param name="context">The context in which the event is being processed, providing access to necessary services and data.</param>
    /// <param name="contractId">The unique identifier for the futures contract whose MDI distribution is being retrieved.</param>
    /// <param name="valueDate">The date for which the MDI distribution is requested, represented as a DateOnly value.</param>
    /// <returns>A FuturesItiMDIDistributionReadModel containing the MDI distribution data, or null if no data is found.</returns>
    public static async Task<FuturesItiMDIDistributionReadModel?> GetFuturesItiMDIDistributionAsync(this IEventActorContext context, string contractId, DateOnly valueDate, TradeTimePeriodType timePeriod)
    {
        var intrinsicTimeGroupId = 0;
        var itiSignal = await context.GetFuturesItiSignalAsync(contractId, valueDate, timePeriod);
        if (itiSignal is not null)
            intrinsicTimeGroupId = itiSignal.IntrinsicTimeGroupId;

        FuturesItiMDIDistributionReadModel mdiDistribution = default!;
        var mdiByTrend = await context.GetFuturesItiSignalMDIByTrendAsync(contractId, valueDate, intrinsicTimeGroupId);
        if (mdiByTrend is not null)
            mdiDistribution = new FuturesItiMDIDistributionReadModel(mdiByTrend);
        return mdiDistribution;
    }

    /// <summary>
    /// Asynchronously retrieves an array of currently traded futures contracts for the specified symbol.
    /// </summary>
    /// <remarks>This method performs an asynchronous request to obtain futures contracts. The result may be
    /// null if the request is unsuccessful or if no contracts are available for the given symbol.</remarks>
    /// <param name="context">The event actor context used to perform the request and access actor capabilities.</param>
    /// <param name="symbol">The symbol representing the futures contract for which currently traded contracts are requested. Cannot be null
    /// or empty.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains an array of
    /// FuturesContractV2ReadModel objects representing the currently traded futures contracts for the specified symbol,
    /// or null if no contracts are found.</returns>
    static async ValueTask<FuturesContractV2ReadModel[]?> GetCurrentlyTradedFuturesContractsAsync(this IEventActorContext context, string symbol)
    {
        var futuresContracts = default(FuturesContractV2ReadModel[]);
        var entityId = new GetCurrentlyTradedFuturesContractsParameter(symbol);
        GetCurrentlyTradedFuturesContractsQuery query = new(symbol)
        {
            Subject = new ActorSubject(ActorType.Query, GetCurrentlyTradedFuturesContractsQuery.Actor, GetCurrentlyTradedFuturesContractsQuery.Verb, entityId.Format()),
            EntityId = entityId,
            ErrorCode = GetCurrentlyTradedFuturesContractsQuery.ErrorId
        };
        var serviceResult = await context.RequestAsync<FuturesContractV2ReadModel[], GetCurrentlyTradedFuturesContractsQuery>(query);
        if (serviceResult.Success && serviceResult.Value is not null)
            futuresContracts = serviceResult.Value;
        return futuresContracts;
    }

    /// <summary>
    /// Asynchronously retrieves the most recent end-of-day data for VIX futures based on the specified contract ID and
    /// value date.
    /// </summary>
    /// <remarks>This method may return null if the data for the specified contract ID and value date does not
    /// exist. Ensure that the contractId and valueDate parameters are valid to avoid unexpected results.</remarks>
    /// <param name="context">The context in which the event is being processed, providing access to the event actor's capabilities.</param>
    /// <param name="contractId">The unique identifier for the VIX futures contract whose end-of-day data is being requested.</param>
    /// <param name="valueDate">The date for which the end-of-day data is being retrieved, specified as a DateOnly value.</param>
    /// <returns>A ValueTask containing a VixFuturesEodDataReadModel representing the end-of-day data for the specified contract
    /// and date, or null if no data is found.</returns>
    static async ValueTask<VixFuturesEodDataReadModel?> GetLastVixFuturesEodDataAsync(this IEventActorContext context, string contractId, DateOnly valueDate)
    {
        var vixFuturesEodData = default(VixFuturesEodDataReadModel);
        var entityId = new GetLastVixFuturesEodDataParameter(contractId, valueDate);
        GetLastVixFuturesEodDataQuery query = new(contractId, valueDate)
        {
            Subject = new ActorSubject(ActorType.Query, GetLastVixFuturesEodDataQuery.Actor, GetLastVixFuturesEodDataQuery.Verb, entityId.Format()),
            EntityId = entityId,
            ErrorCode = GetLastVixFuturesEodDataQuery.ErrorId
        };
        var serviceResult = await context.RequestAsync<VixFuturesEodDataReadModel, GetLastVixFuturesEodDataQuery>(query);
        if (serviceResult.Success && serviceResult.Value is not null)
            vixFuturesEodData = serviceResult.Value;
        return vixFuturesEodData;
    }



}
