using TomasAI.IFM.Application.Blackboard;
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

namespace TomasAI.IFM.Domain.MarketData.Feed.Query.Api;

/// <summary>Provides direct, in-process Market Data Feed queries without actor messaging.</summary>
public sealed class ActorMarketDataFeedQueryApi(
    IDbContextFactory dbFactory,
    IMarketDataSnapshotApi marketDataSnapshotApi,
    IBlackboardService blackboardService) : IActorMarketDataFeedQueryApi
{
    readonly IDbContextFactory _dbFactory = IsArgumentNull.Set(dbFactory);
    readonly IMarketDataSnapshotApi _marketDataSnapshotApi = IsArgumentNull.Set(marketDataSnapshotApi);
    readonly IBlackboardService _blackboardService = IsArgumentNull.Set(blackboardService);
    readonly SemaphoreSlim _snapshotGate = new(1, 1);

    public Task<ServiceResult<FuturesTickDataV2ReadModel>> GetLastFuturesTickDataAsync(
        string contractId, DateOnly valueDate)
        => ExecuteAsync(GetLastFuturesTickDataQuery.ErrorId,
            async () => (await _dbFactory.MarketDataDb.GetLastFuturesTickDataAsync(contractId, valueDate))!);

    public Task<ServiceResult<FuturesTickDataV2ReadModel>> GetLastFuturesTickDataAsync(
        string contractId, DateTime tickDate)
        => ExecuteAsync(GetLastFuturesTickDataByTickDateQuery.ErrorId,
            async () => (await _dbFactory.MarketDataDb.GetLastFuturesTickDataByTickDateAsync(contractId, tickDate))!);

    public Task<ServiceResult<FuturesOptionTickDataV2ReadModel>> GetLastFuturesOptionTickDataAsync(
        string contractId, DateOnly valueDate)
        => ExecuteAsync(GetLastFuturesOptionTickDataQuery.ErrorId,
            async () => (await _dbFactory.MarketDataDb.GetLastFuturesOptionTickDataAsync(contractId, valueDate))!);

    public Task<ServiceResult<FuturesEodDataV2ReadModel>> GetFuturesEodDataAsync(
        string contractId, DateOnly valueDate)
        => ExecuteAsync(GetFuturesEodDataQuery.ErrorId, async () =>
        {
            var result = await _dbFactory.MarketDataDb.GetFuturesEodDataAsync(contractId, valueDate);
            if (result is null)
                return result!;
            var movingAverages = await _dbFactory.GetFuturesEodMovingAveragesAsync(
                contractId, result.GetContractId().Symbol, valueDate);
            return result with
            {
                FiftyDMA = movingAverages.FiftyDMA,
                TwoHundredDMA = movingAverages.TwoHundredDMA
            };
        });

    public Task<ServiceResult<FuturesEodDataV2ReadModel>> GetLastFuturesEodDataAsync(
        string contractId, DateOnly valueDate)
        => ExecuteAsync(GetLastFuturesEodDataQuery.ErrorId, async () =>
        {
            var result = await _dbFactory.MarketDataDb.GetLastFuturesEodDataAsync(contractId, valueDate);
            if (result is null)
                return result!;
            var id = result.GetContractId();
            var movingAverages = await _dbFactory.GetFuturesEodMovingAveragesAsync(
                id.ContractId, id.Symbol, result.ValueDate);
            return result with
            {
                FiftyDMA = movingAverages.FiftyDMA,
                TwoHundredDMA = movingAverages.TwoHundredDMA
            };
        });

    public Task<ServiceResult<FuturesBarDataReadModel>> GetLastFuturesBarDataAsync(
        string contractId, string symbol, DateOnly valueDate)
        => ExecuteAsync(GetLastFuturesBarDataQuery.ErrorId,
            async () => await _dbFactory.MarketDataDb.GetLastFuturesBarDataAsync(contractId, symbol, valueDate));

    public Task<ServiceResult<FuturesEodDataMovingAveragesReadModel>> GetFuturesEodMovingAveragesAsync(
        string contractId, string symbol, DateOnly valueDate)
        => ExecuteAsync(GetFuturesEodDataMovingAveragesQuery.ErrorId,
            async () => await _dbFactory.GetFuturesEodMovingAveragesAsync(contractId, symbol, valueDate));

    public Task<ServiceResult<VixFuturesEodDataReadModel[]>> GetVixFuturesEodDataAsync(
        string contractId, DateOnly valueDate)
        => ExecuteAsync<VixFuturesEodDataReadModel[]>(GetVixFuturesEodDataQuery.ErrorId, async () =>
        {
            if (string.IsNullOrEmpty(contractId))
                return [.. await _dbFactory.MarketDataDb.GetVixFuturesEodDataByValueDateAsync(valueDate)];
            return [(await _dbFactory.MarketDataDb.GetVixFuturesEodDataAsync(contractId, valueDate))!];
        });

    public Task<ServiceResult<VixFuturesEodDataReadModel>> GetLastVixFuturesEodDataAsync(
        string contractId, DateOnly valueDate)
        => ExecuteAsync(GetLastVixFuturesEodDataQuery.ErrorId,
            async () => (await _dbFactory.MarketDataDb.GetLastVixFuturesEodDataAsync(contractId, valueDate))!);

    public Task<ServiceResult<FuturesEodDataV2ReadModel[]>> GetFuturesEodDataAsync(
        string contractId, DateOnly startDate, DateOnly endDate)
        => ExecuteAsync<FuturesEodDataV2ReadModel[]>(GetFuturesEodDataByDateRangeQuery.ErrorId,
            async () => [.. await _dbFactory.MarketDataDb.GetFuturesEodDataByDateRangeAsync(
                contractId, startDate, endDate)]);

    public Task<ServiceResult<FuturesBarDataReadModel[]>> GetFuturesBarDataAsync(
        string contractId, string symbol, DateOnly valueDate, DateTime startDate, DateTime endDate)
        => ExecuteAsync<FuturesBarDataReadModel[]>(GetFuturesBarDataQuery.ErrorId,
            async () => [.. await _dbFactory.MarketDataDb.GetFuturesBarDataAsync(
                contractId, symbol, valueDate, startDate, endDate)]);

    public Task<ServiceResult<IronCondorMarketDataFeedReadModel>> GetIronCondorMarketDataFeedAsync(
        string underlyingContractId,
        string shortPutOptionContractId,
        string longPutOptionContractId,
        string shortCallOptionContractId,
        string longCallOptionContractId,
        DateOnly valueDate)
        => ExecuteAsync(GetIronCondorMarketDataFeedQuery.ErrorId, async () =>
        {
            var db = _dbFactory.MarketDataDb;
            return new IronCondorMarketDataFeedReadModel(
                Convert.ToDecimal((await db.GetLastFuturesTickDataAsync(underlyingContractId, valueDate))?.Price ?? 0),
                (await db.GetLastFuturesOptionTickDataAsync(shortPutOptionContractId, valueDate))!,
                (await db.GetLastFuturesOptionTickDataAsync(longPutOptionContractId, valueDate))!,
                (await db.GetLastFuturesOptionTickDataAsync(shortCallOptionContractId, valueDate))!,
                (await db.GetLastFuturesOptionTickDataAsync(longCallOptionContractId, valueDate))!);
        });

    public Task<ServiceResult<FuturesEodDataParametersReadModel>> GetFuturesEodDataParametersAsync(
        string contractId, DateOnly valueDate)
        => ExecuteAsync(GetFuturesEodDataParametersQuery.ErrorId, async () =>
        {
            var db = _dbFactory.MarketDataDb;
            return new FuturesEodDataParametersReadModel(
                await db.GetFuturesEodDataAsync(contractId, valueDate),
                [.. await db.GetFuturesEodDataByDateRangeAsync(
                    contractId, valueDate.AddMonths(-2), valueDate.AddDays(-1))],
                await db.GetNormalCurveTableAsync());
        });

    public Task<ServiceResult<FuturesOptionContractReadModel>> GetFuturesOptionContractAsync(
        string contractId, FuturesOptionContractReadModel queryForContract)
        => ExecuteAsync(GetFuturesOptionContractQuery.ErrorId,
            async () => await ExecuteSnapshotAsync(() =>
                GetFuturesOptionContract.GetFuturesOptionContractFromBrokerAsync(
                    _marketDataSnapshotApi, contractId, queryForContract)));

    public Task<ServiceResult<FuturesOptionSpreadDataReadModel>> GetFuturesOptionSpreadDataAsync(
        DateOnly valueDate,
        DateOnly maturityDate,
        double assetPrice,
        double riskFreeRate,
        double timeValue,
        FuturesOptionContractReadModel qfShortOptionContract,
        FuturesOptionContractReadModel qfLongOptionContract)
        => ExecuteAsync(GetFuturesOptionSpreadDataQuery.ErrorId,
            async () => await ExecuteSnapshotAsync(() =>
                GetFuturesOptionSpreadData.GetFuturesOptionSpreadDataAsync(
                    _marketDataSnapshotApi,
                    valueDate,
                    maturityDate,
                    assetPrice,
                    riskFreeRate,
                    qfShortOptionContract,
                    qfLongOptionContract)));

    public Task<ServiceResult<NormalCurveTableReadModel>> GetNormalCurveTableAsync()
        => ExecuteAsync(GetNormalCurveTableQuery.ErrorId,
            async () => await _dbFactory.MarketDataDb.GetNormalCurveTableAsync());

    public Task<ServiceResult<RiskPositionTypeReadModel>> GetFuturesRiskPositionTypeAsync(
        DateOnly valueDate, TradeType tradeType)
        => ExecuteAsync(GetFuturesRiskPositionTypeQuery.ErrorId, async () =>
        {
            var data = await _dbFactory.MarketDataDb.GetCurrentFuturesEodDataAsync(valueDate);
            if (data is null)
                return new RiskPositionTypeReadModel(RiskPositionType.Unknown);

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
            return new RiskPositionTypeReadModel(riskType);
        });

    public Task<ServiceResult<ScalarValue<int>>> GetStreamingRequestIdAsync()
        => ExecuteAsync(GetStreamingRequestIdQuery.ErrorId, () => Task.FromResult(
            new ScalarValue<int>(Convert.ToInt32(
                _blackboardService.Application.SequenceCounter.Increment(
                    SequenceName.StreamingRequest_RequestId)))));

    public Task<ServiceResult<ScalarValue<int>>> GetOptionQuoteIdAsync()
        => ExecuteAsync(GetOptionQuoteIdQuery.ErrorId, () => Task.FromResult(
            new ScalarValue<int>(Convert.ToInt32(
                _blackboardService.Application.SequenceCounter.Increment(
                    SequenceName.OptionQuote_QuoteId)))));

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

    async Task<T> ExecuteSnapshotAsync<T>(Func<ValueTask<T>> query)
    {
        await _snapshotGate.WaitAsync();
        try
        {
            return await query();
        }
        finally
        {
            _snapshotGate.Release();
        }
    }

    static async Task<ServiceResult<T>> ExecuteAsync<T>(int errorId, Func<Task<T>> query)
    {
        try
        {
            return new ServiceOk<T>(await query());
        }
        catch (Exception ex)
        {
            return new ServiceFailed<T>(errorId, ex.Message);
        }
    }
}
