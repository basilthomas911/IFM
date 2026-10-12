using TomasAI.IFM.Domain.MarketData.Shared;
using TomasAI.IFM.Domain.Trade.Shared;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Framework.SequenceId;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Domain.Trade.Shared.Extensions;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Shared.Storage;
using TomasAI.IFM.Domain.Trade.Shared.ViewModels;
using TomasAI.IFM.Domain.Trade.Shared.TradeOrder;
using TomasAI.IFM.Domain.Trade.Shared.TradeOrder.ViewModels;

using MessagePack;
using static TomasAI.IFM.Framework.Storage.Postgres.PostgresParameter;
using System.Security.Cryptography;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Events;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Identity;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Model;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Events;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.MarketCondition.Assessment;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.MarketCondition.Model;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.OrderComposition;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.RegimeDiscovery.ViewModels;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.RiskManagement;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.TradeSelection;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.ViewModels;
using TomasAI.IFM.Framework.Serialization;

namespace TomasAI.IFM.Application.Storage.TradeDb;

/// <summary>
/// trade database constructor
/// </summary>
/// <param name="connectionSettings"></param>
/// <param name="dbFactory"></param>
/// <param name="sequenceIdGenerator"></param>
/// <param name="logger"></param>
public sealed class TradeDbContext(
    IDbConnectionSettings connectionSettings,
    IDbContextFactory dbFactory,
    ISequenceIdGenerator sequenceIdGenerator,
    ILogger<DbProvider> logger)
    : ObjectDataRepository<TradeDbContext>(connectionSettings[TradeDbConnection], logger), ITradeDbContext, IEnlistedTradeDbWriter
{
    public const string TradeDbConnection = "TradeDbConnection";
    internal readonly PostgresTradeFlowStore tradeFlowWriter = new();
    PostgresTradeFlowStore EnlistedWriter => string.Equals(ProviderName,"System.Data.Postgres",StringComparison.Ordinal)
        ? tradeFlowWriter : throw new InvalidOperationException("Enlisted trade writes require native PostgreSQL.");

    /// <summary>Writes complete accepted workflow facts on the caller-owned transaction.</summary>
    public Task ProjectWorkflowAsync(EventSourceDb.IEnlistedPostgresTransaction transaction, WorkflowStrategyStateUpdatedEvent source, CancellationToken token = default)
        => PostgresWorkflowSnapshotStore.ProjectAsync(transaction, source, token);

    public Task<int> ProjectOrderAsync(EventSourceDb.IEnlistedPostgresTransaction transaction,TradeOrderDefinition order,DateTime acceptedAtUtc,CancellationToken token=default)
        => EnlistedWriter.ProjectOrderAsync(transaction,order,acceptedAtUtc,token);
    public Task ProjectMonitoringLimitsAsync(TradeLimitReadModel limit,IReadOnlyList<TradeTypeLimitReadModel> spreads,CancellationToken token=default)
    {
        if(string.Equals(ProviderName,"System.Data.Postgres",StringComparison.Ordinal))
            return this.NativeTransaction().ExecuteOnceAsync(async(db,ct)=>{await (tradeFlowWriter.ProjectMonitoringLimitsAsync(db,limit,spreads,ct))
                .ConfigureAwait(false);return true;},token);
        return Task.WhenAll(InsertTradeLimitAsync(limit),InsertTradeTypeLimitsAsync((ICollection<TradeTypeLimitReadModel>)spreads.ToArray()));
    }

    public Task ProjectMonitoringLimitsAsync(EventSourceDb.IEnlistedPostgresTransaction transaction,TradeLimitReadModel limit,
        IReadOnlyList<TradeTypeLimitReadModel> spreads,CancellationToken token=default)
        => EnlistedWriter.ProjectMonitoringLimitsAsync(transaction,limit,spreads,token);
    public Task ProjectExecutionAsync(EventSourceDb.IEnlistedPostgresTransaction transaction,OrderExecutionDefinition execution,CancellationToken token=default)
        => EnlistedWriter.ProjectExecutionAsync(transaction,execution,token);
    public Task ProjectTradeAsync(EventSourceDb.IEnlistedPostgresTransaction transaction,EstablishedTradeDefinition trade,CancellationToken token=default)
        => EnlistedWriter.ProjectTradeAsync(transaction,trade,token);
    public Task ProjectPositionAsync(EventSourceDb.IEnlistedPostgresTransaction transaction,StrategyPositionSnapshot position,CancellationToken token=default)
        => EnlistedWriter.ProjectPositionAsync(transaction,position,token);

    internal readonly IDbContextFactory _dbFactory = IsArgumentNull.Set(dbFactory);
    internal readonly ISequenceIdGenerator _sequenceIdGenerator = IsArgumentNull.Set(sequenceIdGenerator);

    /// <summary>
    /// Gets the database context.
    /// </summary>
    public override TradeDbContext Database => this;

    /// <summary>Gets the read capability for this database context.</summary>
    public ITradeDbReadContext DbReader => this;

    /// <summary>Gets the write capability for this database context.</summary>
    public ITradeDbWriteContext DbWriter => this;

    static TradePlanForwardLossLimitReadModel MapToTradePlanForwardLossLimit<TDataRecord>(TDataRecord e) where TDataRecord : IObjectDataRecord
        => new(
            orderId: e.GetInt(0),
            tradeId: e.GetInt(1),
            valueDate: e.GetDateOnly(2),
            tradeType: e.GetEnum<TradeType>(3),
            limitType: e.GetEnum<ForwardLossLimitType>(4)
        );

    static TradePlanStopLossLimitReadModel MapToTradePlanStopLossLimit<TDataRecord>(TDataRecord e) where TDataRecord : IObjectDataRecord
        => new(
            stopLossLimit: e.GetDouble(0)
        );

    static TradePlanForwardLossRatioReadModel MapToTradePlanForwardLossRatio<TDataRecord>(TDataRecord e) where TDataRecord : IObjectDataRecord
        => new(
            forwardLossRatio: e.GetDouble(0)
        );

    static TradePlanForwardLossRatioRow MapToTradePlanForwardLossRatioRow<TDataRecord>(TDataRecord e)
        where TDataRecord : IObjectDataRecord
        => new(e.GetDouble(0), e.GetLong(1));

    static TradeLiveFeedReadModel MapToTradeLiveFeed<TDataRecord>(TDataRecord e) where TDataRecord : IObjectDataRecord
        => new(
            orderId: e.GetInt(0),
            tradeId: e.GetInt(1),
            liveFeed: e.GetBool(2)
        );

    static TradePlanReadModel MapToTradePlan<TDataRecord>(TDataRecord e) where TDataRecord : IObjectDataRecord
    => new(
        sequenceId: e.GetLong(0),
        orderId: e.GetInt(1),
        tradeId: e.GetInt(2),
        valueDate: e.GetDateOnly(3),
        actionDate: e.GetDateTime(4),
        tradeDate: e.GetDateOnly(5),
        maturityDate: e.GetDateOnly(6),
        tradeType: e.GetEnum<TradeType>(7),
        actionType: e.GetEnum<ActionType>(8),
        actionSubType: e.GetEnum<ActionSubType>(9),
        actionState: e.GetEnum<ActionState>(10),
        actionReason: e.GetString(11),
        tradePnl: e.GetDecimal(12),
        forwardLossRatio: e.GetDouble(13),
        lossProbability: e.GetDouble(14),
        mScore: e.GetDouble(15),
        maxProfit: e.GetDecimal(16),
        maxLoss: e.GetDecimal(17),
        minProfitTarget: e.GetDecimal(18),
        dailyProfitTarget: e.GetDecimal(19),
        assetPrice: e.GetDecimal(20),
        assetStdDev: e.GetDouble(21),
        assetMean: e.GetDouble(22),
        assetPriceChange: e.GetDouble(23),
        marketTrend: e.GetEnum<MarketDirectionType>(24),
        marketVolatility: e.GetEnum<MarketVolatilityType>(25),
        marketDirection: e.GetEnum<PriceDirectionType>(26),
        vixVolatility: e.GetEnum<PriceVolatilityType>(27),
        tradeRisk: e.GetEnum<TradeRiskType>(28),
        fiftyDayMA: e.GetDouble(29),
        fiveDayXMA: e.GetDouble(30),
        putOTMProbability: e.GetDouble(31),
        callOTMProbability: e.GetDouble(32),
        shortPutGamma: e.GetDouble(33),
        shortCallGamma: e.GetDouble(34),
        gammaRisk: e.GetEnum<GammaRiskType>(35),
        netPrice: e.GetDecimal(36),
        forwardPrice: e.GetDecimal(37),
        forwardDelta: e.GetDouble(38),
        stopLossLimit: e.GetDouble(39),
        trendType: e.GetEnum<global::TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesTrendType>(40),
        trendStrength: e.GetEnum<global::TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesTrendStrengthType>(41),
        rsi: e.GetDouble(42),
        rsiSlope: e.GetDouble(43),
        tdi: e.GetEnum<global::TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesTrendDirectionType>(44),
        tdiStrength: e.GetEnum<global::TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesTrendDirectionStrengthType>(45),
        createdOn: e.GetDateTime(46),
        createdBy: e.GetString(47)
    );

    static TradeFillDataReadModel MapToTradeFillData<TDataRecord>(TDataRecord e) where TDataRecord : IObjectDataRecord
        => new(
            orderId: e.GetInt(0),
            tradeId: e.GetInt(1),
            contractId: e.GetString(2),
            fillDate: e.GetDateTime(3),
            bidPrice: e.GetDecimal(4),
            askPrice: e.GetDecimal(5),
            commission: e.GetDecimal(6),
            optionLegAction: e.GetEnum<OptionLegAction>(7),
            createdOn: e.GetDateTime(8),
            createdBy: e.GetString(9)
        );

    static TradeFillReadModel MapToTradeFill<TDataRecord>(TDataRecord e) where TDataRecord : IObjectDataRecord
        => new(
            orderId: e.GetInt(0),
            tradeId: e.GetInt(1),
            fillDate: e.GetDateTime(2),
            fillQuantity: e.GetInt(3),
            createdOn: e.GetDateTime(4),
            createdBy: e.GetString(5)
        );


    internal static TradeTypeLimitReadModel MapToTradeTypeLimit<TDataRecord>(TDataRecord e) where TDataRecord : IObjectDataRecord
        => new(
            tradeId: e.GetInt(0),
            tradeType: e.GetEnum<TradeType>(1),
            maxLossLimit: e.GetDecimal(2),
            minProfitLimit: e.GetDecimal(3),
            maxProfitLimit: e.GetDecimal(4)
        );

    static TradePlacementSignalReadModel MapToTradePlacementSignal<TDataRecord>(TDataRecord e) where TDataRecord : IObjectDataRecord
        => new(
            sequenceId: e.GetLong(0),
            contractId: e.GetString(1),
            valueDate: e.GetDateOnly(2),
            tradePlacementSignal: e.GetEnum<TradePlacementSignalType>(3),
            tradePrice: e.GetDecimal(4),
            createdOn: e.GetDateTime(5),
            createdBy: e.GetString(6)
        );

    static TradeLimitReadModel MapToTradeLimit<TDataRecord>(TDataRecord e) where TDataRecord : IObjectDataRecord
        => new(
            tradeId: e.GetInt(0),
            tradeType: e.GetEnum<TradeType>(1),
            riskMargin: e.GetDecimal(2),
            maxProfit: e.GetDecimal(3),
            maxLoss: e.GetDecimal(4),
            maxReturn: e.GetDecimal(5),
            maxLossLimit: e.GetDecimal(6),
            minProfitLimit: e.GetDecimal(7),
            maxProfitLimit: e.GetDecimal(8),
            minProfitTarget: e.GetDecimal(9),
            dailyProfitTarget: e.GetDecimal(10),
            createdOn: e.GetDateTime(11),
            createdBy: e.GetString(12),
            updatedOn: e.GetDateTime(13),
            updatedBy: e.GetString(14)
        );

    static TradeHistoryReadModel MapToTradeHistory<TDataRecord>(TDataRecord e) where TDataRecord : IObjectDataRecord
        => new(
            orderId: e.GetInt(0),
            tradeId: e.GetInt(1),
            valueDate: e.GetDateOnly(2),
            tradeStatus: e.GetEnum<TradeStatus>(3),
            daysToExpiry: e.GetInt(4),
            tradeType: e.GetEnum<TradeType>(5),
            commission: e.GetDecimal(6),
            netSpread: e.GetDecimal(7),
            tradePnl: e.GetDecimal(8)
        );

    static OptionTradeSpreadBarsDataModel MapToOptionTradeSpreadBarsData<TDataRecord>(TDataRecord e) where TDataRecord : IObjectDataRecord
        => new(
            orderId: e.GetInt(0),
            tradeId: e.GetInt(1),
            valueDate: e.GetDateOnly(3),
            tradeType: e.GetEnum<TradeType>(2),
            barDate: e.GetDateTime(4),
            lossLimit: e.GetDecimal(5),
            winLimit: e.GetDecimal(6),
            forwardSpread: e.GetDecimal(7),
            netSpread: e.GetDecimal(8)
        );

    internal static TradePositionReadModel MapToTradePosition<TDataRecord>(TDataRecord e) where TDataRecord : IObjectDataRecord
        => new(
            orderId: e.GetInt(0),
            tradeId: e.GetInt(1),
            tradeType: e.GetEnum<TradeType>(2),
            valueDate: e.GetDateOnly(3),
            tradeStatus: e.GetEnum<TradeStatus>(4),
            daysToExpiry: e.GetInt(5),
            commission: e.GetDecimal(6),
            deltaHedge: e.GetInt(7),
            netSpread: e.GetDecimal(8),
            tradeValue: e.GetDecimal(9),
            tradePnl: e.GetDecimal(10),
            assetPrice: e.GetDecimal(11),
            otmProbability: e.GetDouble(12),
            forwardPrice: e.GetDecimal(13),
            forwardLossRatio: e.GetDouble(14),
            lossProbability: e.GetDouble(15),
            riskFreeRate: e.GetDouble(16),
            createdOn: e.GetDateTime(17),
            createdBy: e.GetString(18),
            updatedOn: e.GetDateTime(19),
            updatedBy: e.GetString(20)
        );

    internal static OptionTradeLegReadModel MapToOptionLeg<TDataRecord>(TDataRecord e) where TDataRecord : IObjectDataRecord
        => new(
            orderId: e.GetInt(0),
            tradeId: e.GetInt(1),
            contractId: e.GetString(2),
            quantity: e.GetInt(3),
            strikePrice: e.GetDecimal(4),
            optionLegType: e.GetEnum<OptionType>(5),
            optionLegAction: e.GetEnum<OptionLegAction>(6),
            createdOn: e.GetDateTime(7),
            createdBy: e.GetString(8),
            updatedOn: e.GetDateTime(9),
            updatedBy: e.GetString(10)
        );

    internal static OptionTradeLegDataReadModel MapToOptionLegData<TDataRecord>(TDataRecord e) where TDataRecord : IObjectDataRecord
        => new(
            orderId: e.GetInt(0),
            tradeId: e.GetInt(1),
            valueDate: e.GetDateOnly(3),
            optionLegId: e.GetString(6),
            tradeType: e.GetEnum<TradeType>(2),
            daysToExpiry: e.GetInt(4),
            tradeStatus: e.GetEnum<TradeStatus>(5),
            bidPrice: e.GetDecimal(7),
            askPrice: e.GetDecimal(8),
            impliedVolatility: e.GetDouble(9),
            delta: e.GetDouble(10),
            gamma: e.GetDouble(11),
            theta: e.GetDouble(12),
            vega: e.GetDouble(13),
            rho: e.GetDouble(14),
            createdOn: e.GetDateTime(15),
            createdBy: e.GetString(16),
            updatedOn: e.GetDateTime(17),
            updatedBy: e.GetString(18)
        );

    static OptionTradeSpreadsDataModel MapToOptionTradeSpreadData<TDataRecord>(TDataRecord e) where TDataRecord : IObjectDataRecord
        => new(
            orderId: e.GetInt(0),
            tradeId: e.GetInt(1),
            valueDate: e.GetDateOnly(2),
            tradeType: e.GetEnum<TradeType>(3),
            sequenceId: e.GetLong(4),
            lossLimit: e.GetDecimal(5),
            winLimit: e.GetDecimal(6),
            forwardSpread: e.GetDecimal(7),
            netSpread: e.GetDecimal(8),
            createdOn: e.GetDateTime(9),
            createdBy: e.GetString(10)
        );


    static OptionTradeReadModel MapToOptionTrade<TDataRecord>(TDataRecord e) where TDataRecord : IObjectDataRecord
        => new(
            orderId: e.GetInt(0),
            tradeId: e.GetInt(1),
            tradeStrategy: e.GetString(2),
            tradeDate: e.GetDateOnly(3),
            maturityDate: e.GetDateOnly(4),
            tradeType: e.GetEnum<TradeType>(5),
            tradeState: e.GetEnum<TradeState>(6),
            tradeAction: e.GetEnum<TradeAction>(7),
            underlyingContractId: e.GetString(8),
            underlyingAssetType: e.GetEnum<AssetType>(9),
            isPrimaryTrade: e.GetBool(10),
            isHedgeTrade: e.GetBool(11),
            createdOn: e.GetDateTime(12),
            createdBy: e.GetString(13),
            updatedOn: e.GetDateTime(14),
            updatedBy: e.GetString(15)
        );

    static TradePriceReadModel MapToTradePrice<TDataRecord>(TDataRecord e) where TDataRecord : IObjectDataRecord
            => new(
                tradeId: e.GetInt(0),
                valueDate: e.GetDateOnly(1),
                netPrice: e.GetDecimal(2),
                netForwardPrice: e.GetDecimal(3)
            );

    /// <summary>
    /// return collection of option trades by order id
    /// </summary>
    /// <param name="orderId"></param>
    /// <returns></returns>
    public async Task<ICollection<OptionTradeReadModel>> GetOptionTradesAsync(int orderId)
    {
        var db = _dbFactory.TradeDb;
        var optionTrades = await (db.Use($"{nameof(TradeDbCql)}.{nameof(TradeDbCql.GetOptionTrades)}", TradeDbCql.GetOptionTrades)
                .SetParameters(new GetOptionTrades(orderId))
                .ExecuteQueryAsync(MapToOptionTrade!))
                .ConfigureAwait(false);

        var sourceTrades = optionTrades.ToArray();
        var hydratedTrades = new OptionTradeReadModel[sourceTrades.Length];
        await (Parallel.ForEachAsync(
            Enumerable.Range(0, sourceTrades.Length),
            new ParallelOptions { MaxDegreeOfParallelism = 4 },
            async (index, token) => hydratedTrades[index] = await (this.FillOptionTradeAsync(sourceTrades[index], token))
                .ConfigureAwait(false)))
                .ConfigureAwait(false);
        return [.. hydratedTrades.OrderBy(e => e.IsPrimaryTrade)];
    }

    /// <summary>
    /// Asynchronously retrieves a collection of all option trades.
    /// </summary>
    /// <remarks>This method queries the database to obtain all available option trade records. The returned
    /// collection may be empty if no trades are found.</remarks>
    /// <returns>A task that represents the asynchronous operation. The task result contains a collection of  <see
    /// cref="OptionTradeReadModel"/> representing the option trades.</returns>
    public async Task<ICollection<OptionTradeReadModel>> GetOptionTradesAsync()
        => await (_dbFactory.TradeDb
            .Use($"{nameof(TradeDbCql)}.{nameof(TradeDbCql.GetOptionTradesAll)}", TradeDbCql.GetOptionTradesAll)
            .ExecuteQueryAsync(MapToOptionTrade!))
                .ConfigureAwait(false);

    /// <inheritdoc />
    public async Task<ICollection<OptionTradeReadModel>> GetOptionTradesAsync(
        int orderId,
        CancellationToken cancellationToken)
    {
        var db = _dbFactory.TradeDb;
        var optionTrades = await (db.Use($"{nameof(TradeDbCql)}.{nameof(TradeDbCql.GetOptionTrades)}", TradeDbCql.GetOptionTrades)
            .SetParameters(new GetOptionTrades(orderId))
            .ExecuteQueryAsync(MapToOptionTrade!, cancellationToken))
                .ConfigureAwait(false);

        var sourceTrades = optionTrades.ToArray();
        var hydratedTrades = new OptionTradeReadModel[sourceTrades.Length];
        await (Parallel.ForEachAsync(
            Enumerable.Range(0, sourceTrades.Length),
            new ParallelOptions
            {
                MaxDegreeOfParallelism = 4,
                CancellationToken = cancellationToken
            },
            async (index, token) =>
                hydratedTrades[index] = await (this.FillOptionTradeAsync(sourceTrades[index], token))
                .ConfigureAwait(false)))
                .ConfigureAwait(false);
        return [.. hydratedTrades.OrderBy(e => e.IsPrimaryTrade)];
    }

    /// <summary>
    /// return option trade by order id and  trade id
    /// </summary>
    /// <param name="orderId"></param>
    /// <param name="tradeId"></param>
    /// <returns></returns>
    public async Task<OptionTradeReadModel?> GetOptionTradeAsync(int orderId, int tradeId)
    {
        var db = _dbFactory.TradeDb;
        var optionTrade = await (db.Use($"{nameof(TradeDbCql)}.{nameof(TradeDbCql.GetOptionTrade)}", TradeDbCql.GetOptionTrade)
               .SetParameters(new GetOptionTrade(orderId, tradeId))
               .ExecuteSingleAsync(MapToOptionTrade!))
                .ConfigureAwait(false);
        if (optionTrade is not null)
            optionTrade = await (this.FillOptionTradeAsync(optionTrade))
                .ConfigureAwait(false);
        return optionTrade;
    }

    /// <inheritdoc />
    public async Task<OptionTradeReadModel?> GetOptionTradeAsync(
        int orderId,
        int tradeId,
        CancellationToken cancellationToken)
    {
        var db = _dbFactory.TradeDb;
        var optionTrade = await (db.Use($"{nameof(TradeDbCql)}.{nameof(TradeDbCql.GetOptionTrade)}", TradeDbCql.GetOptionTrade)
            .SetParameters(new GetOptionTrade(orderId, tradeId))
            .ExecuteSingleAsync(MapToOptionTrade!, cancellationToken))
                .ConfigureAwait(false);
        return optionTrade is null
            ? null
            : await (this.FillOptionTradeAsync(optionTrade, cancellationToken))
                .ConfigureAwait(false);
    }

    /// <summary>
    /// return option trade spread data
    /// </summary>
    /// <param name="orderId"></param>
    /// <param name="tradeId"></param>
    /// <param name="valueDate"></param>
    /// <param name="tradeType"></param>
    /// <returns></returns>
    public async Task<OptionTradeSpreadsDataModel?> GetOptionTradeSpreadDataAsync(int orderId, int tradeId, DateOnly valueDate, TradeType tradeType)
        => await (_dbFactory.TradeDb
            .Use($"{nameof(TradeDbCql)}.{nameof(TradeDbCql.GetOptionTradeSpreadData)}", TradeDbCql.GetOptionTradeSpreadData)
            .SetParameters(new GetOptionTradeSpreadData(orderId, tradeId, valueDate, tradeType.ToStringFast()))
            .ExecuteSingleAsync(MapToOptionTradeSpreadData!))
                .ConfigureAwait(false);

    /// <summary>
    /// Asynchronously retrieves a collection of option trade spread data.
    /// </summary>
    /// <remarks>This method queries the trade database to obtain all available option trade spread data.  The
    /// returned collection will be empty if no data is found.</remarks>
    /// <returns>A task that represents the asynchronous operation. The task result contains a collection of  <see
    /// cref="OptionTradeSpreadsDataModel"/> representing the option trade spread data.</returns>
    public async Task<ICollection<OptionTradeSpreadsDataModel>> GetOptionTradeSpreadDataAsync()
        => await (_dbFactory.TradeDb
            .Use($"{nameof(TradeDbCql)}.{nameof(TradeDbCql.GetOptionTradeSpreadDataAll)}", TradeDbCql.GetOptionTradeSpreadDataAll)
            .ExecuteQueryAsync(MapToOptionTradeSpreadData!))
                .ConfigureAwait(false);

    /// <inheritdoc />
    public Task<OptionTradeSpreadsDataModel?> GetOptionTradeSpreadDataAsync(
        int orderId,
        int tradeId,
        DateOnly valueDate,
        TradeType tradeType,
        CancellationToken cancellationToken)
        => _dbFactory.TradeDb
            .Use($"{nameof(TradeDbCql)}.{nameof(TradeDbCql.GetOptionTradeSpreadData)}", TradeDbCql.GetOptionTradeSpreadData)
            .SetParameters(new GetOptionTradeSpreadData(
                orderId,
                tradeId,
                valueDate,
                tradeType.ToStringFast()))
            .ExecuteSingleAsync(MapToOptionTradeSpreadData!, cancellationToken);

    /// <summary>
    /// return option trade spread bar data
    /// </summary>
    /// <param name="orderId"></param>
    /// <param name="tradeId"></param>
    /// <param name="valueDate"></param>
    /// <param name="tradeType"></param>
    /// <param name="startDate"></param>
    /// <param name="endDate"></param>
    /// <returns></returns>
    public async Task<ICollection<OptionTradeSpreadBarsDataModel>> GetOptionTradeSpreadBarDataAsync(
        int orderId, int tradeId, DateOnly valueDate, TradeType tradeType, DateTime startDate, DateTime endDate)
        => await (_dbFactory.TradeDb
            .Use($"{nameof(TradeDbCql)}.{nameof(TradeDbCql.GetOptionTradeSpreadBarData)}", TradeDbCql.GetOptionTradeSpreadBarData)
            .SetParameters(new GetOptionTradeSpreadBarData(orderId, tradeId, valueDate, tradeType.ToStringFast(), startDate, endDate))
            .ExecuteQueryAsync(MapToOptionTradeSpreadBarsData!))
                .ConfigureAwait(false);

    /// <summary>
    /// Asynchronously retrieves a collection of option trade spread bar data.
    /// </summary>
    /// <remarks>This method queries the trade database to obtain all available option trade spread bar
    /// data.</remarks>
    /// <returns>A task that represents the asynchronous operation. The task result contains a collection of <see
    /// cref="OptionTradeSpreadBarsDataModel"/> representing the option trade spread bar data.</returns>
    public async Task<ICollection<OptionTradeSpreadBarsDataModel>> GetOptionTradeSpreadBarDataAsync()
        => await (_dbFactory.TradeDb
            .Use($"{nameof(TradeDbCql)}.{nameof(TradeDbCql.GetOptionTradeSpreadBarDataAll)}", TradeDbCql.GetOptionTradeSpreadBarDataAll)
            .ExecuteQueryAsync(MapToOptionTradeSpreadBarsData!))
                .ConfigureAwait(false);

    /// <inheritdoc />
    public Task<ICollection<OptionTradeSpreadBarsDataModel>> GetOptionTradeSpreadBarDataAsync(
        int orderId,
        int tradeId,
        DateOnly valueDate,
        TradeType tradeType,
        DateTime startDate,
        DateTime endDate,
        CancellationToken cancellationToken)
        => _dbFactory.TradeDb
            .Use($"{nameof(TradeDbCql)}.{nameof(TradeDbCql.GetOptionTradeSpreadBarData)}", TradeDbCql.GetOptionTradeSpreadBarData)
            .SetParameters(new GetOptionTradeSpreadBarData(
                orderId,
                tradeId,
                valueDate,
                tradeType.ToStringFast(),
                startDate,
                endDate))
            .ExecuteQueryAsync(MapToOptionTradeSpreadBarsData!, cancellationToken);

    /// <summary>
    /// return iron condor trade price
    /// </summary>
    /// <param name="tradeId"></param>
    /// <param name="valueDate"></param>
    /// <returns></returns>
    public async Task<TradePriceReadModel?> GetIronCondorTradePriceAsync(int tradeId, DateOnly valueDate)
        => await (_dbFactory.TradeDb
            .Use($"{nameof(TradeDbCql)}.{nameof(TradeDbCql.GetOptionLegs)}", TradeDbCql.GetOptionLegs)
            .SetParameters(new GetOptionLegsWithValueDate(tradeId, valueDate))
            .ExecuteSingleAsync(MapToTradePrice))
                .ConfigureAwait(false);

    /// <inheritdoc />
    public Task<TradePriceReadModel?> GetIronCondorTradePriceAsync(
        int tradeId,
        DateOnly valueDate,
        CancellationToken cancellationToken)
        => _dbFactory.TradeDb
            .Use($"{nameof(TradeDbCql)}.{nameof(TradeDbCql.GetOptionLegs)}", TradeDbCql.GetOptionLegs)
            .SetParameters(new GetOptionLegsWithValueDate(tradeId, valueDate))
            .ExecuteSingleAsync(MapToTradePrice, cancellationToken);

    /// <summary>
    /// fill option trade with option trade graph data
    /// </summary>
    /// <param name="optionTrade"></param>
    /// <returns></returns>

    /// <summary>
    /// return all trade positions for selected trade
    /// </summary>
    /// <param name="orderId"></param>
    /// <param name="tradeId"></param>
    /// <returns></returns>
    public async Task<ICollection<TradePositionReadModel>> GetTradePositionsAsync(int orderId, int tradeId)
    {
        var db = _dbFactory.TradeDb;
        var tradePositionsTask = db.Use($"{nameof(TradeDbCql)}.{nameof(TradeDbCql.GetTradePositions)}", TradeDbCql.GetTradePositions)
             .SetParameters(new GetTradePositions(orderId, tradeId))
             .ExecuteQueryAsync(MapToTradePosition!);

        var tradePositions = await (tradePositionsTask)
                .ConfigureAwait(false);
        if (tradePositions.Count == 0)
            return tradePositions;

        var optionLegsTask = db.Use($"{nameof(TradeDbCql)}.{nameof(TradeDbCql.GetOptionLegsByOrderAndTrade)}", TradeDbCql.GetOptionLegsByOrderAndTrade)
            .SetParameters(new GetOptionLegsByOrderAndTrade(orderId, tradeId))
            .ExecuteQueryAsync(MapToOptionLeg!);
        var valueDates = tradePositions.Select(position => position.ValueDate).Distinct().ToArray();
        var legDataByDate = new ICollection<OptionTradeLegDataReadModel>[valueDates.Length];
        await (Parallel.ForEachAsync(
            Enumerable.Range(0, valueDates.Length),
            new ParallelOptions { MaxDegreeOfParallelism = 4 },
            async (dateIndex, _) => legDataByDate[dateIndex] = await (db.Use($"{nameof(TradeDbCql)}.{nameof(TradeDbCql.GetOptionLegData)}", TradeDbCql.GetOptionLegData)
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

    /// <summary>
    /// Asynchronously retrieves all trade positions from the database.
    /// </summary>
    /// <remarks>This method queries the database to obtain all trade positions and maps them to  <see
    /// cref="TradePositionReadModel"/> instances. Ensure that the database connection is properly  configured before
    /// calling this method.</remarks>
    /// <returns>A task that represents the asynchronous operation. The task result contains a collection of  <see
    /// cref="TradePositionReadModel"/> objects representing the trade positions.</returns>
    public async Task<ICollection<TradePositionReadModel>> GetTradePositionsAsync()
        => await (_dbFactory.TradeDb
            .Use($"{nameof(TradeDbCql)}.{nameof(TradeDbCql.GetTradePositionsAll)}", TradeDbCql.GetTradePositionsAll)
            .ExecuteQueryAsync(MapToTradePosition!))
                .ConfigureAwait(false);

    /// <inheritdoc />
    public async Task<ICollection<TradePositionReadModel>> GetTradePositionsAsync(
        int orderId,
        int tradeId,
        CancellationToken cancellationToken)
    {
        var db = _dbFactory.TradeDb;
        var tradePositions = await (db.Use($"{nameof(TradeDbCql)}.{nameof(TradeDbCql.GetTradePositions)}", TradeDbCql.GetTradePositions)
            .SetParameters(new GetTradePositions(orderId, tradeId))
            .ExecuteQueryAsync(MapToTradePosition!, cancellationToken))
                .ConfigureAwait(false);
        if (tradePositions.Count == 0)
            return tradePositions;

        var optionLegsTask = db.Use($"{nameof(TradeDbCql)}.{nameof(TradeDbCql.GetOptionLegsByOrderAndTrade)}", TradeDbCql.GetOptionLegsByOrderAndTrade)
            .SetParameters(new GetOptionLegsByOrderAndTrade(orderId, tradeId))
            .ExecuteQueryAsync(MapToOptionLeg!, cancellationToken);
        var valueDates = tradePositions
            .Select(position => position.ValueDate)
            .Distinct()
            .ToArray();
        var legDataByDate = new ICollection<OptionTradeLegDataReadModel>[valueDates.Length];
        await (Parallel.ForEachAsync(
            Enumerable.Range(0, valueDates.Length),
            new ParallelOptions
            {
                MaxDegreeOfParallelism = 4,
                CancellationToken = cancellationToken
            },
            async (dateIndex, token) =>
                legDataByDate[dateIndex] = await (db.Use($"{nameof(TradeDbCql)}.{nameof(TradeDbCql.GetOptionLegData)}", TradeDbCql.GetOptionLegData)
                    .SetParameters(new GetOptionLegData(orderId, tradeId, valueDates[dateIndex]))
                    .ExecuteQueryAsync(MapToOptionLegData!, token))
                .ConfigureAwait(false)))
                .ConfigureAwait(false);

        var optionLegs = await (optionLegsTask)
                .ConfigureAwait(false);
        var optionLegById = optionLegs.ToDictionary(
            leg => leg.ContractId,
            StringComparer.Ordinal);
        var legDataByPosition = MapToLegDataByPosition(
            legDataByDate,
            optionLegById,
            requireOptionLeg: false);
        foreach (var position in tradePositions)
        {
            if (legDataByPosition.TryGetValue(MapToPositionKey(position), out var legData))
                position.AddOptionLegData(legData);
        }
        return tradePositions;
    }

    internal static (TradeType TradeType, DateOnly ValueDate, int DaysToExpiry, TradeStatus TradeStatus) MapToPositionKey(
        TradePositionReadModel position)
        => (position.TradeType, position.ValueDate, position.DaysToExpiry, position.TradeStatus);

    internal static Dictionary<(TradeType TradeType, DateOnly ValueDate, int DaysToExpiry, TradeStatus TradeStatus), List<OptionTradeLegDataReadModel>> MapToLegDataByPosition(
        IEnumerable<ICollection<OptionTradeLegDataReadModel>> legDataByDate,
        IReadOnlyDictionary<string, OptionTradeLegReadModel> optionLegById,
        bool requireOptionLeg)
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


    /// <summary>
    /// return trade position trade types
    /// </summary>
    /// <param name="orderId"></param>
    /// <param name="tradeId"></param>
    /// <param name="valueDate"></param>
    /// <param name="daysToExpiry"></param>
    /// <param name="tradeStatus"></param>
    /// <returns></returns>
    public async Task<ICollection<string>> GetTradePositionTradeTypesAsync(
       int orderId, int tradeId, DateOnly valueDate, TradeStatus tradeStatus, int daysToExpiry)
        => [.. (await (_dbFactory.TradeDb
                .Use($"{nameof(TradeDbCql)}.{nameof(TradeDbCql.GetTradePositionsById)}", TradeDbCql.GetTradePositionsById)
                .SetParameters(new GetTradePositionsById(
                    orderId,
                    tradeId,
                    valueDate,
                    tradeStatus.ToStringFast(),
                    daysToExpiry
                ))
                .ExecuteQueryAsync(MapToTradePosition!))
                .ConfigureAwait(false)).Select(e => e.TradeType.ToStringFast())];

    /// <inheritdoc />
    public async Task<ICollection<string>> GetTradePositionTradeTypesAsync(
        int orderId,
        int tradeId,
        DateOnly valueDate,
        TradeStatus tradeStatus,
        int daysToExpiry,
        CancellationToken cancellationToken)
        => [.. (await (_dbFactory.TradeDb
            .Use($"{nameof(TradeDbCql)}.{nameof(TradeDbCql.GetTradePositionsById)}", TradeDbCql.GetTradePositionsById)
            .SetParameters(new GetTradePositionsById(
                orderId,
                tradeId,
                valueDate,
                tradeStatus.ToStringFast(),
                daysToExpiry))
            .ExecuteQueryAsync(MapToTradePosition!, cancellationToken))
                .ConfigureAwait(false))
            .Select(e => e.TradeType.ToStringFast())];

    /// <summary>
    /// return trade position
    /// </summary>
    /// <param name="tradeId"></param>
    /// <param name="tradeType"></param>
    /// <param name="valueDate"></param>
    /// <param name="daysToExpiry"></param>
    /// <param name="tradeStatus"></param>
    /// <returns></returns>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "MA0051", Justification = "Keep the transaction, resource ownership and failure ordering together during this provider migration; splitting it would obscure the atomic persistence boundary.")]
    public async Task<TradePositionReadModel?> GetTradePositionAsync(
        int orderId,
        int tradeId,
        TradeType tradeType,
        DateOnly valueDate,
        int daysToExpiry,
        TradeStatus tradeStatus)
    {
        var db = _dbFactory.TradeDb;
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
                updatedOptionLegData.Add((new OptionTradeLegDataReadModel(
                        orderId: e.EntityId.OrderId,
                        tradeId: e.EntityId.TradeId,
                        tradeType: e.EntityId.TradeType,
                        valueDate: e.EntityId.ValueDate,
                        daysToExpiry: e.EntityId.DaysToExpiry,
                        tradeStatus: e.EntityId.TradeStatus,
                        optionLegId: old.OptionLegId,
                        bidPrice: old.BidPrice,
                        askPrice: old.AskPrice,
                        impliedVolatility: old.ImpliedVolatility,
                        delta: old.Delta,
                        gamma: old.Gamma,
                        theta: old.Theta,
                        vega: old.Vega,
                        rho: old.Rho,
                        createdOn: old.CreatedOn,
                        createdBy: old.CreatedBy,
                        updatedOn: old.UpdatedOn,
                        updatedBy: old.UpdatedBy)).SetOptionLeg(optionLegs.SingleOrDefault(o => string.Equals(o.ContractId,old.OptionLegId,StringComparison.Ordinal))));
            }
            tradePosition.AddOptionLegData(updatedOptionLegData);
        }
        return tradePosition;

        async Task<ICollection<OptionTradeLegReadModel>> EnumerateOptionLegs()
            => await (db.Use($"{nameof(TradeDbCql)}.{nameof(TradeDbCql.GetOptionLegsByOrderAndTrade)}", TradeDbCql.GetOptionLegsByOrderAndTrade)
                   .SetParameters(new GetOptionLegsByOrderAndTrade(orderId, tradeId))
                   .ExecuteQueryAsync(MapToOptionLeg!))
                .ConfigureAwait(false);

        async Task<ICollection<OptionTradeLegDataReadModel>> EnumerateOptionLegData()
            => [.. (await (db.Use($"{nameof(TradeDbCql)}.{nameof(TradeDbCql.GetOptionLegData)}", TradeDbCql.GetOptionLegData)
                   .SetParameters(new GetOptionLegData(
                       orderId,
                       tradeId,
                       valueDate
                   ))
                   .ExecuteQueryAsync(MapToOptionLegData!))
                .ConfigureAwait(false))
                        .Where(e => e.TradeType == tradeType
                            && e.TradeStatus == tradeStatus
                            && e.DaysToExpiry == daysToExpiry
                        )];

        async Task<TradePositionReadModel?> GetTradePositionAsync()
            => await (db.Use($"{nameof(TradeDbCql)}.{nameof(TradeDbCql.GetTradePosition)}", TradeDbCql.GetTradePosition)
                   .SetParameters(new GetTradePosition(
                       orderId,
                       tradeId,
                       valueDate,
                       tradeStatus.ToStringFast(),
                       daysToExpiry,
                       tradeType.ToStringFast()
                   ))
                   .ExecuteSingleAsync(MapToTradePosition!))
                .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<TradePositionReadModel?> GetTradePositionAsync(
        int orderId,
        int tradeId,
        TradeType tradeType,
        DateOnly valueDate,
        int daysToExpiry,
        TradeStatus tradeStatus,
        CancellationToken cancellationToken)
    {
        var db = _dbFactory.TradeDb;
        var tradePosition = await (db.Use($"{nameof(TradeDbCql)}.{nameof(TradeDbCql.GetTradePosition)}", TradeDbCql.GetTradePosition)
            .SetParameters(new GetTradePosition(
                orderId,
                tradeId,
                valueDate,
                tradeStatus.ToStringFast(),
                daysToExpiry,
                tradeType.ToStringFast()))
            .ExecuteSingleAsync(MapToTradePosition!, cancellationToken))
                .ConfigureAwait(false);
        if (tradePosition is null)
            return null;

        var optionLegsTask = db.Use($"{nameof(TradeDbCql)}.{nameof(TradeDbCql.GetOptionLegsByOrderAndTrade)}", TradeDbCql.GetOptionLegsByOrderAndTrade)
            .SetParameters(new GetOptionLegsByOrderAndTrade(orderId, tradeId))
            .ExecuteQueryAsync(MapToOptionLeg!, cancellationToken);
        var optionLegDataTask = db.Use($"{nameof(TradeDbCql)}.{nameof(TradeDbCql.GetOptionLegData)}", TradeDbCql.GetOptionLegData)
            .SetParameters(new GetOptionLegData(orderId, tradeId, valueDate))
            .ExecuteQueryAsync(MapToOptionLegData!, cancellationToken);
        await (Task.WhenAll(optionLegsTask, optionLegDataTask))
                .ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();

        var optionLegById = (await (optionLegsTask)
                .ConfigureAwait(false))
            .ToDictionary(leg => leg.ContractId, StringComparer.Ordinal);
        var updatedOptionLegData = (await (optionLegDataTask)
                .ConfigureAwait(false))
            .Where(e => e.TradeType == tradeType
                && e.TradeStatus == tradeStatus
                && e.DaysToExpiry == daysToExpiry)
            .Select(old => (new OptionTradeLegDataReadModel(
                orderId: tradePosition.EntityId.OrderId,
                tradeId: tradePosition.EntityId.TradeId,
                tradeType: tradePosition.EntityId.TradeType,
                valueDate: tradePosition.EntityId.ValueDate,
                daysToExpiry: tradePosition.EntityId.DaysToExpiry,
                tradeStatus: tradePosition.EntityId.TradeStatus,
                optionLegId: old.OptionLegId,
                bidPrice: old.BidPrice,
                askPrice: old.AskPrice,
                impliedVolatility: old.ImpliedVolatility,
                delta: old.Delta,
                gamma: old.Gamma,
                theta: old.Theta,
                vega: old.Vega,
                rho: old.Rho,
                createdOn: old.CreatedOn,
                createdBy: old.CreatedBy,
                updatedOn: old.UpdatedOn,
                updatedBy: old.UpdatedBy))
                .SetOptionLeg(optionLegById.GetValueOrDefault(old.OptionLegId)))
            .ToArray();
        tradePosition.AddOptionLegData(updatedOptionLegData);
        return tradePosition;
    }

    /// <summary>
    /// return trade history by trade order
    /// </summary>
    /// <param name="orderId"></param>
    /// <returns></returns>
    public async Task<ICollection<TradeHistoryReadModel>> GetTradeHistoryAsync(int orderId)
        => [.. (await (_dbFactory.TradeDb
                .Use($"{nameof(TradeDbCql)}.{nameof(TradeDbCql.GetTradeHistory)}", TradeDbCql.GetTradeHistory)
                .SetParameters(new GetTradeHistory(orderId))
                .ExecuteQueryAsync(MapToTradeHistory!))
                .ConfigureAwait(false)).OrderBy(e => e.ValueDate)];

    /// <inheritdoc />
    public async Task<ICollection<TradeHistoryReadModel>> GetTradeHistoryAsync(
        int orderId,
        CancellationToken cancellationToken)
        => [.. (await (_dbFactory.TradeDb
            .Use($"{nameof(TradeDbCql)}.{nameof(TradeDbCql.GetTradeHistory)}", TradeDbCql.GetTradeHistory)
            .SetParameters(new GetTradeHistory(orderId))
            .ExecuteQueryAsync(MapToTradeHistory!, cancellationToken))
                .ConfigureAwait(false))
            .OrderBy(e => e.ValueDate)];


    /// <summary>
    /// return list of contract ids
    /// </summary>
    /// <param name="tradeId"></param>
    /// <returns></returns>
    public async Task<ICollection<string>> GetOptionLegContractIdsAsync(int tradeId)
    {
        var optionLegs = await (_dbFactory.TradeDb
                .Use($"{nameof(TradeDbCql)}.{nameof(TradeDbCql.GetOptionLegs)}", TradeDbCql.GetOptionLegs)
                .SetParameters(new GetOptionLegs(tradeId))
                .ExecuteQueryAsync(MapToOptionLeg!))
                .ConfigureAwait(false);
        var latestTradeLegs = optionLegs
            .GroupBy(e => e.OrderId)
            .OrderByDescending(e => e.Max(leg => leg.UpdatedOn))
            .FirstOrDefault();
        return latestTradeLegs is null
            ? []
            : [.. latestTradeLegs.Select(e => e.ContractId)];
    }

    /// <inheritdoc />
    public async Task<ICollection<string>> GetOptionLegContractIdsAsync(
        int tradeId,
        CancellationToken cancellationToken)
        => [.. (await (_dbFactory.TradeDb
            .Use($"{nameof(TradeDbCql)}.{nameof(TradeDbCql.GetOptionLegs)}", TradeDbCql.GetOptionLegs)
            .SetParameters(new GetOptionLegs(tradeId))
            .ExecuteQueryAsync(MapToOptionLeg!, cancellationToken))
                .ConfigureAwait(false))
            .Select(e => e.ContractId)];

    /// <summary>
    /// Asynchronously retrieves a collection of all option leg data models.
    /// </summary>
    /// <remarks>This method queries the database to obtain all option leg entries. Ensure that the database
    /// connection is properly configured before calling this method.</remarks>
    /// <returns>A task that represents the asynchronous operation. The task result contains a collection of  <see
    /// cref="OptionTradeLegReadModel"/> representing all option legs.</returns>
    public async Task<ICollection<OptionTradeLegReadModel>> GetOptionLegsAsync()
        => await (_dbFactory.TradeDb
            .Use($"{nameof(TradeDbCql)}.{nameof(TradeDbCql.GetOptionLegsAll)}", TradeDbCql.GetOptionLegsAll)
            .ExecuteQueryAsync(MapToOptionLeg!))
                .ConfigureAwait(false);

    /// <summary>
    /// Asynchronously retrieves a collection of option leg data models from the database.
    /// </summary>
    /// <remarks>This method queries the database to obtain all available option leg data. The returned
    /// collection will be empty if no data is found.</remarks>
    /// <returns>A task that represents the asynchronous operation. The task result contains a collection of  <see
    /// cref="OptionTradeLegDataReadModel"/> instances representing the option leg data.</returns>
    public async Task<ICollection<OptionTradeLegDataReadModel>> GetOptionLegDataAsync()
      => await (_dbFactory.TradeDb
          .Use($"{nameof(TradeDbCql)}.{nameof(TradeDbCql.GetOptionLegDataAll)}", TradeDbCql.GetOptionLegDataAll)
          .ExecuteQueryAsync(MapToOptionLegData!))
                .ConfigureAwait(false);


    /// <summary>
    /// return trade quantity
    /// </summary>
    /// <param name="tradeId"></param>
    /// <returns></returns>
    public async Task<int> GetTradeQuantityAsync(int tradeId)
    {
        var db = _dbFactory.TradeDb;
        var optionLegs = await (db.Use($"{nameof(TradeDbCql)}.{nameof(TradeDbCql.GetOptionLegs)}", TradeDbCql.GetOptionLegs)
            .SetParameters(new GetOptionLegs(tradeId))
            .ExecuteQueryAsync(MapToOptionLeg!))
                .ConfigureAwait(false);
        var latestTradeLegs = optionLegs
            .GroupBy(e => e.OrderId)
            .OrderByDescending(e => e.Max(leg => leg.UpdatedOn))
            .FirstOrDefault();
        return latestTradeLegs is not null
            ? latestTradeLegs.Sum(e => e.Quantity) / latestTradeLegs.Count()
            : 0;
    }

    /// <inheritdoc />
    public async Task<int> GetTradeQuantityAsync(
        int tradeId,
        CancellationToken cancellationToken)
    {
        var optionLegs = await (_dbFactory.TradeDb
            .Use($"{nameof(TradeDbCql)}.{nameof(TradeDbCql.GetOptionLegs)}", TradeDbCql.GetOptionLegs)
            .SetParameters(new GetOptionLegs(tradeId))
            .ExecuteQueryAsync(MapToOptionLeg!, cancellationToken))
                .ConfigureAwait(false);
        var latestTradeLegs = optionLegs
            .GroupBy(e => e.OrderId)
            .OrderByDescending(e => e.Max(leg => leg.UpdatedOn))
            .FirstOrDefault();
        return latestTradeLegs is not null
            ? latestTradeLegs.Sum(e => e.Quantity) / latestTradeLegs.Count()
            : 0;
    }

    /// <summary>
    /// return trade limit for selected trade
    /// </summary>
    /// <param name="tradeId"></param>
    /// <returns></returns>
    public async Task<TradeLimitReadModel?> GetTradeLimitAsync(int tradeId)
    {
        if(string.Equals(ProviderName,"System.Data.Postgres",StringComparison.Ordinal))
            return (await (this.NativeTransaction().ExecuteOnceAsync((db,ct)=>new PostgresTradeFlowReadStore().LimitsAsync(db,tradeId,ct)))
                .ConfigureAwait(false)).SingleOrDefault();
        var db = _dbFactory.TradeDb;
        var tradeLimit = await (db.Use($"{nameof(TradeDbCql)}.{nameof(TradeDbCql.GetTradeLimit)}", TradeDbCql.GetTradeLimit)
                .SetParameters(new GetTradeLimit(tradeId))
                .ExecuteSingleAsync(MapToTradeLimit!))
                .ConfigureAwait(false);

        if (tradeLimit is null) return null;
        var tradeTypeLimit = await (GetTradeTypeLimitAsync(tradeLimit.TradeId, tradeLimit.TradeType))
                .ConfigureAwait(false);
        return tradeTypeLimit is not null
            ? tradeLimit with
            {
                MaxLossLimit = tradeTypeLimit.MaxLossLimit,
                MinProfitLimit = tradeTypeLimit.MinProfitLimit,
                MaxProfitLimit = tradeTypeLimit.MaxProfitLimit
            }
            : tradeLimit;
    }

    /// <inheritdoc />
    public async Task<TradeLimitReadModel?> GetTradeLimitAsync(
        int tradeId,
        CancellationToken cancellationToken)
    {
        if(string.Equals(ProviderName,"System.Data.Postgres",StringComparison.Ordinal))
            return (await (this.NativeTransaction().ExecuteOnceAsync((db,ct)=>new PostgresTradeFlowReadStore().LimitsAsync(db,tradeId,ct),cancellationToken))
                .ConfigureAwait(false)).SingleOrDefault();
        var tradeLimit = await (_dbFactory.TradeDb
            .Use($"{nameof(TradeDbCql)}.{nameof(TradeDbCql.GetTradeLimit)}", TradeDbCql.GetTradeLimit)
            .SetParameters(new GetTradeLimit(tradeId))
            .ExecuteSingleAsync(MapToTradeLimit!, cancellationToken))
                .ConfigureAwait(false);
        if (tradeLimit is null)
            return null;

        var tradeTypeLimit = await (GetTradeTypeLimitAsync(
            tradeLimit.TradeId,
            tradeLimit.TradeType,
            cancellationToken))
                .ConfigureAwait(false);
        return tradeTypeLimit is not null
            ? tradeLimit with
            {
                MaxLossLimit = tradeTypeLimit.MaxLossLimit,
                MinProfitLimit = tradeTypeLimit.MinProfitLimit,
                MaxProfitLimit = tradeTypeLimit.MaxProfitLimit
            }
            : tradeLimit;
    }

    /// <summary>
    /// Asynchronously retrieves all trade limits from the database.
    /// </summary>
    /// <remarks>This method queries the database to obtain the current trade limits and returns them as a
    /// collection of view models. The operation is performed asynchronously to avoid blocking the calling
    /// thread.</remarks>
    /// <returns>A task that represents the asynchronous operation. The task result contains a collection of  <see
    /// cref="TradeLimitReadModel"/> objects representing the trade limits.</returns>
    public async Task<ICollection<TradeLimitReadModel>> GetTradeLimitsAsync()
    {
        if(string.Equals(ProviderName,"System.Data.Postgres",StringComparison.Ordinal)) return (await (this.NativeTransaction().ExecuteOnceAsync((db,ct)=>new PostgresTradeFlowReadStore().LimitsAsync(db,null,ct)))
                .ConfigureAwait(false)).ToArray();
        return await (_dbFactory.TradeDb
            .Use($"{nameof(TradeDbCql)}.{nameof(TradeDbCql.GetTradeLimitAll)}", TradeDbCql.GetTradeLimitAll)
            .ExecuteQueryAsync(MapToTradeLimit!))
                .ConfigureAwait(false);
    }

    /// <summary>
    /// return trade limit for selected trade
    /// </summary>
    /// <param name="tradeId"></param>
    /// <param name="tradeType"></param>
    /// <returns></returns>
    public async Task<TradeTypeLimitReadModel?> GetTradeTypeLimitAsync(int tradeId, TradeType tradeType)
    {
        if(string.Equals(ProviderName,"System.Data.Postgres",StringComparison.Ordinal)) return (await (this.NativeTransaction().ExecuteOnceAsync((db,ct)=>new PostgresTradeFlowReadStore().SpreadLimitsAsync(db,tradeId,tradeType,ct)))
                .ConfigureAwait(false)).SingleOrDefault();
        return await (_dbFactory.TradeDb
            .Use($"{nameof(TradeDbCql)}.{nameof(TradeDbCql.GetTradeTypeLimit)}", TradeDbCql.GetTradeTypeLimit)
            .SetParameters(new GetTradeTypeLimit(tradeId, tradeType.ToStringFast()))
            .ExecuteSingleAsync(MapToTradeTypeLimit!))
                .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<TradeTypeLimitReadModel?> GetTradeTypeLimitAsync(
        int tradeId,
        TradeType tradeType,
        CancellationToken cancellationToken)
    {
        if(string.Equals(ProviderName,"System.Data.Postgres",StringComparison.Ordinal))
            return (await (this.NativeTransaction().ExecuteOnceAsync((db,ct)=>new PostgresTradeFlowReadStore().SpreadLimitsAsync(db,tradeId,tradeType,ct),cancellationToken))
                .ConfigureAwait(false)).SingleOrDefault();
        return await (_dbFactory.TradeDb
            .Use($"{nameof(TradeDbCql)}.{nameof(TradeDbCql.GetTradeTypeLimit)}", TradeDbCql.GetTradeTypeLimit)
            .SetParameters(new GetTradeTypeLimit(tradeId, tradeType.ToStringFast()))
            .ExecuteSingleAsync(MapToTradeTypeLimit!, cancellationToken))
                .ConfigureAwait(false);
    }

    /// <summary>
    /// return all trade type limit for selected trade
    /// </summary>
    /// <param name="tradeId"></param>
    /// <returns></returns>
    public async Task<ICollection<TradeTypeLimitReadModel>> GetTradeTypeLimitsAsync(int tradeId)
    {
        if(string.Equals(ProviderName,"System.Data.Postgres",StringComparison.Ordinal)) return (await (this.NativeTransaction().ExecuteOnceAsync((db,ct)=>new PostgresTradeFlowReadStore().SpreadLimitsAsync(db,tradeId,null,ct)))
                .ConfigureAwait(false)).ToArray();
        return await (_dbFactory.TradeDb
            .Use($"{nameof(TradeDbCql)}.{nameof(TradeDbCql.GetTradeTypeLimits)}", TradeDbCql.GetTradeTypeLimits)
            .SetParameters(new GetTradeTypeLimits(tradeId))
            .ExecuteQueryAsync(MapToTradeTypeLimit!))
                .ConfigureAwait(false);
    }

    /// <summary>
    /// Asynchronously retrieves a collection of trade type limits.
    /// </summary>
    /// <returns>A task that represents the asynchronous operation. The task result contains a collection of  <see
    /// cref="TradeTypeLimitReadModel"/> representing the trade type limits.</returns>
    public async Task<ICollection<TradeTypeLimitReadModel>> GetTradeTypeLimitsAsync()
    {
        if(string.Equals(ProviderName,"System.Data.Postgres",StringComparison.Ordinal)) return (await (this.NativeTransaction().ExecuteOnceAsync((db,ct)=>new PostgresTradeFlowReadStore().SpreadLimitsAsync(db,null,null,ct)))
                .ConfigureAwait(false)).ToArray();
        return await (_dbFactory.TradeDb
            .Use($"{nameof(TradeDbCql)}.{nameof(TradeDbCql.GetTradeTypeLimitAll)}", TradeDbCql.GetTradeTypeLimitAll)
            .ExecuteQueryAsync(MapToTradeTypeLimit!))
                .ConfigureAwait(false);
    }

    /// <summary>
    /// Retrieves a trade placement signal for the specified contract, value date, and sequence ID.
    /// </summary>
    /// <remarks>This method queries the database to retrieve a trade placement signal based on the provided
    /// parameters. Ensure that the <paramref name="contractId"/> is valid and corresponds to an existing
    /// contract.</remarks>
    /// <param name="contractId">The unique identifier of the contract for which the trade placement signal is requested. Cannot be null or
    /// empty.</param>
    /// <param name="valueDate">The date associated with the trade placement signal.</param>
    /// <returns>A <see cref="TradePlacementSignalReadModel"/> representing the trade placement signal, or <see langword="null"/>
    /// if no signal is found.</returns>
    public async Task<TradePlacementSignalReadModel?> GetTradePlacementSignalAsync(string contractId, DateOnly valueDate)
        => await (_dbFactory.TradeDb
            .Use($"{nameof(TradeDbCql)}.{nameof(TradeDbCql.GetTradePlacementSignal)}", TradeDbCql.GetTradePlacementSignal)
            .SetParameters(new GetTradePlacementSignal(contractId, valueDate))
            .ExecuteSingleAsync(MapToTradePlacementSignal!))
                .ConfigureAwait(false);

    /// <summary>
    /// Asynchronously retrieves a collection of trade fill records.
    /// </summary>
    /// <remarks>This method queries the database to obtain all trade fill records and maps them to  <see
    /// cref="TradeFillReadModel"/> objects. The operation is performed asynchronously.</remarks>
    /// <returns>A task representing the asynchronous operation. The task result contains a collection of  <see
    /// cref="TradeFillReadModel"/> instances representing the trade fill records.</returns>
    public async Task<ICollection<TradeFillReadModel>> GetTradeFillsAsync()
        => await (_dbFactory.TradeDb
            .Use($"{nameof(TradeDbCql)}.{nameof(TradeDbCql.GetTradeFillsAll)}", TradeDbCql.GetTradeFillsAll)
            .ExecuteQueryAsync(MapToTradeFill!))
                .ConfigureAwait(false);

    /// <summary>
    /// return trade fills for selected trade
    /// </summary>
    /// <param name="tradeId"></param>
    /// <returns></returns>
    public async Task<ICollection<TradeFillReadModel>> GetTradeFillsAsync(int orderId, int tradeId)
    {
        var db = _dbFactory.TradeDb;
        var tradeFills = await (db.Use($"{nameof(TradeDbCql)}.{nameof(TradeDbCql.GetTradeFills)}", TradeDbCql.GetTradeFills)
            .SetParameters(new GetTradeFills(orderId, tradeId))
            .ExecuteQueryAsync(MapToTradeFill!))
                .ConfigureAwait(false);
        if (tradeFills.Count > 0)
            foreach (var tf in tradeFills)
            {
                var tradeFillData = await (db.Use($"{nameof(TradeDbCql)}.{nameof(TradeDbCql.GetTradeFillData)}", TradeDbCql.GetTradeFillData)
                    .SetParameters(new GetTradeFillData(orderId, tradeId, tf.FillDate))
                    .ExecuteQueryAsync(MapToTradeFillData!))
                .ConfigureAwait(false);
                if (tradeFillData.Count > 0)
                    tf.AddTradeFillData(tradeFillData);
            }
        return tradeFills;
    }

    /// <inheritdoc />
    public async Task<ICollection<TradeFillReadModel>> GetTradeFillsAsync(
        int orderId,
        int tradeId,
        CancellationToken cancellationToken)
    {
        var db = _dbFactory.TradeDb;
        var tradeFills = await (db.Use($"{nameof(TradeDbCql)}.{nameof(TradeDbCql.GetTradeFills)}", TradeDbCql.GetTradeFills)
            .SetParameters(new GetTradeFills(orderId, tradeId))
            .ExecuteQueryAsync(MapToTradeFill!, cancellationToken))
                .ConfigureAwait(false);
        foreach (var tradeFill in tradeFills)
        {
            var tradeFillData = await (db.Use($"{nameof(TradeDbCql)}.{nameof(TradeDbCql.GetTradeFillData)}", TradeDbCql.GetTradeFillData)
                .SetParameters(new GetTradeFillData(
                    orderId,
                    tradeId,
                    tradeFill.FillDate))
                .ExecuteQueryAsync(MapToTradeFillData!, cancellationToken))
                .ConfigureAwait(false);
            if (tradeFillData.Count > 0)
                tradeFill.AddTradeFillData(tradeFillData);
        }
        return tradeFills;
    }

    /// <summary>
    /// return trade plans by order id
    /// </summary>
    /// <param name="orderId"></param>
    /// <returns></returns>
    public async Task<ICollection<TradePlanReadModel>> GetTradePlansAsync(int orderId)
        => await (_dbFactory.TradeDb
            .Use($"{nameof(TradeDbCql)}.{nameof(TradeDbCql.GetTradePlans)}", TradeDbCql.GetTradePlans)
            .SetParameters(new GetTradePlans(orderId))
            .ExecuteQueryAsync(MapToTradePlan!))
                .ConfigureAwait(false);

    /// <summary>
    /// return all trade plans
    /// </summary>
    /// <returns></returns>
    public async Task<ICollection<TradePlanReadModel>> GetTradePlansAsync()
        => await (_dbFactory.TradeDb
            .Use($"{nameof(TradeDbCql)}.{nameof(TradeDbCql.GetTradePlansAll)}", TradeDbCql.GetTradePlansAll)
            .ExecuteQueryAsync(MapToTradePlan!))
                .ConfigureAwait(false);

    /// <summary>
    /// return trade plan
    /// </summary>
    /// <param name="orderId"></param>
    /// <param name="tradeId"></param>
    /// <param name="valueDate"></param>
    /// <returns></returns>
    public async Task<ICollection<TradePlanReadModel>> GetTradePlansAsync(int orderId, int tradeId, DateOnly valueDate)
        => await (_dbFactory.TradeDb
            .Use($"{nameof(TradeDbCql)}.{nameof(TradeDbCql.GetTradePlansByValueDate)}", TradeDbCql.GetTradePlansByValueDate)
            .SetParameters(new GetTradePlansByTradeId(orderId, tradeId, valueDate))
            .ExecuteQueryAsync(MapToTradePlan!))
                .ConfigureAwait(false);

    /// <summary>
    /// return trade plan by date range
    /// </summary>
    /// <param name="orderId"></param>
    /// <param name="tradeId"></param>
    /// <param name="startDate"></param>
    /// <param name="endDate"></param>
    public async Task<ICollection<TradePlanReadModel>> GetTradePlansAsync(
        int orderId,
        int tradeId,
        DateOnly startDate,
        DateOnly endDate)
        => await (_dbFactory.TradeDb
            .Use($"{nameof(TradeDbCql)}.{nameof(TradeDbCql.GetTradePlansByDateRange)}", TradeDbCql.GetTradePlansByDateRange)
            .SetParameters(new GetTradePlansByDateRange(orderId, tradeId, startDate, endDate))
            .ExecuteQueryAsync(MapToTradePlan!))
                .ConfigureAwait(false);

    /// <summary>
    /// Retrieves the most recent trade plans associated with the specified order and trade identifiers.
    /// </summary>
    /// <param name="orderId">The unique identifier of the order for which to retrieve trade plans.</param>
    /// <param name="tradeId">The unique identifier of the trade for which to retrieve trade plans.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains a collection of <see
    /// cref="TradePlanReadModel"/> objects representing the latest trade plans for the specified order and trade. The
    /// collection is empty if no trade plans are found.</returns>
    public async Task<ICollection<TradePlanReadModel>> GetLastTradePlansAsync(int orderId, int tradeId)
    => await (_dbFactory.TradeDb
        .Use($"{nameof(TradeDbCql)}.{nameof(TradeDbCql.GetLastTradePlans)}", TradeDbCql.GetLastTradePlans)
        .SetParameters(new GetLastTradePlans(orderId, tradeId))
        .ExecuteQueryAsync(MapToTradePlan!))
                .ConfigureAwait(false);


    /// <summary>
    /// return last trade plan stop loss limit
    /// </summary>
    /// <param name="orderId"></param>
    /// <param name="tradeId"></param>
    /// <returns></returns>
    public async Task<TradePlanStopLossLimitReadModel?> GetTradePlanStopLossLimitAsync(int orderId, int tradeId)
        => await (_dbFactory.TradeDb
                .Use($"{nameof(TradeDbCql)}.{nameof(TradeDbCql.GetTradePlanStopLossLimit)}", TradeDbCql.GetTradePlanStopLossLimit)
                .SetParameters(new GetTradePlanStopLossLimit(orderId, tradeId))
                .ExecuteSingleAsync(MapToTradePlanStopLossLimit!))
                .ConfigureAwait(false);

    /// <summary>
    /// return trade plan forward loss ratios by date range
    /// </summary>
    /// <param name="startDate"></param>
    /// <param name="endDate"></param>
    /// <returns></returns>
    public async Task<ICollection<TradePlanForwardLossRatioReadModel>> GetTradePlanForwardLossRatiosAsync(DateOnly startDate, DateOnly endDate)
        => await (_dbFactory.TradeDb
            .Use($"{nameof(TradeDbCql)}.{nameof(TradeDbCql.GetTradePlanForwardLossRatios)}", TradeDbCql.GetTradePlanForwardLossRatios)
            .SetParameters(new GetTradePlanForwardLossRatios(startDate, endDate))
            .ExecuteQueryAsync(MapToTradePlanForwardLossRatio!))
                .ConfigureAwait(false);

    /// <summary>
    /// return trade plan forward loss ration by value date
    /// </summary>
    /// <param name="valueDate"></param>
    /// <returns></returns>
    public async Task<TradePlanForwardLossRatioReadModel?> GetTradePlanForwardLossRatioAsync(DateOnly valueDate)
    {
        var rows = await (_dbFactory.TradeDb
            .Use($"{nameof(TradeDbCql)}.{nameof(TradeDbCql.GetLastTradePlanForwardLossRatio)}", TradeDbCql.GetLastTradePlanForwardLossRatio)
            .SetParameters(new GetLastTradePlanForwardLossRatio(valueDate))
            .ExecuteQueryAsync(MapToTradePlanForwardLossRatioRow!))
                .ConfigureAwait(false);
        var latest = rows.MaxBy(row => row.SequenceId);
        return latest is null
            ? null
            : new TradePlanForwardLossRatioReadModel(latest.ForwardLossRatio);
    }

    /// <summary>
    /// return trade plan forward loss limit by id
    /// </summary>
    /// <param name="id"></param>
    /// <returns></returns>
    public async Task<TradePlanForwardLossLimitReadModel?> GetTradePlanForwardLossLimitAsync(
        int orderId,
        int tradeId,
        DateOnly valueDate,
        TradeType tradeType)
        => await (_dbFactory.TradeDb
            .Use($"{nameof(TradeDbCql)}.{nameof(TradeDbCql.GetTradePlanForwardLossLimit)}", TradeDbCql.GetTradePlanForwardLossLimit)
            .SetParameters(new GetTradePlanForwardLossLimit(
                orderId,
                tradeId,
                valueDate,
                tradeType.ToStringFast()
            ))
            .ExecuteSingleAsync(MapToTradePlanForwardLossLimit!))
                .ConfigureAwait(false);

    /// <summary>
    /// return trade live feed
    /// </summary>
    /// <param name="orderId"></param>
    /// <param name="tradeId"></param>
    /// <returns></returns>
    public async Task<ICollection<TradeLiveFeedReadModel>> GetTradeLiveFeedAsync(int orderId, int tradeId)
        => await (_dbFactory.TradeDb
                .Use($"{nameof(TradeDbCql)}.{nameof(TradeDbCql.GetTradeLiveFeed)}", TradeDbCql.GetTradeLiveFeed)
                .SetParameters(new GetTradeLiveFeed(orderId, tradeId))
                .ExecuteQueryAsync(MapToTradeLiveFeed!))
                .ConfigureAwait(false);


    /// <summary>
    /// return trade fill data
    /// </summary>
    /// <param name="tradeId"></param>
    /// <returns></returns>
    public async Task<ICollection<TradeFillDataReadModel>> GetTradeFillDataAsync(int tradeId)
        => await (_dbFactory.TradeDb
                .Use($"{nameof(TradeDbCql)}.{nameof(TradeDbCql.GetTradeFillDataByTradeId)}", TradeDbCql.GetTradeFillDataByTradeId)
                .SetParameters(new GetTradeFillDataByTradeId(tradeId))
                .ExecuteQueryAsync(MapToTradeFillData!))
                .ConfigureAwait(false);

    /// <summary>
    /// Inserts or updates an option leg in the database asynchronously.
    /// </summary>
    /// <remarks>This method first deletes any existing option leg with the same trade and contract
    /// identifiers, then inserts the new option leg data. The operation is performed as a queued command to ensure
    /// atomicity and consistency.</remarks>
    /// <param name="e">The <see cref="OptionTradeLegReadModel"/> containing the option leg details to be upserted.</param>
    /// <returns></returns>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1725", Justification = "Preserve the established storage context contract, including public named arguments and default values used by callers.")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S927", Justification = "Preserve the established storage context contract, including public named arguments and default values used by callers.")]
    public Task InsertOptionLegAsync(OptionTradeLegReadModel e)
=>this.InsertOptionLegAsyncCoreBinding(e);
    /// <summary>
    /// Asynchronously inserts a collection of option leg data into the database.
    /// </summary>
    /// <remarks>This method uses the provided option leg data to populate the database with new entries. The
    /// <c>createdOn</c> and <c>updatedOn</c> timestamps are set to the current date and time during
    /// insertion.</remarks>
    /// <param name="optionLegs">A collection of <see cref="OptionTradeLegReadModel"/> instances representing the option legs to be inserted. Each
    /// instance must contain valid trade and contract identifiers, as well as other necessary option leg details.</param>
    /// <returns></returns>
    public async Task InsertOptionLegsAsync(ICollection<OptionTradeLegReadModel> optionLegs)
        => await (_dbFactory.TradeDb
            .Use($"{nameof(TradeDbCql)}.{nameof(TradeDbCql.InsertOptionLeg)}", TradeDbCql.InsertOptionLeg)
            .SetParameters(optionLegs.Select(e => new InsertOptionLeg(
                e.OrderId,
                e.TradeId,
                e.ContractId,
                e.Quantity,
                e.StrikePrice,
                e.OptionLegType.ToStringFast(),
                e.OptionLegAction.ToStringFast(),
                DateTime.Now,
                e.CreatedBy,
                DateTime.Now,
                e.CreatedBy
            )))
            .ExecuteCommandAsync())
                .ConfigureAwait(false);

    /// <summary>
    /// Inserts a collection of option leg records into the database asynchronously.
    /// </summary>
    /// <remarks>This method processes the provided collection of option leg records and inserts them into the
    /// database. The <paramref name="optionLegs"/> parameter must not be null, and each item in the collection must
    /// have valid values for all required fields. The method uses the current date and time for the creation and update
    /// timestamps of the records.</remarks>
    /// <param name="optionLegs">A collection of <see cref="OptionTradeLegReadModel"/> objects representing the option leg records to be inserted.
    /// Each object must contain valid data for the associated order, trade, contract, and other required fields.</param>
    /// <returns>A <see cref="Task{TResult}"/> representing the asynchronous operation. The result contains the total number of
    /// option leg records processed.</returns>
    public Task<long> InsertOptionLegsAsync(IEnumerable<OptionTradeLegReadModel> optionLegs)
=>this.InsertOptionLegsAsyncCoreBinding(optionLegs);
    /// <summary>
    /// Inserts option leg data into the database asynchronously.
    /// </summary>
    /// <remarks>This method first deletes any existing option leg data for the specified trade and value
    /// date, then inserts the new data. The operation is performed asynchronously.</remarks>
    /// <param name="e">The <see cref="OptionTradeLegDataReadModel"/> containing the option leg data to be inserted.</param>
    /// <returns></returns>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1725", Justification = "Preserve the established storage context contract, including public named arguments and default values used by callers.")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S927", Justification = "Preserve the established storage context contract, including public named arguments and default values used by callers.")]
    public Task InsertOptionLegDataAsync(OptionTradeLegDataReadModel e)
=>this.InsertOptionLegDataAsyncCoreBinding(e);
    /// <summary>
    /// Asynchronously inserts a collection of option leg data into the database.
    /// </summary>
    /// <remarks>This method uses the configured database factory to execute an asynchronous command that
    /// inserts the provided option leg data into the database. The method expects each <see
    /// cref="OptionTradeLegDataReadModel"/> to have all necessary fields populated. The <c>createdOn</c> and <c>updatedOn</c>
    /// fields are automatically set to the current date and time.</remarks>
    /// <param name="optionLegData">A collection of <see cref="OptionTradeLegDataReadModel"/> instances representing the option leg data to be inserted.
    /// Each instance must contain valid trade information including trade ID, type, status, and pricing details.</param>
    /// <returns></returns>
    public async Task InsertOptionLegDataAsync(ICollection<OptionTradeLegDataReadModel> optionLegData)
        => await (_dbFactory.TradeDb
            .Use($"{nameof(TradeDbCql)}.{nameof(TradeDbCql.InsertOptionLegData)}", TradeDbCql.InsertOptionLegData)
            .SetParameters(optionLegData.Select(e => new InsertOptionLegData(
                e.OrderId,
                e.TradeId,
                e.TradeType.ToStringFast(),
                e.ValueDate,
                e.DaysToExpiry,
                e.TradeStatus.ToStringFast(),
                e.OptionLegId,
                e.BidPrice,
                e.AskPrice,
                e.ImpliedVolatility,
                e.Delta,
                e.Gamma,
                e.Theta,
                e.Vega,
                e.Rho,
                DateTime.Now,
                e.CreatedBy,
                DateTime.Now,
                e.CreatedBy
            )))
            .ExecuteCommandAsync())
                .ConfigureAwait(false);

    /// <summary>
    /// Inserts a collection of option leg data into the database asynchronously.
    /// </summary>
    /// <remarks>This method processes the provided collection of option leg data and inserts each record into
    /// the database.  The method ensures that all records are processed sequentially, and the total count of processed
    /// records is returned.</remarks>
    /// <param name="optionLegData">A collection of <see cref="OptionTradeLegDataReadModel"/> objects representing the option leg data to be inserted.
    /// Each object must contain valid data for all required fields.</param>
    /// <returns>A <see cref="Task{TResult}"/> representing the asynchronous operation. The result contains the total number of
    /// option leg records processed.</returns>
    public Task<long> InsertOptionLegDataAsync(IEnumerable<OptionTradeLegDataReadModel> optionLegData)
=>this.InsertOptionLegDataAsyncCoreBinding(optionLegData);
    /// <summary>
    /// insert option trade
    /// </summary>
    /// <param name="e">futures bar data</param>
    /// <returns></returns>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "MA0051", Justification = "Keep the transaction, resource ownership and failure ordering together during this provider migration; splitting it would obscure the atomic persistence boundary.")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1725", Justification = "Preserve the established storage context contract, including public named arguments and default values used by callers.")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S927", Justification = "Preserve the established storage context contract, including public named arguments and default values used by callers.")]
    public Task InsertOptionTradeAsync(OptionTradeReadModel e)
=>this.InsertOptionTradeAsyncCoreBinding(e);
    /// <summary>
    /// Asynchronously inserts a collection of option trades into the database.
    /// </summary>
    /// <remarks>This method uses the configured database factory to execute an insert command for each option
    /// trade in the collection. The trade data includes identifiers, dates, types, states, strategies, actions, and
    /// other relevant details. The method is asynchronous and returns a task that represents the asynchronous
    /// operation.</remarks>
    /// <param name="optionTrades">A collection of <see cref="OptionTradeReadModel"/> instances representing the option trades to be inserted. Each
    /// trade must have valid identifiers and dates.</param>
    /// <returns></returns>
    public async Task InsertOptionTradesAsync(ICollection<OptionTradeReadModel> optionTrades)
        => await (_dbFactory.TradeDb
            .Use($"{nameof(TradeDbCql)}.{nameof(TradeDbCql.InsertOptionTrade)}", TradeDbCql.InsertOptionTrade)
            .SetParameters(optionTrades.Select(e => new InsertOptionTrade(
                e.TradeId,
                e.OrderId,
                e.TradeDate,
                e.MaturityDate,
                e.TradeType.ToStringFast(),
                e.TradeState.ToStringFast(),
                e.TradeStrategy ?? string.Empty,
                e.TradeAction.ToStringFast(),
                e.UnderlyingContractId,
                e.UnderlyingAssetType.ToStringFast(),
                e.IsPrimaryTrade,
                e.IsHedgeTrade,
                DateTime.Now,
                e.CreatedBy,
                DateTime.Now,
                e.CreatedBy
            )))
            .ExecuteCommandAsync())
                .ConfigureAwait(false);

    /// <summary>
    /// Inserts a collection of option trade records into the database asynchronously.
    /// </summary>
    /// <remarks>This method processes the provided collection of option trades and inserts them into the
    /// database.  The method ensures that each trade is properly formatted and includes required metadata such as
    /// creation and update timestamps. The database operation is executed asynchronously.</remarks>
    /// <param name="optionTrades">A collection of <see cref="OptionTradeReadModel"/> objects representing the option trades to be inserted. Each
    /// object must contain valid trade details such as trade ID, order ID, trade date, and other required fields.</param>
    /// <returns>A <see cref="Task{TResult}"/> representing the asynchronous operation. The result contains the total number of
    /// option trades successfully processed and prepared for insertion.</returns>
    public Task<long> InsertOptionTradesAsync(IEnumerable<OptionTradeReadModel> optionTrades)
=>this.InsertOptionTradesAsyncCoreBinding(optionTrades);
    /// <summary>
    /// insert trade position
    /// </summary>
    /// <param name="e"></param>
    /// <returns></returns>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "MA0051", Justification = "Keep the transaction, resource ownership and failure ordering together during this provider migration; splitting it would obscure the atomic persistence boundary.")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1725", Justification = "Preserve the established storage context contract, including public named arguments and default values used by callers.")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S927", Justification = "Preserve the established storage context contract, including public named arguments and default values used by callers.")]
    public Task InsertTradePositionAsync(TradePositionReadModel e)
=>this.InsertTradePositionAsyncCoreBinding(e);
    /// <summary>
    /// insert trade position
    /// </summary>
    /// <param name="e"></param>
    /// <returns></returns>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "MA0051", Justification = "Keep the transaction, resource ownership and failure ordering together during this provider migration; splitting it would obscure the atomic persistence boundary.")]
    public Task InsertTradePositionAsync(ICollection<TradePositionReadModel> tradePositions)
=>this.InsertTradePositionAsyncCoreBinding(tradePositions);
    /// <summary>
    /// Asynchronously inserts a collection of trade positions into the database.
    /// </summary>
    /// <remarks>This method uses the <see cref="TradeDbCql.InsertTradePosition"/> command to insert the
    /// provided trade positions. The <paramref name="tradePositions"/> collection must not be null, and each item
    /// within the collection should have all necessary fields populated to ensure successful insertion.</remarks>
    /// <param name="tradePositions">A collection of <see cref="TradePositionReadModel"/> objects representing the trade positions to be inserted.
    /// Each object must contain valid trade details such as order ID, trade ID, trade type, and other relevant
    /// properties.</param>
    /// <returns></returns>
    public async Task InsertTradePositionsAsync(ICollection<TradePositionReadModel> tradePositions)
        => await (_dbFactory.TradeDb
            .Use($"{nameof(TradeDbCql)}.{nameof(TradeDbCql.InsertTradePosition)}", TradeDbCql.InsertTradePosition)
            .SetParameters(tradePositions.Select(e => new InsertTradePosition(
                e.OrderId,
                e.TradeId,
                e.TradeType.ToStringFast(),
                e.ValueDate,
                e.DaysToExpiry,
                e.TradeStatus.ToStringFast(),
                e.Commission,
                e.DeltaHedge,
                e.NetSpread,
                e.TradeValue,
                e.TradePnl,
                e.AssetPrice,
                e.OTMProbability,
                e.ForwardPrice,
                e.ForwardLossRatio,
                e.LossProbability,
                e.RiskFreeRate,
                DateTime.Now,
                e.CreatedBy,
                DateTime.Now,
                e.UpdatedBy
            )))
            .ExecuteCommandAsync())
                .ConfigureAwait(false);

    /// <summary>
    /// Inserts a collection of trade positions into the database asynchronously.
    /// </summary>
    /// <remarks>This method processes the provided trade positions and inserts them into the database. Each
    /// trade position is  transformed into a database-compatible format before being inserted. The method ensures that
    /// all trade positions  are processed sequentially, and the total count of processed positions is returned upon
    /// completion.  The caller is responsible for ensuring that the <paramref name="tradePositions"/> collection is not
    /// null and  contains valid data. If the collection is empty, the method will return 0 without performing any
    /// database operations.</remarks>
    /// <param name="tradePositions">A collection of <see cref="TradePositionReadModel"/> objects representing the trade positions to be inserted.
    /// Each object must contain valid trade data, including identifiers, trade details, and associated metadata.</param>
    /// <returns>A <see cref="Task{TResult}"/> representing the asynchronous operation. The result contains the total number of
    /// trade positions processed.</returns>
    public Task<long> InsertTradePositionsAsync(IEnumerable<TradePositionReadModel> tradePositions)
=>this.InsertTradePositionsAsyncCoreBinding(tradePositions);
    /// <summary>
    /// insert trade fills
    /// </summary>
    /// <param name="tradeFills"></param>
    /// <returns></returns>
    public Task InsertTradeFillAsync(ICollection<TradeFillReadModel> tradeFills)
=>this.InsertTradeFillAsyncCoreBinding(tradeFills);
    /// <summary>
    /// Asynchronously inserts a collection of trade fill records into the database.
    /// </summary>
    /// <remarks>This method uses the configured database factory to execute the insertion command. Ensure
    /// that the database connection is properly configured before calling this method. The operation is performed
    /// asynchronously and will not block the calling thread.</remarks>
    /// <param name="tradeFills">A collection of <see cref="TradeFillReadModel"/> objects representing the trade fill records to be inserted.
    /// Each object must contain valid data for order ID, trade ID, fill date, fill quantity, creation date, and
    /// creator.</param>
    /// <returns>A task that represents the asynchronous operation. The task will complete once the trade fill records have been
    /// inserted.</returns>
    public async Task InsertTradeFillsAsync(ICollection<TradeFillReadModel> tradeFills)
        => await (_dbFactory.TradeDb
            .Use($"{nameof(TradeDbCql)}.{nameof(TradeDbCql.InsertTradeFill)}", TradeDbCql.InsertTradeFill)
            .SetParameters(tradeFills.Select(e => new InsertTradeFill(
                e.OrderId,
                e.TradeId,
                e.FillDate,
                e.FillQuantity,
                e.CreatedOn,
                e.CreatedBy
            )))
            .ExecuteCommandAsync())
                .ConfigureAwait(false);

    /// <summary>
    /// Inserts a collection of trade fills into the database asynchronously.
    /// </summary>
    /// <remarks>This method processes the provided collection of trade fills and inserts them into the
    /// database using a batch operation. The method returns the count of trade fills processed, which corresponds to
    /// the number of items in the input collection.</remarks>
    /// <param name="tradeFills">A collection of <see cref="TradeFillReadModel"/> objects representing the trade fills to be inserted. Each
    /// object must contain valid data for the associated trade fill.</param>
    /// <returns>The total number of trade fills successfully processed and inserted into the database.</returns>
    public Task<long> InsertTradeFillsAsync(IEnumerable<TradeFillReadModel> tradeFills)
=>this.InsertTradeFillsAsyncCoreBinding(tradeFills);
    /// <summary>
    /// insert option tradde spread data
    /// </summary>
    /// <param name="e">option trade spread data</param>
    /// <returns></returns>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1725", Justification = "Preserve the established storage context contract, including public named arguments and default values used by callers.")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S927", Justification = "Preserve the established storage context contract, including public named arguments and default values used by callers.")]
    public Task InsertOptionTradeSpreadDataAsync(OptionTradeSpreadsDataModel e)
=>this.InsertOptionTradeSpreadDataAsyncCoreBinding(e);
    /// <summary>
    /// Asynchronously inserts a collection of option trade spread data into the database.
    /// </summary>
    /// <remarks>The method processes the data in batches of 1000 items to optimize database operations. Each
    /// data entry is assigned a unique sequence ID before insertion.</remarks>
    /// <param name="optionTradeSpreadsData">A collection of <see cref="OptionTradeSpreadsDataModel"/> objects representing the option trade spread data to
    /// be inserted.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1725", Justification = "Preserve the established storage context contract, including public named arguments and default values used by callers.")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S927", Justification = "Preserve the established storage context contract, including public named arguments and default values used by callers.")]
    public Task InsertOptionTradeSpreadDataAsync(ICollection<OptionTradeSpreadsDataModel> optionTradeSpreadsData)
=>this.InsertOptionTradeSpreadDataAsyncCoreBinding(optionTradeSpreadsData);
    /// <summary>
    /// Inserts a collection of option trade spread data into the database asynchronously.
    /// </summary>
    /// <remarks>This method generates a unique sequence ID for each trade spread entry before inserting it
    /// into the database. The method ensures that all provided data is processed and inserted in a single
    /// operation.</remarks>
    /// <param name="optionTradeSpreadsData">A collection of <see cref="OptionTradeSpreadsDataModel"/> objects representing the option trade spread data to
    /// be inserted. Each item in the collection must contain valid trade details.</param>
    /// <returns>A <see cref="Task{TResult}"/> representing the asynchronous operation. The result contains the total number of
    /// rows inserted.</returns>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1725", Justification = "Preserve the established storage context contract, including public named arguments and default values used by callers.")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S927", Justification = "Preserve the established storage context contract, including public named arguments and default values used by callers.")]
    public Task<long> InsertOptionTradeSpreadDataAsync(IEnumerable<OptionTradeSpreadsDataModel> optionTradeSpreadsData)
=>this.InsertOptionTradeSpreadDataAsyncCoreBinding(optionTradeSpreadsData);
    /// <summary>
    /// insert option tradde spread bar data
    /// </summary>
    /// <param name="e">option trade spread bar data</param>
    /// <returns></returns>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1725", Justification = "Preserve the established storage context contract, including public named arguments and default values used by callers.")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S927", Justification = "Preserve the established storage context contract, including public named arguments and default values used by callers.")]
    public Task InsertOptionTradeSpreadBarDataAsync(OptionTradeSpreadBarsDataModel e)
=>this.InsertOptionTradeSpreadBarDataAsyncCoreBinding(e);
    /// <summary>
    /// Asynchronously inserts a collection of option trade spread bar data into the database.
    /// </summary>
    /// <remarks>This method uses the configured database factory to execute the insertion command
    /// asynchronously. Ensure that the provided data collection is not null and contains valid entries to avoid
    /// exceptions.</remarks>
    /// <param name="optionTradeSpreadBarsData">A collection of <see cref="OptionTradeSpreadBarsDataModel"/> objects representing the option trade spread bar
    /// data to be inserted. Each object must contain valid trade and order identifiers, trade type, value and bar
    /// dates, and spread limits.</param>
    /// <returns>A task that represents the asynchronous operation. The task will complete once the data has been successfully
    /// inserted.</returns>
    public async Task InsertOptionTradeSpreadBarDataAsync(ICollection<OptionTradeSpreadBarsDataModel> optionTradeSpreadBarsData)
        => await (_dbFactory.TradeDb
                .Use($"{nameof(TradeDbCql)}.{nameof(TradeDbCql.InsertOptionTradeSpreadBarData)}", TradeDbCql.InsertOptionTradeSpreadBarData)
                .SetParameters(optionTradeSpreadBarsData.Select(e => new InsertOptionTradeSpreadBarData(
                    e.OrderId,
                    e.TradeId,
                    e.TradeType.ToStringFast(),
                    e.ValueDate,
                    e.BarDate,
                    e.LossLimit,
                    e.WinLimit,
                    e.ForwardSpread,
                    e.NetSpread
                )))
                .ExecuteCommandAsync())
                .ConfigureAwait(false);

    /// <summary>
    /// Inserts a collection of option trade spread bar data into the database asynchronously.
    /// </summary>
    /// <remarks>This method processes the provided collection of option trade spread bar data and inserts
    /// each item into the database. The method ensures that all items in the collection are processed sequentially, and
    /// the total count of processed rows is returned.</remarks>
    /// <param name="optionTradeSpreadBarsData">A collection of <see cref="OptionTradeSpreadBarsDataModel"/> objects representing the option trade spread bar
    /// data to be inserted. Each object must contain valid values for all required fields.</param>
    /// <returns>The total number of rows processed during the insertion.</returns>
    public Task<long> InsertOptionTradeSpreadBarDataAsync(IEnumerable<OptionTradeSpreadBarsDataModel> optionTradeSpreadBarsData)
=>this.InsertOptionTradeSpreadBarDataAsyncCoreBinding(optionTradeSpreadBarsData);

    /// <summary>
    /// insert trade liveFeed
    /// </summary>
    /// <param name="e"></param>
    /// <returns></returns>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1725", Justification = "Preserve the established storage context contract, including public named arguments and default values used by callers.")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S927", Justification = "Preserve the established storage context contract, including public named arguments and default values used by callers.")]
    public Task InsertTradeLiveFeedAsync(TradeLiveFeedReadModel e)
=>this.InsertTradeLiveFeedAsyncCoreBinding(e);
    /// <summary>
    /// insert trade position state
    /// </summary>
    /// <param name="e"></param>
    /// <returns></returns>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1725", Justification = "Preserve the established storage context contract, including public named arguments and default values used by callers.")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S927", Justification = "Preserve the established storage context contract, including public named arguments and default values used by callers.")]
    public async Task InsertTradePositionStateAsync(TradePositionStateReadModel e)
        => await (_dbFactory.TradeDb
                .Use($"{nameof(TradeDbCql)}.{nameof(TradeDbCql.InsertTradePositionState)}", TradeDbCql.InsertTradePositionState)
                .SetParameters(new InsertTradePositionState(
                    e.OrderId,
                    e.TradeId,
                    e.TradePositionState.ToStringFast(),
                    e.OpenedOn,
                    e.OpenedBy
                ))
                .ExecuteCommandAsync())
                .ConfigureAwait(false);


    /// <summary>
    /// insert trade plan forward loss limit
    /// </summary>
    /// <param name="e">trade plan forward loss limit</param>
    /// <returns></returns>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1725", Justification = "Preserve the established storage context contract, including public named arguments and default values used by callers.")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S927", Justification = "Preserve the established storage context contract, including public named arguments and default values used by callers.")]
    public Task InsertTradePlanForwardLossLimitAsync(TradePlanForwardLossLimitReadModel e)
=>this.InsertTradePlanForwardLossLimitAsyncCoreBinding(e);
    /// <inheritdoc />
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1725", Justification = "Preserve the established storage context contract, including public named arguments and default values used by callers.")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S927", Justification = "Preserve the established storage context contract, including public named arguments and default values used by callers.")]
    public async Task InsertTradePlanForwardLossRatioAsync(
        DateOnly valueDate,
        double forwardLossRatio)
    {
        var sequenceId = await (_sequenceIdGenerator.GetSequenceIdAsync(SequenceName.TradePlan_SequenceId))
                .ConfigureAwait(false);
        var db = _dbFactory.TradeDb;
        await (db.Use($"{nameof(TradeDbCql)}.{nameof(TradeDbCql.InsertTradePlanForwardLossRatio)}", TradeDbCql.InsertTradePlanForwardLossRatio)
            .SetParameters(new InsertTradePlanForwardLossRatio(
                1,
                valueDate,
                forwardLossRatio,
                sequenceId
            ))
           .ExecuteCommandAsync())
                .ConfigureAwait(false);
    }

    /// <summary>
    /// Inserts a trade placement signal into the database asynchronously and returns the generated sequence ID.
    /// </summary>
    /// <remarks>This method generates a unique sequence ID for the trade placement signal and stores the
    /// provided data in the database. Ensure that all required properties of the <paramref name="e"/> parameter are
    /// populated before calling this method.</remarks>
    /// <param name="e">The trade placement signal data to insert, encapsulated in a <see cref="TradePlacementSignalReadModel"/> object.</param>
    /// <returns>The sequence ID generated for the inserted trade placement signal.</returns>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1725", Justification = "Preserve the established storage context contract, including public named arguments and default values used by callers.")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S927", Justification = "Preserve the established storage context contract, including public named arguments and default values used by callers.")]
    public Task<long> InsertTradePlacementSignalAsync(TradePlacementSignalReadModel e)
=>this.InsertTradePlacementSignalAsyncCoreBinding(e);
    /// <summary>
    /// updated trade position
    /// </summary>
    /// <param name="key"></param>
    /// <param name="commission"></param>
    /// <param name="netSpread"></param>
    /// <param name="tradeValue"></param>
    /// <param name="tradePnl"></param>
    /// <param name="assetPrice"></param>
    /// <param name="otmProbability"></param>
    /// <param name="winRatio"></param>
    /// <param name="maxPrice"></param>
    /// <param name="hedgeProbability"></param>
    /// <param name="riskFreeRate"></param>
    /// <param name="updatedOn"></param>
    /// <param name="updatedBy"></param>
    /// <returns></returns>
    public async Task UpdateTradePositionAsync(
        TradePositionEntityId key,
        decimal commission,
        int deltaHedge,
        decimal netSpread,
        decimal tradeValue,
        decimal tradePnl,
        decimal assetPrice,
        double otmProbability,
        double winRatio,
        decimal maxPrice,
        double hedgeProbability,
        double riskFreeRate,
        DateTime updatedOn,
        string updatedBy)
        => await (_dbFactory.TradeDb
                .Use($"{nameof(TradeDbCql)}.{nameof(TradeDbCql.UpdateTradePosition)}", TradeDbCql.UpdateTradePosition)
                .SetParameters(new UpdateTradePosition(
                    key.OrderId,
                    key.TradeId,
                    key.ValueDate,
                    key.TradeStatus.ToStringFast(),
                    key.DaysToExpiry,
                    key.TradeType.ToStringFast(),
                    commission,
                    deltaHedge,
                    netSpread,
                    tradeValue,
                    tradePnl,
                    assetPrice,
                    otmProbability,
                    winRatio,
                    maxPrice,
                    hedgeProbability,
                    riskFreeRate,
                    updatedOn,
                    updatedBy
                ))
                .ExecuteCommandAsync())
                .ConfigureAwait(false);

    /// <summary>
    /// update option trade state
    /// </summary>
    /// <param name="orderId"></param>
    /// <param name="tradeId"></param>
    /// <param name="tradeState"></param>
    /// <param name="updatedOn"></param>
    /// <param name="updatedBy"></param>
    /// <returns></returns>
    public async Task UpdateOptionTradeStateAsync(int orderId, int tradeId, TradeState tradeState, DateTime updatedOn, string updatedBy)
        => await (_dbFactory.TradeDb
                .Use($"{nameof(TradeDbCql)}.{nameof(TradeDbCql.UpdateOptionTradeState)}", TradeDbCql.UpdateOptionTradeState)
                .SetParameters(new UpdateOptionTradeState(
                    orderId,
                    tradeId,
                    tradeState.ToStringFast(),
                    updatedOn,
                    updatedBy
                ))
                .ExecuteCommandAsync())
                .ConfigureAwait(false);

    /// <summary>
    /// update trade live feed
    /// </summary>
    /// <param name="tradeLiveFeed"></param>
    /// <returns></returns>
    public Task UpdateTradeLiveFeedAsync(TradeLiveFeedReadModel tradeLiveFeed)
=>this.UpdateTradeLiveFeedAsyncCoreBinding(tradeLiveFeed);
    /// <summary>
    /// update trade position status
    /// </summary>
    /// <param name="tradeId"></param>
    /// <param name="tradeType"></param>
    /// <param name="valueDate"></param>
    /// <param name="daysToExpiry"></param>
    /// <param name="oldTradeStatus"></param>
    /// <param name="newTradeStatus"></param>
    /// <param name="updatedOn"></param>
    /// <param name="updatedBy"></param>
    /// <returns></returns>
    public async Task UpdateTradePositionStatusAsync(
        int orderId,
        int tradeId,
        TradeType tradeType,
        DateOnly valueDate,
        int daysToExpiry,
        TradeStatus oldTradeStatus,
        TradeStatus newTradeStatus,
        DateTime updatedOn,
        string updatedBy)
    {
        var db = _dbFactory.TradeDb;
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

        var optionLegData = await (db
                .Use($"{nameof(TradeDbCql)}.{nameof(TradeDbCql.GetOptionLegData)}", TradeDbCql.GetOptionLegData)
                .SetParameters(new GetOptionLegData(orderId, tradeId, valueDate))
                .ExecuteQueryAsync(MapToOptionLegData!))
                .ConfigureAwait(false);
        foreach (var o in optionLegData.Where(e => e.TradeType == tradeType
            && e.DaysToExpiry == daysToExpiry
            && e.TradeStatus == oldTradeStatus))
        {
            await (db.Use($"{nameof(TradeDbCql)}.{nameof(TradeDbCql.UpdateOptionLegDataStatus)}", TradeDbCql.UpdateOptionLegDataStatus)
                .SetParameters(new UpdateOptionLegDataStatus(
                    tradeId,
                    valueDate,
                    o.OptionLegId,
                    newTradeStatus.ToStringFast(),
                    updatedOn,
                    updatedBy
                ))
                .ExecuteCommandAsync())
                .ConfigureAwait(false);
        }
    }

    /// <summary>
    /// update option leg data
    /// </summary>
    /// <param name="e"></param>
    /// <returns></returns>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1725", Justification = "Preserve the established storage context contract, including public named arguments and default values used by callers.")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S927", Justification = "Preserve the established storage context contract, including public named arguments and default values used by callers.")]
    public Task UpdateOptionLegDataAsync(OptionTradeLegDataReadModel e)
=>this.UpdateOptionLegDataAsyncCoreBinding(e);

    /// <summary>
    /// insert trade limit for selected trade
    /// </summary>
    /// <param name="e">trade limit</param>
    /// <returns></returns>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1725", Justification = "Preserve the established storage context contract, including public named arguments and default values used by callers.")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S927", Justification = "Preserve the established storage context contract, including public named arguments and default values used by callers.")]
    public Task InsertTradeLimitAsync(TradeLimitReadModel e)
=>this.InsertTradeLimitAsyncCoreBinding(e);
    /// <summary>
    /// Asynchronously inserts a collection of trade limits into the database.
    /// </summary>
    /// <remarks>This method uses the <c>TradeDbCql.InsertTradeLimit</c> command to insert the provided trade
    /// limits. Ensure that each <see cref="TradeLimitReadModel"/> in the collection has all required fields
    /// populated.</remarks>
    /// <param name="tradeLimits">A collection of <see cref="TradeLimitReadModel"/> objects representing the trade limits to be inserted. Each
    /// object must contain valid trade limit data, including trade identifiers and financial constraints.</param>
    /// <returns>A task that represents the asynchronous operation. The task will complete once the trade limits have been
    /// successfully inserted.</returns>
    public async Task InsertTradeLimitsAsync(ICollection<TradeLimitReadModel> tradeLimits)
        => await (_dbFactory.TradeDb
                .Use($"{nameof(TradeDbCql)}.{nameof(TradeDbCql.InsertTradeLimit)}", TradeDbCql.InsertTradeLimit)
                .SetParameters(tradeLimits.Select(e => new InsertTradeLimit(
                    e.TradeId,
                    e.TradeType.ToStringFast(),
                    e.RiskMargin,
                    e.MaxProfit,
                    e.MaxLoss,
                    e.MaxReturn,
                    e.MaxLossLimit,
                    e.MinProfitLimit,
                    e.MaxProfitLimit,
                    e.MinProfitTarget,
                    e.DailyProfitTarget,
                    e.CreatedOn,
                    e.CreatedBy,
                    e.UpdatedOn,
                    e.UpdatedBy
                )))
                .ExecuteCommandAsync())
                .ConfigureAwait(false);

    /// <summary>
    /// Inserts a collection of trade limits into the database asynchronously.
    /// </summary>
    /// <remarks>This method processes the provided collection of trade limits and inserts them into the
    /// database. The method ensures that each trade limit in the collection is processed sequentially, and the total
    /// count of processed trade limits is returned.</remarks>
    /// <param name="tradeLimits">A collection of <see cref="TradeLimitReadModel"/> objects representing the trade limits to be inserted. Each
    /// object must contain valid trade limit data.</param>
    /// <returns>A <see cref="Task{TResult}"/> representing the asynchronous operation. The result contains the total number of
    /// trade limits processed.</returns>
    public Task<long> InsertTradeLimitsAsync(IEnumerable<TradeLimitReadModel> tradeLimits)
=>this.InsertTradeLimitsAsyncCoreBinding(tradeLimits);
    /// <summary>
    /// insert trade limit for selected trade
    /// </summary>
    /// <param name="e">trade limit</param>
    /// <returns></returns>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1725", Justification = "Preserve the established storage context contract, including public named arguments and default values used by callers.")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S927", Justification = "Preserve the established storage context contract, including public named arguments and default values used by callers.")]
    public Task InsertTradeTypeLimitAsync(TradeTypeLimitReadModel e)
=>this.InsertTradeTypeLimitAsyncCoreBinding(e);
    /// <summary>
    /// Asynchronously inserts a collection of trade type limits into the database.
    /// </summary>
    /// <remarks>This method uses the <c>TradeDbCql.InsertTradeTypeLimit</c> command to insert the specified
    /// trade type limits. Ensure that each <see cref="TradeTypeLimitReadModel"/> in the collection has non-null and
    /// valid properties to avoid database errors.</remarks>
    /// <param name="tradeTypeLimits">A collection of <see cref="TradeTypeLimitReadModel"/> objects representing the trade type limits to be inserted.
    /// Each object must contain valid trade identifiers and limit values.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public async Task InsertTradeTypeLimitsAsync(ICollection<TradeTypeLimitReadModel> tradeTypeLimits)
    {
        if(string.Equals(ProviderName,"System.Data.Postgres",StringComparison.Ordinal))
        {
            await (this.NativeTransaction().ExecuteOnceAsync(async(db,ct)=>{await (tradeFlowWriter.ProjectSpreadLimitsAsync(db,tradeTypeLimits.ToArray(),ct))
                .ConfigureAwait(false);return true;}))
                .ConfigureAwait(false);
            return;
        }
        await (_dbFactory.TradeDb
                .Use($"{nameof(TradeDbCql)}.{nameof(TradeDbCql.InsertTradeTypeLimit)}", TradeDbCql.InsertTradeTypeLimit)
                .SetParameters(tradeTypeLimits.Select(e => new InsertTradeTypeLimit(
                    e.TradeId,
                    e.TradeType.ToStringFast(),
                    e.MaxLossLimit,
                    e.MinProfitLimit,
                    e.MaxProfitLimit
                )))
                .ExecuteCommandAsync())
                .ConfigureAwait(false);
    }

    /// <summary>
    /// Inserts a collection of trade type limits into the database asynchronously.
    /// </summary>
    /// <remarks>This method processes the provided collection of trade type limits and inserts them into the
    /// database.  The method ensures that each item in the collection is processed sequentially, and the total count of
    /// processed  items is returned as the result.</remarks>
    /// <param name="tradeTypeLimits">A collection of <see cref="TradeTypeLimitReadModel"/> objects representing the trade type limits to be inserted.
    /// Each object must contain valid trade type and limit values.</param>
    /// <returns>A <see cref="Task{TResult}"/> representing the asynchronous operation. The result contains the total number of
    /// trade type limits processed and inserted into the database.</returns>
    public Task<long> InsertTradeTypeLimitsAsync(IEnumerable<TradeTypeLimitReadModel> tradeTypeLimits)
=>this.InsertTradeTypeLimitsAsyncCoreBinding(tradeTypeLimits);
    /// <summary>
    /// insert trade plan
    /// </summary>
    /// <param name="e">trade plan</param>
    /// <returns></returns>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "MA0051", Justification = "Keep the transaction, resource ownership and failure ordering together during this provider migration; splitting it would obscure the atomic persistence boundary.")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1725", Justification = "Preserve the established storage context contract, including public named arguments and default values used by callers.")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S927", Justification = "Preserve the established storage context contract, including public named arguments and default values used by callers.")]
    public Task InsertTradePlanAsync(TradePlanReadModel e)
=>this.InsertTradePlanAsyncCoreBinding(e);
    /// <summary>
    /// Inserts a collection of trade plans into the database asynchronously.
    /// </summary>
    /// <remarks>This method processes the provided trade plans in batches of 1,000 to optimize database
    /// operations. Each trade plan is assigned a unique sequence ID before being inserted into the database. The method
    /// performs two database operations for each batch: 1. Inserts the trade plans into the main trade plan table. 2.
    /// Inserts forward loss ratio data into a separate table for analytics purposes.  The method is designed to handle
    /// large collections of trade plans efficiently. Ensure that the input collection is not null and contains valid
    /// trade plan data to avoid runtime exceptions.</remarks>
    /// <param name="tradePlans">A collection of <see cref="TradePlanByIdReadModel"/> objects representing the trade plans to be inserted. Each
    /// trade plan must contain valid data for insertion, including required fields such as order ID, trade ID, and
    /// trade type.</param>
    /// <returns>A task that represents the asynchronous operation. The task completes when all trade plans have been
    /// successfully inserted.</returns>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "MA0051", Justification = "Keep the transaction, resource ownership and failure ordering together during this provider migration; splitting it would obscure the atomic persistence boundary.")]
    public Task InsertTradePlansAsync(ICollection<TradePlanByIdReadModel> tradePlans)
=>this.InsertTradePlansAsyncCoreBinding(tradePlans);
    /// <summary>
    /// Inserts a collection of trade plans into the database asynchronously.
    /// </summary>
    /// <remarks>This method performs two separate database insert operations: one for the main trade plan
    /// data and another for forward loss ratio data. The method processes the provided collection of trade plans and
    /// ensures that each entry is inserted into the appropriate database tables.</remarks>
    /// <param name="tradePlans">A collection of <see cref="TradePlanByIdReadModel"/> objects representing the trade plans to be inserted. Each
    /// trade plan must contain valid data for all required fields.</param>
    /// <returns>A <see cref="Task{TResult}"/> representing the asynchronous operation. The result contains the total number of
    /// trade plans inserted.</returns>
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
            var db = _dbFactory.TradeDb;
            await (db.Use($"{nameof(TradeDbCql)}.{nameof(TradeDbCql.InsertTradePlan)}", TradeDbCql.InsertTradePlan)
                .SetParameters(EnumerateTradePlans().Select(e => new InsertTradePlan(
                    e.Id,
                    e.OrderId,
                    e.TradeId,
                    e.TradeType.ToStringFast(),
                    e.TradeDate,
                    e.ValueDate,
                    e.MaturityDate,
                    e.ActionDate,
                    e.ActionType.ToStringFast(),
                    e.ActionSubType.ToStringFast(),
                    e.ActionState.ToStringFast(),
                    e.ActionReason,
                    e.TradePnl,
                    e.ForwardLossRatio,
                    e.LossProbability,
                    e.MScore,
                    e.MaxProfit,
                    e.MaxLoss,
                    e.MinProfitTarget,
                    e.DailyProfitTarget,
                    e.AssetPrice,
                    e.AssetStdDev,
                    e.AssetMean,
                    e.AssetPriceChange,
                    e.MarketTrend.ToStringFast(),
                    e.MarketVolatility.ToStringFast(),
                    e.MarketDirection.ToStringFast(),
                    e.VixVolatility.ToStringFast(),
                    e.TradeRisk.ToStringFast(),
                    e.FiftyDayMA,
                    e.FiveDayXMA,
                    e.PutOTMProbability,
                    e.CallOTMProbability,
                    e.ShortPutGamma,
                    e.ShortCallGamma,
                    e.GammaRisk.ToStringFast(),
                    e.NetPrice,
                    e.ForwardPrice,
                    e.ForwardDelta,
                    e.StopLossLimit,
                    e.TrendType.ToStringFast(),
                    e.TrendStrength.ToStringFast(),
                    e.RSI,
                    e.RSISlope,
                    e.TDI.ToStringFast(),
                    e.TDIStrength.ToStringFast(),
                    e.CreatedOn,
                    e.CreatedBy
                )))
               .ExecuteCommandAsync())
                .ConfigureAwait(false);

        }

        async Task InsertTradePlanForwardLossRatio()
        {
            var db = _dbFactory.TradeDb;
            await (db.Use($"{nameof(TradeDbCql)}.{nameof(TradeDbCql.InsertTradePlanForwardLossRatio)}", TradeDbCql.InsertTradePlanForwardLossRatio)
                .SetParameters(GetForwardLossRatios().Select(e => new InsertTradePlanForwardLossRatio(
                    1,
                    e.ValueDate,
                    e.ForwardLossRatio,
                    e.Id
                )))
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



    /// <summary>
    /// delete selected trade position state
    /// </summary>
    /// <param name="e"></param>
    /// <returns></returns>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1725", Justification = "Preserve the established storage context contract, including public named arguments and default values used by callers.")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S927", Justification = "Preserve the established storage context contract, including public named arguments and default values used by callers.")]
    public Task DeleteTradePositionStateAsync(OptionTradeEntityId e)
=>this.DeleteTradePositionStateAsyncCoreBinding(e);
    /// <summary>
    /// delete all trade positions for a given order and trade id
    /// </summary>
    /// <param name="orderId"></param>
    /// <param name="tradeId"></param>
    /// <returns></returns>
    public async Task DeleteTradePositionAsync(int orderId, int tradeId)
        => await (_dbFactory.TradeDb
                .Use($"{nameof(TradeDbCql)}.{nameof(TradeDbCql.DeleteTradePosition)}", TradeDbCql.DeleteTradePosition)
                .SetParameters(new DeleteTradePosition(orderId, tradeId))
                .ExecuteCommandAsync())
                .ConfigureAwait(false);

    /// <summary>
    /// delete trade positions by primary key
    /// </summary>
    ///<param name="orderId"></param>
    /// <returns></returns>
    public async Task DeleteTradePositionAsync(
        int orderId,
        int tradeId,
        TradeType tradeType,
        DateOnly valueDate,
        int daysToExpiry,
        TradeStatus tradeStatus)
        => await (_dbFactory.TradeDb
                .Use($"{nameof(TradeDbCql)}.{nameof(TradeDbCql.DeleteTradePositionByPrimaryKey)}", TradeDbCql.DeleteTradePositionByPrimaryKey)
                .SetParameters(new DeleteTradePositionByPrimaryKey(
                    orderId,
                    tradeId,
                    tradeType.ToStringFast(),
                    valueDate,
                    daysToExpiry,
                    tradeStatus.ToStringFast()
                ))
                .ExecuteCommandAsync())
                .ConfigureAwait(false);

    /// <summary>
    /// delete trade limit
    /// </summary>
    /// <param name="tradeId"></param>
    /// <param name="tradeType"></param>
    /// <returns></returns>
    public async Task DeleteTradeLimitAsync(int tradeId, TradeType tradeType)
        => await (_dbFactory.TradeDb
                .Use($"{nameof(TradeDbCql)}.{nameof(TradeDbCql.DeleteTradeLimitByTradeType)}", TradeDbCql.DeleteTradeLimitByTradeType)
                .SetParameters(new DeleteTradeLimitByTradeType(tradeId, tradeType.ToStringFast()))
                .ExecuteCommandAsync())
                .ConfigureAwait(false);

    /// <summary>
    /// Deletes the trade placement signal associated with the specified contract ID and value date.
    /// </summary>
    /// <remarks>This method performs an asynchronous operation to delete the trade placement signal. Ensure
    /// that the provided <paramref name="contractId"/> and <paramref name="valueDate"/>  correspond to an existing
    /// trade placement signal in the database.</remarks>
    /// <param name="contractId">The unique identifier of the contract for which the trade placement signal should be deleted. Cannot be null or
    /// empty.</param>
    /// <param name="valueDate">The value date of the trade placement signal to delete.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public async Task DeleteTradePlacementSignalAsync(string contractId, DateOnly valueDate)
        => await (_dbFactory.TradeDb
                .Use($"{nameof(TradeDbCql)}.{nameof(TradeDbCql.DeleteTradePlacementSignal)}", TradeDbCql.DeleteTradePlacementSignal)
                .SetParameters(new DeleteTradePlacementSignal(contractId, valueDate))
                .ExecuteCommandAsync())
                .ConfigureAwait(false);

    // <summary>
    /// delete selected trade type limit
    /// </summary>
    /// <param name="tradeId"></param>
    /// <returns></returns>
    public async Task DeleteTradeTypeLimitAsync(int tradeId)
        => await (_dbFactory.TradeDb
                .Use($"{nameof(TradeDbCql)}.{nameof(TradeDbCql.DeleteTradeTypeLimit)}", TradeDbCql.DeleteTradeTypeLimit)
                .SetParameters(new DeleteTradeTypeLimit(tradeId))
                .ExecuteCommandAsync())
                .ConfigureAwait(false);

    /// <summary>
    /// delete option trade and linked trades
    /// </summary>
    /// <param name="orderId"></param>
    /// <param name="tradeId"></param>
    /// <returns></returns>
    public async Task DeleteOptionTradeAsync(int orderId, int tradeId)
    {
        var db = _dbFactory.TradeDb;
        List<object> queuedCommands = [
            db.Use($"{nameof(TradeDbCql)}.{nameof(TradeDbCql.DeleteOptionTrade)}", TradeDbCql.DeleteOptionTrade)
              .SetParameters(new DeleteOptionTrade(
                  orderId,
                  tradeId
              ))
                  .QueueCommand(),
         db.Use($"{nameof(TradeDbCql)}.{nameof(TradeDbCql.DeleteTradePosition)}", TradeDbCql.DeleteTradePosition)
              .SetParameters(new DeleteTradePosition(
                  orderId,
                  tradeId
              ))
                  .QueueCommand(),
        db.Use($"{nameof(TradeDbCql)}.{nameof(TradeDbCql.DeleteOptionLeg)}", TradeDbCql.DeleteOptionLeg)
              .SetParameters(new DeleteOptionLeg(
                  orderId,
                  tradeId
              ))
                  .QueueCommand(),
        db.Use($"{nameof(TradeDbCql)}.{nameof(TradeDbCql.DeleteOptionLegData)}", TradeDbCql.DeleteOptionLegData)
              .SetParameters(new DeleteOptionLegData(
                  orderId,
                  tradeId
              ))
                  .QueueCommand(),
        db.Use($"{nameof(TradeDbCql)}.{nameof(TradeDbCql.DeleteTradeTypeLimit)}", TradeDbCql.DeleteTradeTypeLimit)
              .SetParameters(new DeleteTradeTypeLimit(
                  tradeId
              ))
                  .QueueCommand(),
         db.Use($"{nameof(TradeDbCql)}.{nameof(TradeDbCql.DeleteTradeLimit)}", TradeDbCql.DeleteTradeLimit)
              .SetParameters(new DeleteTradeLimit(
                  tradeId
              ))
                  .QueueCommand(),
        db.Use($"{nameof(TradeDbCql)}.{nameof(TradeDbCql.DeleteTradeFill)}", TradeDbCql.DeleteTradeFill)
              .SetParameters(new DeleteTradeFill(
                    orderId,
                    tradeId
              ))
                  .QueueCommand(),
        db.Use($"{nameof(TradeDbCql)}.{nameof(TradeDbCql.DeleteTradeFillData)}", TradeDbCql.DeleteTradeFillData)
              .SetParameters(new DeleteTradeFillData(
                    orderId,
                    tradeId
              ))
                  .QueueCommand()];

        await (db.ExecuteQueuedCommandsAsync(queuedCommands, true))
                .ConfigureAwait(false);
    }

    /// <summary>
    /// delete trade
    /// </summary>
    /// <param name="orderId"></param>
    /// <param name="tradeId"></param>
    /// <param name="valueDate"></param>
    /// <param name="tradeType"></param>
    /// <returns></returns>
    public async Task DeleteOptionTradeSpreadBarDataAsync(int orderId, int tradeId, DateOnly valueDate, TradeType tradeType)
        => await (_dbFactory.TradeDb
                .Use($"{nameof(TradeDbCql)}.{nameof(TradeDbCql.DeleteOptionTradeSpreadBarData)}", TradeDbCql.DeleteOptionTradeSpreadBarData)
                .SetParameters(new DeleteOptionTradeSpreadBarData(
                    orderId,
                    tradeId,
                    valueDate,
                    tradeType.ToStringFast()
                ))
                .ExecuteCommandAsync())
                .ConfigureAwait(false);

    /// <summary>
    /// delete option trade spread data
    /// </summary>
    /// <param name="orderId"></param>
    /// <param name="tradeId"></param>
    /// <param name="valueDate"></param>
    /// <param name="tradeType"></param>
    /// <returns></returns>
    public async Task DeleteOptionTradeSpreadDataAsync(int orderId, int tradeId, DateOnly valueDate, TradeType tradeType)
        => await (_dbFactory.TradeDb
                .Use($"{nameof(TradeDbCql)}.{nameof(TradeDbCql.DeleteOptionTradeSpreadData)}", TradeDbCql.DeleteOptionTradeSpreadData)
                .SetParameters(new DeleteOptionTradeSpreadData(
                    orderId,
                    tradeId,
                    valueDate,
                    tradeType.ToStringFast()
                ))
                .ExecuteCommandAsync())
                .ConfigureAwait(false);

    /// <summary>
    /// delete trade live feed
    /// </summary>
    /// <param name="tradeId"></param>
    /// <param name="orderId"></param>
    /// <returns></returns>
    public async Task DeleteTradeLiveFeedAsync(int orderId, int tradeId)
        => await (_dbFactory.TradeDb
                .Use($"{nameof(TradeDbCql)}.{nameof(TradeDbCql.DeleteTradeLiveFeed)}", TradeDbCql.DeleteTradeLiveFeed)
                .SetParameters(new DeleteTradeLiveFeed(
                    orderId,
                    tradeId
                ))
                .ExecuteCommandAsync())
                .ConfigureAwait(false);

    /// <summary>
    /// delete all trade live feeds for order
    /// </summary>
    /// <param name="orderId"></param>
    /// <returns></returns>
    public async Task DeleteTradeLiveFeedsAsync(int orderId)
        => await (_dbFactory.TradeDb
                .Use($"{nameof(TradeDbCql)}.{nameof(TradeDbCql.DeleteTradeLiveFeeds)}", TradeDbCql.DeleteTradeLiveFeeds)
                .SetParameters(new DeleteTradeLiveFeeds(
                    orderId
                ))
                .ExecuteCommandAsync())
                .ConfigureAwait(false);

    /// <summary>
    /// delete trade plan forward loss limit by id
    /// </summary>
    /// <param name="id"></param>
    /// <returns></returns>
    public Task DeleteTradePlanForwardLossLimitAsync(TradePlanForwardLossLimitEntityId id)
=>this.DeleteTradePlanForwardLossLimitAsyncCoreBinding(id);
    /// <summary>
    /// update the DailyProfitTarget for the TradeLimitReadModel with the given tradeId and tradeType
    /// </summary>
    /// <param name="tradeId"></param>
    /// <param name="tradeType"></param>
    /// <param name="dailyProfitTarget"></param>
    /// <param name="updatedOn"></param>
    /// <param name="updatedBy"></param>
    /// <returns></returns>
    public async Task UpdateTradeLimitDailyProfitTarget(int tradeId, TradeType tradeType, decimal dailyProfitTarget, DateTime updatedOn, string updatedBy)
    {
        var tradeLimit = await (GetTradeLimitAsync(tradeId))
                .ConfigureAwait(false);
        if (tradeLimit != null && tradeLimit.TradeType == tradeType)
        {
            var updatedTradeLimit = tradeLimit with { DailyProfitTarget = dailyProfitTarget, UpdatedOn = updatedOn, UpdatedBy = updatedBy };
            await (InsertTradeLimitAsync(updatedTradeLimit))
                .ConfigureAwait(false);
        }
    }


    /// <inheritdoc />
    public Task<IntrinsicTimeStrategyWorkflowReadModel?> GetIntrinsicTimeStrategyWorkflowAsync(
        StrategyWorkflowId workflowId)
        => GetIntrinsicTimeStrategyWorkflowAsync(workflowId, CancellationToken.None);

    /// <inheritdoc />
    public Task<IntrinsicTimeStrategyWorkflowReadModel?> GetIntrinsicTimeStrategyWorkflowAsync(
        StrategyWorkflowId workflowId,
        CancellationToken cancellationToken)
        => _dbFactory.TradeDb
            .Use($"{nameof(TradeDbCql)}.{nameof(TradeDbCql.GetIntrinsicTimeStrategyWorkflow)}", (ProviderName == "System.Data.Postgres" ? NativeWorkflowReadSql.GetIntrinsicTimeStrategyWorkflow : TradeDbCql.GetIntrinsicTimeStrategyWorkflow))
            .SetParameters(ProviderName == "System.Data.Postgres" ? Values(Uuid(workflowId.Value)) : new GetIntrinsicTimeStrategyWorkflow(workflowId.Value).Bind())
            .ExecuteSingleAsync(MapToWorkflow, cancellationToken);

    /// <inheritdoc />
    public Task<ActiveIntrinsicTimeStrategyWorkflowReadModel?> GetActiveIntrinsicTimeStrategyWorkflowAsync(
        string workflowEntityId)
        => GetActiveIntrinsicTimeStrategyWorkflowAsync(workflowEntityId, CancellationToken.None);

    /// <inheritdoc />
    public Task<ActiveIntrinsicTimeStrategyWorkflowReadModel?> GetActiveIntrinsicTimeStrategyWorkflowAsync(
        string workflowEntityId,
        CancellationToken cancellationToken)
        => _dbFactory.TradeDb
            .Use($"{nameof(TradeDbCql)}.{nameof(TradeDbCql.GetActiveIntrinsicTimeStrategyWorkflow)}", (ProviderName == "System.Data.Postgres" ? NativeWorkflowReadSql.GetActiveIntrinsicTimeStrategyWorkflow : TradeDbCql.GetActiveIntrinsicTimeStrategyWorkflow))
            .SetParameters(ProviderName == "System.Data.Postgres" ? Values(Text(workflowEntityId)) : new GetActiveIntrinsicTimeStrategyWorkflow(workflowEntityId).Bind())
            .ExecuteSingleAsync(MapToActiveWorkflow, cancellationToken);

    /// <inheritdoc />
    public Task<ICollection<IntrinsicTimeStrategyWorkflowStartAttemptReadModel>> GetIntrinsicTimeStrategyWorkflowStartAttemptsAsync(
        string workflowEntityId,
        DateTime beforeUtc,
        int pageSize)
        => GetIntrinsicTimeStrategyWorkflowStartAttemptsAsync(
            workflowEntityId,
            beforeUtc,
            pageSize,
            CancellationToken.None);

    /// <inheritdoc />
    public Task<ICollection<IntrinsicTimeStrategyWorkflowStartAttemptReadModel>> GetIntrinsicTimeStrategyWorkflowStartAttemptsAsync(
        string workflowEntityId,
        DateTime beforeUtc,
        int pageSize,
        CancellationToken cancellationToken)
        => _dbFactory.TradeDb
            .Use($"{nameof(TradeDbCql)}.{nameof(TradeDbCql.GetIntrinsicTimeStrategyWorkflowStartAttempts)}", (ProviderName == "System.Data.Postgres" ? NativeWorkflowReadSql.GetIntrinsicTimeStrategyWorkflowStartAttempts : TradeDbCql.GetIntrinsicTimeStrategyWorkflowStartAttempts))
            .SetParameters(ProviderName == "System.Data.Postgres" ? Values(Text(workflowEntityId), TimestampTz(DateTime.SpecifyKind(beforeUtc, DateTimeKind.Utc)), Integer(pageSize.RequirePageSize())) : new GetIntrinsicTimeStrategyWorkflowStartAttempts(workflowEntityId, beforeUtc, pageSize.RequirePageSize()).Bind())
            .ExecuteQueryAsync(MapToStartAttempt, cancellationToken);

    /// <inheritdoc />
    public Task<ICollection<IntrinsicTimeStrategyWorkflowTimelineReadModel>> GetIntrinsicTimeStrategyWorkflowTimelineAsync(
        StrategyWorkflowId workflowId,
        long afterEventId,
        int pageSize)
        => GetIntrinsicTimeStrategyWorkflowTimelineAsync(
            workflowId,
            afterEventId,
            pageSize,
            CancellationToken.None);

    /// <inheritdoc />
    public Task<ICollection<IntrinsicTimeStrategyWorkflowTimelineReadModel>> GetIntrinsicTimeStrategyWorkflowTimelineAsync(
        StrategyWorkflowId workflowId,
        long afterEventId,
        int pageSize,
        CancellationToken cancellationToken)
        => _dbFactory.TradeDb
            .Use($"{nameof(TradeDbCql)}.{nameof(TradeDbCql.GetIntrinsicTimeStrategyWorkflowTimeline)}", (ProviderName == "System.Data.Postgres" ? NativeWorkflowReadSql.GetIntrinsicTimeStrategyWorkflowTimeline : TradeDbCql.GetIntrinsicTimeStrategyWorkflowTimeline))
            .SetParameters(ProviderName == "System.Data.Postgres" ? Values(Uuid(workflowId.Value), Bigint(afterEventId), Integer(pageSize.RequirePageSize())) : new GetIntrinsicTimeStrategyWorkflowTimeline(workflowId.Value, afterEventId, pageSize.RequirePageSize()).Bind())
            .ExecuteQueryAsync(MapToTimelineEvent, cancellationToken);

    /// <inheritdoc />
    public Task<ICollection<IntrinsicTimeStrategyWorkflowHistoryReadModel>> GetIntrinsicTimeStrategyWorkflowsByEntityAsync(
        string workflowEntityId,
        DateTime beforeUtc,
        int pageSize)
        => GetIntrinsicTimeStrategyWorkflowsByEntityAsync(
            workflowEntityId,
            beforeUtc,
            pageSize,
            CancellationToken.None);

    /// <inheritdoc />
    public Task<ICollection<IntrinsicTimeStrategyWorkflowHistoryReadModel>> GetIntrinsicTimeStrategyWorkflowsByEntityAsync(
        string workflowEntityId,
        DateTime beforeUtc,
        int pageSize,
        CancellationToken cancellationToken)
        => _dbFactory.TradeDb
            .Use($"{nameof(TradeDbCql)}.{nameof(TradeDbCql.GetIntrinsicTimeStrategyWorkflowsByEntity)}", (ProviderName == "System.Data.Postgres" ? NativeWorkflowReadSql.GetIntrinsicTimeStrategyWorkflowsByEntity : TradeDbCql.GetIntrinsicTimeStrategyWorkflowsByEntity))
            .SetParameters(ProviderName == "System.Data.Postgres" ? Values(Text(workflowEntityId), TimestampTz(DateTime.SpecifyKind(beforeUtc, DateTimeKind.Utc)), Integer(pageSize.RequirePageSize())) : new GetIntrinsicTimeStrategyWorkflowsByEntity(workflowEntityId, beforeUtc, pageSize.RequirePageSize()).Bind())
            .ExecuteQueryAsync(MapToWorkflowHistory, cancellationToken);

    /// <inheritdoc />
    public Task<ICollection<IntrinsicTimeStrategyWorkflowHistoryReadModel>> GetIntrinsicTimeStrategyWorkflowsByStatusAsync(
        StrategyWorkflowStatus status,
        DateOnly startDate,
        DateOnly endDate,
        int pageSize)
        => GetIntrinsicTimeStrategyWorkflowsByStatusAsync(
            status,
            startDate,
            endDate,
            pageSize,
            CancellationToken.None);

    /// <inheritdoc />
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S1994", Justification = "The retry/date loop is terminated by explicit success, cancellation, or bounded-history exits in its body.")]
    public async Task<ICollection<IntrinsicTimeStrategyWorkflowHistoryReadModel>> GetIntrinsicTimeStrategyWorkflowsByStatusAsync(
        StrategyWorkflowStatus status,
        DateOnly startDate,
        DateOnly endDate,
        int pageSize,
        CancellationToken cancellationToken)
    {
        endDate.RequireNotBefore(startDate);

        var maximumCount = pageSize.RequirePageSize();
        var results = new List<IntrinsicTimeStrategyWorkflowHistoryReadModel>(maximumCount);
        for (var date = endDate; results.Count < maximumCount; date = date.AddDays(-1))
        {
            var remaining = maximumCount - results.Count;
            var dayResults = await _dbFactory.TradeDb
                .Use($"{nameof(TradeDbCql)}.{nameof(TradeDbCql.GetIntrinsicTimeStrategyWorkflowsByStatusDay)}", (ProviderName == "System.Data.Postgres" ? NativeWorkflowReadSql.GetIntrinsicTimeStrategyWorkflowsByStatusDay : TradeDbCql.GetIntrinsicTimeStrategyWorkflowsByStatusDay))
                .SetParameters(ProviderName == "System.Data.Postgres" ? Values(Text(status.ToString()), Date(date), Integer(remaining)) : new GetIntrinsicTimeStrategyWorkflowsByStatusDay(status.ToString(), date, remaining).Bind())
                .ExecuteQueryAsync(MapToWorkflowHistory, cancellationToken)
                .ConfigureAwait(false);
            results.AddRange(dayResults);
            if (date == startDate)
                break;
        }

        return results;
    }

    /// <inheritdoc />
    public Task UpsertIntrinsicTimeStrategyWorkflowAsync(IntrinsicTimeStrategyWorkflowReadModel workflow)
        => UpsertIntrinsicTimeStrategyWorkflowAsync(workflow, CancellationToken.None);

    /// <inheritdoc />
    public Task UpsertIntrinsicTimeStrategyWorkflowAsync(
        IntrinsicTimeStrategyWorkflowReadModel workflow,
        CancellationToken cancellationToken)
=>this.UpsertIntrinsicTimeStrategyWorkflowAsyncCoreBinding(workflow, cancellationToken);
    /// <inheritdoc />
    public Task UpsertActiveIntrinsicTimeStrategyWorkflowAsync(ActiveIntrinsicTimeStrategyWorkflowReadModel workflow)
        => UpsertActiveIntrinsicTimeStrategyWorkflowAsync(workflow, CancellationToken.None);

    /// <inheritdoc />
    public Task UpsertActiveIntrinsicTimeStrategyWorkflowAsync(
        ActiveIntrinsicTimeStrategyWorkflowReadModel workflow,
        CancellationToken cancellationToken)
=>this.UpsertActiveIntrinsicTimeStrategyWorkflowAsyncCoreBinding(workflow, cancellationToken);
    /// <inheritdoc />
    public Task DeleteActiveIntrinsicTimeStrategyWorkflowAsync(string workflowEntityId)
        => DeleteActiveIntrinsicTimeStrategyWorkflowAsync(workflowEntityId, CancellationToken.None);

    /// <inheritdoc />
    public async Task DeleteActiveIntrinsicTimeStrategyWorkflowAsync(
        string workflowEntityId,
        CancellationToken cancellationToken)
    {
        await _dbFactory.TradeDb
            .Use($"{nameof(TradeDbCql)}.{nameof(TradeDbCql.DeleteActiveIntrinsicTimeStrategyWorkflow)}", TradeDbCql.DeleteActiveIntrinsicTimeStrategyWorkflow)
            .SetParameters(new DeleteActiveIntrinsicTimeStrategyWorkflow(workflowEntityId))
            .ExecuteCommandAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public Task InsertIntrinsicTimeStrategyWorkflowStartAttemptAsync(
        IntrinsicTimeStrategyWorkflowStartAttemptReadModel attempt)
        => InsertIntrinsicTimeStrategyWorkflowStartAttemptAsync(attempt, CancellationToken.None);

    /// <inheritdoc />
    public Task InsertIntrinsicTimeStrategyWorkflowStartAttemptAsync(
        IntrinsicTimeStrategyWorkflowStartAttemptReadModel attempt,
        CancellationToken cancellationToken)
=>this.InsertIntrinsicTimeStrategyWorkflowStartAttemptAsyncCoreBinding(attempt, cancellationToken);
    /// <inheritdoc />
    public Task InsertIntrinsicTimeStrategyWorkflowTimelineAsync(
        IntrinsicTimeStrategyWorkflowTimelineReadModel timelineEvent)
        => InsertIntrinsicTimeStrategyWorkflowTimelineAsync(timelineEvent, CancellationToken.None);

    /// <inheritdoc />
    public Task InsertIntrinsicTimeStrategyWorkflowTimelineAsync(
        IntrinsicTimeStrategyWorkflowTimelineReadModel timelineEvent,
        CancellationToken cancellationToken)
=>this.InsertIntrinsicTimeStrategyWorkflowTimelineAsyncCoreBinding(timelineEvent, cancellationToken);
    /// <inheritdoc />
    public Task UpsertIntrinsicTimeStrategyWorkflowByEntityAsync(
        IntrinsicTimeStrategyWorkflowHistoryReadModel workflow)
        => UpsertIntrinsicTimeStrategyWorkflowByEntityAsync(workflow, CancellationToken.None);

    /// <inheritdoc />
    public Task UpsertIntrinsicTimeStrategyWorkflowByEntityAsync(
        IntrinsicTimeStrategyWorkflowHistoryReadModel workflow,
        CancellationToken cancellationToken)
=>this.UpsertIntrinsicTimeStrategyWorkflowByEntityAsyncCoreBinding(workflow, cancellationToken);
    /// <inheritdoc />
    public Task UpsertIntrinsicTimeStrategyWorkflowByStatusDayAsync(
        IntrinsicTimeStrategyWorkflowHistoryReadModel workflow)
        => UpsertIntrinsicTimeStrategyWorkflowByStatusDayAsync(workflow, CancellationToken.None);

    /// <inheritdoc />
    public Task UpsertIntrinsicTimeStrategyWorkflowByStatusDayAsync(
        IntrinsicTimeStrategyWorkflowHistoryReadModel workflow,
        CancellationToken cancellationToken)
=>this.UpsertIntrinsicTimeStrategyWorkflowByStatusDayAsyncCoreBinding(workflow, cancellationToken);
    internal static UpsertIntrinsicTimeStrategyWorkflowByEntity MapToByEntityParameters(
        IntrinsicTimeStrategyWorkflowHistoryReadModel workflow)
        => new(
            workflow.WorkflowEntityId,
            workflow.StartedAtUtc,
            workflow.WorkflowId.Value,
            workflow.Status.ToString(),
            workflow.Outcome.ToString(),
            workflow.CurrentStage.ToString(),
            workflow.WorkflowRevision,
            workflow.TerminalAtUtc,
            workflow.StopReasonCode);

    internal static IntrinsicTimeStrategyWorkflowReadModel MapToWorkflow(IObjectDataRecord row)
        => new(
            new StrategyWorkflowId(row.GetGuid(0)),
            row.GetString(1),
            row.GetString(2),
            row.GetInt(3),
            row.GetString(4),
            row.GetDateOnly(5),
            row.GetEnum<TimeFrameType>(6),
            row.GetGuid(7),
            row.GetGuid(8),
            row.GetEnum<StrategyWorkflowStatus>(9),
            row.GetEnum<StrategyWorkflowOutcome>(10),
            row.GetEnum<StrategyWorkflowStage>(11),
            row.GetLong(12),
            row.GetLong(13),
            row.GetInt(14),
            row.GetBytes(15),
            row.GetString(16),
            row.GetDateTime(17),
            row.IsNull(18) ? null : row.GetDateTime(18),
            row.GetDateTime(19));

    internal static ActiveIntrinsicTimeStrategyWorkflowReadModel MapToActiveWorkflow(IObjectDataRecord row)
        => new(
            row.GetString(0),
            new StrategyWorkflowId(row.GetGuid(1)),
            row.GetString(2),
            row.GetDateOnly(3),
            row.GetEnum<TimeFrameType>(4),
            row.GetEnum<StrategyWorkflowStage>(5),
            row.GetLong(6),
            row.GetLong(7),
            row.GetInt(8),
            row.GetBytes(9),
            row.GetDateTime(10),
            row.GetDateTime(11));

    internal static IntrinsicTimeStrategyWorkflowStartAttemptReadModel MapToStartAttempt(IObjectDataRecord row)
        => new(
            row.GetString(0),
            row.GetDateTime(1),
            new StrategyWorkflowId(row.GetGuid(2)),
            row.GetEnum<StrategyWorkflowStartDecision>(3),
            row.IsNull(4) ? null : new StrategyWorkflowId(row.GetGuid(4)),
            row.GetGuid(5),
            row.GetGuid(6),
            row.GetEnum<StrategyWorkflowStage>(7),
            row.GetString(8),
            row.GetLong(9));

    internal static IntrinsicTimeStrategyWorkflowTimelineReadModel MapToTimelineEvent(IObjectDataRecord row)
        => new(
            new StrategyWorkflowId(row.GetGuid(0)),
            row.GetLong(1),
            row.GetString(2),
            row.GetLong(3),
            row.GetEnum<StrategyWorkflowStage>(4),
            row.GetString(5),
            row.GetInt(6),
            row.GetBytes(7),
            row.GetDateTime(8));

    internal static IntrinsicTimeStrategyWorkflowHistoryReadModel MapToWorkflowHistory(IObjectDataRecord row)
        => new(
            row.GetString(0),
            row.GetDateTime(1),
            new StrategyWorkflowId(row.GetGuid(2)),
            row.GetEnum<StrategyWorkflowStatus>(3),
            row.GetEnum<StrategyWorkflowOutcome>(4),
            row.GetEnum<StrategyWorkflowStage>(5),
            row.GetLong(6),
            row.IsNull(7) ? null : row.GetDateTime(7),
            row.GetString(8));

    /// <inheritdoc />
    public Task<MarketConditionReadModel?> GetMarketConditionAsync(StrategyWorkflowId workflowId)
        => GetMarketConditionAsync(workflowId, CancellationToken.None);
    /// <inheritdoc />
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1725", Justification = "Preserve the established storage context contract, including public named arguments and default values used by callers.")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S927", Justification = "Preserve the established storage context contract, including public named arguments and default values used by callers.")]
    public Task<MarketConditionReadModel?> GetMarketConditionAsync(StrategyWorkflowId workflowId, CancellationToken token)
        => _dbFactory.TradeDb.Use($"{nameof(TradeDbCql)}.{nameof(TradeDbCql.GetMarketCondition)}", TradeDbCql.GetMarketCondition)
            .SetParameters(new GetMarketCondition(workflowId.Value))
            .ExecuteSingleAsync(MapToMarketCondition, token);
    /// <inheritdoc />
    public Task<ICollection<MarketConditionReadModel>> GetMarketConditionHistoryAsync(
        int fundId, string instrumentRoot, TimeFrameType targetHorizon, DateTime beforeUtc, int pageSize)
        => GetMarketConditionHistoryAsync(fundId, instrumentRoot, targetHorizon, beforeUtc, pageSize,
            CancellationToken.None);
    /// <inheritdoc />
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1725", Justification = "Preserve the established storage context contract, including public named arguments and default values used by callers.")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S927", Justification = "Preserve the established storage context contract, including public named arguments and default values used by callers.")]
    public Task<ICollection<MarketConditionReadModel>> GetMarketConditionHistoryAsync(
        int fundId, string instrumentRoot, TimeFrameType targetHorizon, DateTime beforeUtc, int pageSize,
        CancellationToken token)
    {
        fundId.RequireMarketConditionHistoryScope(instrumentRoot, pageSize);
        return _dbFactory.TradeDb.Use(
                $"{nameof(TradeDbCql)}.{nameof(TradeDbCql.GetMarketConditionHistory)}",
                TradeDbCql.GetMarketConditionHistory)
            .SetParameters(new GetMarketConditionHistory(fundId, instrumentRoot, targetHorizon.ToString(),
                beforeUtc, pageSize))
                    .ExecuteQueryAsync(MapToMarketCondition, token);
    }
    /// <inheritdoc />
    public Task UpsertMarketConditionAsync(MarketConditionReadModel result)
        => UpsertMarketConditionAsync(result, CancellationToken.None);
    /// <inheritdoc />
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1725", Justification = "Preserve the established storage context contract, including public named arguments and default values used by callers.")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S927", Justification = "Preserve the established storage context contract, including public named arguments and default values used by callers.")]
    public Task UpsertMarketConditionAsync(MarketConditionReadModel result, CancellationToken token)
