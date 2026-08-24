using TomasAI.IFM.Domain.MarketData.Shared;
using TomasAI.IFM.Domain.MarketData.Shared;
using TomasAI.IFM.Domain.Trade.Shared;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TomasAI.IFM.Application.Blackboard;
using TomasAI.IFM.Application.Storage.SequenceIdDb;
using TomasAI.IFM.Application.Storage.TradeDb;
using TomasAI.IFM.Framework.Caching;
using TomasAI.IFM.Framework.SequenceId;
using TomasAI.IFM.Framework.SequenceId.Postgres;
using TomasAI.IFM.Framework.Serialization;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Framework.Storage.Extensions;
using TomasAI.IFM.Domain.MarketData.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Shared.Storage;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.ViewModels;
using TomasAI.IFM.Domain.Trade.Shared.ViewModels;
using TomasAI.IFM.Application.Storage.IntegrationTests.TradeDb;

namespace TomasAI.IFM.Application.Storage.LoadTests.SqlServer;

public class TradeDbLoadTests : IClassFixture<TradeDbFixture>
{
    public TradeDbLoadTests(TradeDbFixture testFixture)
    {
        TestFixture = testFixture;
    }

    TradeDbFixture TestFixture { get; }

    [Fact]
    [Trait("read option legs from CSV file and insert into database", "FundDb")]
    public async Task GetOptionLegFromCsvFileOk()
    {
        var rowCount = 0l;
        var db = TestFixture.TradeDb;
        var dbTrade = db as ITradeDbContext;
        await db.UseTest(new Uri("C:\\TomasAI\\data\\SqlServer\\option_leg.csv"))
           .ReadAsync(MapToOptionLeg, async reducer =>
           {
               await db.UseTest($"truncate option_leg").ExecuteCommandAsync();
               rowCount = await dbTrade.InsertOptionLegsAsync(reducer);
           });

        var resultSet = await dbTrade.GetOptionLegsAsync();
        resultSet.Should().NotBeNull();
        rowCount.Should().Be(resultSet.Count);
        return;

        static OptionTradeLegReadModel MapToOptionLeg(string e, int o)
            => new(
                orderId: e.GetInt(ref o),
                tradeId: e.GetInt(ref o),
                contractId: e.GetString(ref o),
                quantity: e.GetInt(ref o),
                strikePrice: e.GetDecimal(ref o),
                optionLegType: e.GetEnum<OptionType>(ref o),
                optionLegAction: e.GetEnum<OptionLegAction>(ref o),
                createdOn: e.GetDateTime(ref o),
                createdBy: e.GetString(ref o),
                updatedOn: e.GetDateTime(ref o),
                updatedBy: e.GetString(ref o)
            );
    }

    [Fact]
    [Trait("read option leg data from CSV file and insert into database", "FundDb")]
    public async Task GetOptionLegDataFromCsvFileOk()
    {
        var rowCount = 0L;
        var db = TestFixture.TradeDb;
        var dbTrade = db as ITradeDbContext;
        await db.UseTest(new Uri("C:\\TomasAI\\data\\SqlServer\\option_leg_data.csv"))
           .ReadAsync(MapToOptionLegData, async reducer =>
           {
               await db.UseTest($"truncate option_leg_data").ExecuteCommandAsync();
               rowCount = await dbTrade.InsertOptionLegDataAsync(reducer);
           });

        var resultSet = await dbTrade.GetOptionLegDataAsync();
        resultSet.Should().NotBeNull();
        rowCount.Should().Be(resultSet.Count);
        return;

        static OptionTradeLegDataReadModel MapToOptionLegData(string e, int o)
            => new(
                orderId: e.GetInt(ref o),
                tradeId: e.GetInt(ref o),
                valueDate: e.GetDateOnly(ref o),
                optionLegId: e.GetString(ref o),
                tradeType: e.GetEnum<TradeType>(ref o),
                daysToExpiry: e.GetInt(ref o),
                tradeStatus: e.GetEnum<TradeStatus>(ref o),
                bidPrice: e.GetDecimal(ref o),
                askPrice: e.GetDecimal(ref o),
                impliedVolatility: e.GetDouble(ref o),
                delta: e.GetDouble(ref o),
                gamma: e.GetDouble(ref o),
                theta: e.GetDouble(ref o),
                vega: e.GetDouble(ref o),
                rho: e.GetDouble(ref o),
                createdOn: e.GetDateTime(ref o),
                createdBy: e.GetString(ref o),
                updatedOn: e.GetDateTime(ref o),
                updatedBy: e.GetString(ref o)
            );
    }

