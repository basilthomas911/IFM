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
    : ObjectDataRepository<TradeDbContext>(connectionSettings[TradeDbConnection], logger), ITradeDbContext
{
    public const string TradeDbConnection = "TradeDbConnection";
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

    static TradeTypeReadModel MapToTradeType<TDataRecord>(TDataRecord e) where TDataRecord : IObjectDataRecord
        => new(
            tradeType: e.GetEnum<TradeType>(0)
        );

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

    static TradeOrderReadModel MapToTradeOrder<TDataRecord>(TDataRecord e) where TDataRecord : IObjectDataRecord
        => new(
            fundId: e.GetInt(0),
            orderId: e.GetInt(1),
            tradeId: e.GetInt(2),
            valueDate: e.GetDateOnly(3),
            tradeType: e.GetEnum<TradeType>(4),
            tradeSubType: e.GetEnum<TradeSubType>(5),
            tradeDate: e.GetDateOnly(6),
            maturityDate: e.GetDateOnly(7),
            tradeOrderState: e.GetEnum<TradeOrderState>(8),
            underlyingContractId: e.GetString(9),
            underlyingAssetType: e.GetEnum<AssetType>(10),
            orderDescription: e.GetString(11),
            orderAction: e.GetEnum<OrderAction>(12),
            orderActionType: e.GetEnum<OrderActionType>(13),
            orderQuantity: e.GetInt(14),
            orderFilled: e.GetInt(15),
            orderType: e.GetEnum<OrderType>(16),
            orderPrice: e.GetDecimal(17),
            orderAmount: e.GetDecimal(18),
            commission: e.GetDecimal(19),
            totalAmount: e.GetDecimal(20),
            tradePnl: e.GetDecimal(21),
            tradeFillType: e.GetEnum<TradeFillType>(22),
            createdOn: e.GetDateTime(23),
            createdBy: e.GetString(24),
            updatedOn: e.GetDateTime(25),
            updatedBy: e.GetString(26)
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
        var optionTrades = await db.Use($"{nameof(TradeDbCql)}.{nameof(TradeDbCql.GetOptionTrades)}", TradeDbCql.GetOptionTrades)
                .SetParameters(new GetOptionTrades(orderId))
                .ExecuteQueryAsync(MapToOptionTrade!);

        var sourceTrades = optionTrades.ToArray();
        var hydratedTrades = new OptionTradeReadModel[sourceTrades.Length];
        await Parallel.ForEachAsync(
            Enumerable.Range(0, sourceTrades.Length),
            new ParallelOptions { MaxDegreeOfParallelism = 4 },
            async (index, _) => hydratedTrades[index] = await this.FillOptionTradeAsync(sourceTrades[index]));
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
        => await _dbFactory.TradeDb
            .Use($"{nameof(TradeDbCql)}.{nameof(TradeDbCql.GetOptionTradesAll)}", TradeDbCql.GetOptionTradesAll)
            .ExecuteQueryAsync(MapToOptionTrade!);

    /// <summary>
    /// return option trade by order id and  trade id
    /// </summary>
    /// <param name="orderId"></param>
    /// <param name="tradeId"></param>
    /// <returns></returns>
    public async Task<OptionTradeReadModel?> GetOptionTradeAsync(int orderId, int tradeId)
    {
        var db = _dbFactory.TradeDb;
        var optionTrade = await db.Use($"{nameof(TradeDbCql)}.{nameof(TradeDbCql.GetOptionTrade)}", TradeDbCql.GetOptionTrade)
               .SetParameters(new GetOptionTrade(orderId, tradeId))
               .ExecuteSingleAsync(MapToOptionTrade!);
        if (optionTrade is not null)
            optionTrade = await this.FillOptionTradeAsync(optionTrade);
        return optionTrade;
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
        => await _dbFactory.TradeDb
            .Use($"{nameof(TradeDbCql)}.{nameof(TradeDbCql.GetOptionTradeSpreadData)}", TradeDbCql.GetOptionTradeSpreadData)
            .SetParameters(new GetOptionTradeSpreadData(orderId, tradeId, valueDate, tradeType.ToStringFast()))
            .ExecuteSingleAsync(MapToOptionTradeSpreadData!);

    /// <summary>
    /// Asynchronously retrieves a collection of option trade spread data.
    /// </summary>
    /// <remarks>This method queries the trade database to obtain all available option trade spread data.  The
    /// returned collection will be empty if no data is found.</remarks>
    /// <returns>A task that represents the asynchronous operation. The task result contains a collection of  <see
    /// cref="OptionTradeSpreadsDataModel"/> representing the option trade spread data.</returns>
    public async Task<ICollection<OptionTradeSpreadsDataModel>> GetOptionTradeSpreadDataAsync()
        => await _dbFactory.TradeDb
            .Use($"{nameof(TradeDbCql)}.{nameof(TradeDbCql.GetOptionTradeSpreadDataAll)}", TradeDbCql.GetOptionTradeSpreadDataAll)
            .ExecuteQueryAsync(MapToOptionTradeSpreadData!);

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
        => await  _dbFactory.TradeDb
            .Use($"{nameof(TradeDbCql)}.{nameof(TradeDbCql.GetOptionTradeSpreadBarData)}", TradeDbCql.GetOptionTradeSpreadBarData)
            .SetParameters(new GetOptionTradeSpreadBarData(orderId, tradeId, valueDate, tradeType.ToStringFast(), startDate, endDate))
            .ExecuteQueryAsync(MapToOptionTradeSpreadBarsData!);

    /// <summary>
    /// Asynchronously retrieves a collection of option trade spread bar data.
    /// </summary>
    /// <remarks>This method queries the trade database to obtain all available option trade spread bar
    /// data.</remarks>
    /// <returns>A task that represents the asynchronous operation. The task result contains a collection of <see
    /// cref="OptionTradeSpreadBarsDataModel"/> representing the option trade spread bar data.</returns>
    public async Task<ICollection<OptionTradeSpreadBarsDataModel>> GetOptionTradeSpreadBarDataAsync()
        => await _dbFactory.TradeDb
            .Use($"{nameof(TradeDbCql)}.{nameof(TradeDbCql.GetOptionTradeSpreadBarDataAll)}", TradeDbCql.GetOptionTradeSpreadBarDataAll)
            .ExecuteQueryAsync(MapToOptionTradeSpreadBarsData!);

    /// <summary>
    /// return iron condor trade price
    /// </summary>
    /// <param name="tradeId"></param>
    /// <param name="valueDate"></param>
    /// <returns></returns>
    public async Task<TradePriceReadModel?> GetIronCondorTradePriceAsync(int tradeId, DateOnly valueDate)
        => await _dbFactory.TradeDb
            .Use($"{nameof(TradeDbCql)}.{nameof(TradeDbCql.GetOptionLegs)}", TradeDbCql.GetOptionLegs)
            .SetParameters(new GetOptionLegsWithValueDate(tradeId, valueDate))
            .ExecuteSingleAsync(MapToTradePrice);

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

        var tradePositions = await tradePositionsTask;
        if (tradePositions.Count == 0)
            return tradePositions;

        var optionLegsTask = db.Use($"{nameof(TradeDbCql)}.{nameof(TradeDbCql.GetOptionLegsByOrderAndTrade)}", TradeDbCql.GetOptionLegsByOrderAndTrade)
            .SetParameters(new GetOptionLegsByOrderAndTrade(orderId, tradeId))
            .ExecuteQueryAsync(MapToOptionLeg!);
        var valueDates = tradePositions.Select(position => position.ValueDate).Distinct().ToArray();
        var legDataByDate = new ICollection<OptionTradeLegDataReadModel>[valueDates.Length];
        await Parallel.ForEachAsync(
            Enumerable.Range(0, valueDates.Length),
            new ParallelOptions { MaxDegreeOfParallelism = 4 },
            async (dateIndex, _) => legDataByDate[dateIndex] = await db.Use($"{nameof(TradeDbCql)}.{nameof(TradeDbCql.GetOptionLegData)}", TradeDbCql.GetOptionLegData)
                .SetParameters(new GetOptionLegData(orderId, tradeId, valueDates[dateIndex]))
                .ExecuteQueryAsync(MapToOptionLegData!));

        var optionLegs = await optionLegsTask;
        var optionLegById = optionLegs.ToDictionary(leg => leg.ContractId, StringComparer.Ordinal);
        var legDataByPosition = MapToLegDataByPosition(legDataByDate, optionLegById, requireOptionLeg: false);
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
    /// Asynchronously retrieves all trade positions from the database.
    /// </summary>
    /// <remarks>This method queries the database to obtain all trade positions and maps them to  <see
    /// cref="TradePositionReadModel"/> instances. Ensure that the database connection is properly  configured before
    /// calling this method.</remarks>
    /// <returns>A task that represents the asynchronous operation. The task result contains a collection of  <see
    /// cref="TradePositionReadModel"/> objects representing the trade positions.</returns>
    public async Task<ICollection<TradePositionReadModel>> GetTradePositionsAsync()
        => await _dbFactory.TradeDb
            .Use($"{nameof(TradeDbCql)}.{nameof(TradeDbCql.GetTradePositionsAll)}", TradeDbCql.GetTradePositionsAll)
            .ExecuteQueryAsync(MapToTradePosition!);


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
       int orderId, int tradeId, DateOnly valueDate,  TradeStatus tradeStatus, int daysToExpiry)
        => [.. (await _dbFactory.TradeDb
                .Use($"{nameof(TradeDbCql)}.{nameof(TradeDbCql.GetTradePositionsById)}", TradeDbCql.GetTradePositionsById)
                .SetParameters(new GetTradePositionsById(
                    orderId,
                    tradeId,
                    valueDate,
                    tradeStatus.ToStringFast(),
                    daysToExpiry
                ))
                .ExecuteQueryAsync(MapToTradePosition!)).Select(e => e.TradeType.ToStringFast())];

    /// <summary>
    /// return trade position
    /// </summary>
    /// <param name="tradeId"></param>
    /// <param name="tradeType"></param>
    /// <param name="valueDate"></param>
    /// <param name="daysToExpiry"></param>
    /// <param name="tradeStatus"></param>
    /// <returns></returns>
    public async Task<TradePositionReadModel?> GetTradePositionAsync(
        int orderId,
        int tradeId,
        TradeType tradeType,
        DateOnly valueDate,
        int daysToExpiry,
        TradeStatus tradeStatus)
    {
        var db = _dbFactory.TradeDb;
        var tradePosition = await GetTradePositionAsync();
        if (tradePosition is not null)
        {
            var e = tradePosition;
            var optionLegs = await GetOptionLegs();
            var updatedOptionLegData = new List<OptionTradeLegDataReadModel>();
            var optionLegData = await GetOptionLegData();
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
                        updatedBy: old.UpdatedBy)).SetOptionLeg(optionLegs.Where(o => o.ContractId == old.OptionLegId).SingleOrDefault()));
            }
            tradePosition.AddOptionLegData(updatedOptionLegData);
        }
        return tradePosition;

        async Task<ICollection<OptionTradeLegReadModel>> GetOptionLegs()
            => await db.Use($"{nameof(TradeDbCql)}.{nameof(TradeDbCql.GetOptionLegsByOrderAndTrade)}", TradeDbCql.GetOptionLegsByOrderAndTrade)
                   .SetParameters(new GetOptionLegsByOrderAndTrade(orderId, tradeId))
                   .ExecuteQueryAsync(MapToOptionLeg!);

        async Task<ICollection<OptionTradeLegDataReadModel>> GetOptionLegData()
            => [.. (await db.Use($"{nameof(TradeDbCql)}.{nameof(TradeDbCql.GetOptionLegData)}", TradeDbCql.GetOptionLegData)
                   .SetParameters(new GetOptionLegData(
                       orderId,
                       tradeId,
                       valueDate
                   ))
                   .ExecuteQueryAsync(MapToOptionLegData!))
                        .Where(e => e.TradeType == tradeType
                            && e.TradeStatus == tradeStatus
                            && e.DaysToExpiry == daysToExpiry
                        )];

        async Task<TradePositionReadModel?> GetTradePositionAsync()
            => await db.Use($"{nameof(TradeDbCql)}.{nameof(TradeDbCql.GetTradePosition)}", TradeDbCql.GetTradePosition)
                   .SetParameters(new GetTradePosition(
                       orderId,
                       tradeId,
                       valueDate,
                       tradeStatus.ToStringFast(),
                       daysToExpiry,
                       tradeType.ToStringFast()
                   ))
                   .ExecuteSingleAsync(MapToTradePosition!);
    }

    /// <summary>
    /// return trade history by trade order
    /// </summary>
    /// <param name="orderId"></param>
    /// <returns></returns>
    public async Task<ICollection<TradeHistoryReadModel>> GetTradeHistoryAsync(int orderId)
        => [.. (await _dbFactory.TradeDb
                .Use($"{nameof(TradeDbCql)}.{nameof(TradeDbCql.GetTradeHistory)}", TradeDbCql.GetTradeHistory)
                .SetParameters(new GetTradeHistory(orderId))
                .ExecuteQueryAsync(MapToTradeHistory!)).OrderBy(e => e.ValueDate)];

    /// <summary>
    /// return trade orders by date range
    /// </summary>
    /// <param name="startDate"></param>
    /// <param name="endDate"></param>
    /// <returns></returns>
    public async Task<ICollection<TradeOrderReadModel>> GetTradeOrdersAsync(DateOnly startDate, DateOnly endDate)
        => await _dbFactory.TradeDb
            .Use($"{nameof(TradeDbCql)}.{nameof(TradeDbCql.GetTradeOrders)}", TradeDbCql.GetTradeOrders)
            .SetParameters(new GetTradeOrders(startDate, endDate))
            .ExecuteQueryAsync(MapToTradeOrder!);

    /// <summary>
    /// return list of contract ids
    /// </summary>
    /// <param name="tradeId"></param>
    /// <returns></returns>
    public async Task<ICollection<string>> GetOptionLegContractIdsAsync(int tradeId)
    {
        var optionLegs = await _dbFactory.TradeDb
                .Use($"{nameof(TradeDbCql)}.{nameof(TradeDbCql.GetOptionLegs)}", TradeDbCql.GetOptionLegs)
                .SetParameters(new GetOptionLegs(tradeId))
                .ExecuteQueryAsync(MapToOptionLeg!);
        var latestTradeLegs = optionLegs
            .GroupBy(e => e.OrderId)
            .OrderByDescending(e => e.Max(leg => leg.UpdatedOn))
            .FirstOrDefault();
        return latestTradeLegs is null
            ? []
            : [.. latestTradeLegs.Select(e => e.ContractId)];
    }

    /// <summary>
    /// Asynchronously retrieves a collection of all option leg data models.
    /// </summary>
    /// <remarks>This method queries the database to obtain all option leg entries. Ensure that the database
    /// connection is properly configured before calling this method.</remarks>
    /// <returns>A task that represents the asynchronous operation. The task result contains a collection of  <see
    /// cref="OptionTradeLegReadModel"/> representing all option legs.</returns>
    public async Task<ICollection<OptionTradeLegReadModel>> GetOptionLegsAsync()
        => await _dbFactory.TradeDb
            .Use($"{nameof(TradeDbCql)}.{nameof(TradeDbCql.GetOptionLegsAll)}", TradeDbCql.GetOptionLegsAll)
            .ExecuteQueryAsync(MapToOptionLeg!);

    /// <summary>
    /// Asynchronously retrieves a collection of option leg data models from the database.
    /// </summary>
    /// <remarks>This method queries the database to obtain all available option leg data. The returned
    /// collection will be empty if no data is found.</remarks>
    /// <returns>A task that represents the asynchronous operation. The task result contains a collection of  <see
    /// cref="OptionTradeLegDataReadModel"/> instances representing the option leg data.</returns>
    public async Task<ICollection<OptionTradeLegDataReadModel>> GetOptionLegDataAsync()
      => await _dbFactory.TradeDb
          .Use($"{nameof(TradeDbCql)}.{nameof(TradeDbCql.GetOptionLegDataAll)}", TradeDbCql.GetOptionLegDataAll)
          .ExecuteQueryAsync(MapToOptionLegData!);


    /// <summary>
    /// return trade quantity
    /// </summary>
    /// <param name="tradeId"></param>
    /// <returns></returns>
    public async Task<int> GetTradeQuantityAsync(int tradeId)
    {
        var db = _dbFactory.TradeDb;
        var optionLegs = await db.Use($"{nameof(TradeDbCql)}.{nameof(TradeDbCql.GetOptionLegs)}", TradeDbCql.GetOptionLegs)
            .SetParameters(new GetOptionLegs(tradeId))
            .ExecuteQueryAsync(MapToOptionLeg!);
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
        var db = _dbFactory.TradeDb;
        var tradeLimit = await db.Use($"{nameof(TradeDbCql)}.{nameof(TradeDbCql.GetTradeLimit)}", TradeDbCql.GetTradeLimit)
                .SetParameters(new GetTradeLimit(tradeId))
                .ExecuteSingleAsync(MapToTradeLimit!);

        var tradeTypeLimit = await GetTradeTypeLimitAsync(tradeLimit!.TradeId, tradeLimit.TradeType);
        return tradeTypeLimit is not null
            ? tradeLimit with {
                MaxLossLimit = tradeTypeLimit.MaxLossLimit,
                MinProfitLimit = tradeTypeLimit.MinProfitLimit,
                MaxProfitLimit = tradeTypeLimit.MaxProfitLimit}
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
        => await _dbFactory.TradeDb
            .Use($"{nameof(TradeDbCql)}.{nameof(TradeDbCql.GetTradeLimitAll)}", TradeDbCql.GetTradeLimitAll)
            .ExecuteQueryAsync(MapToTradeLimit!);

    /// <summary>
    /// return trade limit for selected trade
    /// </summary>
    /// <param name="tradeId"></param>
    /// <param name="tradeType"></param>
    /// <returns></returns>
    public async Task<TradeTypeLimitReadModel?> GetTradeTypeLimitAsync( int tradeId,  TradeType tradeType)
        => await _dbFactory.TradeDb
            .Use($"{nameof(TradeDbCql)}.{nameof(TradeDbCql.GetTradeTypeLimit)}", TradeDbCql.GetTradeTypeLimit)
            .SetParameters(new GetTradeTypeLimit(tradeId, tradeType.ToStringFast()))
            .ExecuteSingleAsync(MapToTradeTypeLimit!);

    /// <summary>
    /// return all trade type limit for selected trade
    /// </summary>
    /// <param name="tradeId"></param>
    /// <returns></returns>
    public async Task<ICollection<TradeTypeLimitReadModel>> GetTradeTypeLimitsAsync(int tradeId)
        => await _dbFactory.TradeDb
            .Use($"{nameof(TradeDbCql)}.{nameof(TradeDbCql.GetTradeTypeLimits)}", TradeDbCql.GetTradeTypeLimits)
            .SetParameters(new GetTradeTypeLimits(tradeId))
            .ExecuteQueryAsync(MapToTradeTypeLimit!);

    /// <summary>
    /// Asynchronously retrieves a collection of trade type limits.
    /// </summary>
    /// <returns>A task that represents the asynchronous operation. The task result contains a collection of  <see
    /// cref="TradeTypeLimitReadModel"/> representing the trade type limits.</returns>
    public async Task<ICollection<TradeTypeLimitReadModel>> GetTradeTypeLimitsAsync()
        => await _dbFactory.TradeDb
            .Use($"{nameof(TradeDbCql)}.{nameof(TradeDbCql.GetTradeTypeLimitAll)}", TradeDbCql.GetTradeTypeLimitAll)
            .ExecuteQueryAsync(MapToTradeTypeLimit!);

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
        => await _dbFactory.TradeDb
            .Use($"{nameof(TradeDbCql)}.{nameof(TradeDbCql.GetTradePlacementSignal)}", TradeDbCql.GetTradePlacementSignal)
            .SetParameters(new GetTradePlacementSignal(contractId, valueDate))
            .ExecuteSingleAsync(MapToTradePlacementSignal!);

    /// <summary>
    /// Asynchronously retrieves a collection of trade fill records.
    /// </summary>
    /// <remarks>This method queries the database to obtain all trade fill records and maps them to  <see
    /// cref="TradeFillReadModel"/> objects. The operation is performed asynchronously.</remarks>
    /// <returns>A task representing the asynchronous operation. The task result contains a collection of  <see
    /// cref="TradeFillReadModel"/> instances representing the trade fill records.</returns>
    public async Task<ICollection<TradeFillReadModel>> GetTradeFillsAsync()
        => await _dbFactory.TradeDb
            .Use($"{nameof(TradeDbCql)}.{nameof(TradeDbCql.GetTradeFillsAll)}", TradeDbCql.GetTradeFillsAll)
            .ExecuteQueryAsync(MapToTradeFill!);

    /// <summary>
    /// return trade fills for selected trade
    /// </summary>
    /// <param name="tradeId"></param>
    /// <returns></returns>
    public async Task<ICollection<TradeFillReadModel>> GetTradeFillsAsync(int orderId, int tradeId)
    {
        var db = _dbFactory.TradeDb;
        var tradeFills = await db.Use($"{nameof(TradeDbCql)}.{nameof(TradeDbCql.GetTradeFills)}", TradeDbCql.GetTradeFills)
            .SetParameters(new GetTradeFills(orderId, tradeId))
            .ExecuteQueryAsync(MapToTradeFill!);
        if (tradeFills.Count  > 0)
            foreach(var tf in tradeFills)
            {
                var tradeFillData = await db.Use($"{nameof(TradeDbCql)}.{nameof(TradeDbCql.GetTradeFillData)}", TradeDbCql.GetTradeFillData)
                    .SetParameters(new GetTradeFillData(orderId, tradeId, tf.FillDate))
                    .ExecuteQueryAsync(MapToTradeFillData!);
                if (tradeFillData.Count  > 0)
                    tf.AddTradeFillData(tradeFillData);
            }
        return tradeFills;
    }

    /// <summary>
    /// return trade plans by order id
    /// </summary>
    /// <param name="orderId"></param>
    /// <returns></returns>
    public async Task<ICollection<TradePlanReadModel>> GetTradePlansAsync(int orderId)
        => await _dbFactory.TradeDb
            .Use($"{nameof(TradeDbCql)}.{nameof(TradeDbCql.GetTradePlans)}", TradeDbCql.GetTradePlans)
            .SetParameters(new GetTradePlans(orderId))
            .ExecuteQueryAsync(MapToTradePlan!);

    /// <summary>
    /// return all trade plans
    /// </summary>
    /// <returns></returns>
    public async Task<ICollection<TradePlanReadModel>> GetTradePlansAsync()
        => await _dbFactory.TradeDb
            .Use($"{nameof(TradeDbCql)}.{nameof(TradeDbCql.GetTradePlansAll)}", TradeDbCql.GetTradePlansAll)
            .ExecuteQueryAsync(MapToTradePlan!);

    /// <summary>
    /// return trade plan
    /// </summary>
    /// <param name="orderId"></param>
    /// <param name="tradeId"></param>
    /// <param name="valueDate"></param>
    /// <returns></returns>
    public async Task<ICollection<TradePlanReadModel>> GetTradePlansAsync(int orderId, int tradeId, DateOnly valueDate)
        => await _dbFactory.TradeDb
            .Use($"{nameof(TradeDbCql)}.{nameof(TradeDbCql.GetTradePlansByValueDate)}", TradeDbCql.GetTradePlansByValueDate)
            .SetParameters(new GetTradePlansByTradeId(orderId, tradeId, valueDate))
            .ExecuteQueryAsync(MapToTradePlan!);

    /// <summary>
    /// Retrieves the most recent trade plans associated with the specified order and trade identifiers.
    /// </summary>
    /// <param name="orderId">The unique identifier of the order for which to retrieve trade plans.</param>
    /// <param name="tradeId">The unique identifier of the trade for which to retrieve trade plans.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains a collection of <see
    /// cref="TradePlanReadModel"/> objects representing the latest trade plans for the specified order and trade. The
    /// collection is empty if no trade plans are found.</returns>
    public async Task<ICollection<TradePlanReadModel>> GetLastTradePlansAsync(int orderId, int tradeId)
    => await _dbFactory.TradeDb
        .Use($"{nameof(TradeDbCql)}.{nameof(TradeDbCql.GetLastTradePlans)}", TradeDbCql.GetLastTradePlans)
        .SetParameters(new GetLastTradePlans(orderId, tradeId))
        .ExecuteQueryAsync(MapToTradePlan!);


    /// <summary>
    /// return last trade plan stop loss limit
    /// </summary>
    /// <param name="orderId"></param>
    /// <param name="tradeId"></param>
    /// <returns></returns>
    public async Task<TradePlanStopLossLimitReadModel?> GetTradePlanStopLossLimitAsync(int orderId, int tradeId)
        => await  _dbFactory.TradeDb
                .Use($"{nameof(TradeDbCql)}.{nameof(TradeDbCql.GetTradePlanStopLossLimit)}", TradeDbCql.GetTradePlanStopLossLimit)
                .SetParameters(new GetTradePlanStopLossLimit(orderId, tradeId))
                .ExecuteSingleAsync(MapToTradePlanStopLossLimit!);

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
        => await _dbFactory.TradeDb
            .Use($"{nameof(TradeDbCql)}.{nameof(TradeDbCql.GetTradePlansByDateRange)}", TradeDbCql.GetTradePlansByDateRange)
            .SetParameters(new GetTradePlansByDateRange(orderId, tradeId, startDate, endDate))
            .ExecuteQueryAsync(MapToTradePlan!);

    /// <summary>
    /// return trade plan forward loss ratios by date range
    /// </summary>
    /// <param name="startDate"></param>
    /// <param name="endDate"></param>
    /// <returns></returns>
    public async Task<ICollection<TradePlanForwardLossRatioReadModel>> GetTradePlanForwardLossRatiosAsync(DateOnly startDate, DateOnly endDate)
        => await _dbFactory.TradeDb
            .Use($"{nameof(TradeDbCql)}.{nameof(TradeDbCql.GetTradePlanForwardLossRatios)}", TradeDbCql.GetTradePlanForwardLossRatios)
            .SetParameters(new GetTradePlanForwardLossRatios(startDate, endDate))
            .ExecuteQueryAsync(MapToTradePlanForwardLossRatio!);

    /// <summary>
    /// return trade plan forward loss ration by value date
    /// </summary>
    /// <param name="valueDate"></param>
    /// <returns></returns>
    public async Task<TradePlanForwardLossRatioReadModel?> GetTradePlanForwardLossRatioAsync(DateOnly valueDate)
    {
        var rows = await _dbFactory.TradeDb
            .Use($"{nameof(TradeDbCql)}.{nameof(TradeDbCql.GetLastTradePlanForwardLossRatio)}", TradeDbCql.GetLastTradePlanForwardLossRatio)
            .SetParameters(new GetLastTradePlanForwardLossRatio(valueDate))
            .ExecuteQueryAsync(MapToTradePlanForwardLossRatioRow!);
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
        => await _dbFactory.TradeDb
            .Use($"{nameof(TradeDbCql)}.{nameof(TradeDbCql.GetTradePlanForwardLossLimit)}", TradeDbCql.GetTradePlanForwardLossLimit)
            .SetParameters(new GetTradePlanForwardLossLimit(
                orderId ,
                tradeId,
                valueDate,
                tradeType.ToStringFast()
            ))
            .ExecuteSingleAsync(MapToTradePlanForwardLossLimit!);

    /// <summary>
    /// return trade live feed
    /// </summary>
    /// <param name="orderId"></param>
    /// <param name="tradeId"></param>
    /// <returns></returns>
    public async Task<ICollection<TradeLiveFeedReadModel>> GetTradeLiveFeedAsync(int orderId, int tradeId)
        => await _dbFactory.TradeDb
                .Use($"{nameof(TradeDbCql)}.{nameof(TradeDbCql.GetTradeLiveFeed)}", TradeDbCql.GetTradeLiveFeed)
                .SetParameters(new GetTradeLiveFeed(orderId, tradeId))
                .ExecuteQueryAsync(MapToTradeLiveFeed!);

    /// <summary>
    /// return trade order
    /// </summary>
    /// <param name="valueDate"></param>
    /// <param name="tradeId"></param>
    /// <returns></returns>
    public async Task<TradeOrderReadModel?> GetTradeOrderAsync(DateOnly valueDate,  int tradeId)
        => await _dbFactory.TradeDb
                .Use($"{nameof(TradeDbCql)}.{nameof(TradeDbCql.GetTradeOrder)}", TradeDbCql.GetTradeOrder)
                .SetParameters(new GetTradeOrder(valueDate, tradeId))
                .ExecuteSingleAsync(MapToTradeOrder!);

    /// <summary>
    /// return trade orders by value date
    /// </summary>
    /// <param name="valueDate"></param>
    /// <param name="fundId"></param>
    /// <returns></returns>
    public async Task<ICollection<TradeOrderReadModel>> GetTradeOrdersByFundIdAsync(DateOnly valueDate, int fundId)
        => [.. (await _dbFactory.TradeDb
                .Use($"{nameof(TradeDbCql)}.{nameof(TradeDbCql.GetTradeOrdersByValueDate)}", TradeDbCql.GetTradeOrdersByValueDate)
                .SetParameters(new GetTradeOrdersByValueDate(valueDate))
                .ExecuteQueryAsync(MapToTradeOrder!)).Where(e => e.FundId == fundId)];

    /// <summary>
    /// return trade fill data
    /// </summary>
    /// <param name="tradeId"></param>
    /// <returns></returns>
    public async Task<ICollection<TradeFillDataReadModel>> GetTradeFillDataAsync(int tradeId)
        => await _dbFactory.TradeDb
                .Use($"{nameof(TradeDbCql)}.{nameof(TradeDbCql.GetTradeFillDataByTradeId)}", TradeDbCql.GetTradeFillDataByTradeId)
                .SetParameters(new GetTradeFillDataByTradeId(tradeId))
                .ExecuteQueryAsync(MapToTradeFillData!);

    /// <summary>
    /// Inserts or updates an option leg in the database asynchronously.
    /// </summary>
    /// <remarks>This method first deletes any existing option leg with the same trade and contract
    /// identifiers, then inserts the new option leg data. The operation is performed as a queued command to ensure
    /// atomicity and consistency.</remarks>
    /// <param name="e">The <see cref="OptionTradeLegReadModel"/> containing the option leg details to be upserted.</param>
    /// <returns></returns>
    public async Task InsertOptionLegAsync(OptionTradeLegReadModel e)
    {
        var db = _dbFactory.TradeDb;
        List<object> queuedCommands = [
            db.Use($"{nameof(TradeDbCql)}.{nameof(TradeDbCql.DeleteOptionLegById)}", TradeDbCql.DeleteOptionLegById)
                .SetParameters(new DeleteOptionLegById(e.OrderId, e.TradeId, e.ContractId))
                .QueueCommand(),
            db.Use($"{nameof(TradeDbCql)}.{nameof(TradeDbCql.InsertOptionLeg)}", TradeDbCql.InsertOptionLeg)
                .SetParameters(new InsertOptionLeg(
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
                ))
                .QueueCommand()
        ];
        await db.ExecuteQueuedCommandsAsync(queuedCommands);
    }

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
        => await _dbFactory.TradeDb
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
            .ExecuteCommandAsync();

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
    public async Task<long> InsertOptionLegsAsync(IEnumerable<OptionTradeLegReadModel> optionLegs)
    {
        var rowCount = 0l;
        await _dbFactory.TradeDb
            .Use($"{nameof(TradeDbCql)}.{nameof(TradeDbCql.InsertOptionLeg)}", TradeDbCql.InsertOptionLeg)
            .SetParameters(GetOptionLegs().Select(e => new InsertOptionLeg(
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
            .ExecuteCommandAsync();
        return rowCount;

        IEnumerable<OptionTradeLegReadModel> GetOptionLegs()
        {
            foreach (var e in optionLegs)
            {
                rowCount++;
                yield return e;
            }
        }

    }

    /// <summary>
    /// Inserts option leg data into the database asynchronously.
    /// </summary>
    /// <remarks>This method first deletes any existing option leg data for the specified trade and value
    /// date, then inserts the new data. The operation is performed asynchronously.</remarks>
    /// <param name="e">The <see cref="OptionTradeLegDataReadModel"/> containing the option leg data to be inserted.</param>
    /// <returns></returns>
    public async Task InsertOptionLegDataAsync(OptionTradeLegDataReadModel e)
    {
        var db = _dbFactory.TradeDb;
        List<object> queuedCommands = [
            db.Use($"{nameof(TradeDbCql)}.{nameof(TradeDbCql.DeleteOptionLegDataById)}", TradeDbCql.DeleteOptionLegDataById)
                .SetParameters(new DeleteOptionLegDataById(
                    e.OrderId,
                    e.TradeId,
                    e.ValueDate,
                    e.TradeType.ToStringFast(),
                    e.DaysToExpiry,
                    e.TradeStatus.ToStringFast(),
                    e.OptionLegId))
                .QueueCommand(),
            db.Use($"{nameof(TradeDbCql)}.{nameof(TradeDbCql.InsertOptionLegData)}", TradeDbCql.InsertOptionLegData)
                .SetParameters(new InsertOptionLegData(
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
                ))
                .QueueCommand()
        ];
        await db.ExecuteQueuedCommandsAsync(queuedCommands);
    }

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
        => await _dbFactory.TradeDb
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
            .ExecuteCommandAsync();

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
    public async Task<long> InsertOptionLegDataAsync(IEnumerable<OptionTradeLegDataReadModel> optionLegData)
    {
        var rowCount = 0L;
        await _dbFactory.TradeDb
        .Use($"{nameof(TradeDbCql)}.{nameof(TradeDbCql.InsertOptionLegData)}", TradeDbCql.InsertOptionLegData)
        .SetParameters(GetOptionLegData().Select(e => new InsertOptionLegData(
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
        .ExecuteCommandAsync();
        return rowCount;

        IEnumerable<OptionTradeLegDataReadModel> GetOptionLegData()
        {
            foreach (var e in optionLegData)
            {
                rowCount++;
                yield return e;
            }
        }
    }

    /// <summary>
    /// insert option trade
    /// </summary>
    /// <param name="e">futures bar data</param>
    /// <returns></returns>
    public async Task InsertOptionTradeAsync(OptionTradeReadModel e)
    {
        // save base option trade...
        var queuedCommands = new List<object>();
        var db = _dbFactory.TradeDb;
        var dbWriter = db as ITradeDbWriteContext;
        await dbWriter!.DeleteOptionTradeAsync(e.OrderId, e.TradeId);
        queuedCommands.Add(
        db.Use($"{nameof(TradeDbCql)}.{nameof(TradeDbCql.InsertOptionTrade)}", TradeDbCql.InsertOptionTrade)
            .SetParameters(new InsertOptionTrade(
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
            ))
            .QueueCommand());

        // save each option leg info...
        if (e.OptionLegs is not null)
        {
            foreach (var ol in e.OptionLegs)
                queuedCommands.Add(
                db.Use($"{nameof(TradeDbCql)}.{nameof(TradeDbCql.InsertOptionLeg)}", TradeDbCql.InsertOptionLeg)
                    .SetParameters(new InsertOptionLeg(
                        e.OrderId,
                        ol.TradeId,
                        ol.ContractId,
                        ol.Quantity,
                        ol.StrikePrice,
                        ol.OptionLegType.ToStringFast(),
                        ol.OptionLegAction.ToStringFast(),
                        DateTime.Now,
                        e.CreatedBy,
                        DateTime.Now,
                        e.CreatedBy
                    ))
                    .QueueCommand());
        }

        // save trade positions...
        if (e.TradePositions is not null)
        {
            foreach (var o in e.TradePositions)
            {
                queuedCommands.Add(
                db.Use($"{nameof(TradeDbCql)}.{nameof(TradeDbCql.InsertTradePosition)}", TradeDbCql.InsertTradePosition)
                    .SetParameters(new InsertTradePosition(
                        e.OrderId,
                        o.TradeId,
                        o.TradeType.ToStringFast(),
                        o.ValueDate,
                        o.DaysToExpiry,
                        o.TradeStatus.ToStringFast(),
                        o.Commission,
                        o.DeltaHedge,
                        o.NetSpread,
                        o.TradeValue,
                        o.TradePnl,
                        o.AssetPrice,
                        o.OTMProbability,
                        o.ForwardPrice,
                        o.ForwardLossRatio,
                        o.LossProbability,
                        o.RiskFreeRate,
                        DateTime.Now,
                        e.CreatedBy,
                        DateTime.Now,
                        e.CreatedBy
                    ))
                    .QueueCommand());

                // save option leg data within each trade position...
                if (o.OptionLegData is not null)
                {
                    foreach (var old in o.OptionLegData)
                        queuedCommands.Add(
                            db.Use($"{nameof(TradeDbCql)}.{nameof(TradeDbCql.InsertOptionLegData)}", TradeDbCql.InsertOptionLegData)
                                .SetParameters(new InsertOptionLegData(
                                    e.OrderId,
                                    old.TradeId,
                                    old.TradeType.ToStringFast(),
                                    old.ValueDate,
                                    old.DaysToExpiry,
                                    old.TradeStatus.ToStringFast(),
                                    old.OptionLegId,
                                    old.BidPrice,
                                    old.AskPrice,
                                    old.ImpliedVolatility,
                                    old.Delta,
                                    old.Gamma,
                                    old.Theta,
                                    old.Vega,
                                    old.Rho,
                                    DateTime.Now,
                                    e.CreatedBy,
                                    DateTime.Now,
                                    e.CreatedBy
                            ))
                            .QueueCommand());
                }
            }
        }

        // save trade limit...
        if (e.TradeLimit is not null)
        {
            queuedCommands.Add(
            db.Use($"{nameof(TradeDbCql)}.{nameof(TradeDbCql.InsertTradeLimit)}", TradeDbCql.InsertTradeLimit)
                .SetParameters(new InsertTradeLimit(
                    e.TradeLimit.TradeId,
                    e.TradeLimit.TradeType.ToStringFast(),
                    e.TradeLimit.RiskMargin,
                    e.TradeLimit.MaxProfit,
                    e.TradeLimit.MaxLoss,
                    e.TradeLimit.MaxReturn,
                    e.TradeLimit.MaxLossLimit,
                    e.TradeLimit.MinProfitLimit,
                    e.TradeLimit.MaxProfitLimit,
                    e.TradeLimit.MinProfitTarget,
                    e.TradeLimit.DailyProfitTarget,
                    DateTime.Now,
                    e.CreatedBy,
                    DateTime.Now,
                    e.CreatedBy
                ))
                .QueueCommand());

            // save trade type limits...
            queuedCommands.Add(
            db.Use($"{nameof(TradeDbCql)}.{nameof(TradeDbCql.InsertTradeTypeLimit)}", TradeDbCql.InsertTradeTypeLimit)
                .SetParameters(new InsertTradeTypeLimit(
                    e.TradeLimit.TradeId,
                    e.TradeLimit.TradeType.ToStringFast(),
                    e.TradeLimit.MaxLossLimit,
                    e.TradeLimit.MinProfitLimit,
                    e.TradeLimit.MaxProfitLimit
                ))
               .QueueCommand());

        }


        if (e.TradeTypeLimits is not null)
        {
            foreach (var ttl in e.TradeTypeLimits)
                queuedCommands.Add(
                db.Use($"{nameof(TradeDbCql)}.{nameof(TradeDbCql.InsertTradeTypeLimit)}", TradeDbCql.InsertTradeTypeLimit)
                    .SetParameters(new InsertTradeTypeLimit(
                        ttl.TradeId,
                        ttl.TradeType.ToStringFast(),
                        ttl.MaxLossLimit,
                        ttl.MinProfitLimit,
                        ttl.MaxProfitLimit
                    ))
                   .QueueCommand());
        }

        // save any trade fills...
        if (e.TradeFills is not null)
        {
            foreach (var tf in e.TradeFills)
            {
                queuedCommands.Add(
                db.Use($"{nameof(TradeDbCql)}.{nameof(TradeDbCql.InsertTradeFill)}", TradeDbCql.InsertTradeFill)
                    .SetParameters(new InsertTradeFill(
                        tf.OrderId,
                        tf.TradeId,
                        tf.FillDate,
                        tf.FillQuantity,
                        tf.CreatedOn,
                        tf.CreatedBy
                    ))
                    .QueueCommand());

                if (tf.TradeFillData is not null)
                    foreach (var tfd in tf.TradeFillData)
                        queuedCommands.Add(
                        db.Use($"{nameof(TradeDbCql)}.{nameof(TradeDbCql.InsertTradeFillData)}", TradeDbCql.InsertTradeFillData)
                            .SetParameters(new InsertTradeFillData(
                                tfd.OrderId,
                                tfd.TradeId,
                                tfd.ContractId,
                                tfd.FillDate,
                                tfd.BidPrice,
                                tfd.AskPrice,
                                tfd.Commission,
                                tfd.OptionLegAction.ToStringFast(),
                                tfd.CreatedOn,
                                tfd.CreatedBy
                            ))
                            .QueueCommand());
            }
        }
        await db.ExecuteQueuedCommandsAsync(queuedCommands);
    }

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
        => await _dbFactory.TradeDb
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
            .ExecuteCommandAsync();

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
    public async Task<long> InsertOptionTradesAsync(IEnumerable<OptionTradeReadModel> optionTrades)
    {
        var rowCount = 0L;
        await _dbFactory.TradeDb
            .Use($"{nameof(TradeDbCql)}.{nameof(TradeDbCql.InsertOptionTrade)}", TradeDbCql.InsertOptionTrade)
            .SetParameters(GetOptionTrades().Select(e => new InsertOptionTrade(
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
            .ExecuteCommandAsync();
        return rowCount;

        IEnumerable<OptionTradeReadModel> GetOptionTrades()
        {
            foreach (var e in optionTrades)
            {
                rowCount++;
                yield return e;
            }
        }
    }

    /// <summary>
    /// insert trade position
    /// </summary>
    /// <param name="e"></param>
    /// <returns></returns>
    public async Task InsertTradePositionAsync(TradePositionReadModel e)
    {
        var db = _dbFactory.TradeDb;
        List<object> queuedCommands = [
         db.Use($"{nameof(TradeDbCql)}.{nameof(TradeDbCql.DeleteTradePosition)}", TradeDbCql.DeleteTradePosition)
              .SetParameters(new DeleteTradePositionLowerCase(
                  e.OrderId,
                  e.TradeId
              ))
                  .QueueCommand(),
        db.Use($"{nameof(TradeDbCql)}.{nameof(TradeDbCql.DeleteOptionLegData)}", TradeDbCql.DeleteOptionLegData)
              .SetParameters(new DeleteOptionLegDataLowerCase(
                  e.OrderId,
                  e.TradeId
              ))
                  .QueueCommand()
        ];
        await db.ExecuteQueuedCommandsAsync(queuedCommands);

        queuedCommands.Clear();
        queuedCommands.Add(
        db.Use($"{nameof(TradeDbCql)}.{nameof(TradeDbCql.InsertTradePosition)}", TradeDbCql.InsertTradePosition)
            .SetParameters(new InsertTradePosition(
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
                e.CreatedOn,
                e.CreatedBy,
                e.UpdatedOn,
                e.UpdatedBy))
            .QueueCommand());

        // save option leg data within trade position...
        if (e.OptionLegData is not null && e.OptionLegData.Length > 0)
            queuedCommands.AddRange(
                e.OptionLegData.Select(old =>
                    db.Use($"{nameof(TradeDbCql)}.{nameof(TradeDbCql.InsertOptionLegData)}", TradeDbCql.InsertOptionLegData)
                    .SetParameters(new InsertOptionLegData(
                        e.OrderId,
                        old.TradeId,
                        old.TradeType.ToStringFast(),
                        old.ValueDate,
                        old.DaysToExpiry,
                        old.TradeStatus.ToStringFast(),
                        old.OptionLegId,
                        old.BidPrice,
                        old.AskPrice,
                        old.ImpliedVolatility,
                        old.Delta,
                        old.Gamma,
                        old.Theta,
                        old.Vega,
                        old.Rho,
                        old.CreatedOn,
                        old.CreatedBy,
                        old.UpdatedOn,
                        old.UpdatedBy
                    ))
                    .QueueCommand()));

        await db.ExecuteQueuedCommandsAsync(queuedCommands);
    }

    /// <summary>
    /// insert trade position
    /// </summary>
    /// <param name="e"></param>
    /// <returns></returns>
    public async Task InsertTradePositionAsync(ICollection<TradePositionReadModel> tradePositions)
    {
        if (tradePositions.Count == 0)
            return;

        var queuedCommands = new List<object>();
        var db = _dbFactory.TradeDb;
        foreach (var e in tradePositions)
        {
            queuedCommands.Add(
                db.Use($"{nameof(TradeDbCql)}.{nameof(TradeDbCql.DeleteTradePosition)}", TradeDbCql.DeleteTradePosition)
                .SetParameters(new DeleteTradePositionLowerCase(
                    e.OrderId,
                    e.TradeId
                ))
                .QueueCommand());

            queuedCommands.Add(
                db.Use($"{nameof(TradeDbCql)}.{nameof(TradeDbCql.DeleteOptionLegData)}", TradeDbCql.DeleteOptionLegData)
                .SetParameters(new DeleteOptionLegDataLowerCase(
                    e.OrderId,
                    e.TradeId
                ))
                .QueueCommand());

            queuedCommands.Add(
            db.Use($"{nameof(TradeDbCql)}.{nameof(TradeDbCql.InsertTradePosition)}", TradeDbCql.InsertTradePosition)
                .SetParameters(new InsertTradePosition(
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
                    e.CreatedOn,
                    e.CreatedBy,
                    e.UpdatedOn,
                    e.UpdatedBy))
                .QueueCommand());

            // save option leg data within trade position...
            if (e.OptionLegData is not null)
            {
                foreach (var old in e.OptionLegData)
                    queuedCommands.Add(
                    db.Use($"{nameof(TradeDbCql)}.{nameof(TradeDbCql.InsertOptionLegData)}", TradeDbCql.InsertOptionLegData)
                    .SetParameters(new InsertOptionLegData(
                        e.OrderId,
                        old.TradeId,
                        old.TradeType.ToStringFast(),
                        old.ValueDate,
                        old.DaysToExpiry,
                        old.TradeStatus.ToStringFast(),
                        old.OptionLegId,
                        old.BidPrice,
                        old.AskPrice,
                        old.ImpliedVolatility,
                        old.Delta,
                        old.Gamma,
                        old.Theta,
                        old.Vega,
                        old.Rho,
                        old.CreatedOn,
                        old.CreatedBy,
                        old.UpdatedOn,
                        old.UpdatedBy
                    ))
                    .QueueCommand());
            }
        }
        await db.ExecuteQueuedCommandsAsync(queuedCommands);
    }

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
        => await _dbFactory.TradeDb
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
            .ExecuteCommandAsync();

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
    public async Task<long> InsertTradePositionsAsync(IEnumerable<TradePositionReadModel> tradePositions)
    {
        var rowCount = 0L;
        await _dbFactory.TradeDb
            .Use($"{nameof(TradeDbCql)}.{nameof(TradeDbCql.InsertTradePosition)}", TradeDbCql.InsertTradePosition)
            .SetParameters(GetTradePositions().Select(e => new InsertTradePosition(
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
            .ExecuteCommandAsync();
        return rowCount;

        IEnumerable<TradePositionReadModel> GetTradePositions()
        {
            foreach (var e in tradePositions)
            {
                rowCount++;
                yield return e;
            }
        }
    }

    /// <summary>
    /// insert trade fills
    /// </summary>
    /// <param name="tradeFills"></param>
    /// <returns></returns>
    public async Task InsertTradeFillAsync(ICollection<TradeFillReadModel> tradeFills)
    {
        // save any trade fills...
        if (tradeFills.Count  > 0)
        {
            var db = _dbFactory.TradeDb;
            foreach (var tf in tradeFills)
            {
                var queuedCommands = new List<object>();
                queuedCommands.Add(
                db.Use($"{nameof(TradeDbCql)}.{nameof(TradeDbCql.DeleteTradeFill)}", TradeDbCql.DeleteTradeFill)
                .SetParameters(new DeleteTradeFill(
                    tf.OrderId,
                    tf.TradeId
                ))
                .QueueCommand());

                queuedCommands.Add(
                    db.Use($"{nameof(TradeDbCql)}.{nameof(TradeDbCql.DeleteTradeFillData)}", TradeDbCql.DeleteTradeFillData)
                    .SetParameters(new DeleteTradeFillData(
                        tf.OrderId,
                        tf.TradeId
                    ))
                    .QueueCommand());

                queuedCommands.Add(
                db.Use($"{nameof(TradeDbCql)}.{nameof(TradeDbCql.InsertTradeFill)}", TradeDbCql.InsertTradeFill)
                    .SetParameters(new InsertTradeFill(
                        tf.OrderId,
                        tf.TradeId,
                        tf.FillDate,
                        tf.FillQuantity,
                        tf.CreatedOn,
                        tf.CreatedBy))
                    .QueueCommand());

                if (tf.TradeFillData?.Length > 0)
                    foreach (var tfd in tf.TradeFillData!)
                        queuedCommands.Add(
                        db.Use($"{nameof(TradeDbCql)}.{nameof(TradeDbCql.InsertTradeFillData)}", TradeDbCql.InsertTradeFillData)
                            .SetParameters(new InsertTradeFillData(
                                tfd.OrderId,
                                tfd.TradeId,
                                tfd.ContractId,
                                tfd.FillDate,
                                tfd.BidPrice,
                                tfd.AskPrice,
                                tfd.Commission,
                                tfd.OptionLegAction.ToStringFast(),
                                tfd.CreatedOn,
                                tfd.CreatedBy))
                            .QueueCommand());
                await db.ExecuteQueuedCommandsAsync(queuedCommands, true);
            }
        }
    }

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
        => await _dbFactory.TradeDb
            .Use($"{nameof(TradeDbCql)}.{nameof(TradeDbCql.InsertTradeFill)}", TradeDbCql.InsertTradeFill)
            .SetParameters(tradeFills.Select(e => new InsertTradeFill(
                e.OrderId,
                e.TradeId,
                e.FillDate,
                e.FillQuantity,
                e.CreatedOn,
                e.CreatedBy
            )))
            .ExecuteCommandAsync();

    /// <summary>
    /// Inserts a collection of trade fills into the database asynchronously.
    /// </summary>
    /// <remarks>This method processes the provided collection of trade fills and inserts them into the
    /// database using a batch operation. The method returns the count of trade fills processed, which corresponds to
    /// the number of items in the input collection.</remarks>
    /// <param name="tradeFills">A collection of <see cref="TradeFillReadModel"/> objects representing the trade fills to be inserted. Each
    /// object must contain valid data for the associated trade fill.</param>
    /// <returns>The total number of trade fills successfully processed and inserted into the database.</returns>
    public async Task<long> InsertTradeFillsAsync(IEnumerable<TradeFillReadModel> tradeFills)
    {
        var rowCount = 0L;
        await _dbFactory.TradeDb
            .Use($"{nameof(TradeDbCql)}.{nameof(TradeDbCql.InsertTradeFill)}", TradeDbCql.InsertTradeFill)
            .SetParameters(GetTradeFills().Select(e => new InsertTradeFill(
                e.OrderId,
                e.TradeId,
                e.FillDate,
                e.FillQuantity,
                e.CreatedOn,
                e.CreatedBy
            )))
            .ExecuteCommandAsync();
        return rowCount;

        IEnumerable<TradeFillReadModel> GetTradeFills()
        {
            foreach (var e in tradeFills)
            {
                rowCount++;
                yield return e;
            }
        }


    }

    /// <summary>
    /// insert option tradde spread data
    /// </summary>
    /// <param name="e">option trade spread data</param>
    /// <returns></returns>
    public async Task InsertOptionTradeSpreadDataAsync(OptionTradeSpreadsDataModel e)
    {
        var sequenceId = e.SequenceId > 0
            ? e.SequenceId
            : await _sequenceIdGenerator.GetSequenceIdAsync(SequenceName.OptionTradeSpreadData_SequenceId);
        await _dbFactory.TradeDb
                .Use($"{nameof(TradeDbCql)}.{nameof(TradeDbCql.InsertOptionTradeSpreadData)}", TradeDbCql.InsertOptionTradeSpreadData)
                .SetParameters(new InsertOptionTradeSpreadData(
                    e.OrderId,
                    e.TradeId,
                    e.ValueDate,
                    e.TradeType.ToStringFast(),
                   sequenceId,
                    e.LossLimit,
                    e.WinLimit,
                    e.ForwardSpread,
                    e.NetSpread,
                    e.CreatedOn,
                    e.CreatedBy
                ))
                .ExecuteCommandAsync();
    }

    /// <summary>
    /// Asynchronously inserts a collection of option trade spread data into the database.
    /// </summary>
    /// <remarks>The method processes the data in batches of 1000 items to optimize database operations. Each
    /// data entry is assigned a unique sequence ID before insertion.</remarks>
    /// <param name="optionTradeSpreadsData">A collection of <see cref="OptionTradeSpreadsDataModel"/> objects representing the option trade spread data to
    /// be inserted.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public async Task InsertOptionTradeSpreadDataAsync(ICollection<OptionTradeSpreadsDataModel> optionTradeSpreadsData)
    {
        List<(long SequenceId, OptionTradeSpreadsDataModel Data)> batchData = [];
        var db = _dbFactory.TradeDb;
        for(var index =0; index < optionTradeSpreadsData.Count; index += 1000)
        {
            batchData.Clear();
            foreach (var e in optionTradeSpreadsData.Skip(index).Take(1000))
            {
                var id = await _sequenceIdGenerator.GetSequenceIdAsync(SequenceName.OptionTradeSpreadData_SequenceId);
                batchData.Add((id, e));
            }
            await db
                .Use($"{nameof(TradeDbCql)}.{nameof(TradeDbCql.InsertOptionTradeSpreadData)}", TradeDbCql.InsertOptionTradeSpreadData)
                .SetParameters(batchData.Select(e => new InsertOptionTradeSpreadData(
                    e.Data.OrderId,
                    e.Data.TradeId,
                    e.Data.ValueDate,
                    e.Data.TradeType.ToStringFast(),
                    e.SequenceId,
                    e.Data.LossLimit,
                    e.Data.WinLimit,
                    e.Data.ForwardSpread,
                    e.Data.NetSpread,
                    e.Data.CreatedOn,
                    e.Data.CreatedBy
                )))
                .ExecuteCommandAsync();
        }
    }

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
    public async Task<long> InsertOptionTradeSpreadDataAsync(IEnumerable<OptionTradeSpreadsDataModel> optionTradeSpreadsData)
    {
        var rowCount = 0L;
        var db = _dbFactory.TradeDb;
        var parameters = new List<InsertOptionTradeSpreadData>();
        foreach (var e in optionTradeSpreadsData)
        {
            var sequenceId = await _sequenceIdGenerator.GetSequenceIdAsync(SequenceName.OptionTradeSpreadData_SequenceId);
            parameters.Add(new InsertOptionTradeSpreadData(
                e.OrderId,
                e.TradeId,
                e.ValueDate,
                e.TradeType.ToStringFast(),
                sequenceId,
                e.LossLimit,
                e.WinLimit,
                e.ForwardSpread,
                e.NetSpread,
                e.CreatedOn,
                e.CreatedBy));
            rowCount++;
        }
        await db
            .Use($"{nameof(TradeDbCql)}.{nameof(TradeDbCql.InsertOptionTradeSpreadData)}", TradeDbCql.InsertOptionTradeSpreadData)
            .SetParameters(parameters)
            .ExecuteCommandAsync();
        return rowCount;
    }

    /// <summary>
    /// insert option tradde spread bar data
    /// </summary>
    /// <param name="e">option trade spread bar data</param>
    /// <returns></returns>
    public async Task InsertOptionTradeSpreadBarDataAsync(OptionTradeSpreadBarsDataModel e)
        => await _dbFactory.TradeDb
                .Use($"{nameof(TradeDbCql)}.{nameof(TradeDbCql.InsertOptionTradeSpreadBarData)}", TradeDbCql.InsertOptionTradeSpreadBarData)
                .SetParameters(new InsertOptionTradeSpreadBarData(
                    e.OrderId,
                    e.TradeId,
                    e.TradeType.ToStringFast(),
                    e.ValueDate,
                    e.BarDate,
                    e.LossLimit,
                    e.WinLimit,
                    e.ForwardSpread,
                    e.NetSpread
                ))
                .ExecuteCommandAsync();

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
        => await _dbFactory.TradeDb
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
                .ExecuteCommandAsync();

    /// <summary>
    /// Inserts a collection of option trade spread bar data into the database asynchronously.
    /// </summary>
    /// <remarks>This method processes the provided collection of option trade spread bar data and inserts
    /// each item into the database. The method ensures that all items in the collection are processed sequentially, and
    /// the total count of processed rows is returned.</remarks>
    /// <param name="optionTradeSpreadBarsData">A collection of <see cref="OptionTradeSpreadBarsDataModel"/> objects representing the option trade spread bar
    /// data to be inserted. Each object must contain valid values for all required fields.</param>
    /// <returns>The total number of rows processed during the insertion.</returns>
    public async Task<long> InsertOptionTradeSpreadBarDataAsync(IEnumerable<OptionTradeSpreadBarsDataModel> optionTradeSpreadBarsData)
    {
        var rowCount = 0L;
        await _dbFactory.TradeDb
            .Use($"{nameof(TradeDbCql)}.{nameof(TradeDbCql.InsertOptionTradeSpreadBarData)}", TradeDbCql.InsertOptionTradeSpreadBarData)
            .SetParameters(GetOptionTradeSpreadBarData().Select(e => new InsertOptionTradeSpreadBarData(
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
            .ExecuteCommandAsync();
        return rowCount;

        IEnumerable<OptionTradeSpreadBarsDataModel> GetOptionTradeSpreadBarData()
        {
            foreach (var e in optionTradeSpreadBarsData)
            {
                rowCount++;
                yield return e;
            }
        }
    }


    /// <summary>
    /// insert trade liveFeed
    /// </summary>
    /// <param name="e"></param>
    /// <returns></returns>
    public async Task InsertTradeLiveFeedAsync(TradeLiveFeedReadModel e)
        => await _dbFactory.TradeDb
                .Use($"{nameof(TradeDbCql)}.{nameof(TradeDbCql.InsertTradeLiveFeed)}", TradeDbCql.InsertTradeLiveFeed)
                .SetParameters(new InsertTradeLiveFeed(
                    e.OrderId,
                    e.TradeId,
                    e.LiveFeed
                ))
                .ExecuteCommandAsync();

    /// <summary>
    /// insert trade position state
    /// </summary>
    /// <param name="e"></param>
    /// <returns></returns>
    public async Task InsertTradePositionStateAsync(TradePositionStateReadModel e)
        => await _dbFactory.TradeDb
                .Use($"{nameof(TradeDbCql)}.{nameof(TradeDbCql.InsertTradePositionState)}", TradeDbCql.InsertTradePositionState)
                .SetParameters(new InsertTradePositionState(
                    e.OrderId,
                    e.TradeId,
                    e.TradePositionState.ToStringFast(),
                    e.OpenedOn,
                    e.OpenedBy
                ))
                .ExecuteCommandAsync();

    /// <summary>
    /// save trade order
    /// </summary>
    /// <param name="e">trade ticket</param>
    /// <returns></returns>
    public async Task InsertTradeOrderAsync(TradeOrderReadModel e)
    {
        var db = _dbFactory.TradeDb;
        List<object> queuedCommands = [
           db.Use($"{nameof(TradeDbCql)}.{nameof(TradeDbCql.DeleteTradeOrder)}", TradeDbCql.DeleteTradeOrder)
              .SetParameters(new DeleteTradeOrder(
                    e.FundId,
                    e.OrderId,
                    e.TradeId
              ))
                  .QueueCommand(),
        db.Use($"{nameof(TradeDbCql)}.{nameof(TradeDbCql.DeleteTradeFill)}", TradeDbCql.DeleteTradeFill)
              .SetParameters(new DeleteTradeFill(
                    e.OrderId,
                    e.TradeId
              ))
                  .QueueCommand(),
        db.Use($"{nameof(TradeDbCql)}.{nameof(TradeDbCql.DeleteTradeFillData)}", TradeDbCql.DeleteTradeFillData)
              .SetParameters(new DeleteTradeFillData(
                    e.OrderId,
                    e.TradeId
              ))
                  .QueueCommand()
          ];
        await db.ExecuteQueuedCommandsAsync(queuedCommands);

        // save trade order...
        queuedCommands.Clear();
        queuedCommands.Add(
        db.Use($"{nameof(TradeDbCql)}.{nameof(TradeDbCql.InsertTradeOrder)}", TradeDbCql.InsertTradeOrder)
            .SetParameters(new InsertTradeOrder(
                e.FundId,
                e.OrderId,
                e.TradeId,
                e.ValueDate,
                e.TradeType.ToStringFast(),
                e.TradeSubType.ToStringFast(),
                e.TradeDate,
                e.MaturityDate,
                e.TradeOrderState.ToStringFast(),
                e.UnderlyingContractId,
                e.UnderlyingAssetType.ToStringFast(),
                e.OrderDescription ?? string.Empty,
                e.OrderAction.ToStringFast(),
                e.OrderActionType.ToStringFast(),
                e.OrderQuantity,
                e.OrderType.ToStringFast(),
                e.OrderPrice,
                e.OrderAmount,
                e.Commission,
                e.TotalAmount,
                e.TradePnl,
                e.TradeFillType.ToStringFast(),
                e.CreatedOn,
                e.CreatedBy,
                e.CreatedOn,
                e.CreatedBy))
            .QueueCommand());

        // save trade fills...
        foreach (var tf in e.TradeFills)
        {
            queuedCommands.Add(
            db.Use($"{nameof(TradeDbCql)}.{nameof(TradeDbCql.InsertTradeFill)}", TradeDbCql.InsertTradeFill)
                .SetParameters(new InsertTradeFill(
                    tf.OrderId,
                    tf.TradeId,
                    tf.FillDate,
                    tf.FillQuantity,
                    tf.CreatedOn,
                    tf.CreatedBy
                ))
                .QueueCommand());

            foreach (var tfd in tf.TradeFillData)
                queuedCommands.Add(
                db.Use($"{nameof(TradeDbCql)}.{nameof(TradeDbCql.InsertTradeFillData)}", TradeDbCql.InsertTradeFillData)
                    .SetParameters(new InsertTradeFillData(
                        tfd.OrderId,
                        tfd.TradeId,
                        tfd.ContractId,
                        tfd.FillDate,
                        tfd.BidPrice,
                        tfd.AskPrice,
                        tfd.Commission,
                        tfd.OptionLegAction.ToStringFast(),
                        tfd.CreatedOn,
                        tfd.CreatedBy
                    ))
                    .QueueCommand());
        }
        await db.ExecuteQueuedCommandsAsync(queuedCommands);

    }

    /// <summary>
    /// insert trade plan forward loss limit
    /// </summary>
    /// <param name="e">trade plan forward loss limit</param>
    /// <returns></returns>
    public async Task InsertTradePlanForwardLossLimitAsync(TradePlanForwardLossLimitReadModel e)
    {
        var db = _dbFactory.TradeDb;
        var dbReader = db as ITradeDbReadContext;
        var existingTradePlanForwardLossLimit = await dbReader!.GetTradePlanForwardLossLimitAsync(e.OrderId, e.TradeId, e.ValueDate, e.TradeType);
        if (existingTradePlanForwardLossLimit is not null)
        {
            await db.Use($"{nameof(TradeDbCql)}.{nameof(TradeDbCql.DeleteTradePlanForwardLossLimit)}", TradeDbCql.DeleteTradePlanForwardLossLimit)
                .SetParameters(new DeleteTradePlanForwardLossLimit(
                    e.OrderId,
                    e.TradeId,
                    e.ValueDate,
                    e.TradeType.ToStringFast()
                ))
                .ExecuteCommandAsync();
        }
        await db.Use($"{nameof(TradeDbCql)}.{nameof(TradeDbCql.InsertTradePlanForwardLossLimit)}", TradeDbCql.InsertTradePlanForwardLossLimit)
            .SetParameters(new InsertTradePlanForwardLossLimit(
                e.OrderId,
                e.TradeId,
                e.ValueDate,
                e.TradeType.ToStringFast(),
                e.LimitType.ToStringFast()
            ))
            .ExecuteCommandAsync();

    }

    /// <inheritdoc />
    public async Task InsertTradePlanForwardLossRatioAsync(
        DateOnly valueDate,
        double forwardLossRatio)
    {
        var sequenceId = await _sequenceIdGenerator.GetSequenceIdAsync(SequenceName.TradePlan_SequenceId);
        var db = _dbFactory.TradeDb;
        await db.Use($"{nameof(TradeDbCql)}.{nameof(TradeDbCql.InsertTradePlanForwardLossRatio)}", TradeDbCql.InsertTradePlanForwardLossRatio)
            .SetParameters(new InsertTradePlanForwardLossRatio(
                1,
                valueDate,
                forwardLossRatio,
                sequenceId
            ))
           .ExecuteCommandAsync();
    }

    /// <summary>
    /// Inserts a trade placement signal into the database asynchronously and returns the generated sequence ID.
    /// </summary>
    /// <remarks>This method generates a unique sequence ID for the trade placement signal and stores the
    /// provided data in the database. Ensure that all required properties of the <paramref name="e"/> parameter are
    /// populated before calling this method.</remarks>
    /// <param name="e">The trade placement signal data to insert, encapsulated in a <see cref="TradePlacementSignalReadModel"/> object.</param>
    /// <returns>The sequence ID generated for the inserted trade placement signal.</returns>
    public async Task<long> InsertTradePlacementSignalAsync(TradePlacementSignalReadModel e)
    {
        var sequenceId = await _sequenceIdGenerator.GetSequenceIdAsync(SequenceName.TradePlacementSignal_SequenceId);
        await _dbFactory.TradeDb
                .Use($"{nameof(TradeDbCql)}.{nameof(TradeDbCql.InsertTradePlacementSignal)}", TradeDbCql.InsertTradePlacementSignal)
                .SetParameters(new InsertTradePlacementSignal(
                    sequenceId,
                    e.ContractId,
                    e.ValueDate,
                    e.TradePlacementSignal.ToStringFast(),
                    e.TradePrice,
                    e.CreatedOn,
                    e.CreatedBy
                ))
                .ExecuteCommandAsync();
        return sequenceId;
    }

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
        => await _dbFactory.TradeDb
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
                .ExecuteCommandAsync();

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
        => await _dbFactory.TradeDb
                .Use($"{nameof(TradeDbCql)}.{nameof(TradeDbCql.UpdateOptionTradeState)}", TradeDbCql.UpdateOptionTradeState)
                .SetParameters(new UpdateOptionTradeState(
                    orderId,
                    tradeId,
                    tradeState.ToStringFast(),
                    updatedOn,
                    updatedBy
                ))
                .ExecuteCommandAsync();

    /// <summary>
    /// update trade live feed
    /// </summary>
    /// <param name="tradeLiveFeed"></param>
    /// <returns></returns>
    public async Task UpdateTradeLiveFeedAsync(TradeLiveFeedReadModel tradeLiveFeed)
        => await _dbFactory.TradeDb
                .Use($"{nameof(TradeDbCql)}.{nameof(TradeDbCql.UpdateTradeLiveFeed)}", TradeDbCql.UpdateTradeLiveFeed)
                .SetParameters(new UpdateTradeLiveFeed(
                    tradeLiveFeed.OrderId,
                    tradeLiveFeed.TradeId,
                    tradeLiveFeed.LiveFeed
                ))
                .ExecuteCommandAsync();

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
        var dbTrade = (db as ITradeDbContext)!;
        var tradePosition = await dbTrade.GetTradePositionAsync(orderId, tradeId, tradeType, valueDate, daysToExpiry, oldTradeStatus);
        if (tradePosition is null)
            return;
       var updatedTradePosition = tradePosition with
        {
            TradeStatus = newTradeStatus,
            UpdatedOn = updatedOn,
            UpdatedBy = updatedBy
        };

        await dbTrade.DeleteTradePositionAsync(orderId, tradeId, tradeType, valueDate, daysToExpiry, oldTradeStatus);
        await dbTrade.InsertTradePositionAsync(updatedTradePosition);

        var optionLegData = await db
                .Use($"{nameof(TradeDbCql)}.{nameof(TradeDbCql.GetOptionLegData)}", TradeDbCql.GetOptionLegData)
                .SetParameters(new GetOptionLegData(orderId, tradeId, valueDate))
                .ExecuteQueryAsync(MapToOptionLegData!);
        foreach (var o in optionLegData.Where(e => e.TradeType == tradeType
            && e.DaysToExpiry == daysToExpiry
            && e.TradeStatus == oldTradeStatus))
        {
            await db.Use($"{nameof(TradeDbCql)}.{nameof(TradeDbCql.UpdateOptionLegDataStatus)}", TradeDbCql.UpdateOptionLegDataStatus)
                .SetParameters(new UpdateOptionLegDataStatus(
                    tradeId,
                    valueDate,
                    o.OptionLegId,
                    newTradeStatus.ToStringFast(),
                    updatedOn,
                    updatedBy
                ))
                .ExecuteCommandAsync();
        }
    }

    /// <summary>
    /// update option leg data
    /// </summary>
    /// <param name="e"></param>
    /// <returns></returns>
    public async Task UpdateOptionLegDataAsync(OptionTradeLegDataReadModel e)
        => await _dbFactory.TradeDb
                .Use($"{nameof(TradeDbCql)}.{nameof(TradeDbCql.UpdateOptionLegData)}", TradeDbCql.UpdateOptionLegData)
                .SetParameters(new UpdateOptionLegData(
                    e.OrderId,
                    e.TradeId,
                    e.ValueDate,
                    e.OptionLegId,
                    e.BidPrice,
                    e.AskPrice,
                    e.ImpliedVolatility,
                    e.Delta,
                    e.Gamma,
                    e.Theta,
                    e.Vega,
                    e.Rho,
                    e.UpdatedOn,
                    e.UpdatedBy
                ))
                .ExecuteCommandAsync();

    /// <summary>
    /// update trade order state
    /// </summary>
    /// <param name="e"></param>
    /// <param name="tradeOrderState"></param>
    /// <param name="updatedOn"></param>
    /// <param name="updatedBy"></param>
    /// <returns></returns>
    public async Task UpdateTradeOrderStateAsync(TradeOrderEntityId e, TradeOrderState tradeOrderState, DateTime updatedOn, string updatedBy)
        => await _dbFactory.TradeDb
                .Use($"{nameof(TradeDbCql)}.{nameof(TradeDbCql.UpdateTradeOrderState)}", TradeDbCql.UpdateTradeOrderState)
                .SetParameters(new UpdateTradeOrderState(
                    e.TradeId,
                    e.ValueDate,
                    tradeOrderState.ToStringFast(),
                    updatedOn,
                    updatedBy
                ))
                .ExecuteCommandAsync();

    /// <summary>
    /// update trade order order price
    /// </summary>
    /// <param name="e"></param>
    /// <param name="orderPrice"></param>
    /// <param name="updatedOn"></param>
    /// <param name="updatedBy"></param>
    /// <returns></returns>
    public async Task UpdateTradeOrderOrderPriceAsync(TradeOrderEntityId e, decimal orderPrice, DateTime updatedOn, string updatedBy)
        => await _dbFactory.TradeDb
                .Use($"{nameof(TradeDbCql)}.{nameof(TradeDbCql.UpdateTradeOrderOrderPrice)}", TradeDbCql.UpdateTradeOrderOrderPrice)
                .SetParameters(new UpdateTradeOrderOrderPrice(
                    e.TradeId,
                    e.ValueDate,
                    orderPrice,
                    updatedOn,
                    updatedBy
                ))
                .ExecuteCommandAsync();

    /// <summary>
    /// insert trade limit for selected trade
    /// </summary>
    /// <param name="e">trade limit</param>
    /// <returns></returns>
    public async Task InsertTradeLimitAsync(TradeLimitReadModel e)
        => await _dbFactory.TradeDb
                .Use($"{nameof(TradeDbCql)}.{nameof(TradeDbCql.InsertTradeLimit)}", TradeDbCql.InsertTradeLimit)
                .SetParameters(new InsertTradeLimitNoMaxLoss(
                    e.TradeId,
                    e.TradeType.ToStringFast(),
                    e.RiskMargin,
                    e.MaxProfit,
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
                ))
                .ExecuteCommandAsync();

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
        => await _dbFactory.TradeDb
                .Use($"{nameof(TradeDbCql)}.{nameof(TradeDbCql.InsertTradeLimit)}", TradeDbCql.InsertTradeLimit)
                .SetParameters(tradeLimits.Select(e => new InsertTradeLimitNoMaxLoss(
                    e.TradeId,
                    e.TradeType.ToStringFast(),
                    e.RiskMargin,
                    e.MaxProfit,
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
                .ExecuteCommandAsync();

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
    public async Task<long> InsertTradeLimitsAsync(IEnumerable<TradeLimitReadModel> tradeLimits)
    {
        var rowCount = 0L;
        await _dbFactory.TradeDb
            .Use($"{nameof(TradeDbCql)}.{nameof(TradeDbCql.InsertTradeLimit)}", TradeDbCql.InsertTradeLimit)
            .SetParameters(GetTradeLimits().Select(e => new InsertTradeLimitNoMaxLoss(
                e.TradeId,
                e.TradeType.ToStringFast(),
                e.RiskMargin,
                e.MaxProfit,
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
            .ExecuteCommandAsync();
        return rowCount;

        IEnumerable<TradeLimitReadModel> GetTradeLimits()
        {
            foreach (var e in tradeLimits)
            {
                rowCount++;
                yield return e;
            }
        }
    }

    /// <summary>
    /// insert trade limit for selected trade
    /// </summary>
    /// <param name="e">trade limit</param>
    /// <returns></returns>
    public async Task InsertTradeTypeLimitAsync(TradeTypeLimitReadModel e)
        => await _dbFactory.TradeDb
                .Use($"{nameof(TradeDbCql)}.{nameof(TradeDbCql.InsertTradeTypeLimit)}", TradeDbCql.InsertTradeTypeLimit)
                .SetParameters(new InsertTradeTypeLimit(
                    e.TradeId,
                    e.TradeType.ToStringFast(),
                    e.MaxLossLimit,
                    e.MinProfitLimit,
                    e.MaxProfitLimit
                ))
                .ExecuteCommandAsync();

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
        => await _dbFactory.TradeDb
                .Use($"{nameof(TradeDbCql)}.{nameof(TradeDbCql.InsertTradeTypeLimit)}", TradeDbCql.InsertTradeTypeLimit)
                .SetParameters(tradeTypeLimits.Select(e => new InsertTradeTypeLimit(
                    e.TradeId,
                    e.TradeType.ToStringFast(),
                    e.MaxLossLimit,
                    e.MinProfitLimit,
                    e.MaxProfitLimit
                )))
                .ExecuteCommandAsync();

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
    public async Task<long> InsertTradeTypeLimitsAsync(IEnumerable<TradeTypeLimitReadModel> tradeTypeLimits)
    {
        var rowCount = 0L;
        await _dbFactory.TradeDb
            .Use($"{nameof(TradeDbCql)}.{nameof(TradeDbCql.InsertTradeTypeLimit)}", TradeDbCql.InsertTradeTypeLimit)
            .SetParameters(GetTradeTypeLimits().Select(e => new InsertTradeTypeLimit(
                e.TradeId,
                e.TradeType.ToStringFast(),
                e.MaxLossLimit,
                e.MinProfitLimit,
                e.MaxProfitLimit
            )))
            .ExecuteCommandAsync();
        return rowCount;

        IEnumerable<TradeTypeLimitReadModel> GetTradeTypeLimits()
        {
            foreach (var e in tradeTypeLimits)
            {
                rowCount++;
                yield return e;
            }
        }

    }

    /// <summary>
    /// insert trade plan
    /// </summary>
    /// <param name="e">trade plan</param>
    /// <returns></returns>
    public async Task InsertTradePlanAsync(TradePlanReadModel e)
    {
        var sequenceId = await _sequenceIdGenerator.GetSequenceIdAsync(SequenceName.TradePlan_SequenceId);
        var db = _dbFactory.TradeDb;
        await db.Use($"{nameof(TradeDbCql)}.{nameof(TradeDbCql.InsertTradePlan)}", TradeDbCql.InsertTradePlan)
            .SetParameters(new InsertTradePlan(
                sequenceId,
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
                e.CreatedBy))
           .ExecuteCommandAsync();

        await db.Use($"{nameof(TradeDbCql)}.{nameof(TradeDbCql.InsertTradePlanForwardLossRatio)}", TradeDbCql.InsertTradePlanForwardLossRatio)
            .SetParameters(new InsertTradePlanForwardLossRatio(
                1,
                e.ValueDate,
                e.ForwardLossRatio,
                sequenceId
            ))
           .ExecuteCommandAsync();

    }

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
    public async Task InsertTradePlansAsync(ICollection<TradePlanByIdReadModel> tradePlans)
    {
        var db = _dbFactory.TradeDb;
        for(var  index = 0; index < tradePlans.Count; index += 2000 )
        {
            var tradePlanPage = new List<TradePlanByIdReadModel>(2000);
            var tradePlanQry = tradePlans
                .Skip(index)
                .Take(2000);
            foreach(var e in tradePlanQry)
            {
                var sequenceId = await _sequenceIdGenerator.GetSequenceIdAsync(SequenceName.TradePlan_SequenceId);
                tradePlanPage.Add(e with { Id = sequenceId });
            }

            await db.Use($"{nameof(TradeDbCql)}.{nameof(TradeDbCql.InsertTradePlan)}", TradeDbCql.InsertTradePlan)
            .SetParameters(tradePlanPage.Select(e => new InsertTradePlan(
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
           .ExecuteCommandAsync();

            await db.Use($"{nameof(TradeDbCql)}.{nameof(TradeDbCql.InsertTradePlanForwardLossRatio)}", TradeDbCql.InsertTradePlanForwardLossRatio)
                .SetParameters(tradePlanPage.Select(e => new InsertTradePlanForwardLossRatio(
                    1,
                    e.ValueDate,
                    e.ForwardLossRatio,
                    e.Id
                )))
               .ExecuteCommandAsync();
        }


    }

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
            await db.Use($"{nameof(TradeDbCql)}.{nameof(TradeDbCql.InsertTradePlan)}", TradeDbCql.InsertTradePlan)
                .SetParameters(GetTradePlans().Select(e => new InsertTradePlan(
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
               .ExecuteCommandAsync();

        }

        async Task InsertTradePlanForwardLossRatio()
        {
            var db = _dbFactory.TradeDb;
            await db.Use($"{nameof(TradeDbCql)}.{nameof(TradeDbCql.InsertTradePlanForwardLossRatio)}", TradeDbCql.InsertTradePlanForwardLossRatio)
                .SetParameters(GetForwardLossRatios().Select(e => new InsertTradePlanForwardLossRatio(
                    1,
                    e.ValueDate,
                    e.ForwardLossRatio,
                    e.Id
                )))
               .ExecuteCommandAsync();
        }

        IEnumerable<TradePlanByIdReadModel> GetTradePlans()
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
    public async Task DeleteTradePositionStateAsync(OptionTradeEntityId e)
        => await _dbFactory.TradeDb
                .Use($"{nameof(TradeDbCql)}.{nameof(TradeDbCql.DeleteTradePositionState)}", TradeDbCql.DeleteTradePositionState)
                .SetParameters(new DeleteTradePositionState(
                    e.OrderId,
                    e.TradeId
                ))
                .ExecuteCommandAsync();

    /// <summary>
    /// delete all trade positions for a given order and trade id
    /// </summary>
    /// <param name="orderId"></param>
    /// <param name="tradeId"></param>
    /// <returns></returns>
    public async Task DeleteTradePositionAsync(int orderId, int tradeId)
        => await _dbFactory.TradeDb
                .Use($"{nameof(TradeDbCql)}.{nameof(TradeDbCql.DeleteTradePosition)}", TradeDbCql.DeleteTradePosition)
                .SetParameters(new DeleteTradePosition(orderId, tradeId))
                .ExecuteCommandAsync();

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
        => await _dbFactory.TradeDb
                .Use($"{nameof(TradeDbCql)}.{nameof(TradeDbCql.DeleteTradePositionByPrimaryKey)}", TradeDbCql.DeleteTradePositionByPrimaryKey)
                .SetParameters(new DeleteTradePositionByPrimaryKey(
                    orderId,
                    tradeId,
                    tradeType.ToStringFast(),
                    valueDate,
                    daysToExpiry,
                    tradeStatus.ToStringFast()
                ))
                .ExecuteCommandAsync();

    /// <summary>
    /// delete trade limit
    /// </summary>
    /// <param name="tradeId"></param>
    /// <param name="tradeType"></param>
    /// <returns></returns>
    public async Task DeleteTradeLimitAsync(int tradeId, TradeType tradeType)
        => await _dbFactory.TradeDb
                .Use($"{nameof(TradeDbCql)}.{nameof(TradeDbCql.DeleteTradeLimitByTradeType)}", TradeDbCql.DeleteTradeLimitByTradeType)
                .SetParameters(new DeleteTradeLimitByTradeType(tradeId, tradeType.ToStringFast()))
                .ExecuteCommandAsync();

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
        => await _dbFactory.TradeDb
                .Use($"{nameof(TradeDbCql)}.{nameof(TradeDbCql.DeleteTradePlacementSignal)}", TradeDbCql.DeleteTradePlacementSignal)
                .SetParameters(new DeleteTradePlacementSignal(contractId, valueDate))
                .ExecuteCommandAsync();

    // <summary>
    /// delete selected trade type limit
    /// </summary>
    /// <param name="tradeId"></param>
    /// <returns></returns>
    public async Task DeleteTradeTypeLimitAsync(int tradeId)
        => await _dbFactory.TradeDb
                .Use($"{nameof(TradeDbCql)}.{nameof(TradeDbCql.DeleteTradeTypeLimit)}", TradeDbCql.DeleteTradeTypeLimit)
                .SetParameters(new DeleteTradeTypeLimit(tradeId))
                .ExecuteCommandAsync();

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

        await db.ExecuteQueuedCommandsAsync(queuedCommands, true);
    }

    /// <summary>
    /// delete trade
    /// </summary>
    /// <param name="orderId"></param>
    /// <param name="tradeId"></param>
    /// <param name="valueDate"></param>
    /// <param name="tradeType"></param>
    /// <returns></returns>
    public async Task DeleteOptionTradeSpreadBarDataAsync(int orderId, int tradeId,  DateOnly valueDate, TradeType tradeType)
        => await _dbFactory.TradeDb
                .Use($"{nameof(TradeDbCql)}.{nameof(TradeDbCql.DeleteOptionTradeSpreadBarData)}", TradeDbCql.DeleteOptionTradeSpreadBarData)
                .SetParameters(new DeleteOptionTradeSpreadBarData(
                    orderId,
                    tradeId,
                    valueDate,
                    tradeType.ToStringFast()
                ))
                .ExecuteCommandAsync();

    /// <summary>
    /// delete option trade spread data
    /// </summary>
    /// <param name="orderId"></param>
    /// <param name="tradeId"></param>
    /// <param name="valueDate"></param>
    /// <param name="tradeType"></param>
    /// <returns></returns>
    public async Task DeleteOptionTradeSpreadDataAsync(int orderId, int tradeId, DateOnly valueDate, TradeType tradeType)
        => await _dbFactory.TradeDb
                .Use($"{nameof(TradeDbCql)}.{nameof(TradeDbCql.DeleteOptionTradeSpreadData)}", TradeDbCql.DeleteOptionTradeSpreadData)
                .SetParameters(new DeleteOptionTradeSpreadData(
                    orderId,
                    tradeId,
                    valueDate,
                    tradeType.ToStringFast()
                ))
                .ExecuteCommandAsync();

    /// <summary>
    /// delete trade live feed
    /// </summary>
    /// <param name="tradeId"></param>
    /// <param name="orderId"></param>
    /// <returns></returns>
    public async Task DeleteTradeLiveFeedAsync(int orderId, int tradeId)
        => await _dbFactory.TradeDb
                .Use($"{nameof(TradeDbCql)}.{nameof(TradeDbCql.DeleteTradeLiveFeed)}", TradeDbCql.DeleteTradeLiveFeed)
                .SetParameters(new DeleteTradeLiveFeed(
                    orderId,
                    tradeId
                ))
                .ExecuteCommandAsync();

    /// <summary>
    /// delete all trade live feeds for order
    /// </summary>
    /// <param name="orderId"></param>
    /// <returns></returns>
    public async Task DeleteTradeLiveFeedsAsync(int orderId)
        => await _dbFactory.TradeDb
                .Use($"{nameof(TradeDbCql)}.{nameof(TradeDbCql.DeleteTradeLiveFeeds)}", TradeDbCql.DeleteTradeLiveFeeds)
                .SetParameters(new DeleteTradeLiveFeeds(
                    orderId
                ))
                .ExecuteCommandAsync();

    /// <summary>
    /// delete trade plan forward loss limit by id
    /// </summary>
    /// <param name="id"></param>
    /// <returns></returns>
    public async Task DeleteTradePlanForwardLossLimitAsync(TradePlanForwardLossLimitEntityId id)
        =>  await _dbFactory.TradeDb
                .Use($"{nameof(TradeDbCql)}.{nameof(TradeDbCql.DeleteTradePlanForwardLossLimit)}", TradeDbCql.DeleteTradePlanForwardLossLimit)
                .SetParameters(new DeleteTradePlanForwardLossLimit(
                    id.OrderId,
                    id.TradeId,
                    id.ValueDate,
                    id.TradeType.ToStringFast()
                ))
                .ExecuteCommandAsync();

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
        var tradeLimit = await GetTradeLimitAsync(tradeId);
        if (tradeLimit != null && tradeLimit.TradeType == tradeType)
        {
            var updatedTradeLimit = tradeLimit with { DailyProfitTarget = dailyProfitTarget, UpdatedOn = updatedOn, UpdatedBy = updatedBy };
            await InsertTradeLimitAsync(updatedTradeLimit);
        }
    }

    /// <inheritdoc />
    public async Task<ICollection<OptionTradeReadModel>> GetOptionTradesAsync(
        int orderId,
        CancellationToken cancellationToken)
    {
        var db = _dbFactory.TradeDb;
        var optionTrades = await db.Use($"{nameof(TradeDbCql)}.{nameof(TradeDbCql.GetOptionTrades)}", TradeDbCql.GetOptionTrades)
            .SetParameters(new GetOptionTrades(orderId))
            .ExecuteQueryAsync(MapToOptionTrade!, cancellationToken);

        var sourceTrades = optionTrades.ToArray();
        var hydratedTrades = new OptionTradeReadModel[sourceTrades.Length];
        await Parallel.ForEachAsync(
            Enumerable.Range(0, sourceTrades.Length),
            new ParallelOptions
            {
                MaxDegreeOfParallelism = 4,
                CancellationToken = cancellationToken
            },
            async (index, token) =>
                hydratedTrades[index] = await this.FillOptionTradeAsync(sourceTrades[index], token));
        return [.. hydratedTrades.OrderBy(e => e.IsPrimaryTrade)];
    }

    /// <inheritdoc />
    public async Task<OptionTradeReadModel?> GetOptionTradeAsync(
        int orderId,
        int tradeId,
        CancellationToken cancellationToken)
    {
        var db = _dbFactory.TradeDb;
        var optionTrade = await db.Use($"{nameof(TradeDbCql)}.{nameof(TradeDbCql.GetOptionTrade)}", TradeDbCql.GetOptionTrade)
            .SetParameters(new GetOptionTrade(orderId, tradeId))
            .ExecuteSingleAsync(MapToOptionTrade!, cancellationToken);
        return optionTrade is null
            ? null
            : await this.FillOptionTradeAsync(optionTrade, cancellationToken);
    }

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

    /// <inheritdoc />
    public Task<TradePriceReadModel?> GetIronCondorTradePriceAsync(
        int tradeId,
        DateOnly valueDate,
        CancellationToken cancellationToken)
        => _dbFactory.TradeDb
            .Use($"{nameof(TradeDbCql)}.{nameof(TradeDbCql.GetOptionLegs)}", TradeDbCql.GetOptionLegs)
            .SetParameters(new GetOptionLegsWithValueDate(tradeId, valueDate))
            .ExecuteSingleAsync(MapToTradePrice, cancellationToken);

    /// <inheritdoc />
    public async Task<ICollection<TradePositionReadModel>> GetTradePositionsAsync(
        int orderId,
        int tradeId,
        CancellationToken cancellationToken)
    {
        var db = _dbFactory.TradeDb;
        var tradePositions = await db.Use($"{nameof(TradeDbCql)}.{nameof(TradeDbCql.GetTradePositions)}", TradeDbCql.GetTradePositions)
            .SetParameters(new GetTradePositions(orderId, tradeId))
            .ExecuteQueryAsync(MapToTradePosition!, cancellationToken);
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
        await Parallel.ForEachAsync(
            Enumerable.Range(0, valueDates.Length),
            new ParallelOptions
            {
                MaxDegreeOfParallelism = 4,
                CancellationToken = cancellationToken
            },
            async (dateIndex, token) =>
                legDataByDate[dateIndex] = await db.Use($"{nameof(TradeDbCql)}.{nameof(TradeDbCql.GetOptionLegData)}", TradeDbCql.GetOptionLegData)
                    .SetParameters(new GetOptionLegData(orderId, tradeId, valueDates[dateIndex]))
                    .ExecuteQueryAsync(MapToOptionLegData!, token));

        var optionLegs = await optionLegsTask;
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
        var tradePosition = await db.Use($"{nameof(TradeDbCql)}.{nameof(TradeDbCql.GetTradePosition)}", TradeDbCql.GetTradePosition)
            .SetParameters(new GetTradePosition(
                orderId,
                tradeId,
                valueDate,
                tradeStatus.ToStringFast(),
                daysToExpiry,
                tradeType.ToStringFast()))
            .ExecuteSingleAsync(MapToTradePosition!, cancellationToken);
        if (tradePosition is null)
            return null;

        var optionLegsTask = db.Use($"{nameof(TradeDbCql)}.{nameof(TradeDbCql.GetOptionLegsByOrderAndTrade)}", TradeDbCql.GetOptionLegsByOrderAndTrade)
            .SetParameters(new GetOptionLegsByOrderAndTrade(orderId, tradeId))
            .ExecuteQueryAsync(MapToOptionLeg!, cancellationToken);
        var optionLegDataTask = db.Use($"{nameof(TradeDbCql)}.{nameof(TradeDbCql.GetOptionLegData)}", TradeDbCql.GetOptionLegData)
            .SetParameters(new GetOptionLegData(orderId, tradeId, valueDate))
            .ExecuteQueryAsync(MapToOptionLegData!, cancellationToken);
        await Task.WhenAll(optionLegsTask, optionLegDataTask);
        cancellationToken.ThrowIfCancellationRequested();

        var optionLegById = (await optionLegsTask)
            .ToDictionary(leg => leg.ContractId, StringComparer.Ordinal);
        var updatedOptionLegData = (await optionLegDataTask)
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

    /// <inheritdoc />
    public async Task<ICollection<TradeHistoryReadModel>> GetTradeHistoryAsync(
        int orderId,
        CancellationToken cancellationToken)
        => [.. (await _dbFactory.TradeDb
            .Use($"{nameof(TradeDbCql)}.{nameof(TradeDbCql.GetTradeHistory)}", TradeDbCql.GetTradeHistory)
            .SetParameters(new GetTradeHistory(orderId))
            .ExecuteQueryAsync(MapToTradeHistory!, cancellationToken))
            .OrderBy(e => e.ValueDate)];

    /// <inheritdoc />
    public async Task<ICollection<TradeFillReadModel>> GetTradeFillsAsync(
        int orderId,
        int tradeId,
        CancellationToken cancellationToken)
    {
        var db = _dbFactory.TradeDb;
        var tradeFills = await db.Use($"{nameof(TradeDbCql)}.{nameof(TradeDbCql.GetTradeFills)}", TradeDbCql.GetTradeFills)
            .SetParameters(new GetTradeFills(orderId, tradeId))
            .ExecuteQueryAsync(MapToTradeFill!, cancellationToken);
        foreach (var tradeFill in tradeFills)
        {
            var tradeFillData = await db.Use($"{nameof(TradeDbCql)}.{nameof(TradeDbCql.GetTradeFillData)}", TradeDbCql.GetTradeFillData)
                .SetParameters(new GetTradeFillData(
                    orderId,
                    tradeId,
                    tradeFill.FillDate))
                .ExecuteQueryAsync(MapToTradeFillData!, cancellationToken);
            if (tradeFillData.Count > 0)
                tradeFill.AddTradeFillData(tradeFillData);
        }
        return tradeFills;
    }

    /// <inheritdoc />
    public async Task<ICollection<string>> GetOptionLegContractIdsAsync(
        int tradeId,
        CancellationToken cancellationToken)
        => [.. (await _dbFactory.TradeDb
            .Use($"{nameof(TradeDbCql)}.{nameof(TradeDbCql.GetOptionLegs)}", TradeDbCql.GetOptionLegs)
            .SetParameters(new GetOptionLegs(tradeId))
            .ExecuteQueryAsync(MapToOptionLeg!, cancellationToken))
            .Select(e => e.ContractId)];

    /// <inheritdoc />
    public async Task<int> GetTradeQuantityAsync(
        int tradeId,
        CancellationToken cancellationToken)
    {
        var optionLegs = await _dbFactory.TradeDb
            .Use($"{nameof(TradeDbCql)}.{nameof(TradeDbCql.GetOptionLegs)}", TradeDbCql.GetOptionLegs)
            .SetParameters(new GetOptionLegs(tradeId))
            .ExecuteQueryAsync(MapToOptionLeg!, cancellationToken);
        var latestTradeLegs = optionLegs
            .GroupBy(e => e.OrderId)
            .OrderByDescending(e => e.Max(leg => leg.UpdatedOn))
            .FirstOrDefault();
        return latestTradeLegs is not null
            ? latestTradeLegs.Sum(e => e.Quantity) / latestTradeLegs.Count()
            : 0;
    }

    /// <inheritdoc />
    public async Task<TradeLimitReadModel?> GetTradeLimitAsync(
        int tradeId,
        CancellationToken cancellationToken)
    {
        var tradeLimit = await _dbFactory.TradeDb
            .Use($"{nameof(TradeDbCql)}.{nameof(TradeDbCql.GetTradeLimit)}", TradeDbCql.GetTradeLimit)
            .SetParameters(new GetTradeLimit(tradeId))
            .ExecuteSingleAsync(MapToTradeLimit!, cancellationToken);
        if (tradeLimit is null)
            return null;

        var tradeTypeLimit = await GetTradeTypeLimitAsync(
            tradeLimit.TradeId,
            tradeLimit.TradeType,
            cancellationToken);
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
    public Task<TradeTypeLimitReadModel?> GetTradeTypeLimitAsync(
        int tradeId,
        TradeType tradeType,
        CancellationToken cancellationToken)
        => _dbFactory.TradeDb
            .Use($"{nameof(TradeDbCql)}.{nameof(TradeDbCql.GetTradeTypeLimit)}", TradeDbCql.GetTradeTypeLimit)
            .SetParameters(new GetTradeTypeLimit(tradeId, tradeType.ToStringFast()))
            .ExecuteSingleAsync(MapToTradeTypeLimit!, cancellationToken);

    /// <inheritdoc />
    public async Task<ICollection<string>> GetTradePositionTradeTypesAsync(
        int orderId,
        int tradeId,
        DateOnly valueDate,
        TradeStatus tradeStatus,
        int daysToExpiry,
        CancellationToken cancellationToken)
        => [.. (await _dbFactory.TradeDb
            .Use($"{nameof(TradeDbCql)}.{nameof(TradeDbCql.GetTradePositionsById)}", TradeDbCql.GetTradePositionsById)
            .SetParameters(new GetTradePositionsById(
                orderId,
                tradeId,
                valueDate,
                tradeStatus.ToStringFast(),
                daysToExpiry))
            .ExecuteQueryAsync(MapToTradePosition!, cancellationToken))
            .Select(e => e.TradeType.ToStringFast())];


    /// <inheritdoc />
    public Task<IntrinsicTimeStrategyWorkflowReadModel?> GetIntrinsicTimeStrategyWorkflowAsync(
        StrategyWorkflowId workflowId)
        => GetIntrinsicTimeStrategyWorkflowAsync(workflowId, CancellationToken.None);

    /// <inheritdoc />
    public Task<IntrinsicTimeStrategyWorkflowReadModel?> GetIntrinsicTimeStrategyWorkflowAsync(
        StrategyWorkflowId workflowId,
        CancellationToken cancellationToken)
        => _dbFactory.TradeDb
            .Use($"{nameof(TradeDbCql)}.{nameof(TradeDbCql.GetIntrinsicTimeStrategyWorkflow)}", TradeDbCql.GetIntrinsicTimeStrategyWorkflow)
            .SetParameters(new GetIntrinsicTimeStrategyWorkflow(workflowId.Value))
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
            .Use($"{nameof(TradeDbCql)}.{nameof(TradeDbCql.GetActiveIntrinsicTimeStrategyWorkflow)}", TradeDbCql.GetActiveIntrinsicTimeStrategyWorkflow)
            .SetParameters(new GetActiveIntrinsicTimeStrategyWorkflow(workflowEntityId))
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
            .Use($"{nameof(TradeDbCql)}.{nameof(TradeDbCql.GetIntrinsicTimeStrategyWorkflowStartAttempts)}", TradeDbCql.GetIntrinsicTimeStrategyWorkflowStartAttempts)
            .SetParameters(new GetIntrinsicTimeStrategyWorkflowStartAttempts(workflowEntityId, beforeUtc, pageSize.RequirePageSize()))
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
            .Use($"{nameof(TradeDbCql)}.{nameof(TradeDbCql.GetIntrinsicTimeStrategyWorkflowTimeline)}", TradeDbCql.GetIntrinsicTimeStrategyWorkflowTimeline)
            .SetParameters(new GetIntrinsicTimeStrategyWorkflowTimeline(workflowId.Value, afterEventId, pageSize.RequirePageSize()))
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
            .Use($"{nameof(TradeDbCql)}.{nameof(TradeDbCql.GetIntrinsicTimeStrategyWorkflowsByEntity)}", TradeDbCql.GetIntrinsicTimeStrategyWorkflowsByEntity)
            .SetParameters(new GetIntrinsicTimeStrategyWorkflowsByEntity(workflowEntityId, beforeUtc, pageSize.RequirePageSize()))
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
                .Use($"{nameof(TradeDbCql)}.{nameof(TradeDbCql.GetIntrinsicTimeStrategyWorkflowsByStatusDay)}", TradeDbCql.GetIntrinsicTimeStrategyWorkflowsByStatusDay)
                .SetParameters(new GetIntrinsicTimeStrategyWorkflowsByStatusDay(status.ToString(), date, remaining))
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
    public async Task UpsertIntrinsicTimeStrategyWorkflowAsync(
        IntrinsicTimeStrategyWorkflowReadModel workflow,
        CancellationToken cancellationToken)
    {
        workflow.RequireNotNull(nameof(workflow));
        await _dbFactory.TradeDb
            .Use($"{nameof(TradeDbCql)}.{nameof(TradeDbCql.UpsertIntrinsicTimeStrategyWorkflow)}", TradeDbCql.UpsertIntrinsicTimeStrategyWorkflow)
            .SetParameters(new UpsertIntrinsicTimeStrategyWorkflow(
                workflow.WorkflowId.Value,
                workflow.WorkflowEntityId,
                workflow.WorkflowDefinitionId,
                workflow.WorkflowDefinitionVersion,
                workflow.ContractId,
                workflow.TimeFrameStartValueDate,
                workflow.TimePeriod.ToString(),
                workflow.TriggerEventId,
                workflow.CorrelationId,
                workflow.Status.ToString(),
                workflow.Outcome.ToString(),
                workflow.CurrentStage.ToString(),
                workflow.WorkflowRevision,
                workflow.LastEventId,
                workflow.StateSchemaVersion,
                workflow.StatePayload.ToArray(),
                workflow.StopReasonCode,
                workflow.StartedAtUtc,
                workflow.TerminalAtUtc,
                workflow.UpdatedAtUtc))
            .ExecuteCommandAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public Task UpsertActiveIntrinsicTimeStrategyWorkflowAsync(ActiveIntrinsicTimeStrategyWorkflowReadModel workflow)
        => UpsertActiveIntrinsicTimeStrategyWorkflowAsync(workflow, CancellationToken.None);

    /// <inheritdoc />
    public async Task UpsertActiveIntrinsicTimeStrategyWorkflowAsync(
        ActiveIntrinsicTimeStrategyWorkflowReadModel workflow,
        CancellationToken cancellationToken)
    {
        workflow.RequireNotNull(nameof(workflow));
        await _dbFactory.TradeDb
            .Use($"{nameof(TradeDbCql)}.{nameof(TradeDbCql.UpsertActiveIntrinsicTimeStrategyWorkflow)}", TradeDbCql.UpsertActiveIntrinsicTimeStrategyWorkflow)
            .SetParameters(new UpsertActiveIntrinsicTimeStrategyWorkflow(
                workflow.WorkflowEntityId,
                workflow.WorkflowId.Value,
                workflow.ContractId,
                workflow.TimeFrameStartValueDate,
                workflow.TimePeriod.ToString(),
                workflow.CurrentStage.ToString(),
                workflow.WorkflowRevision,
                workflow.LastEventId,
                workflow.StateSchemaVersion,
                workflow.StatePayload.ToArray(),
                workflow.StartedAtUtc,
                workflow.UpdatedAtUtc))
            .ExecuteCommandAsync(cancellationToken)
            .ConfigureAwait(false);
    }

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
    public async Task InsertIntrinsicTimeStrategyWorkflowStartAttemptAsync(
        IntrinsicTimeStrategyWorkflowStartAttemptReadModel attempt,
        CancellationToken cancellationToken)
    {
        attempt.RequireNotNull(nameof(attempt));
        await _dbFactory.TradeDb
            .Use($"{nameof(TradeDbCql)}.{nameof(TradeDbCql.InsertIntrinsicTimeStrategyWorkflowStartAttempt)}", TradeDbCql.InsertIntrinsicTimeStrategyWorkflowStartAttempt)
            .SetParameters(new InsertIntrinsicTimeStrategyWorkflowStartAttempt(
                attempt.WorkflowEntityId,
                attempt.RequestedAtUtc,
                attempt.RequestedWorkflowId.Value,
                attempt.Decision.ToString(),
                attempt.ActiveWorkflowId?.Value,
                attempt.StartCommandId,
                attempt.TriggerEventId,
                attempt.ActiveStage.ToString(),
                attempt.ReasonCode,
                attempt.SourceEventId))
            .ExecuteCommandAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public Task InsertIntrinsicTimeStrategyWorkflowTimelineAsync(
        IntrinsicTimeStrategyWorkflowTimelineReadModel timelineEvent)
        => InsertIntrinsicTimeStrategyWorkflowTimelineAsync(timelineEvent, CancellationToken.None);

    /// <inheritdoc />
    public async Task InsertIntrinsicTimeStrategyWorkflowTimelineAsync(
        IntrinsicTimeStrategyWorkflowTimelineReadModel timelineEvent,
        CancellationToken cancellationToken)
    {
        timelineEvent.RequireNotNull(nameof(timelineEvent));
        await _dbFactory.TradeDb
            .Use($"{nameof(TradeDbCql)}.{nameof(TradeDbCql.InsertIntrinsicTimeStrategyWorkflowTimeline)}", TradeDbCql.InsertIntrinsicTimeStrategyWorkflowTimeline)
            .SetParameters(new InsertIntrinsicTimeStrategyWorkflowTimeline(
                timelineEvent.WorkflowId.Value,
                timelineEvent.EventId,
                timelineEvent.WorkflowEntityId,
                timelineEvent.WorkflowRevision,
                timelineEvent.Stage.ToString(),
                timelineEvent.EventName,
                timelineEvent.EventSchemaVersion,
                timelineEvent.EventPayload.ToArray(),
                timelineEvent.OccurredAtUtc))
            .ExecuteCommandAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public Task UpsertIntrinsicTimeStrategyWorkflowByEntityAsync(
        IntrinsicTimeStrategyWorkflowHistoryReadModel workflow)
        => UpsertIntrinsicTimeStrategyWorkflowByEntityAsync(workflow, CancellationToken.None);

    /// <inheritdoc />
    public async Task UpsertIntrinsicTimeStrategyWorkflowByEntityAsync(
        IntrinsicTimeStrategyWorkflowHistoryReadModel workflow,
        CancellationToken cancellationToken)
    {
        workflow.RequireNotNull(nameof(workflow));
        await _dbFactory.TradeDb
            .Use($"{nameof(TradeDbCql)}.{nameof(TradeDbCql.UpsertIntrinsicTimeStrategyWorkflowByEntity)}", TradeDbCql.UpsertIntrinsicTimeStrategyWorkflowByEntity)
            .SetParameters(MapToByEntityParameters(workflow))
            .ExecuteCommandAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public Task UpsertIntrinsicTimeStrategyWorkflowByStatusDayAsync(
        IntrinsicTimeStrategyWorkflowHistoryReadModel workflow)
        => UpsertIntrinsicTimeStrategyWorkflowByStatusDayAsync(workflow, CancellationToken.None);

    /// <inheritdoc />
    public async Task UpsertIntrinsicTimeStrategyWorkflowByStatusDayAsync(
        IntrinsicTimeStrategyWorkflowHistoryReadModel workflow,
        CancellationToken cancellationToken)
    {
        workflow.RequireNotNull(nameof(workflow));
        await _dbFactory.TradeDb
            .Use($"{nameof(TradeDbCql)}.{nameof(TradeDbCql.UpsertIntrinsicTimeStrategyWorkflowByStatusDay)}", TradeDbCql.UpsertIntrinsicTimeStrategyWorkflowByStatusDay)
            .SetParameters(new UpsertIntrinsicTimeStrategyWorkflowByStatusDay(
                workflow.Status.ToString(),
                DateOnly.FromDateTime(workflow.StartedAtUtc),
                workflow.StartedAtUtc,
                workflow.WorkflowId.Value,
                workflow.WorkflowEntityId,
                workflow.Outcome.ToString(),
                workflow.CurrentStage.ToString(),
                workflow.WorkflowRevision,
                workflow.TerminalAtUtc,
                workflow.StopReasonCode))
            .ExecuteCommandAsync(cancellationToken)
            .ConfigureAwait(false);
    }

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
    public async Task UpsertMarketConditionAsync(MarketConditionReadModel result, CancellationToken token)
    {
        result.RequireNotNull(nameof(result));
        await _dbFactory.TradeDb.Use($"{nameof(TradeDbCql)}.{nameof(TradeDbCql.UpsertMarketCondition)}", TradeDbCql.UpsertMarketCondition)
            .SetParameters(new UpsertMarketCondition(result.WorkflowId.Value, result.WorkflowEntityId,
                result.InputWorkflowRevision, result.CommandId, result.SourceEventId, result.FundId,
                result.InstrumentRoot, result.TargetHorizon.ToString(), result.ParameterSetId,
                result.ParameterSetVersion, result.ParameterPayloadSha256, result.SnapshotId,
                result.SnapshotSha256, result.Tradeability.ToString(), result.ConditionType.ToString(),
                result.Direction.ToString(), result.Phase.ToString(), result.Strength, result.Confidence,
                result.PrimaryReasonCode, result.ResultPayload.ToArray(), result.ResultPayloadSha256,
                result.EvaluatedAtUtc, result.ValidUntilUtc, result.MarketDataAsOfUtc,
                result.CompletedAtUtc, result.UpdatedAtUtc, result.VolatilityBehavior.ToString(),
                result.LiquidityQuality.ToString(), result.DataQuality.ToString(),
                result.UpstreamAlignment.ToString(), result.EvidencePayload.ToArray(),
                result.ConflictingEvidencePayload.ToArray(), result.BlockingReasonsPayload.ToArray(),
                result.ReasonsPayload.ToArray(), result.SummaryText))
                    .ExecuteCommandAsync(token)
                    .ConfigureAwait(false);
        await _dbFactory.TradeDb.Use(
                $"{nameof(TradeDbCql)}.{nameof(TradeDbCql.UpsertMarketConditionByFund)}",
                TradeDbCql.UpsertMarketConditionByFund)
            .SetParameters(new UpsertMarketConditionByFund(result.FundId, result.InstrumentRoot,
                result.TargetHorizon.ToString(), result.EvaluatedAtUtc, result.WorkflowId.Value,
                result.WorkflowEntityId, result.InputWorkflowRevision, result.CommandId, result.SourceEventId,
                result.ParameterSetId, result.ParameterSetVersion, result.ParameterPayloadSha256,
                result.SnapshotId, result.SnapshotSha256, result.Tradeability.ToString(),
                result.ConditionType.ToString(), result.Direction.ToString(), result.Phase.ToString(),
                result.Strength, result.Confidence, result.PrimaryReasonCode, result.ResultPayload.ToArray(),
                result.ResultPayloadSha256, result.ValidUntilUtc, result.MarketDataAsOfUtc,
                result.CompletedAtUtc, result.UpdatedAtUtc, result.VolatilityBehavior.ToString(),
                result.LiquidityQuality.ToString(), result.DataQuality.ToString(),
                result.UpstreamAlignment.ToString(), result.EvidencePayload.ToArray(),
                result.ConflictingEvidencePayload.ToArray(), result.BlockingReasonsPayload.ToArray(),
                result.ReasonsPayload.ToArray(), result.SummaryText))
                    .ExecuteCommandAsync(token)
                    .ConfigureAwait(false);
    }
    internal static MarketConditionReadModel MapToMarketCondition(IObjectDataRecord row) => new()
    {
        WorkflowId = new StrategyWorkflowId(row.GetGuid(0)), WorkflowEntityId = row.GetString(1),
        InputWorkflowRevision = row.GetLong(2), CommandId = row.GetGuid(3), SourceEventId = row.GetGuid(4),
        FundId = row.GetInt(5), InstrumentRoot = row.GetString(6),
        TargetHorizon = Enum.Parse<TimeFrameType>(row.GetString(7)), ParameterSetId = row.GetGuid(8),
        ParameterSetVersion = row.GetInt(9), ParameterPayloadSha256 = row.GetString(10),
        SnapshotId = row.GetGuid(11), SnapshotSha256 = row.GetString(12),
        Tradeability = Enum.Parse<MarketTradeability>(row.GetString(13)),
        ConditionType = Enum.Parse<MarketConditionType>(row.GetString(14)),
        Direction = Enum.Parse<MarketConditionDirection>(row.GetString(15)),
        Phase = Enum.Parse<MarketConditionPhase>(row.GetString(16)), Strength = row.GetDecimal(17),
        Confidence = row.GetDecimal(18), PrimaryReasonCode = row.GetString(19),
        ResultPayload = row.GetBytes(20), ResultPayloadSha256 = row.GetString(21),
        EvaluatedAtUtc = row.GetDateTime(22), ValidUntilUtc = row.GetDateTime(23),
        MarketDataAsOfUtc = row.GetDateTime(24), CompletedAtUtc = row.GetDateTime(25), UpdatedAtUtc = row.GetDateTime(26),
        VolatilityBehavior = Enum.Parse<MarketConditionVolatilityBehavior>(row.GetString(27)),
        LiquidityQuality = Enum.Parse<MarketConditionLiquidityQuality>(row.GetString(28)),
        DataQuality = Enum.Parse<MarketConditionDataQuality>(row.GetString(29)),
        UpstreamAlignment = Enum.Parse<MarketConditionUpstreamAlignment>(row.GetString(30)),
        EvidencePayload = row.GetBytes(31), ConflictingEvidencePayload = row.GetBytes(32),
        BlockingReasonsPayload = row.GetBytes(33), ReasonsPayload = row.GetBytes(34),
        SummaryText = row.GetString(35)
    };

    /// <inheritdoc />
    public async Task UpsertMarketConditionAssessmentAsync(MarketConditionAssessmentCompletedEvent completed, CancellationToken cancellationToken = default)
    {
        var r = MarketConditionAssessmentContracts.ReadResult(completed.Result);
        completed.RequireAssessmentProjectionIdentity(r);
        var bytes = MessagePackSerializer.Serialize(completed);
        var hash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(bytes));
        await _dbFactory.TradeDb.Use("AssessmentProjection.Exact", "INSERT INTO market_condition_assessment (workflow_id,payload,payload_sha256) VALUES (?,?,?);")
            .SetParameters(new AssessmentValues([r.WorkflowId.Value, bytes, hash]))
            .ExecuteCommandAsync(cancellationToken)
            .ConfigureAwait(false);
        await _dbFactory.TradeDb.Use("AssessmentProjection.History", "INSERT INTO market_condition_assessment_by_profile (market_profile_id,instrument_root,target_horizon,evaluated_at_utc,workflow_id,payload,payload_sha256) VALUES (?,?,?,?,?,?,?);")
            .SetParameters(new AssessmentValues([r.MarketProfileId, r.InstrumentRoot, r.TargetHorizon.ToString(), r.EvaluatedAtUtc, r.WorkflowId.Value, bytes, hash]))
            .ExecuteCommandAsync(cancellationToken)
            .ConfigureAwait(false);
    }
    /// <inheritdoc />
    public Task<MarketConditionAssessmentCompletedEvent?> GetMarketConditionAssessmentAsync(StrategyWorkflowId workflowId, CancellationToken cancellationToken = default)
        => _dbFactory.TradeDb.Use("AssessmentProjection.Get", "SELECT payload,payload_sha256 FROM market_condition_assessment WHERE workflow_id=?;")
            .SetParameters(new AssessmentValues([workflowId.Value]))
            .ExecuteSingleAsync(MapToAssessment, cancellationToken);
    /// <inheritdoc />
    public Task<ICollection<MarketConditionAssessmentCompletedEvent>> GetMarketConditionAssessmentHistoryAsync(string profile, string root, TimeFrameType horizon, DateTime beforeUtc, int pageSize, CancellationToken cancellationToken = default)
    {
        profile.RequireAssessmentHistoryScope(root, horizon, beforeUtc, pageSize);
        return _dbFactory.TradeDb.Use("AssessmentProjection.HistoryRead", "SELECT payload,payload_sha256 FROM market_condition_assessment_by_profile WHERE market_profile_id=? AND instrument_root=? AND target_horizon=? AND evaluated_at_utc<? LIMIT ?;")
            .SetParameters(new AssessmentValues([profile, root, horizon.ToString(), beforeUtc, pageSize]))
            .ExecuteQueryAsync(MapToAssessment, cancellationToken);
    }
    internal static MarketConditionAssessmentCompletedEvent MapToAssessment(IObjectDataRecord row)
    {
        var bytes = row.GetBytes(0);
        if (Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(bytes)) != row.GetString(1)) throw new InvalidOperationException("Stored assessment payload hash mismatch.");
        var completed = MessagePackSerializer.Deserialize<MarketConditionAssessmentCompletedEvent>(bytes);
        var result = MarketConditionAssessmentContracts.ReadResult(completed.Result);
        if(completed.WorkflowId != result.WorkflowId || completed.Snapshot.PayloadSha256 != result.SnapshotSha256 ||
            completed.Snapshot.ComputeHash() != result.SnapshotSha256)
            throw new InvalidOperationException("Stored assessment snapshot or workflow identity mismatch.");
        return completed;
    }
    /// <inheritdoc />
    public async Task UpsertOrderCompositionAsync(OrderCompositionFunctionCompletedEvent completed, CancellationToken token = default)
    {
        var result = OrderCompositionContracts.ReadResult(completed.Result);
        if (completed.Id != result.ResultId || completed.WorkflowId != result.WorkflowId || completed.EntityId != result.EntityId
            || completed.InputWorkflowRevision != result.InputWorkflowRevision || completed.RequestFingerprint != result.InputSha256)
            throw new InvalidDataException("Composition projection identity mismatch.");
        var body = MessagePackBinarySerializer.Shared.Serialize(completed with { EventId = 0 });
        var inserted = await _dbFactory.TradeDb.Use("OrderComposition.Insert", "INSERT INTO order_composition_invocation (workflow_id,invocation_id,result_hash,input_hash,payload) VALUES (?,?,?,?,?) IF NOT EXISTS;")
            .SetParameters(new CompositionValues([result.WorkflowId.Value, result.InvocationId, completed.Result.PayloadSha256, result.InputSha256, body]))
            .ExecuteSingleAsync(row => row.GetBool(0), token)
            .ConfigureAwait(false);
        if (!inserted)
        {
            var old = await GetOrderCompositionInvocationAsync(result.WorkflowId, result.InvocationId, token)
                .ConfigureAwait(false);
            if (old is null || !OrderCompositionContracts.SameCompletion(old, completed))
                throw new InvalidOperationException("Conflicting composition invocation.");
        }
        // The typed decision retains exact authority even when no candidate is available.
        int portfolioId = result.DecisionContext.PortfolioId;
        int fundId = result.DecisionContext.FundId;
        await _dbFactory.TradeDb.Use("OrderComposition.HistoryInsert", "INSERT INTO order_composition_history (portfolio_id,fund_id,value_date,evaluated_at_utc,invocation_id,workflow_id,event_id,target_horizon,outcome,reason_code,result_id,result_hash) VALUES (?,?,?,?,?,?,?,?,?,?,?,?);")
            .SetParameters(new CompositionValues([portfolioId, fundId, result.DecisionContext.ValueDate, result.EvaluatedAtUtc,
                result.InvocationId, result.WorkflowId.Value, completed.Id, (short)result.TargetHorizon, (sbyte)result.Outcome,
                result.Reasons[0], result.ResultId, completed.Result.PayloadSha256]))
                    .ExecuteCommandAsync(token)
                    .ConfigureAwait(false);
    }
    /// <inheritdoc />
    public Task<OrderCompositionFunctionCompletedEvent?> GetOrderCompositionInvocationAsync(StrategyWorkflowId workflowId, Guid invocationId, CancellationToken token = default)
    {
        workflowId.RequireInvocation(invocationId, "Exact workflow and invocation required.");
        return _dbFactory.TradeDb.Use("OrderComposition.Exact", "SELECT payload,result_hash,input_hash FROM order_composition_invocation WHERE workflow_id=? AND invocation_id=?;")
            .SetParameters(new CompositionValues([workflowId.Value, invocationId]))
            .ExecuteSingleAsync(row =>
            {
                var completed = MessagePackBinarySerializer.Shared.Deserialize<OrderCompositionFunctionCompletedEvent>(row.GetBytes(0));
                var result = OrderCompositionContracts.ReadResult(completed.Result);
                if (result.WorkflowId != workflowId || result.InvocationId != invocationId || completed.Result.PayloadSha256 != row.GetString(1)
                    || result.InputSha256 != row.GetString(2)) throw new InvalidDataException("Stored composition evidence mismatch.");
                return completed;
            }, token);
    }
    /// <inheritdoc />
    public Task<QueryPage<OrderCompositionHistoryRow>> GetOrderCompositionHistoryAsync(int portfolioId, int fundId, DateOnly valueDate,
        int pageSize, byte[]? pagingState = null, CancellationToken token = default)
    {
        portfolioId.RequireHistoryScope(fundId, valueDate, pageSize, 100, "Invalid composition history scope.");
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
        => _dbFactory.TradeDb
            .Use($"{nameof(TradeDbCql)}.{nameof(TradeDbCql.GetRegimeDiscovery)}", TradeDbCql.GetRegimeDiscovery)
            .SetParameters(new GetRegimeDiscovery(workflowId.Value))
            .ExecuteSingleAsync(MapToRegimeDiscovery, cancellationToken);

    /// <inheritdoc />
    public Task UpsertRegimeDiscoveryAsync(RegimeDiscoveryReadModel result)
        => UpsertRegimeDiscoveryAsync(result, CancellationToken.None);

    /// <inheritdoc />
    public async Task UpsertRegimeDiscoveryAsync(
        RegimeDiscoveryReadModel result,
        CancellationToken cancellationToken)
    {
        result.RequireNotNull(nameof(result));
        await _dbFactory.TradeDb
            .Use($"{nameof(TradeDbCql)}.{nameof(TradeDbCql.UpsertRegimeDiscovery)}", TradeDbCql.UpsertRegimeDiscovery)
            .SetParameters(new UpsertRegimeDiscovery(
                result.WorkflowId.Value,
                result.WorkflowEntityId,
                result.InputWorkflowRevision,
                result.CommandId,
                result.SourceEventId,
                result.SourceEventSequence,
                result.Status,
                result.ParameterPayloadSha256,
                result.SignalSnapshotId,
                result.ResultPayload.ToArray(),
                result.ResultPayloadSha256,
                result.FailureCode,
                result.FailureMessage,
                result.ReasonsPayload.ToArray(),
                result.SchemaVersion,
                result.TerminalAtUtc,
                result.UpdatedAtUtc))
            .ExecuteCommandAsync(cancellationToken)
            .ConfigureAwait(false);
    }

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
    public async Task<RiskHistoryProjectionResult> UpsertRiskHistoryAsync(WorkflowStrategyStateUpdatedEvent snapshot, CancellationToken token = default)
    {
        var row = RiskHistoryIdentity.Row(snapshot);
        if (row is null) return new(RiskHistoryProjectionDisposition.NotApplicable);
        token.ThrowIfCancellationRequested();

        // The exact invocation row and its scoped summary are one rebuildable projection unit.
        // Once the unit starts, finish every idempotent Scylla statement even if the recovery
        // worker is asked to stop; otherwise shutdown can leave an exact row without its history
        // row and surface cancellation from the second statement as an unhandled worker error.
        var projectionToken = CancellationToken.None;
        if (snapshot.WorkflowId != snapshot.State.WorkflowId || snapshot.WorkflowRevision != row.Revision || snapshot.EntityId != snapshot.State.EntityId)
            throw new InvalidDataException("Risk history source identity mismatch.");
        var canonical = MessagePackBinarySerializer.Shared.Deserialize<WorkflowStrategyStateUpdatedEvent>(
            MessagePackBinarySerializer.Shared.Serialize(snapshot with { EventId = 0 }));
        var payload = MessagePackBinarySerializer.Shared.Serialize(canonical);
        var hash = RiskContracts.Hash(canonical);
        var inserted = await _dbFactory.TradeDb.Use("RiskHistory.Insert", "INSERT INTO risk_management_invocation (workflow_id,invocation_id,revision,payload,content_hash) VALUES (?,?,?,?,?) IF NOT EXISTS;")
            .SetParameters(new RiskValues([row.WorkflowId,row.InvocationId,row.Revision,payload,hash]))
            .ExecuteSingleAsync(r=>r.GetBool(0),projectionToken);
        if (!inserted)
        {
            var old = await _dbFactory.TradeDb.Use("RiskHistory.Verify", "SELECT content_hash FROM risk_management_invocation WHERE workflow_id=? AND invocation_id=? AND revision=?;")
                .SetParameters(new RiskValues([row.WorkflowId,row.InvocationId,row.Revision]))
                .ExecuteSingleAsync(r=>r.GetString(0),projectionToken);
            if (old != hash) return new(RiskHistoryProjectionDisposition.Conflict,row.WorkflowId,row.InvocationId,row.Revision,old,hash);
        }
        var disposition=inserted?RiskHistoryProjectionDisposition.Projected:RiskHistoryProjectionDisposition.AlreadyProjected;
        var summary = MessagePackBinarySerializer.Shared.Serialize(row);
        var added = await _dbFactory.TradeDb.Use("RiskHistory.SummaryInsert", "INSERT INTO risk_management_history (portfolio_id,fund_id,value_date,evaluated_at_utc,invocation_id,revision,payload) VALUES (?,?,?,?,?,?,?) IF NOT EXISTS;")
            .SetParameters(new RiskValues([row.PortfolioId,row.FundId,row.ValueDate,row.EvaluatedAtUtc,row.InvocationId,row.Revision,summary]))
            .ExecuteSingleAsync(r=>r.GetBool(0),projectionToken);
        if (!added)
            await _dbFactory.TradeDb.Use("RiskHistory.SummaryAdvance", "UPDATE risk_management_history SET revision=?,payload=? WHERE portfolio_id=? AND fund_id=? AND value_date=? AND evaluated_at_utc=? AND invocation_id=? IF revision<?;")
                .SetParameters(new RiskValues([row.Revision,summary,row.PortfolioId,row.FundId,row.ValueDate,row.EvaluatedAtUtc,row.InvocationId,row.Revision]))
                .ExecuteSingleAsync(r=>r.GetBool(0),projectionToken);
        return new(disposition,row.WorkflowId,row.InvocationId,row.Revision,hash,hash);
    }
    /// <inheritdoc />
    public Task<WorkflowStrategyStateUpdatedEvent?> GetRiskInvocationAsync(Guid workflow, Guid invocation, CancellationToken token = default)
    {
        workflow.RequireInvocation(invocation, "Exact Risk identities required.");
        return _dbFactory.TradeDb.Use("RiskHistory.Exact", "SELECT payload,content_hash FROM risk_management_invocation WHERE workflow_id=? AND invocation_id=? ORDER BY revision DESC LIMIT 1;")
            .SetParameters(new RiskValues([workflow,invocation]))
            .ExecuteSingleAsync(r=>
            {
                var payload=r.GetBytes(0);
                var value = MessagePackBinarySerializer.Shared.Deserialize<WorkflowStrategyStateUpdatedEvent>(payload);
                if (RiskContracts.Hash(value) != r.GetString(1) || RiskHistoryIdentity.Row(value) is not { } row || row.WorkflowId != workflow || row.InvocationId != invocation)
                    throw new InvalidDataException("Risk history content hash mismatch.");
                return value;
            },token);
    }
    /// <inheritdoc />
    public Task<QueryPage<RiskHistoryRow>> GetRiskHistoryAsync(int portfolio, int fund, DateOnly date, int size, byte[]? cursor, CancellationToken token = default)
    {
        portfolio.RequireHistoryScope(fund, date, size, 100, "Invalid Risk history scope.");
        return _dbFactory.TradeDb.Use("RiskHistory.Page", "SELECT payload FROM risk_management_history WHERE portfolio_id=? AND fund_id=? AND value_date=?;")
            .SetParameters(new RiskValues([portfolio,fund,date]))
            .ExecutePageAsync(r=>MessagePackBinarySerializer.Shared.Deserialize<RiskHistoryRow>(r.GetBytes(0)),size,cursor,token);
    }
    /// <inheritdoc />
    public Task UpsertTradeOrderAsync(TradeOrderDefinition order, CancellationToken token = default)
    {
        order.RequireNotNull(nameof(order));
        return _dbFactory.TradeDb.Use("TradeFlow.Order.Upsert", "INSERT INTO trade_order_v3 (portfolioId,fundId,orderId,revision,status,valueDate,updatedAtUtc,definitionHash,payload) VALUES (?,?,?,?,?,?,?,?,?);")
            .SetParameters(new TradeDbValues([order.Id.PortfolioId, order.Id.FundId, order.Id.OrderId, order.Revision,
                order.Status.ToString(), order.ValueDate, DateTime.UtcNow, order.DefinitionHash,
                MessagePackBinarySerializer.Shared.Serialize(order)]))
            .ExecuteCommandAsync(token);
    }

    /// <inheritdoc />
    public Task<TradeOrderDefinition?> GetTradeOrderAsync(TradeOrderId id, CancellationToken token = default)
    {
        id.Require();
        return _dbFactory.TradeDb.Use("TradeFlow.Order.Get", "SELECT payload FROM trade_order_v3 WHERE portfolioId=? AND fundId=? AND orderId=? LIMIT 1;")
            .SetParameters(new TradeDbValues([id.PortfolioId, id.FundId, id.OrderId]))
            .ExecuteSingleAsync(row => MessagePackBinarySerializer.Shared.Deserialize<TradeOrderDefinition>(row.GetBytes(0)), token);
    }

    /// <inheritdoc />
    public async Task UpsertOrderExecutionAsync(OrderExecutionDefinition execution, CancellationToken token = default)
    {
        execution.RequireNotNull(nameof(execution));
        execution.TradeOrderId.Require();
        var payload = MessagePackBinarySerializer.Shared.Serialize(execution);
        await _dbFactory.TradeDb.Use("TradeFlow.Execution.Upsert", "INSERT INTO order_execution_v1 (portfolioId,fundId,orderId,executionAttemptId,status,startedAtUtc,completedAtUtc,payload) VALUES (?,?,?,?,?,?,?,?);")
            .SetParameters(new TradeDbValues([execution.TradeOrderId.PortfolioId, execution.TradeOrderId.FundId, execution.TradeOrderId.OrderId,
                execution.ExecutionAttemptId, execution.Status.ToString(), execution.StartedAtUtc, execution.CompletedAtUtc, payload]))
            .ExecuteCommandAsync(token)
            .ConfigureAwait(false);
        foreach (var fill in execution.Fills)
            await _dbFactory.TradeDb.Use("TradeFlow.Fill.Upsert.V2", "INSERT INTO order_execution_fill_v2 (executionAttemptId,executionFillId,componentId,tradeLegId,contractId,filledAtUtc,payload) VALUES (?,?,?,?,?,?,?);")
                .SetParameters(new TradeDbValues([fill.ExecutionAttemptId, fill.ExecutionFillId, fill.ComponentId, fill.TradeLegId,
                    fill.ContractId, fill.FilledAtUtc, MessagePackBinarySerializer.Shared.Serialize(fill)]))
                .ExecuteCommandAsync(token)
                .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public Task<OrderExecutionDefinition?> GetOrderExecutionAsync(TradeOrderId id, Guid executionAttemptId, CancellationToken token = default)
    {
        id.Require();
        executionAttemptId.Require("ExecutionAttemptId is required.", nameof(executionAttemptId));
        return _dbFactory.TradeDb.Use("TradeFlow.Execution.Get", "SELECT payload FROM order_execution_v1 WHERE portfolioId=? AND fundId=? AND orderId=? AND executionAttemptId=?;")
            .SetParameters(new TradeDbValues([id.PortfolioId, id.FundId, id.OrderId, executionAttemptId]))
            .ExecuteSingleAsync(row => MessagePackBinarySerializer.Shared.Deserialize<OrderExecutionDefinition>(row.GetBytes(0)), token);
    }

    /// <inheritdoc />
    public async Task UpsertEstablishedTradeAsync(EstablishedTradeDefinition trade, CancellationToken token = default)
    {
        trade.RequireNotNull(nameof(trade));
        trade.Id.Require();
        var payload = MessagePackBinarySerializer.Shared.Serialize(trade);
        await _dbFactory.TradeDb.Use("TradeFlow.Trade.Upsert", "INSERT INTO established_trade_v1 (portfolioId,fundId,orderId,tradeId,assetFamily,strategyKind,establishedAtUtc,evidenceRevision,payload) VALUES (?,?,?,?,?,?,?,?,?);")
            .SetParameters(new TradeDbValues([trade.Id.PortfolioId, trade.Id.FundId, trade.Id.OrderId, trade.Id.TradeId,
                trade.AssetFamily.ToString(), trade.StrategyKind.ToString(), trade.EstablishedAtUtc, trade.EvidenceRevision,
                payload]))
                    .ExecuteCommandAsync(token)
                    .ConfigureAwait(false);
        await _dbFactory.TradeDb.Use("TradeFlow.Trade.History.Upsert", "INSERT INTO established_trade_history_v2 (portfolioId,fundId,strategyKind,establishedAtUtc,orderId,tradeId,evidenceRevision,payload) VALUES (?,?,?,?,?,?,?,?);")
            .SetParameters(new TradeDbValues([trade.Id.PortfolioId, trade.Id.FundId, trade.StrategyKind.ToString(),
                trade.EstablishedAtUtc, trade.Id.OrderId, trade.Id.TradeId, trade.EvidenceRevision, payload]))
            .ExecuteCommandAsync(token)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public Task<EstablishedTradeDefinition?> GetEstablishedTradeAsync(TradeEntityId id, CancellationToken token = default)
    {
        id.Require();
        return _dbFactory.TradeDb.Use("TradeFlow.Trade.Get", "SELECT payload FROM established_trade_v1 WHERE portfolioId=? AND fundId=? AND orderId=? AND tradeId=?;")
            .SetParameters(new TradeDbValues([id.PortfolioId, id.FundId, id.OrderId, id.TradeId]))
            .ExecuteSingleAsync(row => MessagePackBinarySerializer.Shared.Deserialize<EstablishedTradeDefinition>(row.GetBytes(0)), token);
    }

    /// <inheritdoc />
    public Task<QueryPage<EstablishedTradeDefinition>> GetEstablishedTradesAsync(int portfolioId, int fundId,
        TradeStrategyKind strategyKind, DateTime fromUtc, DateTime toUtc, int pageSize,
        byte[]? pagingState = null, CancellationToken token = default)
    {
        portfolioId.RequireUtcHistoryScope(fundId, fromUtc, toUtc, pageSize);
        return _dbFactory.TradeDb.Use("TradeFlow.Trade.History.Get", "SELECT payload FROM established_trade_history_v2 WHERE portfolioId=? AND fundId=? AND strategyKind=? AND establishedAtUtc>=? AND establishedAtUtc<=?;")
            .SetParameters(new TradeDbValues([portfolioId, fundId, strategyKind.ToString(), fromUtc, toUtc]))
            .ExecutePageAsync(row => MessagePackBinarySerializer.Shared.Deserialize<EstablishedTradeDefinition>(row.GetBytes(0)), pageSize, pagingState, token);
    }

    /// <inheritdoc />
    public async Task UpsertStrategyPositionAsync(StrategyPositionSnapshot position, CancellationToken token = default)
    {
        position.RequireNotNull(nameof(position));
        position.RequireValidIdentity();
        var payload = MessagePackBinarySerializer.Shared.Serialize(position);
        var id = position.Id.Trade;
        await _dbFactory.TradeDb.Use("TradeFlow.Position.Current", "INSERT INTO strategy_position_current_v1 (portfolioId,fundId,orderId,tradeId,positionId,strategyKind,positionSequence,routeGeneration,isOpen,asOfUtc,payload) VALUES (?,?,?,?,?,?,?,?,?,?,?);")
            .SetParameters(new TradeDbValues([id.PortfolioId, id.FundId, id.OrderId, id.TradeId, position.Id.PositionId,
                position.StrategyKind.ToString(), position.PositionSequence, position.RouteGeneration, position.IsOpen, position.AsOfUtc, payload]))
            .ExecuteCommandAsync(token)
            .ConfigureAwait(false);
        await _dbFactory.TradeDb.Use("TradeFlow.Position.History", "INSERT INTO strategy_position_history_v1 (positionId,asOfUtc,positionSequence,phase,payload) VALUES (?,?,?,?,?);")
            .SetParameters(new TradeDbValues([position.Id.PositionId, position.AsOfUtc, position.PositionSequence, position.Phase.ToString(), payload]))
            .ExecuteCommandAsync(token)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public Task<QueryPage<StrategyPositionSnapshot>> GetStrategyPositionHistoryAsync(Guid positionId, DateTime fromUtc, DateTime toUtc, int pageSize, byte[]? pagingState = null, CancellationToken token = default)
    {
        positionId.RequireUtcHistoryScope(fromUtc, toUtc, pageSize);
        return _dbFactory.TradeDb.Use("TradeFlow.Position.History.Get", "SELECT payload FROM strategy_position_history_v1 WHERE positionId=? AND asOfUtc>=? AND asOfUtc<=?;")
            .SetParameters(new TradeDbValues([positionId, fromUtc, toUtc]))
            .ExecutePageAsync(row => MessagePackBinarySerializer.Shared.Deserialize<StrategyPositionSnapshot>(row.GetBytes(0)), pageSize, pagingState, token);
    }

    /// <inheritdoc />
    public Task<StrategyPositionSnapshot?> GetStrategyPositionAsync(StrategyPositionId id, CancellationToken token = default)
    {
        id.Require();
        return _dbFactory.TradeDb.Use("TradeFlow.Position.Current.Get", "SELECT payload FROM strategy_position_current_v1 WHERE portfolioId=? AND fundId=? AND orderId=? AND tradeId=? AND positionId=?;")
            .SetParameters(new TradeDbValues([id.Trade.PortfolioId, id.Trade.FundId, id.Trade.OrderId, id.Trade.TradeId, id.PositionId]))
            .ExecuteSingleAsync(row => MessagePackBinarySerializer.Shared.Deserialize<StrategyPositionSnapshot>(row.GetBytes(0)), token);
    }

    /// <inheritdoc />
    public async Task ReplaceOpenPositionRoutesAsync(StrategyPositionSnapshot position, CancellationToken token = default)
    {
        position.RequireNotNull(nameof(position));
        position.RequireRoutableLegs();
        var existing = await _dbFactory.TradeDb.Use("TradeFlow.Route.Recovery.Position.Get.V2",
                "SELECT contractId,tradeLegId FROM open_position_route_recovery_v2 WHERE shard=? AND positionId=?;")
            .SetParameters(new TradeDbValues([(sbyte)0, position.Id.PositionId]))
            .ExecuteQueryAsync(row => new OpenPositionRouteKeyReadModel(row.GetString(0), row.GetGuid(1)), token)
            .ConfigureAwait(false);
        foreach (var old in existing)
        {
            await _dbFactory.TradeDb.Use("TradeFlow.Route.Delete.V2",
                    "DELETE FROM open_position_route_v2 WHERE contractId=? AND positionId=? AND tradeLegId=?;")
                .SetParameters(new TradeDbValues([old.ContractId, position.Id.PositionId, old.TradeLegId]))
                .ExecuteCommandAsync(token)
                .ConfigureAwait(false);
            await _dbFactory.TradeDb.Use("TradeFlow.Route.Recovery.Delete.V2",
                    "DELETE FROM open_position_route_recovery_v2 WHERE shard=? AND positionId=? AND tradeLegId=?;")
                .SetParameters(new TradeDbValues([(sbyte)0, position.Id.PositionId, old.TradeLegId]))
                .ExecuteCommandAsync(token)
                .ConfigureAwait(false);
        }
        if (!position.IsOpen) return;

        foreach (var leg in position.Legs)
        {
            var route = new PortfolioFundTradeLeg(position.Id.Trade.PortfolioId, position.Id.Trade.FundId,
                position.Id.Trade.OrderId, position.Id.Trade.TradeId, position.Id.PositionId, leg.TradeLegId,
                position.StrategyKind, position.RouteGeneration);
            await _dbFactory.TradeDb.Use("TradeFlow.Route.Upsert.V2", "INSERT INTO open_position_route_v2 (contractId,portfolioId,fundId,orderId,tradeId,positionId,tradeLegId,tradeType,generation) VALUES (?,?,?,?,?,?,?,?,?);")
                .SetParameters(new TradeDbValues([leg.ContractId, route.PortfolioId, route.FundId, route.OrderId,
                    route.TradeId, route.StrategyPositionId, route.TradeLegId, route.TradeType.ToString(), route.Generation]))
                .ExecuteCommandAsync(token)
                .ConfigureAwait(false);
            await _dbFactory.TradeDb.Use("TradeFlow.Route.Recovery.Upsert.V2", "INSERT INTO open_position_route_recovery_v2 (shard,positionId,tradeLegId,contractId,portfolioId,fundId,orderId,tradeId,tradeType,generation) VALUES (?,?,?,?,?,?,?,?,?,?);")
                .SetParameters(new TradeDbValues([(sbyte)0, route.StrategyPositionId, route.TradeLegId, leg.ContractId,
                    route.PortfolioId, route.FundId, route.OrderId, route.TradeId, route.TradeType.ToString(), route.Generation]))
                .ExecuteCommandAsync(token)
                .ConfigureAwait(false);
        }
    }

    /// <inheritdoc />
    public async Task<ICollection<OpenPositionRouteReadModel>> GetOpenPositionRoutesAsync(string contractId, CancellationToken token = default)
    {
        contractId.RequireContractId();
        var result = await _dbFactory.TradeDb.Use("TradeFlow.Route.Get.V2", "SELECT portfolioId,fundId,orderId,tradeId,positionId,tradeLegId,tradeType,generation FROM open_position_route_v2 WHERE contractId=?;")
            .SetParameters(new TradeDbValues([contractId]))
            .ExecuteQueryAsync(row => new OpenPositionRouteReadModel(contractId, new PortfolioFundTradeLeg(
                row.GetInt(0), row.GetInt(1), row.GetInt(2), row.GetInt(3), row.GetGuid(4), row.GetGuid(5),
                row.GetEnum<TradeStrategyKind>(6), row.GetLong(7))), token)
            .ConfigureAwait(false);
        return result;
    }

    /// <inheritdoc />
    public async Task<ICollection<OpenPositionRouteReadModel>> GetOpenPositionRouteSnapshotAsync(CancellationToken token = default)
    {
        var result = await _dbFactory.TradeDb.Use("TradeFlow.Route.Recovery.Get.V2", "SELECT contractId,portfolioId,fundId,orderId,tradeId,positionId,tradeLegId,tradeType,generation FROM open_position_route_recovery_v2 WHERE shard=?;")
            .SetParameters(new TradeDbValues([(sbyte)0]))
            .ExecuteQueryAsync(row => new OpenPositionRouteReadModel(row.GetString(0), new PortfolioFundTradeLeg(
                row.GetInt(1), row.GetInt(2), row.GetInt(3), row.GetInt(4), row.GetGuid(5), row.GetGuid(6),
                row.GetEnum<TradeStrategyKind>(7), row.GetLong(8))), token)
            .ConfigureAwait(false);
        return result;
    }

    /// <inheritdoc />
    public async Task UpsertTradeSelectionAsync(TradeSelectionFunctionCompletedEvent completed, CancellationToken cancellationToken = default)
    {
        var r = TradeSelectionContracts.ReadResult(completed.Result);
        TradeSelectionContracts.Require(completed.WorkflowId == r.WorkflowId && completed.CommandId == r.InvocationId && completed.Id == r.ResultId && completed.EntityId == r.EntityId
            && completed.InputWorkflowRevision == r.InputWorkflowRevision && completed.RequestFingerprint.Length == 64, "TS.RESULT.INVALID", "Projection event identity mismatch.");
        var selected = r.SelectedCandidate;
        var binding = r.DecisionContext.SelectionBinding;
        var bytes = MessagePackBinarySerializer.Shared.Serialize(completed with { EventId = 0 });
        object[] values = [r.WorkflowId.Value,r.InvocationId,1L,completed.Id,r.PortfolioId,r.FundId,(short)r.DecisionHorizon,(sbyte)2,(sbyte)r.Outcome,r.ProducedAtUtc,r.PrimaryReasonCode,
            selected?.DeploymentKey.Id!,selected?.DeploymentKey.Version!,selected?.StrategyKey.Id!,selected?.StrategyKey.Version!,selected?.StructureKey.Id!,selected?.StructureKey.Version!,selected?.VariantKey.Id!,selected?.VariantKey.Version!,
            TradeSelectionContracts.WireHash(binding.Candidates.Select(x=>x.CandidateHash).ToArray()),binding.PayloadSha256,binding.CommonPolicy.Id,binding.CommonPolicy.Version,binding.CommonPolicy.PayloadSha256,r.ResultId,completed.Result.PayloadSha256,MessagePackBinarySerializer.Shared.Serialize(r)!,bytes];
        var inserted = await _dbFactory.TradeDb.Use("TradeSelection.Insert", "INSERT INTO trade_selection_invocation_event (workflow_id,invocation_id,source_sequence,event_id,portfolio_id,fund_id,target_horizon,lifecycle_status,outcome,occurred_at_utc,reason_code,selected_deployment_id,selected_deployment_version,selected_strategy_id,selected_strategy_version,selected_structure_id,selected_structure_version,selected_variant_id,selected_variant_version,candidate_set_sha256,binding_sha256,parameter_set_id,parameter_version,parameter_sha256,result_id,result_sha256,result_payload,event_payload) VALUES (?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?) IF NOT EXISTS;")
            .SetParameters(new SelectionValues(values))
            .ExecuteSingleAsync(row => row.GetBool(0), cancellationToken)
            .ConfigureAwait(false);
        if (!inserted)
        {
            var existing = await GetTradeSelectionInvocationAsync(r.WorkflowId, r.InvocationId, cancellationToken)
                .ConfigureAwait(false);
            if (existing is null || !TradeSelectionContracts.SameCompletion(existing, completed))
                throw new InvalidOperationException("Conflicting selector projection for the same invocation.");
        }
        await _dbFactory.TradeDb.Use("TradeSelection.HistoryInsert", "INSERT INTO trade_selection_history_by_fund_date (portfolio_id,fund_id,value_date,occurred_at_utc,workflow_id,invocation_id,event_id,target_horizon,outcome,reason_code,result_id,result_sha256) VALUES (?,?,?,?,?,?,?,?,?,?,?,?);")
            .SetParameters(new SelectionValues([r.PortfolioId, r.FundId, DateOnly.FromDateTime(r.ProducedAtUtc), r.ProducedAtUtc, r.WorkflowId.Value, r.InvocationId, completed.Id, (short)r.DecisionHorizon, (sbyte)r.Outcome, r.PrimaryReasonCode, r.ResultId, completed.Result.PayloadSha256]))
            .ExecuteCommandAsync(cancellationToken)
            .ConfigureAwait(false);
    }
    /// <inheritdoc />
    public Task<TradeSelectionFunctionCompletedEvent?> GetTradeSelectionInvocationAsync(StrategyWorkflowId workflowId, Guid invocationId, CancellationToken cancellationToken = default)
    {
        workflowId.RequireInvocation(invocationId, "Exact workflow and invocation identities are required.");
        return _dbFactory.TradeDb.Use("TradeSelection.Exact", "SELECT event_payload,result_sha256 FROM trade_selection_invocation_event WHERE workflow_id=? AND invocation_id=? AND source_sequence=1;")
            .SetParameters(new SelectionValues([workflowId.Value, invocationId]))
            .ExecuteSingleAsync(row =>
            {
                var completed = MessagePackBinarySerializer.Shared.Deserialize<TradeSelectionFunctionCompletedEvent>(row.GetBytes(0));
                var r = TradeSelectionContracts.ReadResult(completed.Result);
                if (r.WorkflowId != workflowId || r.InvocationId != invocationId || completed.Result.PayloadSha256 != row.GetString(1)) throw new InvalidOperationException("Stored selector evidence identity/hash mismatch.");
                return completed;
            }, cancellationToken);
    }
    /// <inheritdoc />
    public Task<QueryPage<TradeSelectionHistoryRow>> GetTradeSelectionHistoryAsync(int portfolioId, int fundId, DateOnly valueDate, int pageSize, byte[]? pagingState = null, CancellationToken cancellationToken = default)
    {
        portfolioId.RequireHistoryScope(fundId, valueDate, pageSize, 200, "Invalid history scope or page size.");
        return _dbFactory.TradeDb.Use("TradeSelection.HistoryPage", "SELECT occurred_at_utc,workflow_id,invocation_id,event_id,target_horizon,outcome,reason_code,result_id,result_sha256 FROM trade_selection_history_by_fund_date WHERE portfolio_id=? AND fund_id=? AND value_date=?;")
            .SetParameters(new SelectionValues([portfolioId, fundId, valueDate]))
            .ExecutePageAsync(row => new TradeSelectionHistoryRow(portfolioId, fundId, valueDate, row.GetDateTime(0), row.GetGuid(1), row.GetGuid(2), row.GetGuid(3), row.GetShort(4), (byte)row.GetEnum<SelectionOutcome>(5), row.GetString(6), row.GetGuid(7), row.GetString(8)), pageSize, pagingState, cancellationToken);
    }
}