=>this.UpsertMarketConditionAsyncCoreBinding(result, token);    internal static MarketConditionReadModel MapToMarketCondition(IObjectDataRecord row) => new()
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

    /// <inheritdoc />
    public Task UpsertMarketConditionAssessmentAsync(MarketConditionAssessmentCompletedEvent completed, CancellationToken cancellationToken = default)
=>this.UpsertMarketConditionAssessmentAsyncCoreBinding(completed, cancellationToken);    /// <inheritdoc />
    public Task<MarketConditionAssessmentCompletedEvent?> GetMarketConditionAssessmentAsync(StrategyWorkflowId workflowId, CancellationToken cancellationToken = default)
    {
        if(string.Equals(ProviderName,"System.Data.Postgres",StringComparison.Ordinal))
            return this.NativeTransaction().ExecuteOnceAsync(async(db,ct)=>
                (await (db.QueryAsync("SELECT payload,payload_sha256 FROM trade.market_condition_assessment WHERE workflow_id=$1;",
                    [workflowId.Value],r=>(r.GetFieldValue<byte[]>(0)).VerifyAssessment(r.GetString(1)),ct))
                .ConfigureAwait(false)).SingleOrDefault(),cancellationToken);
        return _dbFactory.TradeDb.Use("AssessmentProjection.Get", "SELECT payload,payload_sha256 FROM market_condition_assessment WHERE workflow_id=?;")
            .SetParameters(new AssessmentValues([workflowId.Value]))
                .ExecuteSingleAsync(MapToAssessment,cancellationToken);
    }
    /// <inheritdoc />
    public Task<ICollection<MarketConditionAssessmentCompletedEvent>> GetMarketConditionAssessmentHistoryAsync(string profile, string root, TimeFrameType horizon, DateTime beforeUtc, int pageSize, CancellationToken cancellationToken = default)