    [Fact]
    [Trait("read option trades from CSV file and insert into database", "FundDb")]
    public async Task GetOptionTradesFromCsvFileOk()
    {
        var rowCount = 0L;
        var db = TestFixture.TradeDb;
        var dbTrade = db as ITradeDbContext;
        await db.UseTest(new Uri("C:\\TomasAI\\data\\SqlServer\\option_trade.csv"))
           .ReadAsync(MapToOptionTrade, async reducer =>
           {
               await db.UseTest($"truncate option_trade").ExecuteCommandAsync();
               rowCount = await dbTrade.InsertOptionTradesAsync(reducer);
           });

        var resultSet = await dbTrade.GetOptionTradesAsync();
        resultSet.Should().NotBeNull();
        rowCount.Should().Be(resultSet.Count);
        return;

        static OptionTradeReadModel MapToOptionTrade(string e, int o)
            => new(
                orderId: e.GetInt(ref o),
                tradeId: e.GetInt(ref o),
                tradeStrategy: e.GetString(ref o),
                tradeDate: e.GetDateOnly(ref o),
                maturityDate: e.GetDateOnly(ref o),
                tradeType: e.GetEnum<TradeType>(ref o),
                tradeState: e.GetEnum<TradeState>(ref o),
                tradeAction: e.GetEnum<TradeAction>(ref o),
                underlyingContractId: e.GetString(ref o),
                underlyingAssetType: e.GetEnum<AssetType>(ref o),
                isPrimaryTrade: e.GetBool(ref o),
                isHedgeTrade: e.GetBool(ref o),
                createdOn: e.GetDateTime(ref o),
                createdBy: e.GetString(ref o),
                updatedOn: e.GetDateTime(ref o),
                updatedBy: e.GetString(ref o)
            );
    }

    [Fact]
    [Trait("read option trade spread bar data from CSV file and insert into database", "FundDb")]
    public async Task GetOptionTradeSpreadBarDataFromCsvFileOk()
    {
        var rowCount = 0L;
        var db = TestFixture.TradeDb;
        var dbTrade = db as ITradeDbContext;
        await db.UseTest(new Uri("C:\\TomasAI\\data\\SqlServer\\option_trade_spread_bar_data.csv"))
           .ReadAsync(MapToOptionTradeSpreadBarData, async reducer =>
           {
               await db.UseTest($"truncate option_trade_spread_bar_data").ExecuteCommandAsync();
               rowCount = await dbTrade.InsertOptionTradeSpreadBarDataAsync(reducer);
           });

        var resultSet = await dbTrade.GetOptionTradeSpreadBarDataAsync();
        resultSet.Should().NotBeNull();
        rowCount.Should().Be(resultSet.Count);
        return;

        static OptionTradeSpreadBarsDataModel MapToOptionTradeSpreadBarData(string e, int o)
            => new(
                orderId: e.GetInt(ref o),
                tradeId: e.GetInt(ref o),
                valueDate: e.GetDateOnly(ref o),
                tradeType: e.GetEnum<TradeType>(ref o),
                barDate: e.GetDateTime(ref o),
                lossLimit: e.GetDecimal(ref o),
                winLimit: e.GetDecimal(ref o),
                forwardSpread: e.GetDecimal(ref o),
                netSpread: e.GetDecimal(ref o)
            );
    }

