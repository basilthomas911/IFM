using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.MarketData;
using TomasAI.IFM.Shared.MarketData.Queries;
using TomasAI.IFM.Shared.MarketData.ViewModels;

namespace TomasAI.IFM.Application.Query.Server.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class MarketDataController(IQueryService queryService, ILogger<MarketDataController> logger) : QueryControllerBase(queryService, logger)
    {
        protected override string ControllerName => "MarketDataController";

        [HttpGet]
        [Route("CurrentlyTradedFuturesContract")]
        [SwaggerOperation(Summary = "return currently traded futures contract")]
        [SwaggerResponse(200, "success", typeof(ServiceResult<FuturesContractV2ReadModel>))]
        [SwaggerResponse(500, "failure")]
        [Produces("application/json")]
        public async Task<ServiceResult<FuturesContractV2ReadModel>> GetCurrentlyTradedFuturesContractAsync()
            => await ExecuteQueryAsync(new GetCurrentlyTradedFuturesContractQuery ());

        [HttpGet]
        [Route("CurrentlyTradedFuturesContracts")]
        [SwaggerOperation(Summary = "return currently traded futures contracts")]
        [SwaggerResponse(200, "success", typeof(ServiceResult<FuturesContractV2ReadModel[]>))]
        [SwaggerResponse(500, "failure")]
        [Produces("application/json")]
        public async Task<ServiceResult<FuturesContractV2ReadModel[]>> GetCurrentlyTradedFuturesContractsAsync()
            => await ExecuteQueryAsync(new GetCurrentlyTradedFuturesContractsQuery ());

        [HttpGet]
        [Route("FuturesContract")]
        [SwaggerOperation(Summary = "return futures contract by contract id")]
        [SwaggerResponse(200, "success", typeof(ServiceResult<FuturesContractV2ReadModel>))]
        [SwaggerResponse(500, "failure")]
        [Produces("application/json")]
        public async Task<ServiceResult<FuturesContractV2ReadModel>> GetFuturesContractAsync(string contractId)
            => await ExecuteQueryAsync(new GetFuturesContractQuery(contractId));

        [HttpGet]
        [Route("FuturesContracts")]
        [SwaggerOperation(Summary = "return all futures contracts")]
        [SwaggerResponse(200, "success", typeof(ServiceResult<FuturesContractV2ReadModel[]>))]
        [SwaggerResponse(500, "failure")]
        [Produces("application/json")]
        public async Task<ServiceResult<FuturesContractV2ReadModel[]>> GetFuturesContractsAsync()
            => await ExecuteQueryAsync(new GetFuturesContractsQuery());

        [HttpGet]
        [Route("FuturesOptionContract")]
        [SwaggerOperation(Summary = "return futures option contract by contract id")]
        [SwaggerResponse(200, "success", typeof(ServiceResult<FuturesOptionContractReadModel>))]
        [SwaggerResponse(500, "failure")]
        [Produces("application/json")]
        public async Task<ServiceResult<FuturesOptionContractReadModel>> GetFuturesOptionContractAsync(string contractId)
            => await ExecuteQueryAsync(new GetFuturesOptionContractQuery (contractId));

        [HttpGet]
        [Route("FuturesOptionContracts")]
        [SwaggerOperation(Summary = "return futures option contracts by symbol")]
        [SwaggerResponse(200, "success", typeof(ServiceResult<FuturesOptionContractReadModel[]>))]
        [SwaggerResponse(500, "failure")]
        [Produces("application/json")]
        public async Task<ServiceResult<FuturesOptionContractReadModel[]>> GetFuturesOptionContractsAsync(string symbol)
            => await ExecuteQueryAsync(new GetFuturesOptionContractsQuery(symbol));

        [HttpGet]
        [Route("LastYieldCurveRate")]
        [SwaggerOperation(Summary = "return last yield curve rate")]
        [SwaggerResponse(200, "success", typeof(ServiceResult<YieldCurveRateReadModel>))]
        [SwaggerResponse(500, "failure")]
        [Produces("application/json")]
        public async Task<ServiceResult<YieldCurveRateReadModel>> GetLastYieldCurveRateAsync()
            => await ExecuteQueryAsync(new GetLastYieldCurveRateQuery { });

        [HttpGet]
        [Route("LastRateOfReturn")]
        [SwaggerOperation(Summary = "return last rate of return")]
        [SwaggerResponse(200, "success", typeof(ServiceResult<RateOfReturnReadModel>))]
        [SwaggerResponse(500, "failure")]
        [Produces("application/json")]
        public async Task<ServiceResult<RateOfReturnReadModel>> GetLastRateOfReturnAsync(string symbol, DateOnly valueDate)
            => await ExecuteQueryAsync(new GetLastRateOfReturnQuery (symbol, valueDate));

        [HttpGet]
        [Route("TradingDays")]
        [SwaggerOperation(Summary = "return trading days")]
        [SwaggerResponse(200, "success", typeof(ServiceResult<ScalarReadModel<int>>))]
        [SwaggerResponse(500, "failure")]
        [Produces("application/json")]
        public async Task<ServiceResult<ScalarReadModel<int>>> GetTradingDaysAsync(DateOnly startDate, DateOnly endDate, MarketType marketType, CurrencyType currencyType)
            => await ExecuteQueryAsync(new GetTradingDaysQuery(startDate, endDate, marketType, currencyType));

        [HttpGet]
        [Route("TradingDates")]
        [SwaggerOperation(Summary = "return trading dates")]
        [SwaggerResponse(200, "success", typeof(ServiceResult<DateTime[]>))]
        [SwaggerResponse(500, "failure")]
        [Produces("application/json")]
        public async Task<ServiceResult<DateOnly[]>> GetTradingDatesAsync(DateOnly startDate, DateOnly endDate, MarketType marketType, CurrencyType currencyType)
            => await ExecuteQueryAsync(new GetTradingDatesQuery (startDate, endDate, marketType, currencyType));

        [HttpGet]
        [Route("YieldCurveRates")]
        [SwaggerOperation(Summary = "return yield curve rates")]
        [SwaggerResponse(200, "success", typeof(ServiceResult<YieldCurveRateReadModel[]>))]
        [SwaggerResponse(500, "failure")]
        [Produces("application/json")]
        public async Task<ServiceResult<YieldCurveRateReadModel[]>> GetYieldCurveRatesAsync(DateOnly startDate, DateOnly endDate)
            => await ExecuteQueryAsync(new GetYieldCurveRatesQuery (startDate, endDate));

        [HttpGet]
        [Route("YieldCurveRateYears")]
        [SwaggerOperation(Summary = "return yield curve rate years")]
        [SwaggerResponse(200, "success", typeof(ServiceResult<YieldCurveRateYearsReadModel>))]
        [SwaggerResponse(500, "failure")]
        [Produces("application/json")]
        public async Task<ServiceResult<YieldCurveRateYearsReadModel>> GetYieldCurveRateYearsAsync()
            => await ExecuteQueryAsync(new GetYieldCurveRateYearsQuery { });

        [HttpGet]
        [Route("YieldCurveRateExists")]
        [SwaggerOperation(Summary = "return yield curve rate exists")]
        [SwaggerResponse(200, "success", typeof(ServiceResult<ScalarReadModel<bool>>))]
        [SwaggerResponse(500, "failure")]
        [Produces("application/json")]
        public async Task<ServiceResult<ScalarReadModel<bool>>> GetYieldCurveRateExistsAsync(DateOnly valueDate)
            => await ExecuteQueryAsync(new GetYieldCurveRateExistsQuery(valueDate));

        [HttpGet]
        [Route("ValueDate")]
        [SwaggerOperation(Summary = "return value date")]
        [SwaggerResponse(200, "success", typeof(ServiceResult<ScalarReadModel<DateOnly>>))]
        [SwaggerResponse(500, "failure")]
        [Produces("application/json")]
        public async Task<ServiceResult<ScalarReadModel<DateOnly>>> GetValueDateAsync()
            => await ExecuteQueryAsync(new GetValueDateQuery ());

        [HttpGet]
        [Route("ExternalYieldCurveRates")]
        [SwaggerOperation(Summary = "return value date")]
        [SwaggerResponse(200, "success", typeof(ServiceResult<YieldCurveRateReadModel[]>))]
        [SwaggerResponse(500, "failure")]
        [Produces("application/json")]
        public async Task<ServiceResult<YieldCurveRateReadModel[]>> GetExternalYieldCurveRatesAsync()
            => await ExecuteQueryAsync(new GetExternalYieldCurveRatesQuery { });

        [HttpGet]
        [Route("IronCondorMarketData")]
        [SwaggerOperation(Summary = "return iron condor market data")]
        [SwaggerResponse(200, "success", typeof(ServiceResult<IronCondorMarketDataReadModel>))]
        [SwaggerResponse(500, "failure")]
        [Produces("application/json")]
        public async Task<ServiceResult<IronCondorMarketDataReadModel>> GetIronCondorMarketDataAsync(string underlyingContractId, string shortPutOptionContractId, string longPutOptionContractId, string shortCallOptionContractId, string longCallOptionContractId,
            DateOnly startDate, DateOnly endDate, MarketType marketType, CurrencyType currencyType)
            => await ExecuteQueryAsync(new GetIronCondorMarketDataQuery(underlyingContractId, shortPutOptionContractId, longPutOptionContractId, shortCallOptionContractId, longCallOptionContractId, startDate, endDate, marketType, currencyType));

        [HttpGet]
        [Route("FuturesOptionContractIds")]
        [SwaggerOperation(Summary = "return futures option contract ids")]
        [SwaggerResponse(200, "success", typeof(ServiceResult<string[]>))]
        [SwaggerResponse(500, "failure")]
        [Produces("application/json")]
        public async Task<ServiceResult<string[]>> ExecuteAsync(string[] contractIds)
            => await ExecuteQueryAsync(new GetFuturesOptionContractIdsQuery(contractIds));
    }
}