=>this.GetMarketConditionAssessmentHistoryAsyncCoreBinding(profile, root, horizon, beforeUtc, pageSize, cancellationToken);    internal static MarketConditionAssessmentCompletedEvent MapToAssessment(IObjectDataRecord row)
        => (row.GetBytes(0)).VerifyAssessment(row.GetString(1));
    /// <inheritdoc />
    public Task UpsertOrderCompositionAsync(OrderCompositionFunctionCompletedEvent completed, CancellationToken token = default)
=>this.UpsertOrderCompositionAsyncCoreBinding(completed, token);    /// <inheritdoc />
    public Task<OrderCompositionFunctionCompletedEvent?> GetOrderCompositionInvocationAsync(StrategyWorkflowId workflowId, Guid invocationId, CancellationToken token = default)
    {
        workflowId.RequireInvocation(invocationId, "Exact workflow and invocation required.");
        if (string.Equals(ProviderName, "System.Data.Postgres", StringComparison.Ordinal))
            return this.NativeTransaction().ExecuteOnceAsync((db, ct) => PostgresPipelineReadStore.CompositionAsync(db, workflowId, invocationId, ct), token);
        return _dbFactory.TradeDb.Use("OrderComposition.Exact", "SELECT payload,result_hash,input_hash FROM order_composition_invocation WHERE workflow_id=? AND invocation_id=?;")
            .SetParameters(new CompositionValues([workflowId.Value, invocationId]))
            .ExecuteSingleAsync(row =>
            {
                var completed = MessagePackBinarySerializer.Shared.Deserialize<OrderCompositionFunctionCompletedEvent>(row.GetBytes(0));
                var result = OrderCompositionContracts.ReadResult(completed.Result);
                if (result.WorkflowId != workflowId || result.InvocationId != invocationId || !string.Equals(completed.Result.PayloadSha256,row.GetString(1),StringComparison.Ordinal)
                    || !string.Equals(result.InputSha256,row.GetString(2),StringComparison.Ordinal)) throw new InvalidDataException("Stored composition evidence mismatch.");
                return completed;
            }, token);
    }
    /// <inheritdoc />
    public Task<QueryPage<OrderCompositionHistoryRow>> GetOrderCompositionHistoryAsync(int portfolioId, int fundId, DateOnly valueDate,
        int pageSize, byte[]? pagingState = null, CancellationToken token = default)
    {
        portfolioId.RequireHistoryScope(fundId, valueDate, pageSize, 100, "Invalid composition history scope.");
        if (string.Equals(ProviderName, "System.Data.Postgres", StringComparison.Ordinal))
            return this.NativeTransaction().ExecuteOnceAsync((db, ct) => PostgresPipelineReadStore.CompositionHistoryAsync(db, portfolioId, fundId, valueDate, pageSize, pagingState, ct), token);
        return _dbFactory.TradeDb.Use("OrderComposition.History", "SELECT evaluated_at_utc,workflow_id,invocation_id,event_id,target_horizon,outcome,reason_code,result_id,result_hash FROM order_composition_history WHERE portfolio_id=? AND fund_id=? AND value_date=?;")
            .SetParameters(new CompositionValues([portfolioId, fundId, valueDate]))
            .ExecutePageAsync(row => new OrderCompositionHistoryRow(portfolioId, fundId,
                valueDate, row.GetDateTime(0), row.GetGuid(1), row.GetGuid(2), row.GetGuid(3), row.GetShort(4),
                (byte)row.GetEnum<CompositionOutcome>(5), row.GetString(6), row.GetGuid(7), row.GetString(8)), pageSize, pagingState, token);
    }
    /// <inheritdoc />
    public Task<RegimeDiscoveryReadModel?> GetRegimeDiscoveryAsync(StrategyWorkflowId workflowId)
        => GetRegimeDiscoveryAsync(workflowId, CancellationToken.None);

    /// <inheritdoc />
    public Task<RegimeDiscoveryReadModel?> GetRegimeDiscoveryAsync(
        StrategyWorkflowId workflowId,
        CancellationToken cancellationToken)
    {
        if (string.Equals(ProviderName, "System.Data.Postgres", StringComparison.Ordinal))
            return this.NativeTransaction().ExecuteOnceAsync((db, ct) => PostgresRegimeDiscoveryStore.ReadAsync(db, workflowId, ct), cancellationToken);
        return _dbFactory.TradeDb
            .Use($"{nameof(TradeDbCql)}.{nameof(TradeDbCql.GetRegimeDiscovery)}", TradeDbCql.GetRegimeDiscovery)
            .SetParameters(new GetRegimeDiscovery(workflowId.Value))
            .ExecuteSingleAsync(MapToRegimeDiscovery, cancellationToken);
    }

    /// <inheritdoc />
    public Task UpsertRegimeDiscoveryAsync(RegimeDiscoveryReadModel result)
        => UpsertRegimeDiscoveryAsync(result, CancellationToken.None);

    /// <inheritdoc />
    public Task UpsertRegimeDiscoveryAsync(
        RegimeDiscoveryReadModel result,
        CancellationToken cancellationToken)