    [Fact]
    [Trait("read option trade spread data from CSV file and insert into database", "FundDb")]
    public async Task GetOptionTradeSpreadDataFromCsvFileOk()
    {
        var rowCount = 0L;
        var db = TestFixture.TradeDb;
        var dbTrade = db as ITradeDbContext;
        await db.UseTest(new Uri("C:\\TomasAI\\data\\SqlServer\\option_trade_spread_data.csv"))
           .ReadAsync(MapToOptionTradeSpreadData, async reducer =>
           {
               await db.UseTest($"truncate option_trade_spread_data").ExecuteCommandAsync();
               rowCount = await dbTrade.InsertOptionTradeSpreadDataAsync(reducer);
           });

        var resultSet = await dbTrade.GetOptionTradeSpreadDataAsync();
        resultSet.Should().NotBeNull();
        rowCount.Should().Be(resultSet.Count);
        return;

        static OptionTradeSpreadsDataModel MapToOptionTradeSpreadData(string e, int o)
            => new(
                orderId: e.GetInt(ref o),
                tradeId: e.GetInt(ref o),
                valueDate: e.GetDateOnly(ref o),
                tradeType: e.GetEnum<TradeType>(ref o),
                sequenceId: e.GetLong(ref o),
                lossLimit: e.GetDecimal(ref o),
                winLimit: e.GetDecimal(ref o),
                forwardSpread: e.GetDecimal(ref o),
                netSpread: e.GetDecimal(ref o),
                createdOn: e.GetDateTime(ref o),
                createdBy: e.GetString(ref o)
            );
    }

    [Fact]
    [Trait("read trade fill from CSV file and insert into database", "FundDb")]
    public async Task GetTradeFillsFromCsvFileOk()
    {
        var rowCount = 0L;
        var db = TestFixture.TradeDb;
        var dbTrade = db as ITradeDbContext;
        await db.UseTest(new Uri("C:\\TomasAI\\data\\SqlServer\\trade_fill.csv"))
           .ReadAsync(MapToTradeFill, async reducer =>
           {
               await db.UseTest($"truncate trade_fill").ExecuteCommandAsync();
               rowCount = await dbTrade.InsertTradeFillsAsync(reducer);
           });

        var resultSet = await dbTrade.GetTradeFillsAsync();
        resultSet.Should().NotBeNull();
        rowCount.Should().Be(resultSet.Count);
        return;

        static TradeFillReadModel MapToTradeFill(string e, int o)
             => new(
                 orderId: e.GetInt(ref o),
                 tradeId: e.GetInt(ref o),
                 fillDate: e.GetDateTime(ref o),
                 fillQuantity: e.GetInt(ref o),
                 createdOn: e.GetDateTime(ref o),
                 createdBy: e.GetString(ref o)
             );
    }

    [Fact]
    [Trait("read trade limit from CSV file and insert into database", "FundDb")]
    public async Task GetTradeLimitsFromCsvFileOk()
    {
        var rowCount = 0L;
        var db = TestFixture.TradeDb;
        var dbTrade = db as ITradeDbContext;
        await db.UseTest(new Uri("C:\\TomasAI\\data\\SqlServer\\trade_limit.csv"))
           .ReadAsync(MapToTradeLimit, async reducer =>
           {
               await db.UseTest($"truncate trade_limit").ExecuteCommandAsync();
               rowCount = await dbTrade.InsertTradeLimitsAsync(reducer);
           });

        var resultSet = await dbTrade.GetTradeLimitsAsync();
        resultSet.Should().NotBeNull();
        rowCount.Should().Be(resultSet.Count);
        return;

        static TradeLimitReadModel MapToTradeLimit(string e, int o)
             => new(
                 tradeId: e.GetInt(ref o),
                 tradeType: e.GetEnum<TradeType>(ref o),
                 riskMargin: e.GetDecimal(ref o),
                 maxProfit: e.GetDecimal(ref o),
                 maxLoss: e.GetDecimal(ref o),
                 maxReturn: e.GetDecimal(ref o),
                 maxLossLimit: e.GetDecimal(ref o),
                 minProfitLimit: e.GetDecimal(ref o),
                 maxProfitLimit: e.GetDecimal(ref o),
                 minProfitTarget: e.GetDecimal(ref o),
                 dailyProfitTarget: e.GetDecimal(ref o),
                 createdOn: e.GetDateTime(ref o),
                 createdBy: e.GetString(ref o),
                 updatedOn: e.GetDateTime(ref o),
                 updatedBy: e.GetString(ref o)
             );

    }

