using TomasAI.IFM.Framework.Messaging;
using TomasAI.IFM.Shared.Application;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.MarketDataAnalytics; // For FuturesTradeSignalId, FuturesRsiSignalType
using TomasAI.IFM.Shared.MarketDataAnalytics.Queries;
using TomasAI.IFM.Shared.MarketDataAnalytics.QueryParameters;
using TomasAI.IFM.Shared.MarketDataAnalytics.ServiceApi;
using TomasAI.IFM.Shared.MarketDataAnalytics.ViewModels;

namespace TomasAI.IFM.Application.Api.Nats.Client;

/// <summary>
/// REST API client for MarketDataAnalytics queries that delegates to an <see cref="IQueryServiceApi"/>.
/// Mirrors the pattern used by <see cref="MarketDataFeedQueryApi"/>.
/// </summary>
/// <param name="querySvc"></param>
public class MarketDataAnalyticsQueryApi(IActorProducer actorProducer)
    : NatsCommandApi(actorProducer), IMarketDataAnalyticsQueryApi
{
    /// <summary>
    /// Gets the futures trade signal for a contract and value date.
    /// </summary>
    public async Task<ServiceResult<FuturesTradeSignalV2ReadModel>> GetFuturesTradeSignalAsync(string contractId, DateOnly valueDate)
    {
        var entityId = new GetFuturesTradeSignalParameter(contractId, valueDate);
        GetFuturesTradeSignalQuery query = new(contractId, valueDate)
        {
            Subject = new ActorSubject(ActorType.Query, GetFuturesTradeSignalQuery.Actor, GetFuturesTradeSignalQuery.Verb, entityId.Format()),
        };
        return await RequestAsync<GetFuturesTradeSignalQuery, FuturesTradeSignalV2ReadModel>(query.Subject, query);
    }

    /// <summary>
    /// Gets the last futures trade signal.
    /// </summary>
    public async Task<ServiceResult<FuturesTradeSignalV2ReadModel>> GetLastFuturesTradeSignalAsync()
    {
        var entityId = new GetLastFuturesTradeSignalParameter();
        GetLastFuturesTradeSignalQuery query = new()
        {
            Subject = new ActorSubject(ActorType.Query, GetLastFuturesTradeSignalQuery.Actor, GetLastFuturesTradeSignalQuery.Verb, entityId.Format()),
        };
        return await RequestAsync<GetLastFuturesTradeSignalQuery, FuturesTradeSignalV2ReadModel>(query.Subject, query);
    }

    /// <summary>
    /// Gets the futures trade signal by symbol and value date.
    /// </summary>
    public async Task<ServiceResult<FuturesTradeSignalV2ReadModel>> GetFuturesTradeSignalBySymbolAsync(string symbol, DateOnly valueDate)
    {
        var entityId = new GetFuturesTradeSignalBySymbolParameter(symbol, valueDate);
        GetFuturesTradeSignalBySymbolQuery query = new(symbol, valueDate)
        {
            Subject = new ActorSubject(ActorType.Query, GetFuturesTradeSignalBySymbolQuery.Actor, GetFuturesTradeSignalBySymbolQuery.Verb, entityId.Format()),
        };
        return await RequestAsync<GetFuturesTradeSignalBySymbolQuery, FuturesTradeSignalV2ReadModel>(query.Subject, query);
    }

    /// <summary>
    /// Gets the futures trade signal IDs for a value date.
    /// </summary>
    public async Task<ServiceResult<FuturesTradeSignalId[]>> GetFuturesTradeSignalIdsAsync(DateOnly valueDate)
    {
        var entityId = new GetFuturesTradeSignalIdsParameter(valueDate);
        GetFuturesTradeSignalIdsQuery query = new(valueDate)
        {
            Subject = new ActorSubject(ActorType.Query, GetFuturesTradeSignalIdsQuery.Actor, GetFuturesTradeSignalIdsQuery.Verb, entityId.Format()),
        };
        return await RequestAsync<GetFuturesTradeSignalIdsQuery, FuturesTradeSignalId[]>(query.Subject, query);
    }

    /// <summary>
    /// Gets the futures RSI signal for a contract and value date (default signal type).
    /// </summary>
    public async Task<ServiceResult<FuturesRsiSignalReadModel>> GetFuturesRsiSignalAsync(string contractId, DateOnly valueDate, TradeTimePeriodType timePeriod, int periodLength)
    {
        var entityId = new GetFuturesRsiSignalParameter(contractId, valueDate, timePeriod, periodLength);
        GetFuturesRsiSignalQuery query = new(contractId, valueDate, timePeriod, periodLength)
        {
            Subject = new ActorSubject(ActorType.Query, GetFuturesRsiSignalQuery.Actor, GetFuturesRsiSignalQuery.Verb, entityId.Format()),
        };
        return await RequestAsync<GetFuturesRsiSignalQuery, FuturesRsiSignalReadModel>(query.Subject, query);
    }

   
    /// <summary>
    /// Gets the futures trend direction from RSI signal.
    /// </summary>
    public async Task<ServiceResult<FuturesTrendDirectionReadModel>> GetFuturesTrendDirectionFromRSISignalAsync(
        string contractId, DateOnly valueDate, DateTime timestamp, int loopbackInterval, DateTime startTime, DateTime endTime)
    {
        var entityId = new GetFuturesTrendDirectionFromRSISignalParameter(contractId, valueDate, timestamp, loopbackInterval, startTime, endTime);
        GetFuturesTrendDirectionFromRSISignalQuery query = new(contractId, valueDate, timestamp, loopbackInterval, startTime, endTime)
        {
            Subject = new ActorSubject(ActorType.Query, GetFuturesTrendDirectionFromRSISignalQuery.Actor, GetFuturesTrendDirectionFromRSISignalQuery.Verb, entityId.Format()),
        };
        return await RequestAsync<GetFuturesTrendDirectionFromRSISignalQuery, FuturesTrendDirectionReadModel>(query.Subject, query);
    }

    /// <summary>
    /// Gets the futures TDI signal for a contract and value date.
    /// </summary>
    public async Task<ServiceResult<FuturesTdiSignalReadModel>> GetFuturesTdiSignalAsync(string contractId, DateOnly valueDate)
    {
        var entityId = new GetFuturesTdiSignalParameter(contractId, valueDate);
        GetFuturesTdiSignalQuery query = new(contractId, valueDate)
        {
            Subject = new ActorSubject(ActorType.Query, GetFuturesTdiSignalQuery.Actor, GetFuturesTdiSignalQuery.Verb, entityId.Format()),
        };
        return await RequestAsync<GetFuturesTdiSignalQuery, FuturesTdiSignalReadModel>(query.Subject, query);
    }

    /// <summary>
    /// Gets the futures ITI signal for a contract and value date.
    /// </summary>
    public async Task<ServiceResult<FuturesItiSignalV2ReadModel>> GetFuturesItiSignalAsync(string contractId, DateOnly valueDate, TradeTimePeriodType timePeriod)
    {
        var entityId = new GetFuturesItiSignalParameter(contractId, valueDate, timePeriod);
        GetFuturesItiSignalQuery query = new(contractId, valueDate, timePeriod)
        {
            Subject = new ActorSubject(ActorType.Query, GetFuturesItiSignalQuery.Actor, GetFuturesItiSignalQuery.Verb, entityId.Format()),
        };
        return await RequestAsync<GetFuturesItiSignalQuery, FuturesItiSignalV2ReadModel>(query.Subject, query);
    }

    /// <summary>
    /// Gets the futures ITI trend direction changed signals for a contract and value date.
    /// </summary>
    public async Task<ServiceResult<FuturesItiSignalV2ReadModel[]>> GetFuturesItiTrendDirectionChangedSignalsAsync(string contractId, DateOnly valueDate, TradeTimePeriodType timePeriod)
    {
        var entityId = new GetFuturesItiTrendDirectionChangedSignalsParameter(contractId, valueDate, timePeriod);
        GetFuturesItiTrendDirectionChangedSignalsQuery query = new(contractId, valueDate, timePeriod)
        {
            Subject = new ActorSubject(ActorType.Query, GetFuturesItiTrendDirectionChangedSignalsQuery.Actor, GetFuturesItiTrendDirectionChangedSignalsQuery.Verb, entityId.Format()),
        };
        return await RequestAsync<GetFuturesItiTrendDirectionChangedSignalsQuery, FuturesItiSignalV2ReadModel[]>(query.Subject, query);
    }

    /// <summary>
    /// Gets the futures ITI signal data for a contract and value date.
    /// </summary>
    public async Task<ServiceResult<FuturesItiSignalDataReadModel>> GetFuturesItiSignalDataAsync(string contractId, DateOnly valueDate, TradeTimePeriodType timePeriod)
    {
        var entityId = new GetFuturesItiSignalDataParameter(contractId, valueDate, timePeriod);
        GetFuturesItiSignalDataQuery query = new(contractId, valueDate, timePeriod)
        {
            Subject = new ActorSubject(ActorType.Query, GetFuturesItiSignalDataQuery.Actor, GetFuturesItiSignalDataQuery.Verb, entityId.Format()),
        };
        return await RequestAsync<GetFuturesItiSignalDataQuery, FuturesItiSignalDataReadModel>(query.Subject, query);
    }

    /// <summary>
    /// Gets the futures ITI MDI distribution for a contract and value date.
    /// </summary>
    public async Task<ServiceResult<FuturesItiMDIDistributionReadModel>> GetFuturesItiMDIDistributionAsync(string contractId, DateOnly valueDate)
    {
        var entityId = new GetFuturesItiMDIDistributionParameter(contractId, valueDate);
        GetFuturesItiMDIDistributionQuery query = new(contractId, valueDate)
        {
            Subject = new ActorSubject(ActorType.Query, GetFuturesItiMDIDistributionQuery.Actor, GetFuturesItiMDIDistributionQuery.Verb, entityId.Format()),
        };
        return await RequestAsync<GetFuturesItiMDIDistributionQuery, FuturesItiMDIDistributionReadModel>(query.Subject, query);
    }

    /// <summary>
    /// Gets the futures ITI MDI distribution by trend for a contract and value date.
    /// </summary>
    public async Task<ServiceResult<FuturesItiMDIDistributionReadModel>> GetFuturesItiMDIDistributionByTrendAsync(string contractId, DateOnly valueDate)
    {
        var entityId = new GetFuturesItiMDIDistributionByTrendParameter(contractId, valueDate);
        GetFuturesItiMDIDistributionByTrendQuery query = new(contractId, valueDate)
        {
            Subject = new ActorSubject(ActorType.Query, GetFuturesItiMDIDistributionByTrendQuery.Actor, GetFuturesItiMDIDistributionByTrendQuery.Verb, entityId.Format()),
        };
        return await RequestAsync<GetFuturesItiMDIDistributionByTrendQuery, FuturesItiMDIDistributionReadModel>(query.Subject, query);
    }

    /// <summary>
    /// Gets the futures ITI signal MDI for a contract and value date.
    /// </summary>
    public async Task<ServiceResult<FuturesItiSignalMDIV2ReadModel[]>> GetFuturesItiSignalMDIAsync(string contractId, DateOnly valueDate)
    {
        var entityId = new GetFuturesItiSignalMDIParameter(contractId, valueDate);
        GetFuturesItiSignalMDIQuery query = new(contractId, valueDate)
        {
            Subject = new ActorSubject(ActorType.Query, GetFuturesItiSignalMDIQuery.Actor, GetFuturesItiSignalMDIQuery.Verb, entityId.Format()),
        };
        return await RequestAsync<GetFuturesItiSignalMDIQuery, FuturesItiSignalMDIV2ReadModel[]>(query.Subject, query);
    }

    /// <summary>
    /// Gets the futures ITI signal MDI by trend for a contract, value date, and group ID.
    /// </summary>
    public async Task<ServiceResult<FuturesItiSignalMDIV2ReadModel[]>> GetFuturesItiSignalMDIByTrendAsync(string contractId, DateOnly valueDate, int groupId)
    {
        var entityId = new GetFuturesItiSignalMDIByTrendParameter(contractId, valueDate, groupId);
        GetFuturesItiSignalMDIByTrendQuery query = new(contractId, valueDate, groupId)
        {
            Subject = new ActorSubject(ActorType.Query, GetFuturesItiSignalMDIByTrendQuery.Actor, GetFuturesItiSignalMDIByTrendQuery.Verb, entityId.Format()),
        };
        return await RequestAsync<GetFuturesItiSignalMDIByTrendQuery, FuturesItiSignalMDIV2ReadModel[]>(query.Subject, query);
    }

    /// <summary>
    /// Gets the futures ATR signal for a contract and value date.
    /// </summary>
    public async Task<ServiceResult<FuturesAtrSignalReadModel>> GetFuturesAtrSignalAsync(string contractId, DateOnly valueDate, TradeTimePeriodType timePeriod, int periodLength)
    {
        var entityId = new GetFuturesAtrSignalParameter(contractId, valueDate, timePeriod, periodLength);
        GetFuturesAtrSignalQuery query = new(contractId, valueDate, timePeriod, periodLength)
        {
            Subject = new ActorSubject(ActorType.Query, GetFuturesAtrSignalQuery.Actor, GetFuturesAtrSignalQuery.Verb, entityId.Format()),
        };
        return await RequestAsync<GetFuturesAtrSignalQuery, FuturesAtrSignalReadModel>(query.Subject, query);
    }

    /// <summary>
    /// Gets the futures ADX signal for a contract and value date.
    /// </summary>
    public async Task<ServiceResult<FuturesAdxSignalReadModel>> GetFuturesAdxSignalAsync(string contractId, DateOnly valueDate, TradeTimePeriodType timePeriod, int periodLength)
    {
        var entityId = new GetFuturesAdxSignalParameter(contractId, valueDate, timePeriod, periodLength);
        GetFuturesAdxSignalQuery query = new(contractId, valueDate, timePeriod, periodLength)
        {
            Subject = new ActorSubject(ActorType.Query, GetFuturesAdxSignalQuery.Actor, GetFuturesAdxSignalQuery.Verb, entityId.Format()),
        };
        return await RequestAsync<GetFuturesAdxSignalQuery, FuturesAdxSignalReadModel>(query.Subject, query);
    }

    /// <summary>
    /// Gets the futures MACD signal for a contract and value date.
    /// </summary>
    public async Task<ServiceResult<FuturesMacdSignalReadModel>> GetFuturesMacdSignalAsync(string contractId, DateOnly valueDate, TradeTimePeriodType timePeriod, int periodLength)
    {
        var entityId = new GetFuturesMacdSignalParameter(contractId, valueDate, timePeriod, periodLength);
        GetFuturesMacdSignalQuery query = new(contractId, valueDate, timePeriod, periodLength)
        {
            Subject = new ActorSubject(ActorType.Query, GetFuturesMacdSignalQuery.Actor, GetFuturesMacdSignalQuery.Verb, entityId.Format()),
        };
        return await RequestAsync<GetFuturesMacdSignalQuery, FuturesMacdSignalReadModel>(query.Subject, query);
    }
}