=>this.UpsertRegimeDiscoveryAsyncCoreBinding(result, cancellationToken);
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

    /// <inheritdoc />
    public Task<RiskHistoryProjectionResult> UpsertRiskHistoryAsync(WorkflowStrategyStateUpdatedEvent snapshot, CancellationToken token = default)
=>this.UpsertRiskHistoryAsyncCoreBinding(snapshot, token);    /// <inheritdoc />
    public Task<WorkflowStrategyStateUpdatedEvent?> GetRiskInvocationAsync(Guid workflow, Guid invocation, CancellationToken token = default)
    {
        workflow.RequireInvocation(invocation, "Exact Risk identities required.");
        return _dbFactory.TradeDb.Use("RiskHistory.Exact", "SELECT payload,content_hash FROM risk_management_invocation WHERE workflow_id=? AND invocation_id=? ORDER BY revision DESC LIMIT 1;")
            .SetParameters(new RiskValues([workflow, invocation]))
            .ExecuteSingleAsync(r =>
            {
                var payload = r.GetBytes(0);
                var value = MessagePackBinarySerializer.Shared.Deserialize<WorkflowStrategyStateUpdatedEvent>(payload);
                if (!string.Equals(RiskContracts.Hash(value),r.GetString(1),StringComparison.Ordinal) || RiskHistoryIdentity.Row(value) is not { } row || row.WorkflowId != workflow || row.InvocationId != invocation)
                    throw new InvalidDataException("Risk history content hash mismatch.");
                return value;
            }, token);
    }
    /// <inheritdoc />
    public Task<QueryPage<RiskHistoryRow>> GetRiskHistoryAsync(int portfolio, int fund, DateOnly date, int size, byte[]? cursor, CancellationToken token = default)
    {
        portfolio.RequireHistoryScope(fund, date, size, 100, "Invalid Risk history scope.");
        return _dbFactory.TradeDb.Use("RiskHistory.Page", "SELECT payload FROM risk_management_history WHERE portfolio_id=? AND fund_id=? AND value_date=?;")
            .SetParameters(new RiskValues([portfolio, fund, date]))
            .ExecutePageAsync(r => MessagePackBinarySerializer.Shared.Deserialize<RiskHistoryRow>(r.GetBytes(0)), size, cursor, token);
    }
    /// <inheritdoc />
    public Task UpsertTradeOrderAsync(TradeOrderDefinition order, DateTime acceptedAtUtc, CancellationToken token = default)
    {
        if (!string.Equals(ProviderName,"System.Data.Postgres",StringComparison.Ordinal)) return UpsertTradeOrderAsync(order, token);
        return this.NativeTransaction().ExecuteOnceAsync((db,ct)=>tradeFlowWriter.ProjectOrderAsync(db,order,acceptedAtUtc,ct),token);
    }

    public Task UpsertTradeOrderAsync(TradeOrderDefinition order, CancellationToken token = default)
