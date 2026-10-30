using TomasAI.IFM.Domain.MarketData.Shared;
using TomasAI.IFM.Domain.Trade.Shared;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Framework.SequenceId;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Shared.Storage;
using TomasAI.IFM.Shared.Extensions;

using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Shared.Storage;
using TomasAI.IFM.Domain.Trade.Shared.ViewModels;
using TomasAI.IFM.Domain.Trade.Shared.TradeOrder;


using static TomasAI.IFM.Shared.Storage.BindValues;

using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Events;

using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Model;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Events;

using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.MarketCondition.Model;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.OrderComposition;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.RegimeDiscovery.ViewModels;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.RiskManagement;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.TradeSelection;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.ViewModels;
using TomasAI.IFM.Framework.Serialization;

namespace TomasAI.IFM.Application.Storage.TradeDb;
/// <summary>
/// Provides typed PostgreSQL trade queries and accepted mutations; domain repositories own execution and workflow coordination.
/// </summary>
/// <param name = "connectionSettings">The named PostgreSQL connection.</param>
/// <param name = "dbFactory">Retained for constructor compatibility with host registration.</param>
/// <param name = "sequenceIdGenerator">The technical sequence allocator.</param>
/// <param name = "logger">The storage logger.</param>
public sealed class TradeDbContext(IDbConnectionSettings connectionSettings, IDbContextFactory dbFactory, ISequenceIdGenerator sequenceIdGenerator, ILogger<DbProvider> logger) : ObjectDataRepository<TradeDbContext>(connectionSettings[TradeDbConnection], logger), ITradeDbContext, IEnlistedTradeDbWriter
{
    public const string TradeDbConnection = "TradeDbConnection";
    /// <inheritdoc/>
    public Task ProjectWorkflowAsync(EventSourceDb.IEnlistedPostgresTransaction transaction, WorkflowStrategyStateUpdatedEvent source, CancellationToken token = default) => TradeDbContextExtensions.WorkflowProjectAsync(transaction, source, token);
    /// <inheritdoc/>
    public Task<int> ProjectOrderAsync(EventSourceDb.IEnlistedPostgresTransaction transaction, TradeOrderDefinition order, DateTime acceptedAtUtc, CancellationToken token = default) => TradeDbContextExtensions.TradeFlowProjectOrderLifecycleAsync(transaction, order, acceptedAtUtc, token);
    /// <inheritdoc/>
    public Task ProjectMonitoringLimitsAsync(TradeLimitReadModel limit, IReadOnlyList<TradeTypeLimitReadModel> spreads, CancellationToken token = default)
    {
        return this.NativeTransaction().ExecuteOnceAsync(async (db, ct) =>
        {
            await (TradeDbContextExtensions.TradeFlowProjectMonitoringLimitsAsync(db, limit, spreads, ct))
            .ConfigureAwait(false);
            return true;
        }, token);
    }

    /// <inheritdoc/>
    public Task ProjectMonitoringLimitsAsync(EventSourceDb.IEnlistedPostgresTransaction transaction, TradeLimitReadModel limit, IReadOnlyList<TradeTypeLimitReadModel> spreads, CancellationToken token = default) => TradeDbContextExtensions.TradeFlowProjectMonitoringLimitsAsync(transaction, limit, spreads, token);
    /// <inheritdoc/>
    public Task ProjectExecutionAsync(EventSourceDb.IEnlistedPostgresTransaction transaction, OrderExecutionDefinition execution, CancellationToken token = default) => TradeDbContextExtensions.TradeFlowProjectExecutionAsync(transaction, execution, token);
    /// <inheritdoc/>
    public Task ProjectTradeAsync(EventSourceDb.IEnlistedPostgresTransaction transaction, EstablishedTradeDefinition trade, CancellationToken token = default) => TradeDbContextExtensions.TradeFlowProjectTradeAsync(transaction, trade, token);
    /// <inheritdoc/>
    public Task ProjectPositionAsync(EventSourceDb.IEnlistedPostgresTransaction transaction, StrategyPositionSnapshot position, CancellationToken token = default) => TradeDbContextExtensions.TradeFlowProjectPositionAsync(transaction, position, token);
    internal readonly ISequenceIdGenerator _sequenceIdGenerator = IsArgumentNull.Set(sequenceIdGenerator);
    /// <summary>
    /// Gets the database context.
    /// </summary>
    public override TradeDbContext Database => this;
    /// <summary>Gets the read capability for this database context.</summary>
    public ITradeDbReadContext DbReader => this;
    /// <summary>Gets the write capability for this database context.</summary>
    public ITradeDbWriteContext DbWriter => this;

    static TradePlanForwardLossLimitReadModel MapToTradePlanForwardLossLimit<TDataRecord>(TDataRecord e)
        where TDataRecord : IObjectDataRecord => new(orderId: e.GetInt(0), tradeId: e.GetInt(1), valueDate: e.GetDateOnly(2), tradeType: e.GetEnum<TradeType>(3), limitType: e.GetEnum<ForwardLossLimitType>(4));
    static TradePlanStopLossLimitReadModel MapToTradePlanStopLossLimit<TDataRecord>(TDataRecord e)
        where TDataRecord : IObjectDataRecord => new(stopLossLimit: e.GetDouble(0));
    static TradePlanForwardLossRatioReadModel MapToTradePlanForwardLossRatio<TDataRecord>(TDataRecord e)
        where TDataRecord : IObjectDataRecord => new(forwardLossRatio: e.GetDouble(0));
    static TradePlanForwardLossRatioRow MapToTradePlanForwardLossRatioRow<TDataRecord>(TDataRecord e)
        where TDataRecord : IObjectDataRecord => new(e.GetDouble(0), e.GetLong(1));
    static TradeLiveFeedReadModel MapToTradeLiveFeed<TDataRecord>(TDataRecord e)
        where TDataRecord : IObjectDataRecord => new(orderId: e.GetInt(0), tradeId: e.GetInt(1), liveFeed: e.GetBool(2));
    static TradePlanReadModel MapToTradePlan<TDataRecord>(TDataRecord e)
        where TDataRecord : IObjectDataRecord => new(sequenceId: e.GetLong(0), orderId: e.GetInt(1), tradeId: e.GetInt(2), valueDate: e.GetDateOnly(3), actionDate: e.GetDateTime(4), tradeDate: e.GetDateOnly(5), maturityDate: e.GetDateOnly(6), tradeType: e.GetEnum<TradeType>(7), actionType: e.GetEnum<ActionType>(8), actionSubType: e.GetEnum<ActionSubType>(9), actionState: e.GetEnum<ActionState>(10), actionReason: e.GetString(11), tradePnl: e.GetDecimal(12), forwardLossRatio: e.GetDouble(13), lossProbability: e.GetDouble(14), mScore: e.GetDouble(15), maxProfit: e.GetDecimal(16), maxLoss: e.GetDecimal(17), minProfitTarget: e.GetDecimal(18), dailyProfitTarget: e.GetDecimal(19), assetPrice: e.GetDecimal(20), assetStdDev: e.GetDouble(21), assetMean: e.GetDouble(22), assetPriceChange: e.GetDouble(23), marketTrend: e.GetEnum<MarketDirectionType>(24), marketVolatility: e.GetEnum<MarketVolatilityType>(25), marketDirection: e.GetEnum<PriceDirectionType>(26), vixVolatility: e.GetEnum<PriceVolatilityType>(27), tradeRisk: e.GetEnum<TradeRiskType>(28), fiftyDayMA: e.GetDouble(29), fiveDayXMA: e.GetDouble(30), putOTMProbability: e.GetDouble(31), callOTMProbability: e.GetDouble(32), shortPutGamma: e.GetDouble(33), shortCallGamma: e.GetDouble(34), gammaRisk: e.GetEnum<GammaRiskType>(35), netPrice: e.GetDecimal(36), forwardPrice: e.GetDecimal(37), forwardDelta: e.GetDouble(38), stopLossLimit: e.GetDouble(39), trendType: e.GetEnum<global::TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesTrendType>(40), trendStrength: e.GetEnum<global::TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesTrendStrengthType>(41), rsi: e.GetDouble(42), rsiSlope: e.GetDouble(43), tdi: e.GetEnum<global::TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesTrendDirectionType>(44), tdiStrength: e.GetEnum<global::TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesTrendDirectionStrengthType>(45), createdOn: e.GetDateTime(46), createdBy: e.GetString(47));
    static TradeFillDataReadModel MapToTradeFillData<TDataRecord>(TDataRecord e)
        where TDataRecord : IObjectDataRecord => new(orderId: e.GetInt(0), tradeId: e.GetInt(1), contractId: e.GetString(2), fillDate: e.GetDateTime(3), bidPrice: e.GetDecimal(4), askPrice: e.GetDecimal(5), commission: e.GetDecimal(6), optionLegAction: e.GetEnum<OptionLegAction>(7), createdOn: e.GetDateTime(8), createdBy: e.GetString(9));
    static TradeFillReadModel MapToTradeFill<TDataRecord>(TDataRecord e)
        where TDataRecord : IObjectDataRecord => new(orderId: e.GetInt(0), tradeId: e.GetInt(1), fillDate: e.GetDateTime(2), fillQuantity: e.GetInt(3), createdOn: e.GetDateTime(4), createdBy: e.GetString(5));
    internal static TradeTypeLimitReadModel MapToTradeTypeLimit<TDataRecord>(TDataRecord e)
        where TDataRecord : IObjectDataRecord => new(tradeId: e.GetInt(0), tradeType: e.GetEnum<TradeType>(1), maxLossLimit: e.GetDecimal(2), minProfitLimit: e.GetDecimal(3), maxProfitLimit: e.GetDecimal(4));
    static TradePlacementSignalReadModel MapToTradePlacementSignal<TDataRecord>(TDataRecord e)
        where TDataRecord : IObjectDataRecord => new(sequenceId: e.GetLong(0), contractId: e.GetString(1), valueDate: e.GetDateOnly(2), tradePlacementSignal: e.GetEnum<TradePlacementSignalType>(3), tradePrice: e.GetDecimal(4), createdOn: e.GetDateTime(5), createdBy: e.GetString(6));
    static TradeLimitReadModel MapToTradeLimit<TDataRecord>(TDataRecord e)
        where TDataRecord : IObjectDataRecord => new(tradeId: e.GetInt(0), tradeType: e.GetEnum<TradeType>(1), riskMargin: e.GetDecimal(2), maxProfit: e.GetDecimal(3), maxLoss: e.GetDecimal(4), maxReturn: e.GetDecimal(5), maxLossLimit: e.GetDecimal(6), minProfitLimit: e.GetDecimal(7), maxProfitLimit: e.GetDecimal(8), minProfitTarget: e.GetDecimal(9), dailyProfitTarget: e.GetDecimal(10), createdOn: e.GetDateTime(11), createdBy: e.GetString(12), updatedOn: e.GetDateTime(13), updatedBy: e.GetString(14));
    static TradeHistoryReadModel MapToTradeHistory<TDataRecord>(TDataRecord e)
        where TDataRecord : IObjectDataRecord => new(orderId: e.GetInt(0), tradeId: e.GetInt(1), valueDate: e.GetDateOnly(2), tradeStatus: e.GetEnum<TradeStatus>(3), daysToExpiry: e.GetInt(4), tradeType: e.GetEnum<TradeType>(5), commission: e.GetDecimal(6), netSpread: e.GetDecimal(7), tradePnl: e.GetDecimal(8));
    static OptionTradeSpreadBarsDataModel MapToOptionTradeSpreadBarsData<TDataRecord>(TDataRecord e)
        where TDataRecord : IObjectDataRecord => new(orderId: e.GetInt(0), tradeId: e.GetInt(1), valueDate: e.GetDateOnly(3), tradeType: e.GetEnum<TradeType>(2), barDate: e.GetDateTime(4), lossLimit: e.GetDecimal(5), winLimit: e.GetDecimal(6), forwardSpread: e.GetDecimal(7), netSpread: e.GetDecimal(8));
    internal static TradePositionReadModel MapToTradePosition<TDataRecord>(TDataRecord e)
        where TDataRecord : IObjectDataRecord => new(orderId: e.GetInt(0), tradeId: e.GetInt(1), tradeType: e.GetEnum<TradeType>(2), valueDate: e.GetDateOnly(3), tradeStatus: e.GetEnum<TradeStatus>(4), daysToExpiry: e.GetInt(5), commission: e.GetDecimal(6), deltaHedge: e.GetInt(7), netSpread: e.GetDecimal(8), tradeValue: e.GetDecimal(9), tradePnl: e.GetDecimal(10), assetPrice: e.GetDecimal(11), otmProbability: e.GetDouble(12), forwardPrice: e.GetDecimal(13), forwardLossRatio: e.GetDouble(14), lossProbability: e.GetDouble(15), riskFreeRate: e.GetDouble(16), createdOn: e.GetDateTime(17), createdBy: e.GetString(18), updatedOn: e.GetDateTime(19), updatedBy: e.GetString(20));
    internal static OptionTradeLegReadModel MapToOptionLeg<TDataRecord>(TDataRecord e)
        where TDataRecord : IObjectDataRecord => new(orderId: e.GetInt(0), tradeId: e.GetInt(1), contractId: e.GetString(2), quantity: e.GetInt(3), strikePrice: e.GetDecimal(4), optionLegType: e.GetEnum<OptionType>(5), optionLegAction: e.GetEnum<OptionLegAction>(6), createdOn: e.GetDateTime(7), createdBy: e.GetString(8), updatedOn: e.GetDateTime(9), updatedBy: e.GetString(10));
    internal static OptionTradeLegDataReadModel MapToOptionLegData<TDataRecord>(TDataRecord e)
        where TDataRecord : IObjectDataRecord => new(orderId: e.GetInt(0), tradeId: e.GetInt(1), valueDate: e.GetDateOnly(3), optionLegId: e.GetString(6), tradeType: e.GetEnum<TradeType>(2), daysToExpiry: e.GetInt(4), tradeStatus: e.GetEnum<TradeStatus>(5), bidPrice: e.GetDecimal(7), askPrice: e.GetDecimal(8), impliedVolatility: e.GetDouble(9), delta: e.GetDouble(10), gamma: e.GetDouble(11), theta: e.GetDouble(12), vega: e.GetDouble(13), rho: e.GetDouble(14), createdOn: e.GetDateTime(15), createdBy: e.GetString(16), updatedOn: e.GetDateTime(17), updatedBy: e.GetString(18));
    static OptionTradeSpreadsDataModel MapToOptionTradeSpreadData<TDataRecord>(TDataRecord e)
        where TDataRecord : IObjectDataRecord => new(orderId: e.GetInt(0), tradeId: e.GetInt(1), valueDate: e.GetDateOnly(2), tradeType: e.GetEnum<TradeType>(3), sequenceId: e.GetLong(4), lossLimit: e.GetDecimal(5), winLimit: e.GetDecimal(6), forwardSpread: e.GetDecimal(7), netSpread: e.GetDecimal(8), createdOn: e.GetDateTime(9), createdBy: e.GetString(10));
    static OptionTradeReadModel MapToOptionTrade<TDataRecord>(TDataRecord e)
        where TDataRecord : IObjectDataRecord => new(orderId: e.GetInt(0), tradeId: e.GetInt(1), tradeStrategy: e.GetString(2), tradeDate: e.GetDateOnly(3), maturityDate: e.GetDateOnly(4), tradeType: e.GetEnum<TradeType>(5), tradeState: e.GetEnum<TradeState>(6), tradeAction: e.GetEnum<TradeAction>(7), underlyingContractId: e.GetString(8), underlyingAssetType: e.GetEnum<AssetType>(9), isPrimaryTrade: e.GetBool(10), isHedgeTrade: e.GetBool(11), createdOn: e.GetDateTime(12), createdBy: e.GetString(13), updatedOn: e.GetDateTime(14), updatedBy: e.GetString(15));
    static TradePriceReadModel MapToTradePrice<TDataRecord>(TDataRecord e)
        where TDataRecord : IObjectDataRecord => new(tradeId: e.GetInt(0), valueDate: e.GetDateOnly(1), netPrice: e.GetDecimal(2), netForwardPrice: e.GetDecimal(3));
    /// <inheritdoc/>
    public async Task<ICollection<OptionTradeReadModel>> GetOptionTradesAsync(int orderId)
    {
        var db = this;
        var optionTrades = await (db.Use($"{nameof(TradeDbSql)}.{nameof(TradeDbSql.GetOptionTrades)}", TradeDbSql.GetOptionTrades)
            .SetParameters(new GetOptionTrades(orderId))
            .ExecuteQueryAsync(MapToOptionTrade!))
            .ConfigureAwait(false);
        var sourceTrades = optionTrades.ToArray();
        var hydratedTrades = new OptionTradeReadModel[sourceTrades.Length];
        await (Parallel.ForEachAsync(Enumerable.Range(0, sourceTrades.Length), new ParallelOptions { MaxDegreeOfParallelism = 4 }, async (index, token) => hydratedTrades[index] = await (this.FillOptionTradeAsync(sourceTrades[index], token))
            .ConfigureAwait(false)))
            .ConfigureAwait(false);
        return[..hydratedTrades.OrderBy(e => e.IsPrimaryTrade)];
    }

