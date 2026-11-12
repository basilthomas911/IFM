using System;
using System.Collections.Generic;
using System.Linq;
using TomasAI.IFM.Application.Storage.MarketDataDb;
using TomasAI.IFM.Application.Storage.MarketDataDb.Schema;
using TomasAI.IFM.Domain.MarketData.Shared;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Shared.Storage;
using Xunit;

namespace TomasAI.IFM.Application.Storage.IntegrationTests.MarketDataDb;

public sealed class MarketDataProjectionPolicyTests
{
    [Fact]
    public void MarketOutlookSnapshot_UsesOneUpsertRowPerDateAndLatestCutoffQuery()
    {
        Assert.Contains("market_data.market_outlook_snapshot", MarketDataSchemaDb.CreateStatement);
        Assert.Contains("ON CONFLICT (contractid, valuedate)", MarketDataDbSql.UpsertMarketOutlookSnapshot);
        Assert.Contains("valuedate <= $2", MarketDataDbSql.GetMarketOutlookSnapshot);
        Assert.Contains("ORDER BY valuedate DESC LIMIT 1", MarketDataDbSql.GetMarketOutlookSnapshot);
    }

    [Fact]
    public void FuturesEodSchema_PersistsMovingAveragesAndSupportsAdditiveUpgrade()
    {
        Assert.Contains("fiftyDMA numeric", MarketDataSchemaDb.CreateStatement);
        Assert.Contains("twoHundredDMA numeric", MarketDataSchemaDb.CreateStatement);
        Assert.Contains("fiftydma AS \"FiftyDMA\"", MarketDataDbSql.GetFuturesEodData);
        Assert.Contains("twohundreddma AS \"TwoHundredDMA\"", MarketDataDbSql.GetFuturesEodData);
    }

    [Fact]
    public void FuturesTradeSignalRepair_IsNonDestructiveAndHasQuarantineStorage()
    {
        Assert.Contains("market_data.futures_trade_signal_quarantine", MarketDataSchemaDb.CreateStatement);
        Assert.Contains("ON CONFLICT (fingerprint)", MarketDataDbSql.InsertFuturesTradeSignalQuarantine);
        Assert.Null(typeof(MarketDataDbSql).GetField("DeleteMalformedFuturesTradeSignal",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static));
    }

    [Fact]
    public void Vx_as_of_projection_uses_the_bounded_PostgreSQL_contract_read()
    {
        Assert.Contains("market_data.vix_futures_eod_data", MarketDataDbSql.GetLastVixFuturesEodData);
        Assert.Contains("contractid = $1 AND valuedate <= $2", MarketDataDbSql.GetLastVixFuturesEodData);
        Assert.Contains("ORDER BY valuedate DESC LIMIT 1", MarketDataDbSql.GetLastVixFuturesEodData);
    }

    [Fact]
    public void FuturesItiProjections_UseBoundedQueryShapedPartitions()
    {
        Assert.Contains("futures_iti_signal_ordered", MarketDataSchemaDb.CreateStatement);
        Assert.Contains("PRIMARY KEY (contractId, timePeriod, calendarBucketStart)", MarketDataSchemaDb.CreateStatement);
        Assert.Contains("timeFrameStartValueDate date", MarketDataSchemaDb.CreateStatement);
        Assert.Contains("bandPercentage double precision", MarketDataSchemaDb.CreateStatement);
        Assert.Contains("calendarbucketstart = $3", MarketDataDbSql.GetFuturesItiTimeFrameState);
    }

    [Fact]
    public void Market_data_schema_does_not_own_raw_tick_tables_or_a_Cql_catalog()
    {
        Assert.Null(typeof(MarketDataDbContext).Assembly.GetType(
            "TomasAI.IFM.Application.Storage.MarketDataDb.MarketDataDbCql"));
        Assert.DoesNotContain("CREATE TABLE IF NOT EXISTS market_data.futures_tick_data (", MarketDataSchemaDb.CreateStatement);
    }

    [Fact]
    public void ScopedProjectionReadiness_FailsClosedWhileAnyOperationIsActive()
    {
        var ready = new MarketDataProjectionScopeStateReadModel(
            "projection", "scope", Guid.NewGuid(), IsReady: true, Blocked: false,
            ActiveOperationsEmpty: true);
        var active = ready with { ActiveOperationsEmpty = false };

        Assert.True(ready.CanRead);
        Assert.False(active.CanRead);
    }

    [Fact]
    public void Reconciliation_RejectsEqualCountsWithDifferentRowIdentities()
    {
        var result = new MarketDataProjectionBackfillReadModel(
            FuturesTicksSource: 1,
            FuturesTicksProjected: 1,
            FuturesTicksSourceFingerprint: "tick-source",
            FuturesTicksProjectedFingerprint: "tick-projection",
            FuturesEodRowsSource: 1,
            FuturesEodRowsProjected: 1,
            FuturesEodSourceFingerprint: "eod",
            FuturesEodProjectedFingerprint: "eod",
            VixFuturesEodRowsSource: 3,
            VixContractsSource: 2,
            VixContractsIndexed: 2,
            VixContractsSourceFingerprint: "vix",
            VixContractsIndexedFingerprint: "vix",
            FuturesItiSignalsSource: 1,
            FuturesItiSignalsByDayProjected: 1,
            FuturesItiSignalsByMonthProjected: 1,
            FuturesItiSignalsByTrendModeProjected: 1,
            FuturesItiSignalsSourceFingerprint: "iti",
            FuturesItiSignalsByDayFingerprint: "iti",
            FuturesItiSignalsByMonthFingerprint: "iti",
            FuturesItiSignalsByTrendModeFingerprint: "iti",
            CutoverCompleted: false);

        Assert.False(result.IsReconciled);
    }
}