=>this.UpsertTradeOrderAsyncCoreBinding(order, token);
    /// <inheritdoc />
    public Task<TradeOrderDefinition?> GetTradeOrderAsync(TradeOrderId id, CancellationToken token = default)
    {
        id.Require();
        return _dbFactory.TradeDb.Use("TradeFlow.Order.Get", string.Equals(ProviderName,"System.Data.Postgres",StringComparison.Ordinal) ? "SELECT payload FROM trade.trade_order WHERE portfolioId=$1 AND fundId=$2 AND orderId=$3 ORDER BY revision DESC LIMIT 1;" : "SELECT payload FROM trade_order WHERE portfolioId=? AND fundId=? AND orderId=? LIMIT 1;")
            .SetParameters(new GetNativeTradeOrder(id.PortfolioId,id.FundId,id.OrderId) { PostgreSql = string.Equals(ProviderName,"System.Data.Postgres",StringComparison.Ordinal) })
            .ExecuteSingleAsync(row => MessagePackBinarySerializer.Shared.Deserialize<TradeOrderDefinition>(row.GetBytes(0)), token);
    }

    /// <inheritdoc />
    public Task UpsertOrderExecutionAsync(OrderExecutionDefinition execution, CancellationToken token = default)
=>this.UpsertOrderExecutionAsyncCoreBinding(execution, token);
    /// <inheritdoc />
    public Task<OrderExecutionDefinition?> GetOrderExecutionAsync(TradeOrderId id, Guid executionAttemptId, CancellationToken token = default)
    {
        id.Require();
        executionAttemptId.Require("ExecutionAttemptId is required.", nameof(executionAttemptId));
        if (string.Equals(ProviderName,"System.Data.Postgres",StringComparison.Ordinal))
            return this.NativeTransaction().ExecuteOnceAsync((db,ct)=>new PostgresTradeFlowReadStore().ExecutionAsync(db,id,executionAttemptId,ct),token);
        return _dbFactory.TradeDb.Use("TradeFlow.Execution.Get", "SELECT payload FROM order_execution WHERE portfolioId=? AND fundId=? AND orderId=? AND executionAttemptId=?;")
            .SetParameters(new TradeDbValues([id.PortfolioId, id.FundId, id.OrderId, executionAttemptId]))
            .ExecuteSingleAsync(row => MessagePackBinarySerializer.Shared.Deserialize<OrderExecutionDefinition>(row.GetBytes(0)), token);
    }

    /// <inheritdoc />
    public Task UpsertEstablishedTradeAsync(EstablishedTradeDefinition trade, CancellationToken token = default)