    [Fact]
    [Trait("read trade type limit from CSV file and insert into database", "FundDb")]
    public async Task GetTradeTypeLimitsFromCsvFileOk()
    {
        var rowCount = 0L;
        var db = TestFixture.TradeDb;
        var dbTrade = db as ITradeDbContext;
        await db.UseTest(new Uri("C:\\TomasAI\\data\\SqlServer\\trade_type_limit.csv"))
           .ReadAsync(MapToTradeTypeLimit, async reducer =>
           {
               await db.UseTest($"truncate trade_type_limit").ExecuteCommandAsync();
               rowCount = await dbTrade.InsertTradeTypeLimitsAsync(reducer);
           });

        var resultSet = await dbTrade.GetTradeTypeLimitsAsync();
        resultSet.Should().NotBeNull();
        rowCount.Should().Be(resultSet.Count);
        return;

        static TradeTypeLimitReadModel MapToTradeTypeLimit(string e, int o)
            => new(
                tradeId: e.GetInt(ref o),
                tradeType: e.GetEnum<TradeType>(ref o),
                maxLossLimit: e.GetDecimal(ref o),
                minProfitLimit: e.GetDecimal(ref o),
                maxProfitLimit: e.GetDecimal(ref o)
            );
    }

    [Fact]
    [Trait("read trade position from CSV file and insert into database", "FundDb")]
    public async Task GetTradePositionsFromCsvFileOk()
    {

        var rowCount = 0L;
        var db = TestFixture.TradeDb;
        var dbTrade = db as ITradeDbContext;
        await db.UseTest(new Uri("C:\\TomasAI\\data\\SqlServer\\trade_position.csv"))
           .ReadAsync(MapToTradePosition, async reducer =>
           {
               await db.UseTest($"truncate trade_position").ExecuteCommandAsync();
               rowCount = await dbTrade.InsertTradePositionsAsync(reducer);
           });

        var resultSet = await dbTrade.GetTradePositionsAsync();
        resultSet.Should().NotBeNull();
        rowCount.Should().Be(resultSet.Count);
        return;

        static TradePositionReadModel MapToTradePosition(string e, int o)
            => new(
                orderId: e.GetInt(ref o),
                tradeId: e.GetInt(ref o),
                valueDate: e.GetDateOnly(ref o),
                tradeType: e.GetEnum<TradeType>(ref o),
                tradeStatus: e.GetEnum<TradeStatus>(ref o),
                daysToExpiry: e.GetInt(ref o),
                commission: e.GetDecimal(ref o),
                deltaHedge: e.GetInt(ref o),
                netSpread: e.GetDecimal(ref o),
                tradeValue: e.GetDecimal(ref o),
                tradePnl: e.GetDecimal(ref o),
                assetPrice: e.GetDecimal(ref o),
                otmProbability: e.GetDouble(ref o),
                forwardPrice: e.GetDecimal(ref o),
                forwardLossRatio: e.GetDouble(ref o),
                lossProbability: e.GetDouble(ref o),
                riskFreeRate: e.GetDouble(ref o),
                createdOn: e.GetDateTime(ref o),
                createdBy: e.GetString(ref o),
                updatedOn: e.GetDateTime(ref o),
                updatedBy: e.GetString(ref o)
            );

    }

