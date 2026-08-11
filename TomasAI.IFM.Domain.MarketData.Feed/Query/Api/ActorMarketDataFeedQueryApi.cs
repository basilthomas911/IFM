using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Domain.MarketData.Feed.FuturesEodData.Query.Extensions;
using TomasAI.IFM.Domain.MarketData.Feed.Shared;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.Queries;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.ServiceApi;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.ViewModels;
using TomasAI.IFM.Domain.MarketData.Shared;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Framework.SequenceId;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;
using ApplicationMarketDataApi = TomasAI.IFM.Application.MarketData.Contracts.IMarketDataApi;

namespace TomasAI.IFM.Domain.MarketData.Feed.Query.Api;

/// <summary>
/// Provides direct, in-process Market Data Feed queries without actor messaging.
/// </summary>
/// <remarks>
/// Storage queries use <see cref="IDbContextFactory.MarketDataDb"/>; provider contract and hot-price queries use
/// the application market-data API; sequence identifiers
/// are allocated through <see cref="ISequenceIdGenerator"/>. Every public method returns a typed service result
/// with its query-specific error identifier. The implementation may be registered as a singleton.
/// </remarks>
public sealed class ActorMarketDataFeedQueryApi(
    IDbContextFactory dbFactory,
    ApplicationMarketDataApi marketDataApi,
    ISequenceIdGenerator sequenceIdGenerator) : IActorMarketDataFeedQueryApi
{
    readonly IDbContextFactory _dbFactory = IsArgumentNull.Set(dbFactory);
    readonly ApplicationMarketDataApi _marketDataApi = IsArgumentNull.Set(marketDataApi);
    readonly ISequenceIdGenerator _sequenceIdGenerator = IsArgumentNull.Set(sequenceIdGenerator);

    /// <summary>
    /// Gets last futures tick data.
    /// </summary>
    /// <param name="contractId">The contract identifier.</param>
    /// <param name="valueDate">The applicable market value date.</param>
    /// <returns>A task containing the typed success result or the operation-specific failure result.</returns>
    public async Task<ServiceResult<FuturesTickDataV2ReadModel>> GetLastFuturesTickDataAsync(
        string contractId, DateOnly valueDate)
    {
        try
        {
            FuturesTickDataV2ReadModel result =
                (await _dbFactory.MarketDataDb.GetLastFuturesTickDataAsync(contractId, valueDate))!;
            return new ServiceOk<FuturesTickDataV2ReadModel>(result);
        }
        catch (Exception ex)
        {
            return new ServiceFailed<FuturesTickDataV2ReadModel>(GetLastFuturesTickDataQuery.ErrorId, ex.Message);
        }
    }

    /// <summary>
    /// Gets last futures tick data.
    /// </summary>
    /// <param name="contractId">The contract identifier.</param>
    /// <param name="tickDate">The tick timestamp.</param>
    /// <returns>A task containing the typed success result or the operation-specific failure result.</returns>
    public async Task<ServiceResult<FuturesTickDataV2ReadModel>> GetLastFuturesTickDataAsync(
        string contractId, DateTime tickDate)
    {
        try
        {
            FuturesTickDataV2ReadModel result =
                (await _dbFactory.MarketDataDb.GetLastFuturesTickDataByTickDateAsync(contractId, tickDate))!;
            return new ServiceOk<FuturesTickDataV2ReadModel>(result);
        }
        catch (Exception ex)
        {
            return new ServiceFailed<FuturesTickDataV2ReadModel>(
                GetLastFuturesTickDataByTickDateQuery.ErrorId,
                ex.Message);
        }
    }

    /// <summary>
    /// Gets last futures option tick data.
    /// </summary>
    /// <param name="contractId">The contract identifier.</param>
    /// <param name="valueDate">The applicable market value date.</param>
    /// <returns>A task containing the typed success result or the operation-specific failure result.</returns>
    public async Task<ServiceResult<FuturesOptionTickDataV2ReadModel>> GetLastFuturesOptionTickDataAsync(
        string contractId, DateOnly valueDate)
    {
        try
        {
            FuturesOptionTickDataV2ReadModel result =
                (await _dbFactory.MarketDataDb.GetLastFuturesOptionTickDataAsync(contractId, valueDate))!;
            return new ServiceOk<FuturesOptionTickDataV2ReadModel>(result);
        }
        catch (Exception ex)
        {
            return new ServiceFailed<FuturesOptionTickDataV2ReadModel>(
                GetLastFuturesOptionTickDataQuery.ErrorId,
                ex.Message);
        }
    }

    /// <summary>
    /// Gets futures EOD data.
    /// </summary>
    /// <param name="contractId">The contract identifier.</param>
    /// <param name="valueDate">The applicable market value date.</param>
    /// <returns>A task containing the typed success result or the operation-specific failure result.</returns>
    public async Task<ServiceResult<FuturesEodDataV2ReadModel>> GetFuturesEodDataAsync(
        string contractId, DateOnly valueDate)
    {
        try
        {
            var result = await _dbFactory.MarketDataDb.GetFuturesEodDataAsync(contractId, valueDate);
            if (result is null)
                return new ServiceOk<FuturesEodDataV2ReadModel>(result!);
            var movingAverages = await _dbFactory.GetFuturesEodMovingAveragesAsync(
                contractId, result.GetContractId().Symbol, valueDate);
            FuturesEodDataV2ReadModel enrichedResult = result with
            {
                FiftyDMA = movingAverages.FiftyDMA,
                TwoHundredDMA = movingAverages.TwoHundredDMA
            };
            return new ServiceOk<FuturesEodDataV2ReadModel>(enrichedResult);
        }
        catch (Exception ex)
        {
            return new ServiceFailed<FuturesEodDataV2ReadModel>(GetFuturesEodDataQuery.ErrorId, ex.Message);
        }
    }

    /// <summary>
    /// Gets last futures EOD data.
    /// </summary>
    /// <param name="contractId">The contract identifier.</param>
    /// <param name="valueDate">The applicable market value date.</param>
    /// <returns>A task containing the typed success result or the operation-specific failure result.</returns>
    public async Task<ServiceResult<FuturesEodDataV2ReadModel>> GetLastFuturesEodDataAsync(
        string contractId, DateOnly valueDate)
    {
        try
        {
            var result = await _dbFactory.MarketDataDb.GetLastFuturesEodDataAsync(contractId, valueDate);
            if (result is null)
                return new ServiceOk<FuturesEodDataV2ReadModel>(result!);
            var id = result.GetContractId();
            var movingAverages = await _dbFactory.GetFuturesEodMovingAveragesAsync(
                id.ContractId, id.Symbol, result.ValueDate);
            FuturesEodDataV2ReadModel enrichedResult = result with
            {
                FiftyDMA = movingAverages.FiftyDMA,
                TwoHundredDMA = movingAverages.TwoHundredDMA
            };
            return new ServiceOk<FuturesEodDataV2ReadModel>(enrichedResult);
        }
        catch (Exception ex)
        {
            return new ServiceFailed<FuturesEodDataV2ReadModel>(GetLastFuturesEodDataQuery.ErrorId, ex.Message);
        }
    }

    /// <summary>
    /// Gets last futures bar data.
    /// </summary>
    /// <param name="contractId">The contract identifier.</param>
    /// <param name="symbol">The market symbol.</param>
    /// <param name="valueDate">The applicable market value date.</param>
    /// <returns>A task containing the typed success result or the operation-specific failure result.</returns>
    public async Task<ServiceResult<FuturesBarDataReadModel>> GetLastFuturesBarDataAsync(
        string contractId, string symbol, DateOnly valueDate)
    {
        try
        {
            FuturesBarDataReadModel result =
                await _dbFactory.MarketDataDb.GetLastFuturesBarDataAsync(contractId, symbol, valueDate);
            return new ServiceOk<FuturesBarDataReadModel>(result);
        }
        catch (Exception ex)
        {
            return new ServiceFailed<FuturesBarDataReadModel>(GetLastFuturesBarDataQuery.ErrorId, ex.Message);
        }
    }

    /// <summary>
    /// Gets futures EOD moving averages.
    /// </summary>
    /// <param name="contractId">The contract identifier.</param>
    /// <param name="symbol">The market symbol.</param>
    /// <param name="valueDate">The applicable market value date.</param>
    /// <returns>A task containing the typed success result or the operation-specific failure result.</returns>
    public async Task<ServiceResult<FuturesEodDataMovingAveragesReadModel>> GetFuturesEodMovingAveragesAsync(
        string contractId, string symbol, DateOnly valueDate)
    {
        try
        {
            FuturesEodDataMovingAveragesReadModel result =
                await _dbFactory.GetFuturesEodMovingAveragesAsync(contractId, symbol, valueDate);
            return new ServiceOk<FuturesEodDataMovingAveragesReadModel>(result);
        }
        catch (Exception ex)
        {
            return new ServiceFailed<FuturesEodDataMovingAveragesReadModel>(
                GetFuturesEodDataMovingAveragesQuery.ErrorId,
                ex.Message);
        }
    }

    /// <summary>
    /// Gets VIX futures EOD data.
    /// </summary>
    /// <param name="contractId">The contract identifier.</param>
    /// <param name="valueDate">The applicable market value date.</param>
    /// <returns>A task containing the typed success result or the operation-specific failure result.</returns>
    public async Task<ServiceResult<VixFuturesEodDataReadModel[]>> GetVixFuturesEodDataAsync(
        string contractId, DateOnly valueDate)
    {
        try
        {
            if (string.IsNullOrEmpty(contractId))
            {
                VixFuturesEodDataReadModel[] values =
                    [.. await _dbFactory.MarketDataDb.GetVixFuturesEodDataByValueDateAsync(valueDate)];
                return new ServiceOk<VixFuturesEodDataReadModel[]>(values);
            }
            VixFuturesEodDataReadModel[] result =
                [(await _dbFactory.MarketDataDb.GetVixFuturesEodDataAsync(contractId, valueDate))!];
            return new ServiceOk<VixFuturesEodDataReadModel[]>(result);
        }
        catch (Exception ex)
        {
            return new ServiceFailed<VixFuturesEodDataReadModel[]>(GetVixFuturesEodDataQuery.ErrorId, ex.Message);
        }
    }

    /// <summary>
    /// Gets last VIX futures EOD data.
    /// </summary>
    /// <param name="contractId">The contract identifier.</param>
    /// <param name="valueDate">The applicable market value date.</param>
    /// <returns>A task containing the typed success result or the operation-specific failure result.</returns>
    public async Task<ServiceResult<VixFuturesEodDataReadModel>> GetLastVixFuturesEodDataAsync(
        string contractId, DateOnly valueDate)
    {
        try
        {
            VixFuturesEodDataReadModel result =
                (await _dbFactory.MarketDataDb.GetLastVixFuturesEodDataAsync(contractId, valueDate))!;
            return new ServiceOk<VixFuturesEodDataReadModel>(result);
        }
        catch (Exception ex)
        {
            return new ServiceFailed<VixFuturesEodDataReadModel>(
                GetLastVixFuturesEodDataQuery.ErrorId,
                ex.Message);
        }
    }

    /// <summary>
    /// Gets futures EOD data.
    /// </summary>
    /// <param name="contractId">The contract identifier.</param>
    /// <param name="startDate">The inclusive start date or timestamp.</param>
    /// <param name="endDate">The inclusive end date or timestamp.</param>
    /// <returns>A task containing the typed success result or the operation-specific failure result.</returns>
    public async Task<ServiceResult<FuturesEodDataV2ReadModel[]>> GetFuturesEodDataAsync(
        string contractId, DateOnly startDate, DateOnly endDate)
    {
        try
        {
            FuturesEodDataV2ReadModel[] result =
                [.. await _dbFactory.MarketDataDb.GetFuturesEodDataByDateRangeAsync(
                    contractId,
                    startDate,
                    endDate)];
            return new ServiceOk<FuturesEodDataV2ReadModel[]>(result);
        }
        catch (Exception ex)
        {
            return new ServiceFailed<FuturesEodDataV2ReadModel[]>(
                GetFuturesEodDataByDateRangeQuery.ErrorId,
                ex.Message);
        }
    }

    /// <summary>
    /// Gets futures bar data.
    /// </summary>
    /// <param name="contractId">The contract identifier.</param>
    /// <param name="symbol">The market symbol.</param>
    /// <param name="valueDate">The applicable market value date.</param>
    /// <param name="startDate">The inclusive start date or timestamp.</param>
    /// <param name="endDate">The inclusive end date or timestamp.</param>
    /// <returns>A task containing the typed success result or the operation-specific failure result.</returns>
    public async Task<ServiceResult<FuturesBarDataReadModel[]>> GetFuturesBarDataAsync(
        string contractId, string symbol, DateOnly valueDate, DateTime startDate, DateTime endDate)
    {
        try
        {
            FuturesBarDataReadModel[] result =
                [.. await _dbFactory.MarketDataDb.GetFuturesBarDataAsync(
                    contractId,
                    symbol,
                    valueDate,
                    startDate,
                    endDate)];
            return new ServiceOk<FuturesBarDataReadModel[]>(result);
        }
        catch (Exception ex)
        {
            return new ServiceFailed<FuturesBarDataReadModel[]>(GetFuturesBarDataQuery.ErrorId, ex.Message);
        }
    }

    /// <summary>
    /// Gets iron condor market data feed.
    /// </summary>
    /// <param name="underlyingContractId">The underlying contract ID.</param>
    /// <param name="shortPutOptionContractId">The short put option contract ID.</param>
    /// <param name="longPutOptionContractId">The long put option contract ID.</param>
    /// <param name="shortCallOptionContractId">The short call option contract ID.</param>
    /// <param name="longCallOptionContractId">The long call option contract ID.</param>
    /// <param name="valueDate">The applicable market value date.</param>
    /// <returns>A task containing the typed success result or the operation-specific failure result.</returns>
    public async Task<ServiceResult<IronCondorMarketDataFeedReadModel>> GetIronCondorMarketDataFeedAsync(
        string underlyingContractId,
        string shortPutOptionContractId,
        string longPutOptionContractId,
        string shortCallOptionContractId,
        string longCallOptionContractId,
        DateOnly valueDate)
    {
        try
        {
            var db = _dbFactory.MarketDataDb;
            var underlyingTask = db.GetLastFuturesTickDataAsync(underlyingContractId, valueDate);
            var shortPutTask = db.GetLastFuturesOptionTickDataAsync(shortPutOptionContractId, valueDate);
            var longPutTask = db.GetLastFuturesOptionTickDataAsync(longPutOptionContractId, valueDate);
            var shortCallTask = db.GetLastFuturesOptionTickDataAsync(shortCallOptionContractId, valueDate);
            var longCallTask = db.GetLastFuturesOptionTickDataAsync(longCallOptionContractId, valueDate);
            await Task.WhenAll(underlyingTask, shortPutTask, longPutTask, shortCallTask, longCallTask)
                .ConfigureAwait(false);

            var result = new IronCondorMarketDataFeedReadModel(
                Convert.ToDecimal((await underlyingTask.ConfigureAwait(false))?.Price ?? 0),
                (await shortPutTask.ConfigureAwait(false))!,
                (await longPutTask.ConfigureAwait(false))!,
                (await shortCallTask.ConfigureAwait(false))!,
                (await longCallTask.ConfigureAwait(false))!);
            return new ServiceOk<IronCondorMarketDataFeedReadModel>(result);
        }
        catch (Exception ex)
        {
            return new ServiceFailed<IronCondorMarketDataFeedReadModel>(
                GetIronCondorMarketDataFeedQuery.ErrorId,
                ex.Message);
        }
    }

    /// <summary>
    /// Gets futures EOD data parameters.
    /// </summary>
    /// <param name="contractId">The contract identifier.</param>
    /// <param name="valueDate">The applicable market value date.</param>
    /// <returns>A task containing the typed success result or the operation-specific failure result.</returns>
    public async Task<ServiceResult<FuturesEodDataParametersReadModel>> GetFuturesEodDataParametersAsync(
        string contractId, DateOnly valueDate)
    {
        try
        {
            var db = _dbFactory.MarketDataDb;
            var currentTask = db.GetFuturesEodDataAsync(contractId, valueDate);
            var rangeTask = db.GetFuturesEodDataByDateRangeAsync(
                contractId, valueDate.AddMonths(-2), valueDate.AddDays(-1));
            var normalCurveTask = db.GetNormalCurveTableAsync();
            await Task.WhenAll(currentTask, rangeTask, normalCurveTask).ConfigureAwait(false);

            var result = new FuturesEodDataParametersReadModel(
                await currentTask.ConfigureAwait(false),
                [.. await rangeTask.ConfigureAwait(false)],
                await normalCurveTask.ConfigureAwait(false));
            return new ServiceOk<FuturesEodDataParametersReadModel>(result);
        }
        catch (Exception ex)
        {
            return new ServiceFailed<FuturesEodDataParametersReadModel>(
                GetFuturesEodDataParametersQuery.ErrorId,
                ex.Message);
        }
    }

    /// <summary>
    /// Gets futures option contract.
    /// </summary>
    /// <param name="contractId">The contract identifier.</param>
    /// <param name="queryForContract">The contract template used for the broker lookup.</param>
    /// <returns>A task containing the typed success result or the operation-specific failure result.</returns>
    public async Task<ServiceResult<FuturesOptionContractReadModel>> GetFuturesOptionContractAsync(
        string contractId, FuturesOptionContractReadModel queryForContract)
    {
        try
        {
            FuturesOptionContractReadModel result =
                await GetFuturesOptionContract.GetFuturesOptionContractFromProviderAsync(
                    _marketDataApi,
                    contractId);
            return new ServiceOk<FuturesOptionContractReadModel>(result);
        }
        catch (Exception ex)
        {
            return new ServiceFailed<FuturesOptionContractReadModel>(
                GetFuturesOptionContractQuery.ErrorId,
                ex.Message);
        }
    }

    /// <summary>
    /// Gets futures option spread data.
    /// </summary>
    /// <param name="valueDate">The applicable market value date.</param>
    /// <param name="maturityDate">The option maturity date.</param>
    /// <param name="assetPrice">The underlying asset price.</param>
    /// <param name="riskFreeRate">The annualized risk-free rate.</param>
    /// <param name="timeValue">The option time value supplied by the caller.</param>
    /// <param name="qfShortOptionContract">The short option contract used for the spread snapshot.</param>
    /// <param name="qfLongOptionContract">The long option contract used for the spread snapshot.</param>
    /// <returns>A task containing the typed success result or the operation-specific failure result.</returns>
    public async Task<ServiceResult<FuturesOptionSpreadDataReadModel>> GetFuturesOptionSpreadDataAsync(
        DateOnly valueDate,
        DateOnly maturityDate,
        double assetPrice,
        double riskFreeRate,
        double timeValue,
        FuturesOptionContractReadModel qfShortOptionContract,
        FuturesOptionContractReadModel qfLongOptionContract)
    {
        try
        {
            FuturesOptionSpreadDataReadModel result =
                await GetFuturesOptionSpreadData.GetFuturesOptionSpreadDataAsync(
                    _marketDataApi,
                    qfShortOptionContract.ContractId,
                    qfLongOptionContract.ContractId);
            return new ServiceOk<FuturesOptionSpreadDataReadModel>(result);
        }
        catch (Exception ex)
        {
            return new ServiceFailed<FuturesOptionSpreadDataReadModel>(
                GetFuturesOptionSpreadDataQuery.ErrorId,
                ex.Message);
        }
    }

    /// <summary>
    /// Gets normal curve table.
    /// </summary>
    /// <returns>A task containing the typed success result or the operation-specific failure result.</returns>
    public async Task<ServiceResult<NormalCurveTableReadModel>> GetNormalCurveTableAsync()
    {
        try
        {
            NormalCurveTableReadModel result = await _dbFactory.MarketDataDb.GetNormalCurveTableAsync();
            return new ServiceOk<NormalCurveTableReadModel>(result);
        }
        catch (Exception ex)
        {
            return new ServiceFailed<NormalCurveTableReadModel>(GetNormalCurveTableQuery.ErrorId, ex.Message);
        }
    }

    /// <summary>
    /// Gets futures risk position type.
    /// </summary>
    /// <param name="valueDate">The applicable market value date.</param>
    /// <param name="tradeType">The trade strategy type.</param>
    /// <returns>A task containing the typed success result or the operation-specific failure result.</returns>
    public async Task<ServiceResult<RiskPositionTypeReadModel>> GetFuturesRiskPositionTypeAsync(
        DateOnly valueDate, TradeType tradeType)
    {
        try
        {
            var data = await _dbFactory.MarketDataDb.GetCurrentFuturesEodDataAsync(valueDate);
            if (data is null)
            {
                var unknownResult = new RiskPositionTypeReadModel(RiskPositionType.Unknown);
                return new ServiceOk<RiskPositionTypeReadModel>(unknownResult);
            }

            var riskValue = tradeType switch
            {
                TradeType.ShortIronCondor => GetShortIronCondorRiskPosition(data),
                TradeType.LongIronCondor => GetLongIronCondorRiskPosition(data),
                _ => throw new NotImplementedException($"Risk Position Type not implemented for: {tradeType}")
            };
            var riskType = riskValue switch
            {
                < 3 => RiskPositionType.Low,
                3 => RiskPositionType.Medium,
                > 3 => RiskPositionType.High
            };
            var result = new RiskPositionTypeReadModel(riskType);
            return new ServiceOk<RiskPositionTypeReadModel>(result);
        }
        catch (Exception ex)
        {
            return new ServiceFailed<RiskPositionTypeReadModel>(
                GetFuturesRiskPositionTypeQuery.ErrorId,
                ex.Message);
        }
    }

    /// <summary>
    /// Gets streaming request ID.
    /// </summary>
    /// <returns>A task containing the typed success result or the operation-specific failure result.</returns>
    public async Task<ServiceResult<ScalarValue<int>>> GetStreamingRequestIdAsync()
    {
        try
        {
            var result = new ScalarValue<int>(checked((int)await _sequenceIdGenerator
                .GetSequenceIdAsync(SequenceName.StreamingRequest_RequestId)
                .ConfigureAwait(false)));
            return new ServiceOk<ScalarValue<int>>(result);
        }
        catch (Exception ex)
        {
            return new ServiceFailed<ScalarValue<int>>(GetStreamingRequestIdQuery.ErrorId, ex.Message);
        }
    }

    static int GetShortIronCondorRiskPosition(FuturesEodDataV2ReadModel data)
    {
        var value = 0;
        if (data.MarketDirection is MarketDirectionType.NeutralUp or MarketDirectionType.Up) value++;
        if (data.MarketVolatility is MarketVolatilityType.Normal or MarketVolatilityType.Falling) value++;
        if (data.PriceDirection == PriceDirectionType.Rising) value++;
        if (data.PriceVolatility == PriceVolatilityType.Falling) value++;
        return value;
    }

    static int GetLongIronCondorRiskPosition(FuturesEodDataV2ReadModel data)
    {
        var value = 0;
        if (data.MarketDirection is MarketDirectionType.NeutralDown or MarketDirectionType.Down) value++;
        if (data.MarketVolatility is MarketVolatilityType.Rising or MarketVolatilityType.High) value++;
        if (data.PriceDirection == PriceDirectionType.Falling) value++;
        if (data.PriceVolatility == PriceVolatilityType.Rising) value++;
        return value;
    }
}