=>this.UpsertEstablishedTradeAsyncCoreBinding(trade, token);
    /// <inheritdoc />
    public Task<EstablishedTradeDefinition?> GetEstablishedTradeAsync(TradeEntityId id, CancellationToken token = default)
    {
        id.Require();
        if (string.Equals(ProviderName,"System.Data.Postgres",StringComparison.Ordinal))
            return this.NativeTransaction().ExecuteOnceAsync((db,ct)=>new PostgresTradeFlowReadStore().TradeAsync(db,id,ct),token);
        return _dbFactory.TradeDb.Use("TradeFlow.Trade.Get", "SELECT payload FROM established_trade WHERE portfolioId=? AND fundId=? AND orderId=? AND tradeId=?;")
            .SetParameters(new TradeDbValues([id.PortfolioId, id.FundId, id.OrderId, id.TradeId]))
            .ExecuteSingleAsync(row => MessagePackBinarySerializer.Shared.Deserialize<EstablishedTradeDefinition>(row.GetBytes(0)), token);
    }

    /// <inheritdoc />
    public Task<QueryPage<EstablishedTradeDefinition>> GetEstablishedTradesAsync(int portfolioId, int fundId,
        TradeStrategyKind strategyKind, DateTime fromUtc, DateTime toUtc, int pageSize,
        byte[]? pagingState = null, CancellationToken token = default)
    {
        portfolioId.RequireUtcHistoryScope(fundId, fromUtc, toUtc, pageSize);
        return _dbFactory.TradeDb.Use("TradeFlow.Trade.History.Get", "SELECT payload FROM established_trade_history WHERE portfolioId=? AND fundId=? AND strategyKind=? AND establishedAtUtc>=? AND establishedAtUtc<=?;")
            .SetParameters(new TradeDbValues([portfolioId, fundId, strategyKind.ToString(), fromUtc, toUtc]))
            .ExecutePageAsync(row => MessagePackBinarySerializer.Shared.Deserialize<EstablishedTradeDefinition>(row.GetBytes(0)), pageSize, pagingState, token);
    }

    /// <inheritdoc />
    public Task UpsertStrategyPositionAsync(StrategyPositionSnapshot position, CancellationToken token = default)
