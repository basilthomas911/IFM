using FluentAssertions;
using FluentAssertions.Extensions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TomasAI.IFM.Application.Blackboard;
using TomasAI.IFM.Application.Storage.SequenceIdDb;
using TomasAI.IFM.Application.Storage.MarketDataDb;
using TomasAI.IFM.Application.Storage.SecuritiesDb;
using TomasAI.IFM.Application.Storage.TradeDb;
using TomasAI.IFM.Framework.Caching;
using TomasAI.IFM.Framework.SequenceId;
using TomasAI.IFM.Framework.SequenceId.Postgres;
using TomasAI.IFM.Framework.Serialization;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Shared.Exceptions;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Domain.MarketData.Shared;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.ViewModels;
using TomasAI.IFM.Domain.MarketData.Feed.Shared;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.ViewModels;
using TomasAI.IFM.Shared.Storage;
using TomasAI.IFM.Domain.Trade.Shared.ViewModels;
using Xunit;
using TomasAI.IFM.Domain.Application.Shared.Commands;
using TomasAI.IFM.Framework.Storage.Extensions;
using TomasAI.IFM.Application.Storage.IntegrationTests.MarketDataDb;

namespace TomasAI.IFM.Application.Storage.LoadTests.SqlServer;

public class MarketDataDbLoadTests(MarketDataFixture testFixture) : IClassFixture<MarketDataFixture>
{
    MarketDataFixture TestFixture { get; } = testFixture;

    [Fact]
    [Trait("read futures eod data from CSV file and insert into database", "FundDb")]
    public async Task GetFuturesEodDataFromCsvFileOk()
    {
        var rowCount = 0l;
        var db = TestFixture.DevDatabase;
        var dbMarketData = db as IMarketDataDbContext;
        await db.Use(new Uri("C:\\TomasAI\\data\\SqlServer\\futures_eod_data.csv"))
           .ReadAsync(MapToFuturesEodData, async reducer =>
           {
               await db.Use($"truncate futures_eod_data").ExecuteCommandAsync();
               rowCount = await dbMarketData.InsertFuturesEodDataAsync(reducer);
           });

        var resultSet = await dbMarketData.GetFuturesEodDataAsync();
        resultSet.Should().NotBeNull();
        rowCount.Should().Be(resultSet.Count);
        return;

        static FuturesEodDataV2ReadModel MapToFuturesEodData(string e, int o)
            => new(
                contractId: e.GetString(ref o),
                valueDate: e.GetDateOnly(ref o),
                symbol: e.GetString(ref o),
                openPrice: e.GetDecimal(ref o),
                highPrice: e.GetDecimal(ref o),
                lowPrice: e.GetDecimal(ref o),
                closePrice: e.GetDecimal(ref o),
                volume: e.GetInt(ref o),
                dailyPercentChange: e.GetDouble(ref o),
                dailyStdDev: e.GetDouble(ref o),
                dailyStdDevAmount: e.GetDouble(ref o),
                upperBand: e.GetDouble(ref o),
                mean: e.GetDouble(ref o),
                lowerBand: e.GetDouble(ref o),
                marketDirection: e.GetEnum<MarketDirectionType>(ref o),
                marketVolatility: e.GetEnum<MarketVolatilityType>(ref o),
                priceDirection: e.GetEnum<PriceDirectionType>(ref o),
                priceVolatility: e.GetEnum<PriceVolatilityType>(ref o),
                marketDirectionIndicator: e.GetDouble(ref o),
                windowSize: e.GetInt(ref o)
            );


    }

    [Fact]
    [Trait("read futures bar data from CSV file and insert into database", "FundDb")]
    public async Task GetFuturesBarDataFromCsvFileOk()
    {
        var rowCount = 0l;
        var db = TestFixture.DevDatabase;
        var dbMarketData = db as IMarketDataDbContext;
        await db.Use(new Uri("C:\\TomasAI\\data\\SqlServer\\futures_bar_data.csv"))
           .ReadAsync(MapToFuturesBarData, async reducer =>
           {
               await db.Use($"truncate futures_bar_data").ExecuteCommandAsync();
               rowCount = await dbMarketData.InsertFuturesBarDataAsync(reducer);
           });

        var resultSet = await dbMarketData.GetFuturesBarDataAsync();
        resultSet.Should().NotBeNull();
        rowCount.Should().Be(resultSet.Count);
        return;

        static FuturesBarDataReadModel MapToFuturesBarData(string e, int o)
            => new(
                contractId: e.GetString(ref o),
                symbol: e.GetString(ref o),
                valueDate: e.GetDateOnly(ref o),
                barDate: e.GetDateTime(ref o),
                barRateType: e.GetEnum<BarRateType>(ref o),
                barValue: e.GetDecimal(ref o),
                upTrendTrigger: e.GetDouble(ref o),
                downTrendTrigger: e.GetDouble(ref o)
            );

    }

    [Fact]
    [Trait("read futures trade signal from CSV file and insert into database", "FundDb")]
    public async Task GetFuturesTradeSignalsFromCsvFileOk()
    {
        var rowCount = 0l;
        var db = TestFixture.DevDatabase;
        await db.Use(new Uri("C:\\TomasAI\\data\\SqlServer\\futures_trade_signal.csv"))
           .ReadAsync(MapToFuturesTradeSignal,  async futureTradeSignals => {
               await db.Use($"truncate futures_trade_signal").ExecuteCommandAsync();
               rowCount = await db.InsertFuturesTradeSignalsAsync(futureTradeSignals);
           });
        var resultSet = await db.GetFuturesTradeSignalsAsync();
        Assert.NotNull(resultSet);
        Assert.Equal(rowCount, resultSet.Count);

        static FuturesTradeSignalV2ReadModel MapToFuturesTradeSignal(string e, int start)
            => new(
                contractId: e.GetString(ref start),
                valueDate: e.GetDateOnly(ref start),
                timePeriod: TimeFrameType.FifteenSeconds,
                sequenceId: e.GetLong(ref start),
                timestamp: e.GetTimeOnly(ref start),
                mean: e.GetDouble(ref start),
                stdDev: e.GetDouble(ref start),
                futuresPrice: e.GetDouble(ref start),
                priceChangePercent: e.GetDouble(ref start),
                fundRiskPercent: e.GetDouble(ref start),
                rsi: e.GetDouble(ref start),
                rsiSlope: e.GetDouble(ref start),
                trendType: e.GetEnum<FuturesTrendType>(ref start),
                trendStrength: e.GetEnum<FuturesTrendStrengthType>(ref start),
                tradeSignal: e.GetEnum<TradeSignalType>(ref start),
                tdi: e.GetEnum<FuturesTrendDirectionType>(ref start),
                tdiStrength: e.GetEnum<FuturesTrendDirectionStrengthType>(ref start),
                mdi: e.GetDouble(ref start),
                mdiTrend: e.GetEnum<FuturesMDITrendType>(ref start),
                mdiUpTrendLimit: e.GetDouble(ref start),
                mdiDownTrendLimit: e.GetDouble(ref start),
                upTrendingTrigger: e.GetDouble(ref start),
                downTrendingTrigger: e.GetDouble(ref start),
                entryTrigger: e.GetDouble(ref start),
                exitTrigger: e.GetDouble(ref start),
                trendDelta: e.GetDouble(ref start),
                trendExtreme: e.GetDouble(ref start),
                trendReversal: e.GetDouble(ref start),
                fiftyDMA: e.GetDecimal(ref start),
                twoHundredDMA: e.GetDecimal(ref start),
                tradeExecuteState: e.GetEnum<TradeExecuteState>(ref start));
        }
}
