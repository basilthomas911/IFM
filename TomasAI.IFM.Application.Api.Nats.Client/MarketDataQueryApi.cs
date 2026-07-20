using TomasAI.IFM.Shared.Application;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.MarketData;
using TomasAI.IFM.Shared.MarketData.Queries;
using TomasAI.IFM.Shared.MarketData.QueryParameters;
using TomasAI.IFM.Shared.MarketData.ServiceApi;
using TomasAI.IFM.Shared.MarketData.ViewModels;

namespace TomasAI.IFM.Application.Api.Nats.Client;

public class MarketDataQueryApi(IActorProducer actorProducer)
    : NatsCommandApi(actorProducer), IMarketDataQueryApi
{
    /// <inheritdoc />
    public async Task<ServiceResult<FuturesContractV2ReadModel>> GetCurrentlyTradedFuturesContractAsync(string symbol)
    {
        var entityId = new GetCurrentlyTradedFuturesContractParameter(symbol);
        GetCurrentlyTradedFuturesContractQuery query = new(symbol)
        {
            Subject = new ActorSubject(ActorType.Query, GetCurrentlyTradedFuturesContractQuery.Actor, GetCurrentlyTradedFuturesContractQuery.Verb, entityId.Format()),
        };
        return await RequestAsync<GetCurrentlyTradedFuturesContractQuery, FuturesContractV2ReadModel>(query.Subject, query);
    }

    /// <inheritdoc />
    public async Task<ServiceResult<FuturesContractV2ReadModel[]>> GetCurrentlyTradedFuturesContractsAsync(string symbol)
    {
        var entityId = new GetCurrentlyTradedFuturesContractsParameter(symbol);
        GetCurrentlyTradedFuturesContractsQuery query = new(symbol)
        {
            Subject = new ActorSubject(ActorType.Query, GetCurrentlyTradedFuturesContractsQuery.Actor, GetCurrentlyTradedFuturesContractsQuery.Verb, entityId.Format()),
        };
        return await RequestAsync<GetCurrentlyTradedFuturesContractsQuery, FuturesContractV2ReadModel[]>(query.Subject, query);
    }

    /// <inheritdoc />
    public async Task<ServiceResult<FuturesContractV2ReadModel>> GetFuturesContractAsync(string contractId)
    {
        var entityId = new GetFuturesContractParameter(contractId);
        GetFuturesContractQuery query = new(contractId)
        {
            Subject = new ActorSubject(ActorType.Query, GetFuturesContractQuery.Actor, GetFuturesContractQuery.Verb, entityId.Format()),
        };
        return await RequestAsync<GetFuturesContractQuery, FuturesContractV2ReadModel>(query.Subject, query);
    }

    /// <inheritdoc />
    public async Task<ServiceResult<string>> GetFuturesContractSymbolAsync(string contractId)
    {
        var entityId = new GetFuturesContractSymbolParameter(contractId);
        GetFuturesContractSymbolQuery query = new(contractId)
        {
            Subject = new ActorSubject(ActorType.Query, GetFuturesContractSymbolQuery.Actor, GetFuturesContractSymbolQuery.Verb, entityId.Format()),
        };
        return await RequestAsync<GetFuturesContractSymbolQuery, string>(query.Subject, query);
    }

    /// <inheritdoc />
    public async Task<ServiceResult<FuturesOptionContractReadModel>> GetFuturesOptionContractAsync(string contractId)
    {
        var entityId = new GetFuturesOptionContractParameter(contractId);
        GetFuturesOptionContractQuery query = new(contractId)
        {
            Subject = new ActorSubject(ActorType.Query, GetFuturesOptionContractQuery.Actor, GetFuturesOptionContractQuery.Verb, entityId.Format()),
        };
        return await RequestAsync<GetFuturesOptionContractQuery, FuturesOptionContractReadModel>(query.Subject, query);
    }

    /// <inheritdoc />
    public async Task<ServiceResult<FuturesContractV2ReadModel[]>> GetFuturesContractsAsync()
    {
        var entityId = new GetFuturesContractsParameter();
        GetFuturesContractsQuery query = new()
        {
            Subject = new ActorSubject(ActorType.Query, GetFuturesContractsQuery.Actor, GetFuturesContractsQuery.Verb, entityId.Format()),
        };
        return await RequestAsync<GetFuturesContractsQuery, FuturesContractV2ReadModel[]>(query.Subject, query);
    }

    /// <inheritdoc />
    public async Task<ServiceResult<FuturesOptionContractReadModel[]>> GetFuturesOptionContractsAsync(string symbol)
    {
        var entityId = new GetFuturesOptionContractsParameter(symbol);
        GetFuturesOptionContractsQuery query = new(symbol)
        {
            Subject = new ActorSubject(ActorType.Query, GetFuturesOptionContractsQuery.Actor, GetFuturesOptionContractsQuery.Verb, entityId.Format()),
        };
        return await RequestAsync<GetFuturesOptionContractsQuery, FuturesOptionContractReadModel[]>(query.Subject, query);
    }

    /// <inheritdoc />
    public async Task<ServiceResult<string[]>> GetFuturesOptionContractIdsAsync(string[] contractIds)
    {
        var entityId = new GetFuturesOptionContractIdsParameter(contractIds);
        GetFuturesOptionContractIdsQuery query = new(contractIds)
        {
            Subject = new ActorSubject(ActorType.Query, GetFuturesOptionContractIdsQuery.Actor, GetFuturesOptionContractIdsQuery.Verb, entityId.Format()),
        };
        return await RequestAsync<GetFuturesOptionContractIdsQuery, string[]>(query.Subject, query);
    }

    /// <inheritdoc />
    public async Task<ServiceResult<YieldCurveRateReadModel>> GetLastYieldCurveRateAsync()
    {
        var entityId = new GetLastYieldCurveRateParameter();
        GetLastYieldCurveRateQuery query = new()
        {
            Subject = new ActorSubject(ActorType.Query, GetLastYieldCurveRateQuery.Actor, GetLastYieldCurveRateQuery.Verb, entityId.Format()),
        };
        return await RequestAsync<GetLastYieldCurveRateQuery, YieldCurveRateReadModel>(query.Subject, query);
    }

    /// <inheritdoc />
    public async Task<ServiceResult<RateOfReturnReadModel>> GetLastRateOfReturnAsync(string symbol, DateOnly valueDate)
    {
        var entityId = new GetLastRateOfReturnParameter(symbol, valueDate);
        GetLastRateOfReturnQuery query = new(symbol, valueDate)
        {
            Subject = new ActorSubject(ActorType.Query, GetLastRateOfReturnQuery.Actor, GetLastRateOfReturnQuery.Verb, entityId.Format()),
        };
        return await RequestAsync<GetLastRateOfReturnQuery, RateOfReturnReadModel>(query.Subject, query);
    }

    /// <inheritdoc />
    public async Task<ServiceResult<ScalarReadModel<int>>> GetTradingDaysAsync(DateOnly startDate, DateOnly endDate, MarketType marketType, CurrencyType currencyType)
    {
        var entityId = new GetTradingDaysParameter(startDate, endDate, marketType, currencyType);
        GetTradingDaysQuery query = new(startDate, endDate, marketType, currencyType)
        {
            Subject = new ActorSubject(ActorType.Query, GetTradingDaysQuery.Actor, GetTradingDaysQuery.Verb, entityId.Format()),
        };
        return await RequestAsync<GetTradingDaysQuery, ScalarReadModel<int>>(query.Subject, query);
    }

    /// <inheritdoc />
    public async Task<ServiceResult<DateOnly[]>> GetTradingDatesAsync(DateOnly startDate, DateOnly endDate, MarketType marketType, CurrencyType currencyType)
    {
        var entityId = new GetTradingDatesParameter(startDate, endDate, marketType, currencyType);
        GetTradingDatesQuery query = new(startDate, endDate, marketType, currencyType)
        {
            Subject = new ActorSubject(ActorType.Query, GetTradingDatesQuery.Actor, GetTradingDatesQuery.Verb, entityId.Format()),
        };
        return await RequestAsync<GetTradingDatesQuery, DateOnly[]>(query.Subject, query);
    }

    /// <inheritdoc />
    public async Task<ServiceResult<YieldCurveRateReadModel[]>> GetYieldCurveRatesAsync(DateOnly startDate, DateOnly endDate)
    {
        var entityId = new GetYieldCurveRatesParameter(startDate, endDate);
        GetYieldCurveRatesQuery query = new(startDate, endDate)
        {
            Subject = new ActorSubject(ActorType.Query, GetYieldCurveRatesQuery.Actor, GetYieldCurveRatesQuery.Verb, entityId.Format()),
        };
        return await RequestAsync<GetYieldCurveRatesQuery, YieldCurveRateReadModel[]>(query.Subject, query);
    }

    /// <inheritdoc />
    public async Task<ServiceResult<YieldCurveRateReadModel[]>> GetExternalYieldCurveRatesAsync()
    {
        var entityId = new GetExternalYieldCurveRatesParameter();
        GetExternalYieldCurveRatesQuery query = new()
        {
            Subject = new ActorSubject(ActorType.Query, GetExternalYieldCurveRatesQuery.Actor, GetExternalYieldCurveRatesQuery.Verb, entityId.Format()),
        };
        return await RequestAsync<GetExternalYieldCurveRatesQuery, YieldCurveRateReadModel[]>(query.Subject, query);
    }

    /// <inheritdoc />
    public async Task<ServiceResult<YieldCurveRateYearsReadModel>> GetYieldCurveRateYearsAsync()
    {
        var entityId = new GetYieldCurveRateYearsParameter();
        GetYieldCurveRateYearsQuery query = new()
        {
            Subject = new ActorSubject(ActorType.Query, GetYieldCurveRateYearsQuery.Actor, GetYieldCurveRateYearsQuery.Verb, entityId.Format()),
        };
        return await RequestAsync<GetYieldCurveRateYearsQuery, YieldCurveRateYearsReadModel>(query.Subject, query);
    }

    /// <inheritdoc />
    public async Task<ServiceResult<ScalarReadModel<bool>>> YieldCurveRateExistsAsync(DateOnly valueDate)
    {
        var entityId = new GetYieldCurveRateExistsParameter(valueDate);
        GetYieldCurveRateExistsQuery query = new(valueDate)
        {
            Subject = new ActorSubject(ActorType.Query, GetYieldCurveRateExistsQuery.Actor, GetYieldCurveRateExistsQuery.Verb, entityId.Format()),
        };
        return await RequestAsync<GetYieldCurveRateExistsQuery, ScalarReadModel<bool>>(query.Subject, query);
    }

    /// <inheritdoc />
    public async Task<ServiceResult<ScalarReadModel<DateOnly>>> GetValueDateAsync()
    {
        var entityId = new GetValueDateParameter();
        GetValueDateQuery query = new()
        {
            Subject = new ActorSubject(ActorType.Query, GetValueDateQuery.Actor, GetValueDateQuery.Verb, entityId.Format()),
        };
        return await RequestAsync<GetValueDateQuery, ScalarReadModel<DateOnly>>(query.Subject, query);
    }

    /// <inheritdoc />
    public async Task<ServiceResult<IronCondorMarketDataReadModel>> GetIronCondorMarketDataAsync(
        string underlyingContractId,
        string shortPutOptionContractId,
        string longPutOptionContractId,
        string shortCallOptionContractId,
        string longCallOptionContractId,
        DateOnly startDate,
        DateOnly endDate,
        MarketType marketType,
        CurrencyType currencyType)
    {
        var entityId = new GetIronCondorMarketDataParameter(
            underlyingContractId,
            shortPutOptionContractId,
            longPutOptionContractId,
            shortCallOptionContractId,
            longCallOptionContractId,
            startDate,
            endDate,
            marketType,
            currencyType);
        GetIronCondorMarketDataQuery query = new(
            underlyingContractId,
            shortPutOptionContractId,
            longPutOptionContractId,
            shortCallOptionContractId,
            longCallOptionContractId,
            startDate,
            endDate,
            marketType,
            currencyType)
        {
            Subject = new ActorSubject(ActorType.Query, GetIronCondorMarketDataQuery.Actor, GetIronCondorMarketDataQuery.Verb, entityId.Format()),
        };
        return await RequestAsync<GetIronCondorMarketDataQuery, IronCondorMarketDataReadModel>(query.Subject, query);
    }
}