=>this.UpsertStrategyPositionAsyncCoreBinding(position, token);
    /// <inheritdoc />
    public Task<QueryPage<StrategyPositionSnapshot>> GetStrategyPositionHistoryAsync(Guid positionId, DateTime fromUtc, DateTime toUtc, int pageSize, byte[]? pagingState = null, CancellationToken token = default)
    {
        positionId.RequireUtcHistoryScope(fromUtc, toUtc, pageSize);
        if (string.Equals(ProviderName,"System.Data.Postgres",StringComparison.Ordinal))
            return this.NativeTransaction().ExecuteOnceAsync((db,ct)=>new PostgresTradeFlowReadStore().PositionHistoryAsync(db,positionId,fromUtc,toUtc,pageSize,pagingState,ct),token);
        return _dbFactory.TradeDb.Use("TradeFlow.Position.History.Get", "SELECT payload FROM strategy_position_history WHERE positionId=? AND asOfUtc>=? AND asOfUtc<=?;")
            .SetParameters(new TradeDbValues([positionId, fromUtc, toUtc]))
            .ExecutePageAsync(row => MessagePackBinarySerializer.Shared.Deserialize<StrategyPositionSnapshot>(row.GetBytes(0)), pageSize, pagingState, token);
    }

    /// <inheritdoc />
    public Task<StrategyPositionSnapshot?> GetStrategyPositionAsync(StrategyPositionId id, CancellationToken token = default)
    {
        id.Require();
        if (string.Equals(ProviderName,"System.Data.Postgres",StringComparison.Ordinal))
            return this.NativeTransaction().ExecuteOnceAsync((db,ct)=>new PostgresTradeFlowReadStore().PositionAsync(db,id,ct),token);
        return _dbFactory.TradeDb.Use("TradeFlow.Position.Current.Get", "SELECT payload FROM strategy_position_current WHERE portfolioId=? AND fundId=? AND orderId=? AND tradeId=? AND positionId=?;")
            .SetParameters(new TradeDbValues([id.Trade.PortfolioId, id.Trade.FundId, id.Trade.OrderId, id.Trade.TradeId, id.PositionId]))
            .ExecuteSingleAsync(row => MessagePackBinarySerializer.Shared.Deserialize<StrategyPositionSnapshot>(row.GetBytes(0)), token);
    }

    /// <inheritdoc />
    public Task ReplaceOpenPositionRoutesAsync(StrategyPositionSnapshot position, CancellationToken token = default)
