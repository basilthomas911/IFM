using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.MarketData.ViewModels;
using TomasAI.IFM.Shared.MarketDataFeed.Queries;
using TomasAI.IFM.Shared.MarketDataFeed.QueryParameters;
using TomasAI.IFM.Shared.MarketDataFeed.ServiceApi;
using TomasAI.IFM.Shared.MarketDataFeed.ViewModels;
using TomasAI.IFM.Shared.Trade;

namespace TomasAI.IFM.Application.Api.Nats.Client;

public class MarketDataFeedQueryApi(IActorProducer actorProducer)
    : NatsCommandApi(actorProducer), IMarketDataFeedQueryApi
{
    /// <summary>
    /// Return last futures tick data for a contract on a specific value date.
    /// </summary>
    public async Task<ServiceResult<FuturesTickDataV2ReadModel>> GetLastFuturesTickDataAsync(string contractId, DateOnly valueDate)
    {
        var entityId = new GetLastFuturesTickDataParameter(contractId, valueDate);
        GetLastFuturesTickDataQuery query = new(contractId, valueDate)
        {
            Subject = new ActorSubject(ActorType.Query, GetLastFuturesTickDataQuery.Actor, GetLastFuturesTickDataQuery.Verb, entityId.Format()),
        };
        return await RequestAsync<GetLastFuturesTickDataQuery, FuturesTickDataV2ReadModel>(query.Subject, query);
    }

    /// <summary>
    /// Return last futures tick data for a contract by tick date/time.
    /// </summary>
    public async Task<ServiceResult<FuturesTickDataV2ReadModel>> GetLastFuturesTickDataAsync(string contractId, DateTime tickDate)
    {
        var entityId = new GetLastFuturesTickDataByTickDateParameter(contractId, tickDate);
        GetLastFuturesTickDataByTickDateQuery query = new(contractId, tickDate)
        {
            Subject = new ActorSubject(ActorType.Query, GetLastFuturesTickDataByTickDateQuery.Actor, GetLastFuturesTickDataByTickDateQuery.Verb, entityId.Format()),
        };
        return await RequestAsync<GetLastFuturesTickDataByTickDateQuery, FuturesTickDataV2ReadModel>(query.Subject, query);
    }

    /// <summary>
    /// Return last futures option tick data for a contract on a specific value date.
    /// </summary>
    public async Task<ServiceResult<FuturesOptionTickDataV2ReadModel>> GetLastFuturesOptionTickDataAsync(string contractId, DateOnly valueDate)
    {
        var entityId = new GetLastFuturesOptionTickDataParameter(contractId, valueDate);
        GetLastFuturesOptionTickDataQuery query = new(contractId, valueDate)
        {
            Subject = new ActorSubject(ActorType.Query, GetLastFuturesOptionTickDataQuery.Actor, GetLastFuturesOptionTickDataQuery.Verb, entityId.Format()),
        };
        return await RequestAsync<GetLastFuturesOptionTickDataQuery, FuturesOptionTickDataV2ReadModel>(query.Subject, query);
    }

    /// <summary>
    /// Return futures end-of-day data for the specified contract and value date.
    /// </summary>
    public async Task<ServiceResult<FuturesEodDataV2ReadModel>> GetFuturesEodDataAsync(string contractId, DateOnly valueDate)
    {
        var entityId = new GetFuturesEodDataParameter(contractId, valueDate);
        GetFuturesEodDataQuery query = new(contractId, valueDate)
        {
            Subject = new ActorSubject(ActorType.Query, GetFuturesEodDataQuery.Actor, GetFuturesEodDataQuery.Verb, entityId.Format()),
        };
        return await RequestAsync<GetFuturesEodDataQuery, FuturesEodDataV2ReadModel>(query.Subject, query);
    }

    /// <summary>
    /// Asynchronously retrieves the most recent end-of-day futures data for the specified contract and value date.
    /// </summary>
    public async Task<ServiceResult<FuturesEodDataV2ReadModel>> GetLastFuturesEodDataAsync(string contractId, DateOnly valueDate)
    {
        var entityId = new GetLastFuturesEodDataParameter(contractId, valueDate);
        GetLastFuturesEodDataQuery query = new(contractId, valueDate)
        {
            Subject = new ActorSubject(ActorType.Query, GetLastFuturesEodDataQuery.Actor, GetLastFuturesEodDataQuery.Verb, entityId.Format()),
        };
        return await RequestAsync<GetLastFuturesEodDataQuery, FuturesEodDataV2ReadModel>(query.Subject, query);
    }

    /// <summary>
    /// Return futures EOD data in a date range for the specified contract.
    /// </summary>
    public async Task<ServiceResult<FuturesEodDataV2ReadModel[]>> GetFuturesEodDataAsync(string contractId, DateOnly startDate, DateOnly endDate)
    {
        var entityId = new GetFuturesEodDataByDateRangeParameter(contractId, startDate, endDate);
        GetFuturesEodDataByDateRangeQuery query = new(contractId, startDate, endDate)
        {
            Subject = new ActorSubject(ActorType.Query, GetFuturesEodDataByDateRangeQuery.Actor, GetFuturesEodDataByDateRangeQuery.Verb, entityId.Format()),
        };
        return await RequestAsync<GetFuturesEodDataByDateRangeQuery, FuturesEodDataV2ReadModel[]>(query.Subject, query);
    }

    /// <summary>
    /// Return futures bar data for a contract/symbol between start and end times on a value date.
    /// </summary>
    public async Task<ServiceResult<FuturesBarDataReadModel[]>> GetFuturesBarDataAsync(string contractId, string symbol, DateOnly valueDate, DateTime startDate, DateTime endDate)
    {
        var entityId = new GetFuturesBarDataParameter(contractId, symbol, valueDate, startDate, endDate);
        GetFuturesBarDataQuery query = new(contractId, symbol, valueDate, startDate, endDate)
        {
            Subject = new ActorSubject(ActorType.Query, GetFuturesBarDataQuery.Actor, GetFuturesBarDataQuery.Verb, entityId.Format()),
        };
        return await RequestAsync<GetFuturesBarDataQuery, FuturesBarDataReadModel[]>(query.Subject, query);
    }

    /// <summary>
    /// Return the last futures bar data available.
    /// </summary>
    public async Task<ServiceResult<FuturesBarDataReadModel>> GetLastFuturesBarDataAsync(string contractId, string symbol, DateOnly valueDate)
    {
        var entityId = new GetLastFuturesBarDataParameter(contractId, symbol, valueDate);
        GetLastFuturesBarDataQuery query = new(contractId, symbol, valueDate)
        {
            Subject = new ActorSubject(ActorType.Query, GetLastFuturesBarDataQuery.Actor, GetLastFuturesBarDataQuery.Verb, entityId.Format()),
        };
        return await RequestAsync<GetLastFuturesBarDataQuery, FuturesBarDataReadModel>(query.Subject, query);
    }

    /// <summary>
    /// Return iron condor market data feed composed from the supplied option and underlying contract ids.
    /// </summary>
    public async Task<ServiceResult<IronCondorMarketDataFeedReadModel>> GetIronCondorMarketDataFeedAsync(
           string underlyingContractId,
           string shortPutOptionContractId,
           string longPutOptionContractId,
           string shortCallOptionContractId,
           string longCallOptionContractId,
           DateOnly valueDate)
    {
        var entityId = new GetIronCondorMarketDataFeedParameter(underlyingContractId, shortPutOptionContractId, longPutOptionContractId, shortCallOptionContractId, longCallOptionContractId, valueDate);
        GetIronCondorMarketDataFeedQuery query = new(underlyingContractId, shortPutOptionContractId, longPutOptionContractId, shortCallOptionContractId, longCallOptionContractId, valueDate)
        {
            Subject = new ActorSubject(ActorType.Query, GetIronCondorMarketDataFeedQuery.Actor, GetIronCondorMarketDataFeedQuery.Verb, entityId.Format()),
        };
        return await RequestAsync<GetIronCondorMarketDataFeedQuery, IronCondorMarketDataFeedReadModel>(query.Subject, query);
    }

    /// <summary>
    /// Return futures EOD data parameters (today + range + normal curve) for the specified contract/date.
    /// </summary>
    public async Task<ServiceResult<FuturesEodDataParametersReadModel>> GetFuturesEodDataParametersAsync(string contractId, DateOnly valueDate)
    {
        var entityId = new GetFuturesEodDataParametersParameter(contractId, valueDate);
        GetFuturesEodDataParametersQuery query = new(contractId, valueDate)
        {
            Subject = new ActorSubject(ActorType.Query, GetFuturesEodDataParametersQuery.Actor, GetFuturesEodDataParametersQuery.Verb, entityId.Format()),
        };
        return await RequestAsync<GetFuturesEodDataParametersQuery, FuturesEodDataParametersReadModel>(query.Subject, query);
    }

    /// <summary>
    /// Return a single futures option contract (POST because a query-for model is supplied).
    /// </summary>
    public async Task<ServiceResult<FuturesOptionContractReadModel>> GetFuturesOptionContractAsync(string contractId, FuturesOptionContractReadModel queryForContract)
    {
        var entityId = new GetFuturesOptionContractParameter(contractId, queryForContract);
        GetFuturesOptionContractQuery query = new(contractId, queryForContract)
        {
            Subject = new ActorSubject(ActorType.Query, GetFuturesOptionContractQuery.Actor, GetFuturesOptionContractQuery.Verb, entityId.Format()),
        };
        return await RequestAsync<GetFuturesOptionContractQuery, FuturesOptionContractReadModel>(query.Subject, query);
    }

    /// <summary>
    /// Return spread data for a short/long option pair (POST since complex model is supplied).
    /// </summary>
    public async Task<ServiceResult<FuturesOptionSpreadDataReadModel>> GetFuturesOptionSpreadDataAsync(DateOnly valueDate, DateOnly maturityDate, double assetPrice, double riskFreeRate, double timeValue, FuturesOptionContractReadModel qfShortOptionContract, FuturesOptionContractReadModel qfLongOptionContract)
    {
        var queryForOptionContracts = new FuturesOptionContractsReadModel([qfShortOptionContract, qfLongOptionContract ]);
        var entityId = new GetFuturesOptionSpreadDataParameter(valueDate, maturityDate, assetPrice, riskFreeRate, timeValue, queryForOptionContracts);
        GetFuturesOptionSpreadDataQuery query = new(valueDate, maturityDate, assetPrice, riskFreeRate, timeValue, queryForOptionContracts)
        {
            Subject = new ActorSubject(ActorType.Query, GetFuturesOptionSpreadDataQuery.Actor, GetFuturesOptionSpreadDataQuery.Verb, entityId.Format()),
        };
        return await RequestAsync<GetFuturesOptionSpreadDataQuery, FuturesOptionSpreadDataReadModel>(query.Subject, query);
    }

    /// <summary>
    /// Return normal curve table used for option probability computations.
    /// </summary>
    public async Task<ServiceResult<NormalCurveTableReadModel>> GetNormalCurveTableAsync()
    {
        var entityId = new GetNormalCurveTableParameter();
        GetNormalCurveTableQuery query = new()
        {
            Subject = new ActorSubject(ActorType.Query, GetNormalCurveTableQuery.Actor, GetNormalCurveTableQuery.Verb, entityId.Format()),
        };
        return await RequestAsync<GetNormalCurveTableQuery, NormalCurveTableReadModel>(query.Subject, query);
    }

    /// <summary>
    /// Return VIX futures EOD history for a contract on a specified value date.
    /// </summary>
    public async Task<ServiceResult<VixFuturesEodDataReadModel[]>> GetVixFuturesEodDataAsync(string contractId, DateOnly valueDate)
    {
        var entityId = new GetVixFuturesEodDataParameter(contractId, valueDate);
        GetVixFuturesEodDataQuery query = new(contractId, valueDate)
        {
            Subject = new ActorSubject(ActorType.Query, GetVixFuturesEodDataQuery.Actor, GetVixFuturesEodDataQuery.Verb, entityId.Format()),
        };
        return await RequestAsync<GetVixFuturesEodDataQuery, VixFuturesEodDataReadModel[]>(query.Subject, query);
    }

    /// <summary>
    /// Return most recent VIX futures EOD for a contract on the specified value date.
    /// </summary>
    public async Task<ServiceResult<VixFuturesEodDataReadModel>> GetLastVixFuturesEodDataAsync(string contractId, DateOnly valueDate)
    {
        var entityId = new GetLastVixFuturesEodDataParameter(contractId, valueDate);
        GetLastVixFuturesEodDataQuery query = new(contractId, valueDate)
        {
            Subject = new ActorSubject(ActorType.Query, GetLastVixFuturesEodDataQuery.Actor, GetLastVixFuturesEodDataQuery.Verb, entityId.Format()),
        };
        return await RequestAsync<GetLastVixFuturesEodDataQuery, VixFuturesEodDataReadModel>(query.Subject, query);
    }

    /// <summary>
    /// Return the futures risk position type for the provided value date and trade type.
    /// </summary>
    public async Task<ServiceResult<RiskPositionTypeReadModel>> GetFuturesRiskPositionTypeAsync(DateOnly valueDate, TradeType tradeType)
    {
        var entityId = new GetFuturesRiskPositionTypeParameter(valueDate, tradeType);
        GetFuturesRiskPositionTypeQuery query = new(valueDate, tradeType)
        {
            Subject = new ActorSubject(ActorType.Query, GetFuturesRiskPositionTypeQuery.Actor, GetFuturesRiskPositionTypeQuery.Verb, entityId.Format()),
        };
        return await RequestAsync<GetFuturesRiskPositionTypeQuery, RiskPositionTypeReadModel>(query.Subject, query);
    }

    /// <summary>
    /// Return futures EOD moving averages for a contract/symbol on a value date.
    /// </summary>
    public async Task<ServiceResult<FuturesEodDataMovingAveragesReadModel>> GetFuturesEodMovingAveragesAsync(string contractId, string symbol, DateOnly valueDate)
    {
        var entityId = new GetFuturesEodMovingAveragesParameter(contractId, symbol, valueDate);
        GetFuturesEodDataMovingAveragesQuery query = new(contractId, symbol, valueDate)
        {
            Subject = new ActorSubject(ActorType.Query, GetFuturesEodDataMovingAveragesQuery.Actor, GetFuturesEodDataMovingAveragesQuery    .Verb, entityId.Format()),
        };
        return await RequestAsync<GetFuturesEodDataMovingAveragesQuery, FuturesEodDataMovingAveragesReadModel>(query.Subject, query);
    }

    /// <summary>
    /// Return a streaming request id for starting live feeds.
    /// </summary>
    public async Task<ServiceResult<ScalarValue<int>>> GetStreamingRequestIdAsync()
    {
        var entityId = new GetStreamingRequestIdParameter();
        GetStreamingRequestIdQuery query = new()
        {
            Subject = new ActorSubject(ActorType.Query, GetStreamingRequestIdQuery.Actor, GetStreamingRequestIdQuery.Verb, entityId.Format()),
        };
        return await RequestAsync<GetStreamingRequestIdQuery, ScalarValue<int>>(query.Subject, query);
    }

    /// <summary>
    /// Return an option quote id used when streaming option quotes.
    /// </summary>
    public async Task<ServiceResult<ScalarValue<int>>> GetOptionQuoteIdAsync()
    {
        var entityId = new GetOptionQuoteIdParameter();
        GetOptionQuoteIdQuery query = new()
        {
            Subject = new ActorSubject(ActorType.Query, GetOptionQuoteIdQuery.Actor, GetOptionQuoteIdQuery.Verb, entityId.Format()),
        };
        return await RequestAsync<GetOptionQuoteIdQuery, ScalarValue<int>>(query.Subject, query);
    }
}

