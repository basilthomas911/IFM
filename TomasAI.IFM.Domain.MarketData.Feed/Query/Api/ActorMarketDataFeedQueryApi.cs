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
            var result = new IronCondorMarketDataFeedReadModel(
                Convert.ToDecimal((await db.GetLastFuturesTickDataAsync(underlyingContractId, valueDate))?.Price ?? 0),
                (await db.GetLastFuturesOptionTickDataAsync(shortPutOptionContractId, valueDate))!,
                (await db.GetLastFuturesOptionTickDataAsync(longPutOptionContractId, valueDate))!,
                (await db.GetLastFuturesOptionTickDataAsync(shortCallOptionContractId, valueDate))!,
                (await db.GetLastFuturesOptionTickDataAsync(longCallOptionContractId, valueDate))!);
            return new ServiceOk<IronCondorMarketDataFeedReadModel>(result);
        }
        catch (Exception ex)
        {
            return new ServiceFailed<IronCondorMarketDataFeedReadModel>(
                GetIronCondorMarketDataFeedQuery.ErrorId,
                ex.Message);
        }
    }

    public async Task<ServiceResult<FuturesEodDataParametersReadModel>> GetFuturesEodDataParametersAsync(
        string contractId, DateOnly valueDate)
    {
        try
        {
            var db = _dbFactory.MarketDataDb;
            var result = new FuturesEodDataParametersReadModel(
                await db.GetFuturesEodDataAsync(contractId, valueDate),
                [.. await db.GetFuturesEodDataByDateRangeAsync(
                    contractId, valueDate.AddMonths(-2), valueDate.AddDays(-1))],
                await db.GetNormalCurveTableAsync());
            return new ServiceOk<FuturesEodDataParametersReadModel>(result);
        }
        catch (Exception ex)
        {
            return new ServiceFailed<FuturesEodDataParametersReadModel>(
                GetFuturesEodDataParametersQuery.ErrorId,
                ex.Message);
        }
    }

    public async Task<ServiceResult<FuturesOptionContractReadModel>> GetFuturesOptionContractAsync(
        string contractId, FuturesOptionContractReadModel queryForContract)
    {
        try
        {
            await _snapshotGate.WaitAsync();
            try
            {
                FuturesOptionContractReadModel result =
                    await GetFuturesOptionContract.GetFuturesOptionContractFromBrokerAsync(
                        _marketDataSnapshotApi,
                        contractId,
                        queryForContract);
                return new ServiceOk<FuturesOptionContractReadModel>(result);
            }
            finally
            {
                _snapshotGate.Release();
            }
        }
        catch (Exception ex)
        {
            return new ServiceFailed<FuturesOptionContractReadModel>(
                GetFuturesOptionContractQuery.ErrorId,
                ex.Message);
        }
    }

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
            await _snapshotGate.WaitAsync();
            try
            {
                FuturesOptionSpreadDataReadModel result =
                    await GetFuturesOptionSpreadData.GetFuturesOptionSpreadDataAsync(
                        _marketDataSnapshotApi,
                        valueDate,
                        maturityDate,
                        assetPrice,
                        riskFreeRate,
                        qfShortOptionContract,
                        qfLongOptionContract);
                return new ServiceOk<FuturesOptionSpreadDataReadModel>(result);
            }
            finally
            {
                _snapshotGate.Release();
            }
        }
        catch (Exception ex)
        {
            return new ServiceFailed<FuturesOptionSpreadDataReadModel>(
                GetFuturesOptionSpreadDataQuery.ErrorId,
                ex.Message);
        }
    }

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

    public Task<ServiceResult<ScalarValue<int>>> GetStreamingRequestIdAsync()
    {
        try
        {
            var result = new ScalarValue<int>(Convert.ToInt32(
                _blackboardService.Application.SequenceCounter.Increment(
                    SequenceName.StreamingRequest_RequestId)));
            return Task.FromResult<ServiceResult<ScalarValue<int>>>(
                new ServiceOk<ScalarValue<int>>(result));
        }
        catch (Exception ex)
        {
            return Task.FromResult<ServiceResult<ScalarValue<int>>>(
                new ServiceFailed<ScalarValue<int>>(GetStreamingRequestIdQuery.ErrorId, ex.Message));
        }
    }

    public Task<ServiceResult<ScalarValue<int>>> GetOptionQuoteIdAsync()
    {
        try
        {
            var result = new ScalarValue<int>(Convert.ToInt32(
                _blackboardService.Application.SequenceCounter.Increment(
                    SequenceName.OptionQuote_QuoteId)));
            return Task.FromResult<ServiceResult<ScalarValue<int>>>(
                new ServiceOk<ScalarValue<int>>(result));
        }
        catch (Exception ex)
        {
            return Task.FromResult<ServiceResult<ScalarValue<int>>>(
                new ServiceFailed<ScalarValue<int>>(GetOptionQuoteIdQuery.ErrorId, ex.Message));
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