=>this.ReplaceOpenPositionRoutesAsyncCoreBinding(position, token);
    /// <inheritdoc />
    public Task<ICollection<OpenPositionRouteReadModel>> GetOpenPositionRoutesAsync(string contractId, CancellationToken token = default)
=>this.GetOpenPositionRoutesAsyncCoreBinding(contractId, token);
    /// <inheritdoc />
    public async Task<ICollection<OpenPositionRouteReadModel>> GetOpenPositionRouteSnapshotAsync(CancellationToken token = default)
    {
        if (string.Equals(ProviderName,"System.Data.Postgres",StringComparison.Ordinal))
            return (await (this.NativeTransaction().ExecuteOnceAsync((db,ct)=>new PostgresTradeFlowReadStore().RoutesAsync(db,null,ct),token))
                .ConfigureAwait(false)).ToArray();
        var result = await _dbFactory.TradeDb.Use("TradeFlow.Route.Recovery.Get.V2", "SELECT contractId,portfolioId,fundId,orderId,tradeId,positionId,tradeLegId,tradeType,generation FROM open_position_route_recovery WHERE shard=?;")
            .SetParameters(new TradeDbValues([(sbyte)0]))
            .ExecuteQueryAsync(row => new OpenPositionRouteReadModel(row.GetString(0), new PortfolioFundTradeLeg(
                row.GetInt(1), row.GetInt(2), row.GetInt(3), row.GetInt(4), row.GetGuid(5), row.GetGuid(6),
                row.GetEnum<TradeStrategyKind>(7), row.GetLong(8))), token)
            .ConfigureAwait(false);
        return result;
    }

    /// <inheritdoc />
    public Task<QueryPage<OpenPositionRouteReadModel>> GetOpenPositionRoutePageAsync(int pageSize, byte[]? pagingState = null, CancellationToken token = default)
    {
        this.ValidateRoutePageSize(pageSize);
        if (string.Equals(ProviderName,"System.Data.Postgres",StringComparison.Ordinal))
            return this.NativeTransaction().ExecuteOnceAsync((db,ct)=>new PostgresTradeFlowReadStore().RoutePageAsync(db,pageSize,pagingState,ct),token);
        return _dbFactory.TradeDb.Use("TradeFlow.Route.Recovery.Page", "SELECT contractId,portfolioId,fundId,orderId,tradeId,positionId,tradeLegId,tradeType,generation FROM open_position_route_recovery WHERE shard=?;")
            .SetParameters(new TradeDbValues([(sbyte)0]))
            .ExecutePageAsync(row => new OpenPositionRouteReadModel(row.GetString(0), new PortfolioFundTradeLeg(
                row.GetInt(1), row.GetInt(2), row.GetInt(3), row.GetInt(4), row.GetGuid(5), row.GetGuid(6),
                row.GetEnum<TradeStrategyKind>(7), row.GetLong(8))), pageSize, pagingState, token);
    }

    /// <inheritdoc />
    public Task UpsertTradeSelectionAsync(TradeSelectionFunctionCompletedEvent completed, CancellationToken cancellationToken = default)
=>this.UpsertTradeSelectionAsyncCoreBinding(completed, cancellationToken);    /// <inheritdoc />
    public Task<TradeSelectionFunctionCompletedEvent?> GetTradeSelectionInvocationAsync(StrategyWorkflowId workflowId, Guid invocationId, CancellationToken cancellationToken = default)
    {
        workflowId.RequireInvocation(invocationId, "Exact workflow and invocation identities are required.");
        if (string.Equals(ProviderName, "System.Data.Postgres", StringComparison.Ordinal))
            return this.NativeTransaction().ExecuteOnceAsync((db, ct) => PostgresPipelineReadStore.SelectionAsync(db, workflowId, invocationId, ct), cancellationToken);
        return _dbFactory.TradeDb.Use("TradeSelection.Exact", "SELECT event_payload,result_sha256 FROM trade_selection_invocation_event WHERE workflow_id=? AND invocation_id=? AND source_sequence=1;")
            .SetParameters(new SelectionValues([workflowId.Value, invocationId]))
            .ExecuteSingleAsync(row =>
            {
                var completed = MessagePackBinarySerializer.Shared.Deserialize<TradeSelectionFunctionCompletedEvent>(row.GetBytes(0));
                var r = TradeSelectionContracts.ReadResult(completed.Result);
                if (r.WorkflowId != workflowId || r.InvocationId != invocationId || !string.Equals(completed.Result.PayloadSha256,row.GetString(1),StringComparison.Ordinal)) throw new InvalidOperationException("Stored selector evidence identity/hash mismatch.");
                return completed;
            }, cancellationToken);
    }
    /// <inheritdoc />
    public Task<QueryPage<TradeSelectionHistoryRow>> GetTradeSelectionHistoryAsync(int portfolioId, int fundId, DateOnly valueDate, int pageSize, byte[]? pagingState = null, CancellationToken cancellationToken = default)
    {
        portfolioId.RequireHistoryScope(fundId, valueDate, pageSize, 200, "Invalid history scope or page size.");
        if (string.Equals(ProviderName, "System.Data.Postgres", StringComparison.Ordinal))
            return this.NativeTransaction().ExecuteOnceAsync((db, ct) => PostgresPipelineReadStore.SelectionHistoryAsync(db, portfolioId, fundId, valueDate, pageSize, pagingState, ct), cancellationToken);
        return _dbFactory.TradeDb.Use("TradeSelection.HistoryPage", "SELECT occurred_at_utc,workflow_id,invocation_id,event_id,target_horizon,outcome,reason_code,result_id,result_sha256 FROM trade_selection_history_by_fund_date WHERE portfolio_id=? AND fund_id=? AND value_date=?;")
            .SetParameters(new SelectionValues([portfolioId, fundId, valueDate]))
            .ExecutePageAsync(row => new TradeSelectionHistoryRow(portfolioId, fundId, valueDate, row.GetDateTime(0), row.GetGuid(1), row.GetGuid(2), row.GetGuid(3), row.GetShort(4), (byte)row.GetEnum<SelectionOutcome>(5), row.GetString(6), row.GetGuid(7), row.GetString(8)), pageSize, pagingState, cancellationToken);
    }
}