    /// <inheritdoc/>
    public async Task<ICollection<OptionTradeReadModel>> GetOptionTradesAsync() => await (this.Use($"{nameof(TradeDbSql)}.{nameof(TradeDbSql.GetOptionTradesAll)}", TradeDbSql.GetOptionTradesAll)
            .ExecuteQueryAsync(MapToOptionTrade!))
            .ConfigureAwait(false);
    /// <inheritdoc/>
    public async Task<ICollection<OptionTradeReadModel>> GetOptionTradesAsync(int orderId, CancellationToken cancellationToken)
    {
        var db = this;
        var optionTrades = await (db.Use($"{nameof(TradeDbSql)}.{nameof(TradeDbSql.GetOptionTrades)}", TradeDbSql.GetOptionTrades)
            .SetParameters(new GetOptionTrades(orderId))
            .ExecuteQueryAsync(MapToOptionTrade!, cancellationToken))
            .ConfigureAwait(false);
        var sourceTrades = optionTrades.ToArray();
        var hydratedTrades = new OptionTradeReadModel[sourceTrades.Length];
        await (Parallel.ForEachAsync(Enumerable.Range(0, sourceTrades.Length), new ParallelOptions { MaxDegreeOfParallelism = 4, CancellationToken = cancellationToken }, async (index, token) => hydratedTrades[index] = await (this.FillOptionTradeAsync(sourceTrades[index], token))
            .ConfigureAwait(false)))
            .ConfigureAwait(false);
        return[..hydratedTrades.OrderBy(e => e.IsPrimaryTrade)];
    }

    /// <inheritdoc/>
    public async Task<OptionTradeReadModel?> GetOptionTradeAsync(int orderId, int tradeId)
    {
        var db = this;
        var optionTrade = await (db.Use($"{nameof(TradeDbSql)}.{nameof(TradeDbSql.GetOptionTrade)}", TradeDbSql.GetOptionTrade)
            .SetParameters(new GetOptionTrade(orderId, tradeId))
            .ExecuteSingleAsync(MapToOptionTrade!))
            .ConfigureAwait(false);
        if (optionTrade is not null)
            optionTrade = await (this.FillOptionTradeAsync(optionTrade))
            .ConfigureAwait(false);
        return optionTrade;
    }