    [Fact]
    [Trait("read trade plan from CSV file and insert into database", "FundDb")]
    public async Task GetTradePlansFromCsvFileOk()
    {
        var rowCount = 0L;
        var db = TestFixture.TradeDb;
        var dbTrade = db as ITradeDbContext;
        await db.UseTest(new Uri("C:\\TomasAI\\data\\SqlServer\\trade_plan.csv"))
           .ReadAsync(MapToTradePlan, async reducer =>
           {
               await db.UseTest($"truncate trade_plan").ExecuteCommandAsync();
               rowCount = await dbTrade.InsertTradePlansAsync(reducer);
           });

        var resultSet = await dbTrade.GetTradePlansAsync();

        resultSet.Should().NotBeNull();
        rowCount.Should().Be(resultSet.Count);
        return;

        static TradePlanByIdReadModel MapToTradePlan(string e, int o)
            => new(
                id: e.GetInt(ref o),
                orderId: e.GetInt(ref o),
                tradeId: e.GetInt(ref o),
                valueDate: e.GetDateOnly(ref o),
                actionDate: e.GetDateTime(ref o),
                tradeDate: e.GetDateOnly(ref o),
                maturityDate: e.GetDateOnly(ref o),
                tradeType: e.GetEnum<TradeType>(ref o),
                actionType: e.GetEnum<ActionType>(ref o),
                actionSubType: e.GetEnum<ActionSubType>(ref o),
                actionState: e.GetEnum<ActionState>(ref o),
                actionReason: e.GetString(ref o),
                tradePnl: e.GetDecimal(ref o),
                forwardLossRatio: e.GetDouble(ref o),
                lossProbability: e.GetDouble(ref o),
                mScore: e.GetDouble(ref o),
                maxProfit: e.GetDecimal(ref o),
                maxLoss: e.GetDecimal(ref o),
                minProfitTarget: e.GetDecimal(ref o),
                dailyProfitTarget: e.GetDecimal(ref o),
                assetPrice: e.GetDecimal(ref o),
                assetStdDev: e.GetDouble(ref o),
                assetMean: e.GetDouble(ref o),
                assetPriceChange: e.GetDouble(ref o),
                marketTrend: e.GetEnum<MarketDirectionType>(ref o),
                marketVolatility: e.GetEnum<MarketVolatilityType>(ref o),
                marketDirection: e.GetEnum<PriceDirectionType>(ref o),
                vixVolatility: e.GetEnum<PriceVolatilityType>(ref o),
                tradeRisk: e.GetEnum<TradeRiskType>(ref o),
                fiftyDayMA: e.GetDouble(ref o),
                fiveDayXMA: e.GetDouble(ref o),
                putOTMProbability: e.GetDouble(ref o),
                callOTMProbability: e.GetDouble(ref o),
                shortPutGamma: e.GetDouble(ref o),
                shortCallGamma: e.GetDouble(ref o),
                gammaRisk: e.GetEnum<GammaRiskType>(ref o),
                netPrice: e.GetDecimal(ref o),
                forwardPrice: e.GetDecimal(ref o),
                forwardDelta: e.GetDouble(ref o),
                stopLossLimit: e.GetDouble(ref o),
                trendType: e.GetEnum<FuturesTrendType>(ref o),
                trendStrength: e.GetEnum<FuturesTrendStrengthType>(ref o),
                rsi: e.GetDouble(ref o),
                rsiSlope: e.GetDouble(ref o),
                tdi: e.GetEnum<FuturesTrendDirectionType>(ref o),
                tdiStrength: e.GetEnum<FuturesTrendDirectionStrengthType>(ref o),
                createdOn: e.GetDateTime(ref o),
                createdBy: e.GetString(ref o)
            );
    }
}