    /// <inheritdoc/>
    public async Task<OptionTradeReadModel?> GetOptionTradeAsync(int orderId, int tradeId, CancellationToken cancellationToken)
    {
        var db = this;
        var optionTrade = await (db.Use($"{nameof(TradeDbSql)}.{nameof(TradeDbSql.GetOptionTrade)}", TradeDbSql.GetOptionTrade)
            .SetParameters(new GetOptionTrade(orderId, tradeId))
            .ExecuteSingleAsync(MapToOptionTrade!, cancellationToken))
            .ConfigureAwait(false);
        return optionTrade is null ? null : await (this.FillOptionTradeAsync(optionTrade, cancellationToken))
            .ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task<OptionTradeSpreadsDataModel?> GetOptionTradeSpreadDataAsync(int orderId, int tradeId, DateOnly valueDate, TradeType tradeType) => await (this.Use($"{nameof(TradeDbSql)}.{nameof(TradeDbSql.GetOptionTradeSpreadData)}", TradeDbSql.GetOptionTradeSpreadData)
            .SetParameters(new GetOptionTradeSpreadData(orderId, tradeId, valueDate, tradeType.ToStringFast()))
            .ExecuteSingleAsync(MapToOptionTradeSpreadData!))
            .ConfigureAwait(false);
    /// <inheritdoc/>
    public async Task<ICollection<OptionTradeSpreadsDataModel>> GetOptionTradeSpreadDataAsync() => await (this.Use($"{nameof(TradeDbSql)}.{nameof(TradeDbSql.GetOptionTradeSpreadDataAll)}", TradeDbSql.GetOptionTradeSpreadDataAll)
            .ExecuteQueryAsync(MapToOptionTradeSpreadData!))
            .ConfigureAwait(false);
    /// <inheritdoc/>
    public Task<OptionTradeSpreadsDataModel?> GetOptionTradeSpreadDataAsync(int orderId, int tradeId, DateOnly valueDate, TradeType tradeType, CancellationToken cancellationToken) => this.Use($"{nameof(TradeDbSql)}.{nameof(TradeDbSql.GetOptionTradeSpreadData)}", TradeDbSql.GetOptionTradeSpreadData)
            .SetParameters(new GetOptionTradeSpreadData(orderId, tradeId, valueDate, tradeType.ToStringFast()))
            .ExecuteSingleAsync(MapToOptionTradeSpreadData!, cancellationToken);
    /// <inheritdoc/>
    public async Task<ICollection<OptionTradeSpreadBarsDataModel>> GetOptionTradeSpreadBarDataAsync(int orderId, int tradeId, DateOnly valueDate, TradeType tradeType, DateTime startDate, DateTime endDate) => await (this.Use($"{nameof(TradeDbSql)}.{nameof(TradeDbSql.GetOptionTradeSpreadBarData)}", TradeDbSql.GetOptionTradeSpreadBarData)
            .SetParameters(new GetOptionTradeSpreadBarData(orderId, tradeId, valueDate, tradeType.ToStringFast(), startDate, endDate))
            .ExecuteQueryAsync(MapToOptionTradeSpreadBarsData!))
            .ConfigureAwait(false);
    /// <inheritdoc/>
    public async Task<ICollection<OptionTradeSpreadBarsDataModel>> GetOptionTradeSpreadBarDataAsync() => await (this.Use($"{nameof(TradeDbSql)}.{nameof(TradeDbSql.GetOptionTradeSpreadBarDataAll)}", TradeDbSql.GetOptionTradeSpreadBarDataAll)
            .ExecuteQueryAsync(MapToOptionTradeSpreadBarsData!))
            .ConfigureAwait(false);
    /// <inheritdoc/>
    public Task<ICollection<OptionTradeSpreadBarsDataModel>> GetOptionTradeSpreadBarDataAsync(int orderId, int tradeId, DateOnly valueDate, TradeType tradeType, DateTime startDate, DateTime endDate, CancellationToken cancellationToken) => this.Use($"{nameof(TradeDbSql)}.{nameof(TradeDbSql.GetOptionTradeSpreadBarData)}", TradeDbSql.GetOptionTradeSpreadBarData)
            .SetParameters(new GetOptionTradeSpreadBarData(orderId, tradeId, valueDate, tradeType.ToStringFast(), startDate, endDate))
            .ExecuteQueryAsync(MapToOptionTradeSpreadBarsData!, cancellationToken);
    /// <inheritdoc/>
    public async Task<TradePriceReadModel?> GetIronCondorTradePriceAsync(int tradeId, DateOnly valueDate) => await (this.Use($"{nameof(TradeDbSql)}.{nameof(TradeDbSql.GetOptionLegs)}", TradeDbSql.GetOptionLegs)
            .SetParameters(new GetOptionLegsWithValueDate(tradeId, valueDate))
            .ExecuteSingleAsync(MapToTradePrice))
            .ConfigureAwait(false);
    /// <inheritdoc/>
    public Task<TradePriceReadModel?> GetIronCondorTradePriceAsync(int tradeId, DateOnly valueDate, CancellationToken cancellationToken) => this.Use($"{nameof(TradeDbSql)}.{nameof(TradeDbSql.GetOptionLegs)}", TradeDbSql.GetOptionLegs)
            .SetParameters(new GetOptionLegsWithValueDate(tradeId, valueDate))
            .ExecuteSingleAsync(MapToTradePrice, cancellationToken);
    /// <inheritdoc/>
    public async Task<ICollection<TradePositionReadModel>> GetTradePositionsAsync(int orderId, int tradeId)
    {
        var db = this;
        var tradePositionsTask = db.Use($"{nameof(TradeDbSql)}.{nameof(TradeDbSql.GetTradePositions)}", TradeDbSql.GetTradePositions)
            .SetParameters(new GetTradePositions(orderId, tradeId))
            .ExecuteQueryAsync(MapToTradePosition!);
        var tradePositions = await (tradePositionsTask)
            .ConfigureAwait(false);
        if (tradePositions.Count == 0)
            return tradePositions;
        var optionLegsTask = db.Use($"{nameof(TradeDbSql)}.{nameof(TradeDbSql.GetOptionLegsByOrderAndTrade)}", TradeDbSql.GetOptionLegsByOrderAndTrade)
            .SetParameters(new GetOptionLegsByOrderAndTrade(orderId, tradeId))
            .ExecuteQueryAsync(MapToOptionLeg!);
        var valueDates = tradePositions.Select(position => position.ValueDate).Distinct().ToArray();
        var legDataByDate = new ICollection<OptionTradeLegDataReadModel>[valueDates.Length];
        await (Parallel.ForEachAsync(Enumerable.Range(0, valueDates.Length), new ParallelOptions { MaxDegreeOfParallelism = 4 }, async (dateIndex, _) => legDataByDate[dateIndex] = await (db.Use($"{nameof(TradeDbSql)}.{nameof(TradeDbSql.GetOptionLegData)}", TradeDbSql.GetOptionLegData)
            .SetParameters(new GetOptionLegData(orderId, tradeId, valueDates[dateIndex]))
            .ExecuteQueryAsync(MapToOptionLegData!))
            .ConfigureAwait(false)))
            .ConfigureAwait(false);
        var optionLegs = await (optionLegsTask)
            .ConfigureAwait(false);
        var optionLegById = optionLegs.ToDictionary(leg => leg.ContractId, StringComparer.Ordinal);
        var legDataByPosition = MapToLegDataByPosition(legDataByDate, optionLegById, requireOptionLeg: false);
        foreach (var position in tradePositions)
        {
            if (legDataByPosition.TryGetValue(MapToPositionKey(position), out var legData))
                position.AddOptionLegData(legData);
        }

        return tradePositions;
    }

    /// <inheritdoc/>
    public async Task<ICollection<TradePositionReadModel>> GetTradePositionsAsync() => await (this.Use($"{nameof(TradeDbSql)}.{nameof(TradeDbSql.GetTradePositionsAll)}", TradeDbSql.GetTradePositionsAll)
            .ExecuteQueryAsync(MapToTradePosition!))
            .ConfigureAwait(false);
    /// <inheritdoc/>
    public async Task<ICollection<TradePositionReadModel>> GetTradePositionsAsync(int orderId, int tradeId, CancellationToken cancellationToken)
    {
        var db = this;
        var tradePositions = await (db.Use($"{nameof(TradeDbSql)}.{nameof(TradeDbSql.GetTradePositions)}", TradeDbSql.GetTradePositions)
            .SetParameters(new GetTradePositions(orderId, tradeId))
            .ExecuteQueryAsync(MapToTradePosition!, cancellationToken))
            .ConfigureAwait(false);
        if (tradePositions.Count == 0)
            return tradePositions;
        var optionLegsTask = db.Use($"{nameof(TradeDbSql)}.{nameof(TradeDbSql.GetOptionLegsByOrderAndTrade)}", TradeDbSql.GetOptionLegsByOrderAndTrade)
            .SetParameters(new GetOptionLegsByOrderAndTrade(orderId, tradeId))
            .ExecuteQueryAsync(MapToOptionLeg!, cancellationToken);
        var valueDates = tradePositions.Select(position => position.ValueDate).Distinct().ToArray();
        var legDataByDate = new ICollection<OptionTradeLegDataReadModel>[valueDates.Length];
        await (Parallel.ForEachAsync(Enumerable.Range(0, valueDates.Length), new ParallelOptions { MaxDegreeOfParallelism = 4, CancellationToken = cancellationToken }, async (dateIndex, token) => legDataByDate[dateIndex] = await (db.Use($"{nameof(TradeDbSql)}.{nameof(TradeDbSql.GetOptionLegData)}", TradeDbSql.GetOptionLegData)
            .SetParameters(new GetOptionLegData(orderId, tradeId, valueDates[dateIndex]))
            .ExecuteQueryAsync(MapToOptionLegData!, token))
            .ConfigureAwait(false)))
            .ConfigureAwait(false);
        var optionLegs = await (optionLegsTask)
            .ConfigureAwait(false);
        var optionLegById = optionLegs.ToDictionary(leg => leg.ContractId, StringComparer.Ordinal);
        var legDataByPosition = MapToLegDataByPosition(legDataByDate, optionLegById, requireOptionLeg: false);
        foreach (var position in tradePositions)
        {
            if (legDataByPosition.TryGetValue(MapToPositionKey(position), out var legData))
                position.AddOptionLegData(legData);
        }

        return tradePositions;
    }

    internal static (TradeType TradeType, DateOnly ValueDate, int DaysToExpiry, TradeStatus TradeStatus) MapToPositionKey(TradePositionReadModel position) => (position.TradeType, position.ValueDate, position.DaysToExpiry, position.TradeStatus);
    internal static Dictionary<(TradeType TradeType, DateOnly ValueDate, int DaysToExpiry, TradeStatus TradeStatus), List<OptionTradeLegDataReadModel>> MapToLegDataByPosition(IEnumerable<ICollection<OptionTradeLegDataReadModel>> legDataByDate, IReadOnlyDictionary<string, OptionTradeLegReadModel> optionLegById, bool requireOptionLeg)
    {
        var result = new Dictionary<(TradeType, DateOnly, int, TradeStatus), List<OptionTradeLegDataReadModel>>();
        foreach (var legDataForDate in legDataByDate)
        {
            foreach (var legData in legDataForDate)
            {
                OptionTradeLegReadModel? optionLeg;
                if (requireOptionLeg)
                    optionLeg = optionLegById[legData.OptionLegId];
                else
                    optionLegById.TryGetValue(legData.OptionLegId, out optionLeg);
                var hydratedLegData = legData.SetOptionLeg(optionLeg);
                var key = (legData.TradeType, legData.ValueDate, legData.DaysToExpiry, legData.TradeStatus);
                if (!result.TryGetValue(key, out var positionLegData))
                {
                    positionLegData = [];
                    result.Add(key, positionLegData);
                }

                positionLegData.Add(hydratedLegData);
            }
        }

        return result;
    }

    /// <inheritdoc/>
    public async Task<ICollection<string>> GetTradePositionTradeTypesAsync(int orderId, int tradeId, DateOnly valueDate, TradeStatus tradeStatus, int daysToExpiry) => [..(await (this.Use($"{nameof(TradeDbSql)}.{nameof(TradeDbSql.GetTradePositionsById)}", TradeDbSql.GetTradePositionsById)
            .SetParameters(new GetTradePositionsById(orderId, tradeId, valueDate, tradeStatus.ToStringFast(), daysToExpiry))
            .ExecuteQueryAsync(MapToTradePosition!))
            .ConfigureAwait(false)).Select(e => e.TradeType.ToStringFast())];
    /// <inheritdoc/>
    public async Task<ICollection<string>> GetTradePositionTradeTypesAsync(int orderId, int tradeId, DateOnly valueDate, TradeStatus tradeStatus, int daysToExpiry, CancellationToken cancellationToken) => [..(await (this.Use($"{nameof(TradeDbSql)}.{nameof(TradeDbSql.GetTradePositionsById)}", TradeDbSql.GetTradePositionsById)
            .SetParameters(new GetTradePositionsById(orderId, tradeId, valueDate, tradeStatus.ToStringFast(), daysToExpiry))
            .ExecuteQueryAsync(MapToTradePosition!, cancellationToken))
            .ConfigureAwait(false)).Select(e => e.TradeType.ToStringFast())];
    /// <inheritdoc/>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "MA0051", Justification = "Keep the transaction, resource ownership and failure ordering together during this provider migration; splitting it would obscure the atomic persistence boundary.")]
    public async Task<TradePositionReadModel?> GetTradePositionAsync(int orderId, int tradeId, TradeType tradeType, DateOnly valueDate, int daysToExpiry, TradeStatus tradeStatus)
    {
        var db = this;
        var tradePosition = await (GetTradePositionAsync())
            .ConfigureAwait(false);
        if (tradePosition is not null)
        {
            var e = tradePosition;
            var optionLegs = await (EnumerateOptionLegs())
            .ConfigureAwait(false);
            var updatedOptionLegData = new List<OptionTradeLegDataReadModel>();
            var optionLegData = await (EnumerateOptionLegData())
            .ConfigureAwait(false);
            foreach (var old in optionLegData)
            {
                updatedOptionLegData.Add((new OptionTradeLegDataReadModel(orderId: e.EntityId.OrderId, tradeId: e.EntityId.TradeId, tradeType: e.EntityId.TradeType, valueDate: e.EntityId.ValueDate, daysToExpiry: e.EntityId.DaysToExpiry, tradeStatus: e.EntityId.TradeStatus, optionLegId: old.OptionLegId, bidPrice: old.BidPrice, askPrice: old.AskPrice, impliedVolatility: old.ImpliedVolatility, delta: old.Delta, gamma: old.Gamma, theta: old.Theta, vega: old.Vega, rho: old.Rho, createdOn: old.CreatedOn, createdBy: old.CreatedBy, updatedOn: old.UpdatedOn, updatedBy: old.UpdatedBy)).SetOptionLeg(optionLegs.SingleOrDefault(o => string.Equals(o.ContractId, old.OptionLegId, StringComparison.Ordinal))));
            }

            tradePosition.AddOptionLegData(updatedOptionLegData);
        }

        return tradePosition;
        async Task<ICollection<OptionTradeLegReadModel>> EnumerateOptionLegs() => await (db.Use($"{nameof(TradeDbSql)}.{nameof(TradeDbSql.GetOptionLegsByOrderAndTrade)}", TradeDbSql.GetOptionLegsByOrderAndTrade)
            .SetParameters(new GetOptionLegsByOrderAndTrade(orderId, tradeId))
            .ExecuteQueryAsync(MapToOptionLeg!))
            .ConfigureAwait(false);
        async Task<ICollection<OptionTradeLegDataReadModel>> EnumerateOptionLegData() => [..(await (db.Use($"{nameof(TradeDbSql)}.{nameof(TradeDbSql.GetOptionLegData)}", TradeDbSql.GetOptionLegData)
            .SetParameters(new GetOptionLegData(orderId, tradeId, valueDate))
            .ExecuteQueryAsync(MapToOptionLegData!))
            .ConfigureAwait(false)).Where(e => e.TradeType == tradeType && e.TradeStatus == tradeStatus && e.DaysToExpiry == daysToExpiry)];
        async Task<TradePositionReadModel?> GetTradePositionAsync() => await (db.Use($"{nameof(TradeDbSql)}.{nameof(TradeDbSql.GetTradePosition)}", TradeDbSql.GetTradePosition)
            .SetParameters(new GetTradePosition(orderId, tradeId, valueDate, tradeStatus.ToStringFast(), daysToExpiry, tradeType.ToStringFast()))
            .ExecuteSingleAsync(MapToTradePosition!))
            .ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task<TradePositionReadModel?> GetTradePositionAsync(int orderId, int tradeId, TradeType tradeType, DateOnly valueDate, int daysToExpiry, TradeStatus tradeStatus, CancellationToken cancellationToken)
    {
        var db = this;
        var tradePosition = await (db.Use($"{nameof(TradeDbSql)}.{nameof(TradeDbSql.GetTradePosition)}", TradeDbSql.GetTradePosition)
            .SetParameters(new GetTradePosition(orderId, tradeId, valueDate, tradeStatus.ToStringFast(), daysToExpiry, tradeType.ToStringFast()))
            .ExecuteSingleAsync(MapToTradePosition!, cancellationToken))
            .ConfigureAwait(false);
        if (tradePosition is null)
            return null;
        var optionLegsTask = db.Use($"{nameof(TradeDbSql)}.{nameof(TradeDbSql.GetOptionLegsByOrderAndTrade)}", TradeDbSql.GetOptionLegsByOrderAndTrade)
            .SetParameters(new GetOptionLegsByOrderAndTrade(orderId, tradeId))
            .ExecuteQueryAsync(MapToOptionLeg!, cancellationToken);
        var optionLegDataTask = db.Use($"{nameof(TradeDbSql)}.{nameof(TradeDbSql.GetOptionLegData)}", TradeDbSql.GetOptionLegData)
            .SetParameters(new GetOptionLegData(orderId, tradeId, valueDate))
            .ExecuteQueryAsync(MapToOptionLegData!, cancellationToken);
        await (Task.WhenAll(optionLegsTask, optionLegDataTask))
            .ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        var optionLegById = (await (optionLegsTask)
            .ConfigureAwait(false)).ToDictionary(leg => leg.ContractId, StringComparer.Ordinal);
        var updatedOptionLegData = (await (optionLegDataTask)
            .ConfigureAwait(false)).Where(e => e.TradeType == tradeType && e.TradeStatus == tradeStatus && e.DaysToExpiry == daysToExpiry).Select(old => (new OptionTradeLegDataReadModel(orderId: tradePosition.EntityId.OrderId, tradeId: tradePosition.EntityId.TradeId, tradeType: tradePosition.EntityId.TradeType, valueDate: tradePosition.EntityId.ValueDate, daysToExpiry: tradePosition.EntityId.DaysToExpiry, tradeStatus: tradePosition.EntityId.TradeStatus, optionLegId: old.OptionLegId, bidPrice: old.BidPrice, askPrice: old.AskPrice, impliedVolatility: old.ImpliedVolatility, delta: old.Delta, gamma: old.Gamma, theta: old.Theta, vega: old.Vega, rho: old.Rho, createdOn: old.CreatedOn, createdBy: old.CreatedBy, updatedOn: old.UpdatedOn, updatedBy: old.UpdatedBy)).SetOptionLeg(optionLegById.GetValueOrDefault(old.OptionLegId))).ToArray();
        tradePosition.AddOptionLegData(updatedOptionLegData);
        return tradePosition;
    }

    /// <inheritdoc/>
    public async Task<ICollection<TradeHistoryReadModel>> GetTradeHistoryAsync(int orderId) => [..(await (this.Use($"{nameof(TradeDbSql)}.{nameof(TradeDbSql.GetTradeHistory)}", TradeDbSql.GetTradeHistory)
            .SetParameters(new GetTradeHistory(orderId))
            .ExecuteQueryAsync(MapToTradeHistory!))
            .ConfigureAwait(false)).OrderBy(e => e.ValueDate)];
    /// <inheritdoc/>
    public async Task<ICollection<TradeHistoryReadModel>> GetTradeHistoryAsync(int orderId, CancellationToken cancellationToken) => [..(await (this.Use($"{nameof(TradeDbSql)}.{nameof(TradeDbSql.GetTradeHistory)}", TradeDbSql.GetTradeHistory)
            .SetParameters(new GetTradeHistory(orderId))
            .ExecuteQueryAsync(MapToTradeHistory!, cancellationToken))
            .ConfigureAwait(false)).OrderBy(e => e.ValueDate)];
    /// <inheritdoc/>
    public async Task<ICollection<string>> GetOptionLegContractIdsAsync(int tradeId)
    {
        var optionLegs = await (this.Use($"{nameof(TradeDbSql)}.{nameof(TradeDbSql.GetOptionLegs)}", TradeDbSql.GetOptionLegs)
            .SetParameters(new GetOptionLegs(tradeId))
            .ExecuteQueryAsync(MapToOptionLeg!))
            .ConfigureAwait(false);
        var latestTradeLegs = optionLegs.GroupBy(e => e.OrderId).OrderByDescending(e => e.Max(leg => leg.UpdatedOn)).FirstOrDefault();
        return latestTradeLegs is null ? [] : [..latestTradeLegs.Select(e => e.ContractId)];
    }

    /// <inheritdoc/>
    public async Task<ICollection<string>> GetOptionLegContractIdsAsync(int tradeId, CancellationToken cancellationToken) => [..(await (this.Use($"{nameof(TradeDbSql)}.{nameof(TradeDbSql.GetOptionLegs)}", TradeDbSql.GetOptionLegs)
            .SetParameters(new GetOptionLegs(tradeId))
            .ExecuteQueryAsync(MapToOptionLeg!, cancellationToken))
            .ConfigureAwait(false)).Select(e => e.ContractId)];
    /// <inheritdoc/>
    public async Task<ICollection<OptionTradeLegReadModel>> GetOptionLegsAsync() => await (this.Use($"{nameof(TradeDbSql)}.{nameof(TradeDbSql.GetOptionLegsAll)}", TradeDbSql.GetOptionLegsAll)
            .ExecuteQueryAsync(MapToOptionLeg!))
            .ConfigureAwait(false);
    /// <inheritdoc/>
    public async Task<ICollection<OptionTradeLegDataReadModel>> GetOptionLegDataAsync() => await (this.Use($"{nameof(TradeDbSql)}.{nameof(TradeDbSql.GetOptionLegDataAll)}", TradeDbSql.GetOptionLegDataAll)
            .ExecuteQueryAsync(MapToOptionLegData!))
            .ConfigureAwait(false);
    /// <inheritdoc/>
    public async Task<int> GetTradeQuantityAsync(int tradeId)
    {
        var db = this;
        var optionLegs = await (db.Use($"{nameof(TradeDbSql)}.{nameof(TradeDbSql.GetOptionLegs)}", TradeDbSql.GetOptionLegs)
            .SetParameters(new GetOptionLegs(tradeId))
            .ExecuteQueryAsync(MapToOptionLeg!))
            .ConfigureAwait(false);
        var latestTradeLegs = optionLegs.GroupBy(e => e.OrderId).OrderByDescending(e => e.Max(leg => leg.UpdatedOn)).FirstOrDefault();
        return latestTradeLegs is not null ? latestTradeLegs.Sum(e => e.Quantity) / latestTradeLegs.Count() : 0;
    }

    /// <inheritdoc/>
    public async Task<int> GetTradeQuantityAsync(int tradeId, CancellationToken cancellationToken)
    {
        var optionLegs = await (this.Use($"{nameof(TradeDbSql)}.{nameof(TradeDbSql.GetOptionLegs)}", TradeDbSql.GetOptionLegs)
            .SetParameters(new GetOptionLegs(tradeId))
            .ExecuteQueryAsync(MapToOptionLeg!, cancellationToken))
            .ConfigureAwait(false);
        var latestTradeLegs = optionLegs.GroupBy(e => e.OrderId).OrderByDescending(e => e.Max(leg => leg.UpdatedOn)).FirstOrDefault();
        return latestTradeLegs is not null ? latestTradeLegs.Sum(e => e.Quantity) / latestTradeLegs.Count() : 0;
    }

    /// <inheritdoc/>
    public async Task<TradeLimitReadModel?> GetTradeLimitAsync(int tradeId)
    {
        return (await (this.NativeTransaction().ExecuteOnceAsync((db, ct) => TradeDbContextExtensions.TradeReadLimitsAsync(db, tradeId, ct)))
            .ConfigureAwait(false)).SingleOrDefault();
    }

    /// <inheritdoc/>
    public async Task<TradeLimitReadModel?> GetTradeLimitAsync(int tradeId, CancellationToken cancellationToken)
    {
        return (await (this.NativeTransaction().ExecuteOnceAsync((db, ct) => TradeDbContextExtensions.TradeReadLimitsAsync(db, tradeId, ct), cancellationToken))
            .ConfigureAwait(false)).SingleOrDefault();
    }

    /// <inheritdoc/>
    public async Task<ICollection<TradeLimitReadModel>> GetTradeLimitsAsync()
    {
        return (await (this.NativeTransaction().ExecuteOnceAsync((db, ct) => TradeDbContextExtensions.TradeReadLimitsAsync(db, null, ct)))
            .ConfigureAwait(false)).ToArray();
    }

    /// <inheritdoc/>
    public async Task<TradeTypeLimitReadModel?> GetTradeTypeLimitAsync(int tradeId, TradeType tradeType)
    {
        return (await (this.NativeTransaction().ExecuteOnceAsync((db, ct) => TradeDbContextExtensions.TradeReadSpreadLimitsAsync(db, tradeId, tradeType, ct)))
            .ConfigureAwait(false)).SingleOrDefault();
    }

    /// <inheritdoc/>
    public async Task<TradeTypeLimitReadModel?> GetTradeTypeLimitAsync(int tradeId, TradeType tradeType, CancellationToken cancellationToken)
    {
        return (await (this.NativeTransaction().ExecuteOnceAsync((db, ct) => TradeDbContextExtensions.TradeReadSpreadLimitsAsync(db, tradeId, tradeType, ct), cancellationToken))
            .ConfigureAwait(false)).SingleOrDefault();
    }

    /// <inheritdoc/>
    public async Task<ICollection<TradeTypeLimitReadModel>> GetTradeTypeLimitsAsync(int tradeId)
    {
        return (await (this.NativeTransaction().ExecuteOnceAsync((db, ct) => TradeDbContextExtensions.TradeReadSpreadLimitsAsync(db, tradeId, null, ct)))
            .ConfigureAwait(false)).ToArray();
    }

    /// <inheritdoc/>
    public async Task<ICollection<TradeTypeLimitReadModel>> GetTradeTypeLimitsAsync()
    {
        return (await (this.NativeTransaction().ExecuteOnceAsync((db, ct) => TradeDbContextExtensions.TradeReadSpreadLimitsAsync(db, null, null, ct)))
            .ConfigureAwait(false)).ToArray();
    }

    /// <inheritdoc/>
    public async Task<TradePlacementSignalReadModel?> GetTradePlacementSignalAsync(string contractId, DateOnly valueDate) => await (this.Use($"{nameof(TradeDbSql)}.{nameof(TradeDbSql.GetTradePlacementSignal)}", TradeDbSql.GetTradePlacementSignal)
            .SetParameters(new GetTradePlacementSignal(contractId, valueDate))
            .ExecuteSingleAsync(MapToTradePlacementSignal!))
            .ConfigureAwait(false);
    /// <inheritdoc/>
    public async Task<ICollection<TradeFillReadModel>> GetTradeFillsAsync() => await (this.Use($"{nameof(TradeDbSql)}.{nameof(TradeDbSql.GetTradeFillsAll)}", TradeDbSql.GetTradeFillsAll)
            .ExecuteQueryAsync(MapToTradeFill!))
            .ConfigureAwait(false);
    /// <inheritdoc/>
    public async Task<ICollection<TradeFillReadModel>> GetTradeFillsAsync(int orderId, int tradeId)
    {
        var db = this;
        var tradeFills = await (db.Use($"{nameof(TradeDbSql)}.{nameof(TradeDbSql.GetTradeFills)}", TradeDbSql.GetTradeFills)
            .SetParameters(new GetTradeFills(orderId, tradeId))
            .ExecuteQueryAsync(MapToTradeFill!))
            .ConfigureAwait(false);
        if (tradeFills.Count > 0)
            foreach (var tf in tradeFills)
            {
                var tradeFillData = await (db.Use($"{nameof(TradeDbSql)}.{nameof(TradeDbSql.GetTradeFillData)}", TradeDbSql.GetTradeFillData)
            .SetParameters(new GetTradeFillData(orderId, tradeId, tf.FillDate))
            .ExecuteQueryAsync(MapToTradeFillData!))
            .ConfigureAwait(false);
                if (tradeFillData.Count > 0)
                    tf.AddTradeFillData(tradeFillData);
            }

        return tradeFills;
    }

    /// <inheritdoc/>
    public async Task<ICollection<TradeFillReadModel>> GetTradeFillsAsync(int orderId, int tradeId, CancellationToken cancellationToken)
    {
        var db = this;
        var tradeFills = await (db.Use($"{nameof(TradeDbSql)}.{nameof(TradeDbSql.GetTradeFills)}", TradeDbSql.GetTradeFills)
            .SetParameters(new GetTradeFills(orderId, tradeId))
            .ExecuteQueryAsync(MapToTradeFill!, cancellationToken))
            .ConfigureAwait(false);
        foreach (var tradeFill in tradeFills)
        {
            var tradeFillData = await (db.Use($"{nameof(TradeDbSql)}.{nameof(TradeDbSql.GetTradeFillData)}", TradeDbSql.GetTradeFillData)
            .SetParameters(new GetTradeFillData(orderId, tradeId, tradeFill.FillDate))
            .ExecuteQueryAsync(MapToTradeFillData!, cancellationToken))
            .ConfigureAwait(false);
            if (tradeFillData.Count > 0)
                tradeFill.AddTradeFillData(tradeFillData);
        }

        return tradeFills;
    }

    /// <inheritdoc/>
    public async Task<ICollection<TradePlanReadModel>> GetTradePlansAsync(int orderId) => await (this.Use($"{nameof(TradeDbSql)}.{nameof(TradeDbSql.GetTradePlans)}", TradeDbSql.GetTradePlans)
            .SetParameters(new GetTradePlans(orderId))
            .ExecuteQueryAsync(MapToTradePlan!))
            .ConfigureAwait(false);
    /// <inheritdoc/>
    public async Task<ICollection<TradePlanReadModel>> GetTradePlansAsync() => await (this.Use($"{nameof(TradeDbSql)}.{nameof(TradeDbSql.GetTradePlansAll)}", TradeDbSql.GetTradePlansAll)
            .ExecuteQueryAsync(MapToTradePlan!))
            .ConfigureAwait(false);
    /// <inheritdoc/>
    public async Task<ICollection<TradePlanReadModel>> GetTradePlansAsync(int orderId, int tradeId, DateOnly valueDate) => await (this.Use($"{nameof(TradeDbSql)}.{nameof(TradeDbSql.GetTradePlansByValueDate)}", TradeDbSql.GetTradePlansByValueDate)
            .SetParameters(new GetTradePlansByTradeId(orderId, tradeId, valueDate))
            .ExecuteQueryAsync(MapToTradePlan!))
            .ConfigureAwait(false);
    /// <inheritdoc/>
    public async Task<ICollection<TradePlanReadModel>> GetTradePlansAsync(int orderId, int tradeId, DateOnly startDate, DateOnly endDate) => await (this.Use($"{nameof(TradeDbSql)}.{nameof(TradeDbSql.GetTradePlansByDateRange)}", TradeDbSql.GetTradePlansByDateRange)
            .SetParameters(new GetTradePlansByDateRange(orderId, tradeId, startDate, endDate))
            .ExecuteQueryAsync(MapToTradePlan!))
            .ConfigureAwait(false);
    /// <inheritdoc/>
    public async Task<ICollection<TradePlanReadModel>> GetLastTradePlansAsync(int orderId, int tradeId) => await (this.Use($"{nameof(TradeDbSql)}.{nameof(TradeDbSql.GetLastTradePlans)}", TradeDbSql.GetLastTradePlans)
            .SetParameters(new GetLastTradePlans(orderId, tradeId))
            .ExecuteQueryAsync(MapToTradePlan!))
            .ConfigureAwait(false);
    /// <inheritdoc/>
    public async Task<TradePlanStopLossLimitReadModel?> GetTradePlanStopLossLimitAsync(int orderId, int tradeId) => await (this.Use($"{nameof(TradeDbSql)}.{nameof(TradeDbSql.GetTradePlanStopLossLimit)}", TradeDbSql.GetTradePlanStopLossLimit)
            .SetParameters(new GetTradePlanStopLossLimit(orderId, tradeId))
            .ExecuteSingleAsync(MapToTradePlanStopLossLimit!))
            .ConfigureAwait(false);
    /// <inheritdoc/>
    public async Task<ICollection<TradePlanForwardLossRatioReadModel>> GetTradePlanForwardLossRatiosAsync(DateOnly startDate, DateOnly endDate) => await (this.Use($"{nameof(TradeDbSql)}.{nameof(TradeDbSql.GetTradePlanForwardLossRatios)}", TradeDbSql.GetTradePlanForwardLossRatios)
            .SetParameters(new GetTradePlanForwardLossRatios(startDate, endDate))
            .ExecuteQueryAsync(MapToTradePlanForwardLossRatio!))
            .ConfigureAwait(false);
    /// <inheritdoc/>
    public async Task<TradePlanForwardLossRatioReadModel?> GetTradePlanForwardLossRatioAsync(DateOnly valueDate)
    {
        var rows = await (this.Use($"{nameof(TradeDbSql)}.{nameof(TradeDbSql.GetLastTradePlanForwardLossRatio)}", TradeDbSql.GetLastTradePlanForwardLossRatio)
            .SetParameters(new GetLastTradePlanForwardLossRatio(valueDate))
            .ExecuteQueryAsync(MapToTradePlanForwardLossRatioRow!))
            .ConfigureAwait(false);
        var latest = rows.MaxBy(row => row.SequenceId);
        return latest is null ? null : new TradePlanForwardLossRatioReadModel(latest.ForwardLossRatio);
    }

    /// <inheritdoc/>
    public async Task<TradePlanForwardLossLimitReadModel?> GetTradePlanForwardLossLimitAsync(int orderId, int tradeId, DateOnly valueDate, TradeType tradeType) => await (this.Use($"{nameof(TradeDbSql)}.{nameof(TradeDbSql.GetTradePlanForwardLossLimit)}", TradeDbSql.GetTradePlanForwardLossLimit)
            .SetParameters(new GetTradePlanForwardLossLimit(orderId, tradeId, valueDate, tradeType.ToStringFast()))
            .ExecuteSingleAsync(MapToTradePlanForwardLossLimit!))
            .ConfigureAwait(false);
    /// <inheritdoc/>
    public async Task<ICollection<TradeLiveFeedReadModel>> GetTradeLiveFeedAsync(int orderId, int tradeId) => await (this.Use($"{nameof(TradeDbSql)}.{nameof(TradeDbSql.GetTradeLiveFeed)}", TradeDbSql.GetTradeLiveFeed)
            .SetParameters(new GetTradeLiveFeed(orderId, tradeId))
            .ExecuteQueryAsync(MapToTradeLiveFeed!))
            .ConfigureAwait(false);
    /// <inheritdoc/>
    public async Task<ICollection<TradeFillDataReadModel>> GetTradeFillDataAsync(int tradeId) => await (this.Use($"{nameof(TradeDbSql)}.{nameof(TradeDbSql.GetTradeFillDataByTradeId)}", TradeDbSql.GetTradeFillDataByTradeId)
            .SetParameters(new GetTradeFillDataByTradeId(tradeId))
            .ExecuteQueryAsync(MapToTradeFillData!))
            .ConfigureAwait(false);
    /// <inheritdoc/>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1725", Justification = "Preserve the established storage context contract, including public named arguments and default values used by callers.")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S927", Justification = "Preserve the established storage context contract, including public named arguments and default values used by callers.")]
    public Task InsertOptionLegAsync(OptionTradeLegReadModel e) => this.InsertOptionLegAsyncCoreBinding(e);
    /// <inheritdoc/>
    public async Task InsertOptionLegsAsync(ICollection<OptionTradeLegReadModel> optionLegs) => await (this.Use($"{nameof(TradeDbSql)}.{nameof(TradeDbSql.InsertOptionLeg)}", TradeDbSql.InsertOptionLeg)
            .SetParameters(optionLegs.Select(e => new InsertOptionLeg(e.OrderId, e.TradeId, e.ContractId, e.Quantity, e.StrikePrice, e.OptionLegType.ToStringFast(), e.OptionLegAction.ToStringFast(), DateTime.Now, e.CreatedBy, DateTime.Now, e.CreatedBy)))
            .ExecuteCommandAsync())
            .ConfigureAwait(false);
    /// <inheritdoc/>
    public Task<long> InsertOptionLegsAsync(IEnumerable<OptionTradeLegReadModel> optionLegs) => this.InsertOptionLegsAsyncCoreBinding(optionLegs);
    /// <inheritdoc/>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1725", Justification = "Preserve the established storage context contract, including public named arguments and default values used by callers.")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S927", Justification = "Preserve the established storage context contract, including public named arguments and default values used by callers.")]
    public Task InsertOptionLegDataAsync(OptionTradeLegDataReadModel e) => this.InsertOptionLegDataAsyncCoreBinding(e);
    /// <inheritdoc/>
    public async Task InsertOptionLegDataAsync(ICollection<OptionTradeLegDataReadModel> optionLegData) => await (this.Use($"{nameof(TradeDbSql)}.{nameof(TradeDbSql.InsertOptionLegData)}", TradeDbSql.InsertOptionLegData)
            .SetParameters(optionLegData.Select(e => new InsertOptionLegData(e.OrderId, e.TradeId, e.TradeType.ToStringFast(), e.ValueDate, e.DaysToExpiry, e.TradeStatus.ToStringFast(), e.OptionLegId, e.BidPrice, e.AskPrice, e.ImpliedVolatility, e.Delta, e.Gamma, e.Theta, e.Vega, e.Rho, DateTime.Now, e.CreatedBy, DateTime.Now, e.CreatedBy)))
            .ExecuteCommandAsync())
            .ConfigureAwait(false);
    /// <inheritdoc/>
    public Task<long> InsertOptionLegDataAsync(IEnumerable<OptionTradeLegDataReadModel> optionLegData) => this.InsertOptionLegDataAsyncCoreBinding(optionLegData);
    /// <inheritdoc/>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "MA0051", Justification = "Keep the transaction, resource ownership and failure ordering together during this provider migration; splitting it would obscure the atomic persistence boundary.")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1725", Justification = "Preserve the established storage context contract, including public named arguments and default values used by callers.")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S927", Justification = "Preserve the established storage context contract, including public named arguments and default values used by callers.")]
    public Task InsertOptionTradeAsync(OptionTradeReadModel e) => this.InsertOptionTradeAsyncCoreBinding(e);
    /// <inheritdoc/>
    public async Task InsertOptionTradesAsync(ICollection<OptionTradeReadModel> optionTrades) => await (this.Use($"{nameof(TradeDbSql)}.{nameof(TradeDbSql.InsertOptionTrade)}", TradeDbSql.InsertOptionTrade)
            .SetParameters(optionTrades.Select(e => new InsertOptionTrade(e.TradeId, e.OrderId, e.TradeDate, e.MaturityDate, e.TradeType.ToStringFast(), e.TradeState.ToStringFast(), e.TradeStrategy ?? string.Empty, e.TradeAction.ToStringFast(), e.UnderlyingContractId, e.UnderlyingAssetType.ToStringFast(), e.IsPrimaryTrade, e.IsHedgeTrade, DateTime.Now, e.CreatedBy, DateTime.Now, e.CreatedBy)))
            .ExecuteCommandAsync())
            .ConfigureAwait(false);
    /// <inheritdoc/>
    public Task<long> InsertOptionTradesAsync(IEnumerable<OptionTradeReadModel> optionTrades) => this.InsertOptionTradesAsyncCoreBinding(optionTrades);
    /// <inheritdoc/>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "MA0051", Justification = "Keep the transaction, resource ownership and failure ordering together during this provider migration; splitting it would obscure the atomic persistence boundary.")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1725", Justification = "Preserve the established storage context contract, including public named arguments and default values used by callers.")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S927", Justification = "Preserve the established storage context contract, including public named arguments and default values used by callers.")]
    public Task InsertTradePositionAsync(TradePositionReadModel e) => this.InsertTradePositionAsyncCoreBinding(e);
    /// <inheritdoc/>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "MA0051", Justification = "Keep the transaction, resource ownership and failure ordering together during this provider migration; splitting it would obscure the atomic persistence boundary.")]
    public Task InsertTradePositionAsync(ICollection<TradePositionReadModel> tradePositions) => this.InsertTradePositionAsyncCoreBinding(tradePositions);
    /// <inheritdoc/>
    public async Task InsertTradePositionsAsync(ICollection<TradePositionReadModel> tradePositions) => await (this.Use($"{nameof(TradeDbSql)}.{nameof(TradeDbSql.InsertTradePosition)}", TradeDbSql.InsertTradePosition)
            .SetParameters(tradePositions.Select(e => new InsertTradePosition(e.OrderId, e.TradeId, e.TradeType.ToStringFast(), e.ValueDate, e.DaysToExpiry, e.TradeStatus.ToStringFast(), e.Commission, e.DeltaHedge, e.NetSpread, e.TradeValue, e.TradePnl, e.AssetPrice, e.OTMProbability, e.ForwardPrice, e.ForwardLossRatio, e.LossProbability, e.RiskFreeRate, DateTime.Now, e.CreatedBy, DateTime.Now, e.UpdatedBy)))
            .ExecuteCommandAsync())
            .ConfigureAwait(false);
    /// <inheritdoc/>
    public Task<long> InsertTradePositionsAsync(IEnumerable<TradePositionReadModel> tradePositions) => this.InsertTradePositionsAsyncCoreBinding(tradePositions);
    /// <inheritdoc/>
    public Task InsertTradeFillAsync(ICollection<TradeFillReadModel> tradeFills) => this.InsertTradeFillAsyncCoreBinding(tradeFills);
    /// <inheritdoc/>
    public async Task InsertTradeFillsAsync(ICollection<TradeFillReadModel> tradeFills) => await (this.Use($"{nameof(TradeDbSql)}.{nameof(TradeDbSql.InsertTradeFill)}", TradeDbSql.InsertTradeFill)
            .SetParameters(tradeFills.Select(e => new InsertTradeFill(e.OrderId, e.TradeId, e.FillDate, e.FillQuantity, e.CreatedOn, e.CreatedBy)))
            .ExecuteCommandAsync())
            .ConfigureAwait(false);
    /// <inheritdoc/>
    public Task<long> InsertTradeFillsAsync(IEnumerable<TradeFillReadModel> tradeFills) => this.InsertTradeFillsAsyncCoreBinding(tradeFills);
    /// <inheritdoc/>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1725", Justification = "Preserve the established storage context contract, including public named arguments and default values used by callers.")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S927", Justification = "Preserve the established storage context contract, including public named arguments and default values used by callers.")]
    public Task InsertOptionTradeSpreadDataAsync(OptionTradeSpreadsDataModel e) => this.InsertOptionTradeSpreadDataAsyncCoreBinding(e);
    /// <inheritdoc/>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1725", Justification = "Preserve the established storage context contract, including public named arguments and default values used by callers.")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S927", Justification = "Preserve the established storage context contract, including public named arguments and default values used by callers.")]
    public Task InsertOptionTradeSpreadDataAsync(ICollection<OptionTradeSpreadsDataModel> optionTradeSpreadsData) => this.InsertOptionTradeSpreadDataAsyncCoreBinding(optionTradeSpreadsData);
    /// <inheritdoc/>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1725", Justification = "Preserve the established storage context contract, including public named arguments and default values used by callers.")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S927", Justification = "Preserve the established storage context contract, including public named arguments and default values used by callers.")]
    public Task<long> InsertOptionTradeSpreadDataAsync(IEnumerable<OptionTradeSpreadsDataModel> optionTradeSpreadsData) => this.InsertOptionTradeSpreadDataAsyncCoreBinding(optionTradeSpreadsData);
    /// <inheritdoc/>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1725", Justification = "Preserve the established storage context contract, including public named arguments and default values used by callers.")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S927", Justification = "Preserve the established storage context contract, including public named arguments and default values used by callers.")]
    public Task InsertOptionTradeSpreadBarDataAsync(OptionTradeSpreadBarsDataModel e) => this.InsertOptionTradeSpreadBarDataAsyncCoreBinding(e);
    /// <inheritdoc/>
    public async Task InsertOptionTradeSpreadBarDataAsync(ICollection<OptionTradeSpreadBarsDataModel> optionTradeSpreadBarsData) => await (this.Use($"{nameof(TradeDbSql)}.{nameof(TradeDbSql.InsertOptionTradeSpreadBarData)}", TradeDbSql.InsertOptionTradeSpreadBarData)
            .SetParameters(optionTradeSpreadBarsData.Select(e => new InsertOptionTradeSpreadBarData(e.OrderId, e.TradeId, e.TradeType.ToStringFast(), e.ValueDate, e.BarDate, e.LossLimit, e.WinLimit, e.ForwardSpread, e.NetSpread)))
            .ExecuteCommandAsync())
            .ConfigureAwait(false);
    /// <inheritdoc/>
    public Task<long> InsertOptionTradeSpreadBarDataAsync(IEnumerable<OptionTradeSpreadBarsDataModel> optionTradeSpreadBarsData) => this.InsertOptionTradeSpreadBarDataAsyncCoreBinding(optionTradeSpreadBarsData);
    /// <inheritdoc/>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1725", Justification = "Preserve the established storage context contract, including public named arguments and default values used by callers.")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S927", Justification = "Preserve the established storage context contract, including public named arguments and default values used by callers.")]
    public Task InsertTradeLiveFeedAsync(TradeLiveFeedReadModel e) => this.InsertTradeLiveFeedAsyncCoreBinding(e);
    /// <inheritdoc/>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1725", Justification = "Preserve the established storage context contract, including public named arguments and default values used by callers.")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S927", Justification = "Preserve the established storage context contract, including public named arguments and default values used by callers.")]
    public async Task InsertTradePositionStateAsync(TradePositionStateReadModel e) => await (this.Use($"{nameof(TradeDbSql)}.{nameof(TradeDbSql.InsertTradePositionState)}", TradeDbSql.InsertTradePositionState)
            .SetParameters(new InsertTradePositionState(e.OrderId, e.TradeId, e.TradePositionState.ToStringFast(), e.OpenedOn, e.OpenedBy))
            .ExecuteCommandAsync())
            .ConfigureAwait(false);
    /// <inheritdoc/>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1725", Justification = "Preserve the established storage context contract, including public named arguments and default values used by callers.")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S927", Justification = "Preserve the established storage context contract, including public named arguments and default values used by callers.")]
    public Task InsertTradePlanForwardLossLimitAsync(TradePlanForwardLossLimitReadModel e) => this.InsertTradePlanForwardLossLimitAsyncCoreBinding(e);
    /// <inheritdoc/>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1725", Justification = "Preserve the established storage context contract, including public named arguments and default values used by callers.")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S927", Justification = "Preserve the established storage context contract, including public named arguments and default values used by callers.")]
    public async Task InsertTradePlanForwardLossRatioAsync(DateOnly valueDate, double forwardLossRatio)
    {
        var sequenceId = await (_sequenceIdGenerator.GetSequenceIdAsync(SequenceName.TradePlan_SequenceId))
            .ConfigureAwait(false);
        var db = this;
        await (db.Use($"{nameof(TradeDbSql)}.{nameof(TradeDbSql.InsertTradePlanForwardLossRatio)}", TradeDbSql.InsertTradePlanForwardLossRatio)
            .SetParameters(new InsertTradePlanForwardLossRatio(1, valueDate, forwardLossRatio, sequenceId))
            .ExecuteCommandAsync())
            .ConfigureAwait(false);
    }

    /// <inheritdoc/>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1725", Justification = "Preserve the established storage context contract, including public named arguments and default values used by callers.")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S927", Justification = "Preserve the established storage context contract, including public named arguments and default values used by callers.")]
    public Task<long> InsertTradePlacementSignalAsync(TradePlacementSignalReadModel e) => this.InsertTradePlacementSignalAsyncCoreBinding(e);
    /// <inheritdoc/>
    public async Task UpdateTradePositionAsync(TradePositionEntityId key, decimal commission, int deltaHedge, decimal netSpread, decimal tradeValue, decimal tradePnl, decimal assetPrice, double otmProbability, double winRatio, decimal maxPrice, double hedgeProbability, double riskFreeRate, DateTime updatedOn, string updatedBy) => await (this.Use($"{nameof(TradeDbSql)}.{nameof(TradeDbSql.UpdateTradePosition)}", TradeDbSql.UpdateTradePosition)
            .SetParameters(new UpdateTradePosition(key.OrderId, key.TradeId, key.ValueDate, key.TradeStatus.ToStringFast(), key.DaysToExpiry, key.TradeType.ToStringFast(), commission, deltaHedge, netSpread, tradeValue, tradePnl, assetPrice, otmProbability, winRatio, maxPrice, hedgeProbability, riskFreeRate, updatedOn, updatedBy))
            .ExecuteCommandAsync())
            .ConfigureAwait(false);
    /// <inheritdoc/>
    public async Task UpdateOptionTradeStateAsync(int orderId, int tradeId, TradeState tradeState, DateTime updatedOn, string updatedBy) => await (this.Use($"{nameof(TradeDbSql)}.{nameof(TradeDbSql.UpdateOptionTradeState)}", TradeDbSql.UpdateOptionTradeState)
            .SetParameters(new UpdateOptionTradeState(orderId, tradeId, tradeState.ToStringFast(), updatedOn, updatedBy))
            .ExecuteCommandAsync())
            .ConfigureAwait(false);
    /// <inheritdoc/>
    public Task UpdateTradeLiveFeedAsync(TradeLiveFeedReadModel tradeLiveFeed) => this.UpdateTradeLiveFeedAsyncCoreBinding(tradeLiveFeed);
    /// <inheritdoc/>
    public async Task UpdateTradePositionStatusAsync(int orderId, int tradeId, TradeType tradeType, DateOnly valueDate, int daysToExpiry, TradeStatus oldTradeStatus, TradeStatus newTradeStatus, DateTime updatedOn, string updatedBy)
    {
        var db = this;
        var dbTrade = db;
        var tradePosition = await (dbTrade.GetTradePositionAsync(orderId, tradeId, tradeType, valueDate, daysToExpiry, oldTradeStatus))
            .ConfigureAwait(false);
        if (tradePosition is null)
            return;
        var updatedTradePosition = tradePosition with
        {
            TradeStatus = newTradeStatus,
            UpdatedOn = updatedOn,
            UpdatedBy = updatedBy
        };
        await (dbTrade.DeleteTradePositionAsync(orderId, tradeId, tradeType, valueDate, daysToExpiry, oldTradeStatus))
            .ConfigureAwait(false);
        await (dbTrade.InsertTradePositionAsync(updatedTradePosition))
            .ConfigureAwait(false);
        var optionLegData = await (db.Use($"{nameof(TradeDbSql)}.{nameof(TradeDbSql.GetOptionLegData)}", TradeDbSql.GetOptionLegData)
            .SetParameters(new GetOptionLegData(orderId, tradeId, valueDate))
            .ExecuteQueryAsync(MapToOptionLegData!))
            .ConfigureAwait(false);
        foreach (var o in optionLegData.Where(e => e.TradeType == tradeType && e.DaysToExpiry == daysToExpiry && e.TradeStatus == oldTradeStatus))
        {
            await (db.Use($"{nameof(TradeDbSql)}.{nameof(TradeDbSql.UpdateOptionLegDataStatus)}", TradeDbSql.UpdateOptionLegDataStatus)
            .SetParameters(new UpdateOptionLegDataStatus(tradeId, valueDate, o.OptionLegId, newTradeStatus.ToStringFast(), updatedOn, updatedBy))
            .ExecuteCommandAsync())
            .ConfigureAwait(false);
        }
    }

    /// <inheritdoc/>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1725", Justification = "Preserve the established storage context contract, including public named arguments and default values used by callers.")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S927", Justification = "Preserve the established storage context contract, including public named arguments and default values used by callers.")]
    public Task UpdateOptionLegDataAsync(OptionTradeLegDataReadModel e) => this.UpdateOptionLegDataAsyncCoreBinding(e);
    /// <inheritdoc/>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1725", Justification = "Preserve the established storage context contract, including public named arguments and default values used by callers.")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S927", Justification = "Preserve the established storage context contract, including public named arguments and default values used by callers.")]
    public Task InsertTradeLimitAsync(TradeLimitReadModel e) => this.InsertTradeLimitAsyncCoreBinding(e);
    /// <inheritdoc/>
    public async Task InsertTradeLimitsAsync(ICollection<TradeLimitReadModel> tradeLimits) => await (this.Use($"{nameof(TradeDbSql)}.{nameof(TradeDbSql.InsertTradeLimit)}", TradeDbSql.InsertTradeLimit)
            .SetParameters(tradeLimits.Select(e => new InsertTradeLimit(e.TradeId, e.TradeType.ToStringFast(), e.RiskMargin, e.MaxProfit, e.MaxLoss, e.MaxReturn, e.MaxLossLimit, e.MinProfitLimit, e.MaxProfitLimit, e.MinProfitTarget, e.DailyProfitTarget, e.CreatedOn, e.CreatedBy, e.UpdatedOn, e.UpdatedBy)))
            .ExecuteCommandAsync())
            .ConfigureAwait(false);
    /// <inheritdoc/>
    public Task<long> InsertTradeLimitsAsync(IEnumerable<TradeLimitReadModel> tradeLimits) => this.InsertTradeLimitsAsyncCoreBinding(tradeLimits);
    /// <inheritdoc/>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1725", Justification = "Preserve the established storage context contract, including public named arguments and default values used by callers.")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S927", Justification = "Preserve the established storage context contract, including public named arguments and default values used by callers.")]
    public Task InsertTradeTypeLimitAsync(TradeTypeLimitReadModel e) => this.InsertTradeTypeLimitAsyncCoreBinding(e);
    /// <inheritdoc/>
    public async Task InsertTradeTypeLimitsAsync(ICollection<TradeTypeLimitReadModel> tradeTypeLimits)
    {
        await (this.NativeTransaction().ExecuteOnceAsync(async (db, ct) =>
        {
            await (TradeDbContextExtensions.TradeFlowProjectSpreadLimitsAsync(db, tradeTypeLimits.ToArray(), ct))
            .ConfigureAwait(false);
            return true;
        }))
            .ConfigureAwait(false);
        return;
    }

    /// <inheritdoc/>
    public Task<long> InsertTradeTypeLimitsAsync(IEnumerable<TradeTypeLimitReadModel> tradeTypeLimits) => this.InsertTradeTypeLimitsAsyncCoreBinding(tradeTypeLimits);
    /// <inheritdoc/>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "MA0051", Justification = "Keep the transaction, resource ownership and failure ordering together during this provider migration; splitting it would obscure the atomic persistence boundary.")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1725", Justification = "Preserve the established storage context contract, including public named arguments and default values used by callers.")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S927", Justification = "Preserve the established storage context contract, including public named arguments and default values used by callers.")]
    public Task InsertTradePlanAsync(TradePlanReadModel e) => this.InsertTradePlanAsyncCoreBinding(e);
    /// <inheritdoc/>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "MA0051", Justification = "Keep the transaction, resource ownership and failure ordering together during this provider migration; splitting it would obscure the atomic persistence boundary.")]
    public Task InsertTradePlansAsync(ICollection<TradePlanByIdReadModel> tradePlans) => this.InsertTradePlansAsyncCoreBinding(tradePlans);
    /// <inheritdoc/>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "MA0051", Justification = "Keep the transaction, resource ownership and failure ordering together during this provider migration; splitting it would obscure the atomic persistence boundary.")]
    public async Task<long> InsertTradePlansAsync(IEnumerable<TradePlanByIdReadModel> tradePlans)
    {
        var rowCount = 0L;
        var insertTradePlansTask = InsertTradePlans();
        var insertForwardLossRatiosTask = InsertTradePlanForwardLossRatio();
        await Task.WhenAll(insertTradePlansTask, insertForwardLossRatiosTask)
            .ConfigureAwait(false);
        return rowCount;
        async Task InsertTradePlans()
        {
            var db = this;
            await (db.Use($"{nameof(TradeDbSql)}.{nameof(TradeDbSql.InsertTradePlan)}", TradeDbSql.InsertTradePlan)
            .SetParameters(EnumerateTradePlans().Select(e => new InsertTradePlan(e.Id, e.OrderId, e.TradeId, e.TradeType.ToStringFast(), e.TradeDate, e.ValueDate, e.MaturityDate, e.ActionDate, e.ActionType.ToStringFast(), e.ActionSubType.ToStringFast(), e.ActionState.ToStringFast(), e.ActionReason, e.TradePnl, e.ForwardLossRatio, e.LossProbability, e.MScore, e.MaxProfit, e.MaxLoss, e.MinProfitTarget, e.DailyProfitTarget, e.AssetPrice, e.AssetStdDev, e.AssetMean, e.AssetPriceChange, e.MarketTrend.ToStringFast(), e.MarketVolatility.ToStringFast(), e.MarketDirection.ToStringFast(), e.VixVolatility.ToStringFast(), e.TradeRisk.ToStringFast(), e.FiftyDayMA, e.FiveDayXMA, e.PutOTMProbability, e.CallOTMProbability, e.ShortPutGamma, e.ShortCallGamma, e.GammaRisk.ToStringFast(), e.NetPrice, e.ForwardPrice, e.ForwardDelta, e.StopLossLimit, e.TrendType.ToStringFast(), e.TrendStrength.ToStringFast(), e.RSI, e.RSISlope, e.TDI.ToStringFast(), e.TDIStrength.ToStringFast(), e.CreatedOn, e.CreatedBy)))
            .ExecuteCommandAsync())
            .ConfigureAwait(false);
        }

        async Task InsertTradePlanForwardLossRatio()
        {
            var db = this;
            await (db.Use($"{nameof(TradeDbSql)}.{nameof(TradeDbSql.InsertTradePlanForwardLossRatio)}", TradeDbSql.InsertTradePlanForwardLossRatio)
            .SetParameters(GetForwardLossRatios().Select(e => new InsertTradePlanForwardLossRatio(1, e.ValueDate, e.ForwardLossRatio, e.Id)))
            .ExecuteCommandAsync())
            .ConfigureAwait(false);
        }

        IEnumerable<TradePlanByIdReadModel> EnumerateTradePlans()
        {
            foreach (var e in tradePlans)
            {
                rowCount++;
                yield return e;
            }
        }

        IEnumerable<TradePlanByIdReadModel> GetForwardLossRatios()
        {
            foreach (var e in tradePlans)
                yield return e;
        }
    }

    /// <inheritdoc/>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1725", Justification = "Preserve the established storage context contract, including public named arguments and default values used by callers.")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S927", Justification = "Preserve the established storage context contract, including public named arguments and default values used by callers.")]
    public Task DeleteTradePositionStateAsync(OptionTradeEntityId e) => this.DeleteTradePositionStateAsyncCoreBinding(e);
    /// <inheritdoc/>
    public async Task DeleteTradePositionAsync(int orderId, int tradeId) => await (this.Use($"{nameof(TradeDbSql)}.{nameof(TradeDbSql.DeleteTradePosition)}", TradeDbSql.DeleteTradePosition)
            .SetParameters(new DeleteTradePosition(orderId, tradeId))
            .ExecuteCommandAsync())
            .ConfigureAwait(false);
    /// <inheritdoc/>
    public async Task DeleteTradePositionAsync(int orderId, int tradeId, TradeType tradeType, DateOnly valueDate, int daysToExpiry, TradeStatus tradeStatus) => await (this.Use($"{nameof(TradeDbSql)}.{nameof(TradeDbSql.DeleteTradePositionByPrimaryKey)}", TradeDbSql.DeleteTradePositionByPrimaryKey)
            .SetParameters(new DeleteTradePositionByPrimaryKey(orderId, tradeId, tradeType.ToStringFast(), valueDate, daysToExpiry, tradeStatus.ToStringFast()))
            .ExecuteCommandAsync())
            .ConfigureAwait(false);
    /// <inheritdoc/>
    public async Task DeleteTradeLimitAsync(int tradeId, TradeType tradeType) => await (this.Use($"{nameof(TradeDbSql)}.{nameof(TradeDbSql.DeleteTradeLimitByTradeType)}", TradeDbSql.DeleteTradeLimitByTradeType)
            .SetParameters(new DeleteTradeLimitByTradeType(tradeId, tradeType.ToStringFast()))
            .ExecuteCommandAsync())
            .ConfigureAwait(false);
    /// <inheritdoc/>
    public async Task DeleteTradePlacementSignalAsync(string contractId, DateOnly valueDate) => await (this.Use($"{nameof(TradeDbSql)}.{nameof(TradeDbSql.DeleteTradePlacementSignal)}", TradeDbSql.DeleteTradePlacementSignal)
            .SetParameters(new DeleteTradePlacementSignal(contractId, valueDate))
            .ExecuteCommandAsync())
            .ConfigureAwait(false);
    /// <inheritdoc/>
     // <summary>
    public async Task DeleteTradeTypeLimitAsync(int tradeId) => await (this.Use($"{nameof(TradeDbSql)}.{nameof(TradeDbSql.DeleteTradeTypeLimit)}", TradeDbSql.DeleteTradeTypeLimit)
            .SetParameters(new DeleteTradeTypeLimit(tradeId))
            .ExecuteCommandAsync())
            .ConfigureAwait(false);
    /// <inheritdoc/>
    public async Task DeleteOptionTradeAsync(int orderId, int tradeId)
    {
        var db = this;
        List<object> queuedCommands = [db.Use($"{nameof(TradeDbSql)}.{nameof(TradeDbSql.DeleteOptionTrade)}", TradeDbSql.DeleteOptionTrade)
            .SetParameters(new DeleteOptionTrade(orderId, tradeId))
            .QueueCommand(), db.Use($"{nameof(TradeDbSql)}.{nameof(TradeDbSql.DeleteTradePosition)}", TradeDbSql.DeleteTradePosition)
            .SetParameters(new DeleteTradePosition(orderId, tradeId))
            .QueueCommand(), db.Use($"{nameof(TradeDbSql)}.{nameof(TradeDbSql.DeleteOptionLeg)}", TradeDbSql.DeleteOptionLeg)
            .SetParameters(new DeleteOptionLeg(orderId, tradeId))
            .QueueCommand(), db.Use($"{nameof(TradeDbSql)}.{nameof(TradeDbSql.DeleteOptionLegData)}", TradeDbSql.DeleteOptionLegData)
            .SetParameters(new DeleteOptionLegData(orderId, tradeId))
            .QueueCommand(), db.Use($"{nameof(TradeDbSql)}.{nameof(TradeDbSql.DeleteTradeTypeLimit)}", TradeDbSql.DeleteTradeTypeLimit)
            .SetParameters(new DeleteTradeTypeLimit(tradeId))
            .QueueCommand(), db.Use($"{nameof(TradeDbSql)}.{nameof(TradeDbSql.DeleteTradeLimit)}", TradeDbSql.DeleteTradeLimit)
            .SetParameters(new DeleteTradeLimit(tradeId))
            .QueueCommand(), db.Use($"{nameof(TradeDbSql)}.{nameof(TradeDbSql.DeleteTradeFill)}", TradeDbSql.DeleteTradeFill)
            .SetParameters(new DeleteTradeFill(orderId, tradeId))
            .QueueCommand(), db.Use($"{nameof(TradeDbSql)}.{nameof(TradeDbSql.DeleteTradeFillData)}", TradeDbSql.DeleteTradeFillData)
            .SetParameters(new DeleteTradeFillData(orderId, tradeId))
            .QueueCommand()];
        await (db.ExecuteQueuedCommandsAsync(queuedCommands, true))
            .ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task DeleteOptionTradeSpreadBarDataAsync(int orderId, int tradeId, DateOnly valueDate, TradeType tradeType) => await (this.Use($"{nameof(TradeDbSql)}.{nameof(TradeDbSql.DeleteOptionTradeSpreadBarData)}", TradeDbSql.DeleteOptionTradeSpreadBarData)
            .SetParameters(new DeleteOptionTradeSpreadBarData(orderId, tradeId, valueDate, tradeType.ToStringFast()))
            .ExecuteCommandAsync())
            .ConfigureAwait(false);
    /// <inheritdoc/>
    public async Task DeleteOptionTradeSpreadDataAsync(int orderId, int tradeId, DateOnly valueDate, TradeType tradeType) => await (this.Use($"{nameof(TradeDbSql)}.{nameof(TradeDbSql.DeleteOptionTradeSpreadData)}", TradeDbSql.DeleteOptionTradeSpreadData)
            .SetParameters(new DeleteOptionTradeSpreadData(orderId, tradeId, valueDate, tradeType.ToStringFast()))
            .ExecuteCommandAsync())
            .ConfigureAwait(false);
    /// <inheritdoc/>
    public async Task DeleteTradeLiveFeedAsync(int orderId, int tradeId) => await (this.Use($"{nameof(TradeDbSql)}.{nameof(TradeDbSql.DeleteTradeLiveFeed)}", TradeDbSql.DeleteTradeLiveFeed)
            .SetParameters(new DeleteTradeLiveFeed(orderId, tradeId))
            .ExecuteCommandAsync())
            .ConfigureAwait(false);
    /// <inheritdoc/>
    public async Task DeleteTradeLiveFeedsAsync(int orderId) => await (this.Use($"{nameof(TradeDbSql)}.{nameof(TradeDbSql.DeleteTradeLiveFeeds)}", TradeDbSql.DeleteTradeLiveFeeds)
            .SetParameters(new DeleteTradeLiveFeeds(orderId))
            .ExecuteCommandAsync())
            .ConfigureAwait(false);
    /// <inheritdoc/>
    public Task DeleteTradePlanForwardLossLimitAsync(TradePlanForwardLossLimitEntityId id) => this.DeleteTradePlanForwardLossLimitAsyncCoreBinding(id);
    /// <inheritdoc/>
    public async Task UpdateTradeLimitDailyProfitTarget(int tradeId, TradeType tradeType, decimal dailyProfitTarget, DateTime updatedOn, string updatedBy)
    {
        var tradeLimit = await (GetTradeLimitAsync(tradeId))
            .ConfigureAwait(false);
        if (tradeLimit != null && tradeLimit.TradeType == tradeType)
        {
            var updatedTradeLimit = tradeLimit with
            {
                DailyProfitTarget = dailyProfitTarget,
                UpdatedOn = updatedOn,
                UpdatedBy = updatedBy
            };
            await (InsertTradeLimitAsync(updatedTradeLimit))
            .ConfigureAwait(false);
        }
    }

    /// <inheritdoc/>
    public Task<IntrinsicTimeStrategyWorkflowReadModel?> GetIntrinsicTimeStrategyWorkflowAsync(StrategyWorkflowId workflowId) => GetIntrinsicTimeStrategyWorkflowAsync(workflowId, CancellationToken.None);
    /// <inheritdoc/>
    public Task<IntrinsicTimeStrategyWorkflowReadModel?> GetIntrinsicTimeStrategyWorkflowAsync(StrategyWorkflowId workflowId, CancellationToken cancellationToken) => this.Use($"{nameof(TradeDbSql)}.{nameof(TradeDbSql.GetIntrinsicTimeStrategyWorkflow)}", (TradeDbSql.NativeGetIntrinsicTimeStrategyWorkflow))
            .SetParameters(new TradeDbValues(Values(Uuid(workflowId.Value))))
            .ExecuteSingleAsync(MapToWorkflow, cancellationToken);
    /// <inheritdoc/>
    public Task<ActiveIntrinsicTimeStrategyWorkflowReadModel?> GetActiveIntrinsicTimeStrategyWorkflowAsync(string workflowEntityId) => GetActiveIntrinsicTimeStrategyWorkflowAsync(workflowEntityId, CancellationToken.None);
    /// <inheritdoc/>
    public Task<ActiveIntrinsicTimeStrategyWorkflowReadModel?> GetActiveIntrinsicTimeStrategyWorkflowAsync(string workflowEntityId, CancellationToken cancellationToken) => this.Use($"{nameof(TradeDbSql)}.{nameof(TradeDbSql.GetActiveIntrinsicTimeStrategyWorkflow)}", (TradeDbSql.NativeGetActiveIntrinsicTimeStrategyWorkflow))
            .SetParameters(new TradeDbValues(Values(Text(workflowEntityId))))
            .ExecuteSingleAsync(MapToActiveWorkflow, cancellationToken);
    /// <inheritdoc/>
    public Task<ICollection<IntrinsicTimeStrategyWorkflowStartAttemptReadModel>> GetIntrinsicTimeStrategyWorkflowStartAttemptsAsync(string workflowEntityId, DateTime beforeUtc, int pageSize) => GetIntrinsicTimeStrategyWorkflowStartAttemptsAsync(workflowEntityId, beforeUtc, pageSize, CancellationToken.None);
    /// <inheritdoc/>
    public Task<ICollection<IntrinsicTimeStrategyWorkflowStartAttemptReadModel>> GetIntrinsicTimeStrategyWorkflowStartAttemptsAsync(string workflowEntityId, DateTime beforeUtc, int pageSize, CancellationToken cancellationToken) => this.Use($"{nameof(TradeDbSql)}.{nameof(TradeDbSql.GetIntrinsicTimeStrategyWorkflowStartAttempts)}", (TradeDbSql.NativeGetIntrinsicTimeStrategyWorkflowStartAttempts))
            .SetParameters(new TradeDbValues(Values(Text(workflowEntityId), TimestampTz(DateTime.SpecifyKind(beforeUtc, DateTimeKind.Utc)), Integer(pageSize.RequirePageSize()))))
            .ExecuteQueryAsync(MapToStartAttempt, cancellationToken);
    /// <inheritdoc/>
    public Task<ICollection<IntrinsicTimeStrategyWorkflowTimelineReadModel>> GetIntrinsicTimeStrategyWorkflowTimelineAsync(StrategyWorkflowId workflowId, long afterEventId, int pageSize) => GetIntrinsicTimeStrategyWorkflowTimelineAsync(workflowId, afterEventId, pageSize, CancellationToken.None);
    /// <inheritdoc/>
    public Task<ICollection<IntrinsicTimeStrategyWorkflowTimelineReadModel>> GetIntrinsicTimeStrategyWorkflowTimelineAsync(StrategyWorkflowId workflowId, long afterEventId, int pageSize, CancellationToken cancellationToken) => this.Use($"{nameof(TradeDbSql)}.{nameof(TradeDbSql.GetIntrinsicTimeStrategyWorkflowTimeline)}", (TradeDbSql.NativeGetIntrinsicTimeStrategyWorkflowTimeline))
            .SetParameters(new TradeDbValues(Values(Uuid(workflowId.Value), Bigint(afterEventId), Integer(pageSize.RequirePageSize()))))
            .ExecuteQueryAsync(MapToTimelineEvent, cancellationToken);
    /// <inheritdoc/>
    public Task<ICollection<IntrinsicTimeStrategyWorkflowHistoryReadModel>> GetIntrinsicTimeStrategyWorkflowsByEntityAsync(string workflowEntityId, DateTime beforeUtc, int pageSize) => GetIntrinsicTimeStrategyWorkflowsByEntityAsync(workflowEntityId, beforeUtc, pageSize, CancellationToken.None);
    /// <inheritdoc/>
    public Task<ICollection<IntrinsicTimeStrategyWorkflowHistoryReadModel>> GetIntrinsicTimeStrategyWorkflowsByEntityAsync(string workflowEntityId, DateTime beforeUtc, int pageSize, CancellationToken cancellationToken) => this.Use($"{nameof(TradeDbSql)}.{nameof(TradeDbSql.GetIntrinsicTimeStrategyWorkflowsByEntity)}", (TradeDbSql.NativeGetIntrinsicTimeStrategyWorkflowsByEntity))
            .SetParameters(new TradeDbValues(Values(Text(workflowEntityId), TimestampTz(DateTime.SpecifyKind(beforeUtc, DateTimeKind.Utc)), Integer(pageSize.RequirePageSize()))))
            .ExecuteQueryAsync(MapToWorkflowHistory, cancellationToken);
    /// <inheritdoc/>
    public Task<ICollection<IntrinsicTimeStrategyWorkflowHistoryReadModel>> GetIntrinsicTimeStrategyWorkflowsByStatusAsync(StrategyWorkflowStatus status, DateOnly startDate, DateOnly endDate, int pageSize) => GetIntrinsicTimeStrategyWorkflowsByStatusAsync(status, startDate, endDate, pageSize, CancellationToken.None);
    /// <inheritdoc/>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S1994", Justification = "The retry/date loop is terminated by explicit success, cancellation, or bounded-history exits in its body.")]
    public async Task<ICollection<IntrinsicTimeStrategyWorkflowHistoryReadModel>> GetIntrinsicTimeStrategyWorkflowsByStatusAsync(StrategyWorkflowStatus status, DateOnly startDate, DateOnly endDate, int pageSize, CancellationToken cancellationToken)
    {
        endDate.RequireNotBefore(startDate);
        var maximumCount = pageSize.RequirePageSize();
        var results = new List<IntrinsicTimeStrategyWorkflowHistoryReadModel>(maximumCount);
        for (var date = endDate; results.Count < maximumCount; date = date.AddDays(-1))
        {
            var remaining = maximumCount - results.Count;
            var dayResults = await this.Use($"{nameof(TradeDbSql)}.{nameof(TradeDbSql.GetIntrinsicTimeStrategyWorkflowsByStatusDay)}", (TradeDbSql.NativeGetIntrinsicTimeStrategyWorkflowsByStatusDay))
            .SetParameters(new TradeDbValues(Values(Text(status.ToString()), Date(date), Integer(remaining))))
            .ExecuteQueryAsync(MapToWorkflowHistory, cancellationToken)
            .ConfigureAwait(false);
            results.AddRange(dayResults);
            if (date == startDate)
                break;
        }

        return results;
    }

    /// <inheritdoc/>
    public Task UpsertIntrinsicTimeStrategyWorkflowAsync(IntrinsicTimeStrategyWorkflowReadModel workflow) => UpsertIntrinsicTimeStrategyWorkflowAsync(workflow, CancellationToken.None);
    /// <inheritdoc/>
    public Task UpsertIntrinsicTimeStrategyWorkflowAsync(IntrinsicTimeStrategyWorkflowReadModel workflow, CancellationToken cancellationToken) => this.UpsertIntrinsicTimeStrategyWorkflowAsyncCoreBinding(workflow, cancellationToken);
    /// <inheritdoc/>
    public Task UpsertActiveIntrinsicTimeStrategyWorkflowAsync(ActiveIntrinsicTimeStrategyWorkflowReadModel workflow) => UpsertActiveIntrinsicTimeStrategyWorkflowAsync(workflow, CancellationToken.None);
    /// <inheritdoc/>
    public Task UpsertActiveIntrinsicTimeStrategyWorkflowAsync(ActiveIntrinsicTimeStrategyWorkflowReadModel workflow, CancellationToken cancellationToken) => this.UpsertActiveIntrinsicTimeStrategyWorkflowAsyncCoreBinding(workflow, cancellationToken);
    /// <inheritdoc/>
    public Task DeleteActiveIntrinsicTimeStrategyWorkflowAsync(string workflowEntityId) => DeleteActiveIntrinsicTimeStrategyWorkflowAsync(workflowEntityId, CancellationToken.None);
    /// <inheritdoc/>
    public async Task DeleteActiveIntrinsicTimeStrategyWorkflowAsync(string workflowEntityId, CancellationToken cancellationToken)
    {
        await this.Use($"{nameof(TradeDbSql)}.{nameof(TradeDbSql.DeleteActiveIntrinsicTimeStrategyWorkflow)}", TradeDbSql.DeleteActiveIntrinsicTimeStrategyWorkflow)
            .SetParameters(new DeleteActiveIntrinsicTimeStrategyWorkflow(workflowEntityId))
            .ExecuteCommandAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public Task InsertIntrinsicTimeStrategyWorkflowStartAttemptAsync(IntrinsicTimeStrategyWorkflowStartAttemptReadModel attempt) => InsertIntrinsicTimeStrategyWorkflowStartAttemptAsync(attempt, CancellationToken.None);
    /// <inheritdoc/>
    public Task InsertIntrinsicTimeStrategyWorkflowStartAttemptAsync(IntrinsicTimeStrategyWorkflowStartAttemptReadModel attempt, CancellationToken cancellationToken) => this.InsertIntrinsicTimeStrategyWorkflowStartAttemptAsyncCoreBinding(attempt, cancellationToken);
    /// <inheritdoc/>
    public Task InsertIntrinsicTimeStrategyWorkflowTimelineAsync(IntrinsicTimeStrategyWorkflowTimelineReadModel timelineEvent) => InsertIntrinsicTimeStrategyWorkflowTimelineAsync(timelineEvent, CancellationToken.None);
    /// <inheritdoc/>
    public Task InsertIntrinsicTimeStrategyWorkflowTimelineAsync(IntrinsicTimeStrategyWorkflowTimelineReadModel timelineEvent, CancellationToken cancellationToken) => this.InsertIntrinsicTimeStrategyWorkflowTimelineAsyncCoreBinding(timelineEvent, cancellationToken);
    /// <inheritdoc/>
    public Task UpsertIntrinsicTimeStrategyWorkflowByEntityAsync(IntrinsicTimeStrategyWorkflowHistoryReadModel workflow) => UpsertIntrinsicTimeStrategyWorkflowByEntityAsync(workflow, CancellationToken.None);
    /// <inheritdoc/>
    public Task UpsertIntrinsicTimeStrategyWorkflowByEntityAsync(IntrinsicTimeStrategyWorkflowHistoryReadModel workflow, CancellationToken cancellationToken) => this.UpsertIntrinsicTimeStrategyWorkflowByEntityAsyncCoreBinding(workflow, cancellationToken);
    /// <inheritdoc/>
    public Task UpsertIntrinsicTimeStrategyWorkflowByStatusDayAsync(IntrinsicTimeStrategyWorkflowHistoryReadModel workflow) => UpsertIntrinsicTimeStrategyWorkflowByStatusDayAsync(workflow, CancellationToken.None);
    /// <inheritdoc/>
    public Task UpsertIntrinsicTimeStrategyWorkflowByStatusDayAsync(IntrinsicTimeStrategyWorkflowHistoryReadModel workflow, CancellationToken cancellationToken) => this.UpsertIntrinsicTimeStrategyWorkflowByStatusDayAsyncCoreBinding(workflow, cancellationToken);
    internal static UpsertIntrinsicTimeStrategyWorkflowByEntity MapToByEntityParameters(IntrinsicTimeStrategyWorkflowHistoryReadModel workflow) => new(workflow.WorkflowEntityId, workflow.StartedAtUtc, workflow.WorkflowId.Value, workflow.Status.ToString(), workflow.Outcome.ToString(), workflow.CurrentStage.ToString(), workflow.WorkflowRevision, workflow.TerminalAtUtc, workflow.StopReasonCode);
    internal static IntrinsicTimeStrategyWorkflowReadModel MapToWorkflow(IObjectDataRecord row) => new(new StrategyWorkflowId(row.GetGuid(0)), row.GetString(1), row.GetString(2), row.GetInt(3), row.GetString(4), row.GetDateOnly(5), row.GetEnum<TimeFrameType>(6), row.GetGuid(7), row.GetGuid(8), row.GetEnum<StrategyWorkflowStatus>(9), row.GetEnum<StrategyWorkflowOutcome>(10), row.GetEnum<StrategyWorkflowStage>(11), row.GetLong(12), row.GetLong(13), row.GetInt(14), row.GetBytes(15), row.GetString(16), row.GetDateTime(17), row.IsNull(18) ? null : row.GetDateTime(18), row.GetDateTime(19));
    internal static ActiveIntrinsicTimeStrategyWorkflowReadModel MapToActiveWorkflow(IObjectDataRecord row) => new(row.GetString(0), new StrategyWorkflowId(row.GetGuid(1)), row.GetString(2), row.GetDateOnly(3), row.GetEnum<TimeFrameType>(4), row.GetEnum<StrategyWorkflowStage>(5), row.GetLong(6), row.GetLong(7), row.GetInt(8), row.GetBytes(9), row.GetDateTime(10), row.GetDateTime(11));
    internal static IntrinsicTimeStrategyWorkflowStartAttemptReadModel MapToStartAttempt(IObjectDataRecord row) => new(row.GetString(0), row.GetDateTime(1), new StrategyWorkflowId(row.GetGuid(2)), row.GetEnum<StrategyWorkflowStartDecision>(3), row.IsNull(4) ? null : new StrategyWorkflowId(row.GetGuid(4)), row.GetGuid(5), row.GetGuid(6), row.GetEnum<StrategyWorkflowStage>(7), row.GetString(8), row.GetLong(9));
    internal static IntrinsicTimeStrategyWorkflowTimelineReadModel MapToTimelineEvent(IObjectDataRecord row) => new(new StrategyWorkflowId(row.GetGuid(0)), row.GetLong(1), row.GetString(2), row.GetLong(3), row.GetEnum<StrategyWorkflowStage>(4), row.GetString(5), row.GetInt(6), row.GetBytes(7), row.GetDateTime(8));
    internal static IntrinsicTimeStrategyWorkflowHistoryReadModel MapToWorkflowHistory(IObjectDataRecord row) => new(row.GetString(0), row.GetDateTime(1), new StrategyWorkflowId(row.GetGuid(2)), row.GetEnum<StrategyWorkflowStatus>(3), row.GetEnum<StrategyWorkflowOutcome>(4), row.GetEnum<StrategyWorkflowStage>(5), row.GetLong(6), row.IsNull(7) ? null : row.GetDateTime(7), row.GetString(8));
    /// <inheritdoc/>
    public Task<MarketConditionReadModel?> GetMarketConditionAsync(StrategyWorkflowId workflowId) => GetMarketConditionAsync(workflowId, CancellationToken.None);
    /// <inheritdoc/>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1725", Justification = "Preserve the established storage context contract, including public named arguments and default values used by callers.")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S927", Justification = "Preserve the established storage context contract, including public named arguments and default values used by callers.")]
    public Task<MarketConditionReadModel?> GetMarketConditionAsync(StrategyWorkflowId workflowId, CancellationToken token) => this.Use($"{nameof(TradeDbSql)}.{nameof(TradeDbSql.GetMarketCondition)}", TradeDbSql.GetMarketCondition)
            .SetParameters(new GetMarketCondition(workflowId.Value))
            .ExecuteSingleAsync(MapToMarketCondition, token);
    /// <inheritdoc/>
    public Task<ICollection<MarketConditionReadModel>> GetMarketConditionHistoryAsync(int fundId, string instrumentRoot, TimeFrameType targetHorizon, DateTime beforeUtc, int pageSize) => GetMarketConditionHistoryAsync(fundId, instrumentRoot, targetHorizon, beforeUtc, pageSize, CancellationToken.None);
    /// <inheritdoc/>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1725", Justification = "Preserve the established storage context contract, including public named arguments and default values used by callers.")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S927", Justification = "Preserve the established storage context contract, including public named arguments and default values used by callers.")]
    public Task<ICollection<MarketConditionReadModel>> GetMarketConditionHistoryAsync(int fundId, string instrumentRoot, TimeFrameType targetHorizon, DateTime beforeUtc, int pageSize, CancellationToken token)
    {
        fundId.RequireMarketConditionHistoryScope(instrumentRoot, pageSize);
        return this.Use($"{nameof(TradeDbSql)}.{nameof(TradeDbSql.GetMarketConditionHistory)}", TradeDbSql.GetMarketConditionHistory)
            .SetParameters(new GetMarketConditionHistory(fundId, instrumentRoot, targetHorizon.ToString(), beforeUtc, pageSize))
            .ExecuteQueryAsync(MapToMarketCondition, token);
    }

    /// <inheritdoc/>
    public Task UpsertMarketConditionAsync(MarketConditionReadModel result) => UpsertMarketConditionAsync(result, CancellationToken.None);
    /// <inheritdoc/>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1725", Justification = "Preserve the established storage context contract, including public named arguments and default values used by callers.")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S927", Justification = "Preserve the established storage context contract, including public named arguments and default values used by callers.")]
    public Task UpsertMarketConditionAsync(MarketConditionReadModel result, CancellationToken token) => this.UpsertMarketConditionAsyncCoreBinding(result, token);
    internal static MarketConditionReadModel MapToMarketCondition(IObjectDataRecord row) => new()
    {
        WorkflowId = new StrategyWorkflowId(row.GetGuid(0)),
        WorkflowEntityId = row.GetString(1),
        InputWorkflowRevision = row.GetLong(2),
        CommandId = row.GetGuid(3),
        SourceEventId = row.GetGuid(4),
        FundId = row.GetInt(5),
        InstrumentRoot = row.GetString(6),
        TargetHorizon = Enum.Parse<TimeFrameType>(row.GetString(7)),
        ParameterSetId = row.GetGuid(8),
        ParameterSetVersion = row.GetInt(9),
        ParameterPayloadSha256 = row.GetString(10),
        SnapshotId = row.GetGuid(11),
        SnapshotSha256 = row.GetString(12),
        Tradeability = Enum.Parse<MarketTradeability>(row.GetString(13)),
        ConditionType = Enum.Parse<MarketConditionType>(row.GetString(14)),
        Direction = Enum.Parse<MarketConditionDirection>(row.GetString(15)),
        Phase = Enum.Parse<MarketConditionPhase>(row.GetString(16)),
        Strength = row.GetDecimal(17),
        Confidence = row.GetDecimal(18),
        PrimaryReasonCode = row.GetString(19),
        ResultPayload = row.GetBytes(20),
        ResultPayloadSha256 = row.GetString(21),
        EvaluatedAtUtc = row.GetDateTime(22),
        ValidUntilUtc = row.GetDateTime(23),
        MarketDataAsOfUtc = row.GetDateTime(24),
        CompletedAtUtc = row.GetDateTime(25),
        UpdatedAtUtc = row.GetDateTime(26),
        VolatilityBehavior = Enum.Parse<MarketConditionVolatilityBehavior>(row.GetString(27)),
        LiquidityQuality = Enum.Parse<MarketConditionLiquidityQuality>(row.GetString(28)),
        DataQuality = Enum.Parse<MarketConditionDataQuality>(row.GetString(29)),
        UpstreamAlignment = Enum.Parse<MarketConditionUpstreamAlignment>(row.GetString(30)),
        EvidencePayload = row.GetBytes(31),
        ConflictingEvidencePayload = row.GetBytes(32),
        BlockingReasonsPayload = row.GetBytes(33),
        ReasonsPayload = row.GetBytes(34),
        SummaryText = row.GetString(35)
    };
    /// <inheritdoc/>
    public Task UpsertMarketConditionAssessmentAsync(MarketConditionAssessmentCompletedEvent completed, CancellationToken cancellationToken = default) => this.UpsertMarketConditionAssessmentAsyncCoreBinding(completed, cancellationToken);
    /// <inheritdoc/>
    public Task<MarketConditionAssessmentCompletedEvent?> GetMarketConditionAssessmentAsync(StrategyWorkflowId workflowId, CancellationToken cancellationToken = default)
    {
        return this.NativeTransaction().ExecuteOnceAsync(async (db, ct) => (await (db.QueryAsync(TradeDbSql.ContextStatement1, [workflowId.Value], r => (r.GetFieldValue<byte[]>(0)).VerifyAssessment(r.GetString(1)), ct))
            .ConfigureAwait(false)).SingleOrDefault(), cancellationToken);
    }

    /// <inheritdoc/>
    public Task<ICollection<MarketConditionAssessmentCompletedEvent>> GetMarketConditionAssessmentHistoryAsync(string profile, string root, TimeFrameType horizon, DateTime beforeUtc, int pageSize, CancellationToken cancellationToken = default) => this.GetMarketConditionAssessmentHistoryAsyncCoreBinding(profile, root, horizon, beforeUtc, pageSize, cancellationToken);
    internal static MarketConditionAssessmentCompletedEvent MapToAssessment(IObjectDataRecord row) => (row.GetBytes(0)).VerifyAssessment(row.GetString(1));
    /// <inheritdoc/>
    public Task UpsertOrderCompositionAsync(OrderCompositionFunctionCompletedEvent completed, CancellationToken token = default) => this.UpsertOrderCompositionAsyncCoreBinding(completed, token);
    /// <inheritdoc/>
    public Task<OrderCompositionFunctionCompletedEvent?> GetOrderCompositionInvocationAsync(StrategyWorkflowId workflowId, Guid invocationId, CancellationToken token = default)
    {
        workflowId.RequireInvocation(invocationId, "Exact workflow and invocation required.");
        return this.NativeTransaction().ExecuteOnceAsync((db, ct) => TradeDbContextExtensions.PipelineReadCompositionAsync(db, workflowId, invocationId, ct), token);
    }

    /// <inheritdoc/>
    public Task<QueryPage<OrderCompositionHistoryRow>> GetOrderCompositionHistoryAsync(int portfolioId, int fundId, DateOnly valueDate, int pageSize, byte[]? pagingState = null, CancellationToken token = default)
    {
        portfolioId.RequireHistoryScope(fundId, valueDate, pageSize, 100, "Invalid composition history scope.");
        return this.NativeTransaction().ExecuteOnceAsync((db, ct) => TradeDbContextExtensions.PipelineReadCompositionHistoryAsync(db, portfolioId, fundId, valueDate, pageSize, pagingState, ct), token);
    }

    /// <inheritdoc/>
    public Task<RegimeDiscoveryReadModel?> GetRegimeDiscoveryAsync(StrategyWorkflowId workflowId) => GetRegimeDiscoveryAsync(workflowId, CancellationToken.None);
    /// <inheritdoc/>
    public Task<RegimeDiscoveryReadModel?> GetRegimeDiscoveryAsync(StrategyWorkflowId workflowId, CancellationToken cancellationToken)
    {
        return this.NativeTransaction().ExecuteOnceAsync((db, ct) => TradeDbContextExtensions.RegimeReadAsync(db, workflowId, ct), cancellationToken);
    }

    /// <inheritdoc/>
    public Task UpsertRegimeDiscoveryAsync(RegimeDiscoveryReadModel result) => UpsertRegimeDiscoveryAsync(result, CancellationToken.None);
    /// <inheritdoc/>
    public Task UpsertRegimeDiscoveryAsync(RegimeDiscoveryReadModel result, CancellationToken cancellationToken) => this.UpsertRegimeDiscoveryAsyncCoreBinding(result, cancellationToken);
    internal static RegimeDiscoveryReadModel MapToRegimeDiscovery(IObjectDataRecord row) => new()
    {
        WorkflowId = new StrategyWorkflowId(row.GetGuid(0)),
        WorkflowEntityId = row.GetString(1),
        InputWorkflowRevision = row.GetLong(2),
        CommandId = row.GetGuid(3),
        SourceEventId = row.GetGuid(4),
        SourceEventSequence = row.GetLong(5),
        Status = row.GetString(6),
        ParameterPayloadSha256 = row.GetString(7),
        SignalSnapshotId = row.GetGuid(8),
        ResultPayload = row.IsNull(9) ? ReadOnlyMemory<byte>.Empty : row.GetBytes(9),
        ResultPayloadSha256 = row.IsNull(10) ? string.Empty : row.GetString(10),
        FailureCode = row.IsNull(11) ? 0 : row.GetInt(11),
        FailureMessage = row.IsNull(12) ? string.Empty : row.GetString(12),
        ReasonsPayload = row.IsNull(13) ? ReadOnlyMemory<byte>.Empty : row.GetBytes(13),
        SchemaVersion = row.GetInt(14),
        TerminalAtUtc = row.GetDateTime(15),
        UpdatedAtUtc = row.GetDateTime(16)
    };
    /// <inheritdoc/>
    public Task<RiskHistoryProjectionResult> UpsertRiskHistoryAsync(WorkflowStrategyStateUpdatedEvent snapshot, CancellationToken token = default) => this.UpsertRiskHistoryAsyncCoreBinding(snapshot, token);
    /// <inheritdoc/>
    public Task<WorkflowStrategyStateUpdatedEvent?> GetRiskInvocationAsync(Guid workflow, Guid invocation, CancellationToken token = default)
    {
        workflow.RequireInvocation(invocation, "Exact Risk identities required.");
        return this.Use("RiskHistory.Exact", TradeDbSql.ContextStatement2)
            .SetParameters(new RiskValues([workflow, invocation]))
            .ExecuteSingleAsync(r =>
        {
            var payload = r.GetBytes(0);
            var value = MessagePackBinarySerializer.Shared.Deserialize<WorkflowStrategyStateUpdatedEvent>(payload);
            if (!string.Equals(RiskContracts.Hash(value), r.GetString(1), StringComparison.Ordinal) || RiskHistoryIdentity.Row(value)is not { } row || row.WorkflowId != workflow || row.InvocationId != invocation)
                throw new InvalidDataException("Risk history content hash mismatch.");
            return value;
        }, token);
    }

    /// <inheritdoc/>
    public Task<QueryPage<RiskHistoryRow>> GetRiskHistoryAsync(int portfolio, int fund, DateOnly date, int size, byte[]? cursor, CancellationToken token = default)
    {
        portfolio.RequireHistoryScope(fund, date, size, 100, "Invalid Risk history scope.");
        return this.NativeTransaction().ExecuteOnceAsync((db,ct)=>TradeDbContextExtensions.RiskReadHistoryAsync(db,portfolio,fund,date,size,cursor,ct),token);
    }

    /// <inheritdoc/>
    public Task UpsertTradeOrderAsync(TradeOrderDefinition order, DateTime acceptedAtUtc, CancellationToken token = default)
    {
        return this.NativeTransaction().ExecuteOnceAsync((db, ct) => TradeDbContextExtensions.TradeFlowProjectOrderAsync(db, order, acceptedAtUtc, ct), token);
    }

    /// <inheritdoc/>
    public Task UpsertTradeOrderAsync(TradeOrderDefinition order, CancellationToken token = default) => this.UpsertTradeOrderAsyncCoreBinding(order, token);
    /// <inheritdoc/>
    public Task<TradeOrderDefinition?> GetTradeOrderAsync(TradeOrderId id, CancellationToken token = default)
    {
        id.Require();
        return this.Use("TradeFlow.Order.Get", TradeDbSql.ContextStatement4)
            .SetParameters(new GetNativeTradeOrder(id.PortfolioId, id.FundId, id.OrderId))
            .ExecuteSingleAsync(row => MessagePackBinarySerializer.Shared.Deserialize<TradeOrderDefinition>(row.GetBytes(0)), token);
    }

    /// <inheritdoc/>
    public Task UpsertOrderExecutionAsync(OrderExecutionDefinition execution, CancellationToken token = default) => this.UpsertOrderExecutionAsyncCoreBinding(execution, token);
    /// <inheritdoc/>
    public Task<OrderExecutionDefinition?> GetOrderExecutionAsync(TradeOrderId id, Guid executionAttemptId, CancellationToken token = default)
    {
        id.Require();
        executionAttemptId.Require("ExecutionAttemptId is required.", nameof(executionAttemptId));
        return this.NativeTransaction().ExecuteOnceAsync((db, ct) => TradeDbContextExtensions.TradeReadExecutionAsync(db, id, executionAttemptId, ct), token);
    }

    /// <inheritdoc/>
    public Task UpsertEstablishedTradeAsync(EstablishedTradeDefinition trade, CancellationToken token = default) => this.UpsertEstablishedTradeAsyncCoreBinding(trade, token);
    /// <inheritdoc/>
    public Task<EstablishedTradeDefinition?> GetEstablishedTradeAsync(TradeEntityId id, CancellationToken token = default)
    {
        id.Require();
        return this.NativeTransaction().ExecuteOnceAsync((db, ct) => TradeDbContextExtensions.TradeReadTradeAsync(db, id, ct), token);
    }

    /// <inheritdoc/>
    public Task<QueryPage<EstablishedTradeDefinition>> GetEstablishedTradesAsync(int portfolioId, int fundId, TradeStrategyKind strategyKind, DateTime fromUtc, DateTime toUtc, int pageSize, byte[]? pagingState = null, CancellationToken token = default)
    {
        portfolioId.RequireUtcHistoryScope(fundId, fromUtc, toUtc, pageSize);
        return this.Use("TradeFlow.Trade.History.Get", TradeDbSql.ContextStatement5)
            .SetParameters(new TradeDbValues([portfolioId, fundId, strategyKind.ToString(), fromUtc, toUtc]))
            .ExecutePageAsync(row => MessagePackBinarySerializer.Shared.Deserialize<EstablishedTradeDefinition>(row.GetBytes(0)), pageSize, pagingState, token);
    }

    /// <inheritdoc/>
    public Task UpsertStrategyPositionAsync(StrategyPositionSnapshot position, CancellationToken token = default) => this.UpsertStrategyPositionAsyncCoreBinding(position, token);
    /// <inheritdoc/>
    public Task<QueryPage<StrategyPositionSnapshot>> GetStrategyPositionHistoryAsync(Guid positionId, DateTime fromUtc, DateTime toUtc, int pageSize, byte[]? pagingState = null, CancellationToken token = default)
    {
        positionId.RequireUtcHistoryScope(fromUtc, toUtc, pageSize);
        return this.NativeTransaction().ExecuteOnceAsync((db, ct) => TradeDbContextExtensions.TradeReadPositionHistoryAsync(db, positionId, fromUtc, toUtc, pageSize, pagingState, ct), token);
    }

    /// <inheritdoc/>
    public Task<StrategyPositionSnapshot?> GetStrategyPositionAsync(StrategyPositionId id, CancellationToken token = default)
    {
        id.Require();
        return this.NativeTransaction().ExecuteOnceAsync((db, ct) => TradeDbContextExtensions.TradeReadPositionAsync(db, id, ct), token);
    }

    /// <inheritdoc/>
    public Task ReplaceOpenPositionRoutesAsync(StrategyPositionSnapshot position, CancellationToken token = default) => this.ReplaceOpenPositionRoutesAsyncCoreBinding(position, token);
    /// <inheritdoc/>
    public Task<ICollection<OpenPositionRouteReadModel>> GetOpenPositionRoutesAsync(string contractId, CancellationToken token = default) => this.GetOpenPositionRoutesAsyncCoreBinding(contractId, token);
    /// <inheritdoc/>
    public async Task<ICollection<OpenPositionRouteReadModel>> GetOpenPositionRouteSnapshotAsync(CancellationToken token = default)
    {
        return (await (this.NativeTransaction().ExecuteOnceAsync((db, ct) => TradeDbContextExtensions.TradeReadRoutesAsync(db, null, ct), token))
            .ConfigureAwait(false)).ToArray();
    }

    /// <inheritdoc/>
    public Task<QueryPage<OpenPositionRouteReadModel>> GetOpenPositionRoutePageAsync(int pageSize, byte[]? pagingState = null, CancellationToken token = default)
    {
        this.ValidateRoutePageSize(pageSize);
        return this.NativeTransaction().ExecuteOnceAsync((db, ct) => TradeDbContextExtensions.TradeReadRoutePageAsync(db, pageSize, pagingState, ct), token);
    }

    /// <inheritdoc/>
    public Task UpsertTradeSelectionAsync(TradeSelectionFunctionCompletedEvent completed, CancellationToken cancellationToken = default) => this.UpsertTradeSelectionAsyncCoreBinding(completed, cancellationToken);
    /// <inheritdoc/>
    public Task<TradeSelectionFunctionCompletedEvent?> GetTradeSelectionInvocationAsync(StrategyWorkflowId workflowId, Guid invocationId, CancellationToken cancellationToken = default)
    {
        workflowId.RequireInvocation(invocationId, "Exact workflow and invocation identities are required.");
        return this.NativeTransaction().ExecuteOnceAsync((db, ct) => TradeDbContextExtensions.PipelineReadSelectionAsync(db, workflowId, invocationId, ct), cancellationToken);
    }

    /// <inheritdoc/>
    public Task<QueryPage<TradeSelectionHistoryRow>> GetTradeSelectionHistoryAsync(int portfolioId, int fundId, DateOnly valueDate, int pageSize, byte[]? pagingState = null, CancellationToken cancellationToken = default)
    {
        portfolioId.RequireHistoryScope(fundId, valueDate, pageSize, 200, "Invalid history scope or page size.");
        return this.NativeTransaction().ExecuteOnceAsync((db, ct) => TradeDbContextExtensions.PipelineReadSelectionHistoryAsync(db, portfolioId, fundId, valueDate, pageSize, pagingState, ct), cancellationToken);
    }

internal static (DateTime At, long Sequence, StrategyPositionSnapshot Position) MapToNativePositionHistory(System.Data.Common.DbDataReader r) => (At: r.GetDateTime(0), Sequence: r.GetInt64(1), Position: MessagePackBinarySerializer.Shared.Deserialize<StrategyPositionSnapshot>((byte[])r.GetValue(2)));
internal static TradeLimitReadModel MapToNativeTradeLimit(System.Data.Common.DbDataReader r) => new TomasAI.IFM.Domain.Trade.Shared.ViewModels.TradeLimitReadModel { TradeId = r.GetInt32(0), TradeType = Enum.Parse<TradeType>(r.GetString(1)), RiskMargin = r.GetDecimal(2), MaxProfit = r.GetDecimal(3), MaxLoss = r.GetDecimal(4), MaxReturn = r.GetDecimal(5), MaxLossLimit = r.GetDecimal(6), MinProfitLimit = r.GetDecimal(7), MaxProfitLimit = r.GetDecimal(8), MinProfitTarget = r.GetDecimal(9), DailyProfitTarget = r.GetDecimal(10), CreatedOn = r.GetDateTime(11), CreatedBy = r.GetString(12), UpdatedOn = r.GetDateTime(13), UpdatedBy = r.GetString(14) };
internal static TradeTypeLimitReadModel MapToNativeSpreadLimit(System.Data.Common.DbDataReader r) => new TomasAI.IFM.Domain.Trade.Shared.ViewModels.TradeTypeLimitReadModel { TradeId = r.GetInt32(0), TradeType = Enum.Parse<TradeType>(r.GetString(1)), MaxLossLimit = r.GetDecimal(2), MinProfitLimit = r.GetDecimal(3), MaxProfitLimit = r.GetDecimal(4) };
internal static (byte[] Payload, string Result, string Input) MapToCompositionEvidence(System.Data.Common.DbDataReader r) => (Payload: r.GetFieldValue<byte[]>(0), Result: r.GetString(1), Input: r.GetString(2));
internal static (byte[] Payload, string Result) MapToSelectionEvidence(System.Data.Common.DbDataReader r) => (Payload: r.GetFieldValue<byte[]>(0), Result: r.GetString(1));
internal static PipelineReadHistoryRow MapToPipelineHistory(System.Data.Common.DbDataReader r) => new PipelineReadHistoryRow(r.GetDateTime(0), r.GetGuid(1), r.GetGuid(2), r.GetGuid(3), r.GetInt16(4), checked((byte)r.GetInt16(5)), r.GetString(6), r.GetGuid(7), r.GetString(8));
internal static RegimeDiscoveryReadModel MapToNativeRegime(System.Data.Common.DbDataReader r) => new RegimeDiscoveryReadModel { WorkflowId = new StrategyWorkflowId(r.GetGuid(0)), WorkflowEntityId = r.GetString(1), InputWorkflowRevision = r.GetInt64(2), CommandId = r.GetGuid(3), SourceEventId = r.GetGuid(4), SourceEventSequence = r.GetInt64(5), Status = r.GetString(6), ParameterPayloadSha256 = r.GetString(7), SignalSnapshotId = r.GetGuid(8), ResultPayload = r.IsDBNull(9) ? ReadOnlyMemory<byte>.Empty : r.GetFieldValue<byte[]>(9), ResultPayloadSha256 = r.IsDBNull(10) ? string.Empty : r.GetString(10), FailureCode = r.IsDBNull(11) ? 0 : r.GetInt32(11), FailureMessage = r.IsDBNull(12) ? string.Empty : r.GetString(12), ReasonsPayload = r.IsDBNull(13) ? ReadOnlyMemory<byte>.Empty : r.GetFieldValue<byte[]>(13), SchemaVersion = r.GetInt32(14), TerminalAtUtc = r.GetDateTime(15), UpdatedAtUtc = r.GetDateTime(16) };
internal static OpenPositionRouteReadModel MapToNativeRoute(System.Data.Common.DbDataReader r) => new(r.GetString(0), new PortfolioFundTradeLeg(r.GetInt32(1), r.GetInt32(2), r.GetInt32(3), r.GetInt32(4), r.GetGuid(5), r.GetGuid(6), Enum.Parse<TradeStrategyKind>(r.GetString(7)), r.GetInt64(8)));
}
