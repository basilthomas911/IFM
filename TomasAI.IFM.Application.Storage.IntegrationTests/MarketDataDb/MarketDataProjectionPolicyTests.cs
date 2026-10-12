using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using TomasAI.IFM.Application.Storage.MarketDataDb;
using TomasAI.IFM.Application.Storage.MarketDataDb.Schema;
using TomasAI.IFM.Domain.MarketData.Shared;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Framework.Storage;
using Xunit;

namespace TomasAI.IFM.Application.Storage.IntegrationTests.MarketDataDb;

public sealed class MarketDataProjectionPolicyTests
{
    static readonly Type CqlType = typeof(MarketDataDbContext).Assembly.GetType(
        "TomasAI.IFM.Application.Storage.MarketDataDb.MarketDataDbCql",
        throwOnError: true)!;

    [Fact]
    public void MarketOutlookSnapshot_UsesOneUpsertRowPerDateAndLatestCutoffQuery()
    {
        MarketDataSchemaSql.Create.ShouldContain("market_data.market_outlook_snapshot");
        MarketDataDbSql.UpsertMarketOutlookSnapshot.ShouldContain("ON CONFLICT (contractId, valueDate)");
        MarketDataDbSql.GetMarketOutlookSnapshot.ShouldContain("valueDate <= $2");
        MarketDataDbSql.GetMarketOutlookSnapshot.ShouldContain("ORDER BY valueDate DESC LIMIT 1");
    }

    [Fact]
    public void FuturesEodSchema_PersistsMovingAveragesAndSupportsAdditiveUpgrade()
    {
        MarketDataSchemaSql.Create.ShouldContain("fiftyDMA numeric");
        MarketDataSchemaSql.Create.ShouldContain("twoHundredDMA numeric");
        MarketDataDbSql.GetFuturesEodData.ShouldContain("fiftyDMA AS \"FiftyDMA\"");
        MarketDataDbSql.GetFuturesEodData.ShouldContain("twoHundredDMA AS \"TwoHundredDMA\"");
    }

    [Fact]
    public void FuturesTradeSignalRepair_IsNonDestructiveAndHasQuarantineStorage()
    {
        MarketDataSchemaSql.Create.ShouldContain("market_data.futures_trade_signal_quarantine");
        MarketDataDbSql.InsertFuturesTradeSignalQuarantine.ShouldContain("ON CONFLICT (fingerprint)");
        Assert.Null(typeof(MarketDataDbSql).GetField("DeleteMalformedFuturesTradeSignal"));
    }

    [Fact]
    public void VixAsOfProjection_UsesContractIndexAndCanonicalContractRead()
    {
        GetCql("GetVixFuturesContractIds").ShouldContain("vix_futures_contract_index");
        GetCql("GetLastVixFuturesEodData").ShouldContain("vix_futures_eod_data");
        GetCql("GetLastVixFuturesEodData").ShouldContain("valueDate <= :valueDate");
        GetCql("GetVixFuturesEodDataThroughDate").ShouldContain("valueDate <= :valueDate");

        Assert.Null(CqlType.GetField(
            "InsertVixFuturesEodDataByMonth",
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static));
        Assert.Null(CqlType.GetField(
            "GetVixFuturesEodDataByMonth",
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static));
        Assert.Null(CqlType.GetField(
            "TruncateLegacyVixFuturesEodDataByMonth",
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static));
    }

    [Fact]
    public void ProjectionCutover_HasDurableStateAndInFlightMutationQueries()
    {
        GetCql("GetMarketDataProjectionState").ShouldContain("market_data_projection_state");
        GetCql("BeginMarketDataProjectionOperation").ShouldContain("activeOperations = activeOperations + :activeOperations");
        GetCql("EndMarketDataProjectionOperation").ShouldContain("isReady = false");
        GetCql("CompleteMarketDataProjectionState").ShouldContain("isReady = true");
        GetCql("CompleteMarketDataProjectionState").ShouldContain("activeOperations = :expectedActiveOperations");
        GetCql("RestoreMarketDataProjectionState").ShouldContain("IF generation = :generation");
        GetCql("InsertMarketDataProjectionMutation").ShouldContain("market_data_projection_mutation");
        GetCql("GetMarketDataProjectionMutation").ShouldContain("LIMIT 1");
        GetCql("FailMarketDataProjectionMutation").ShouldContain("startedOn = :startedOn");
    }

    [Fact]
    public void FuturesItiProjections_UseBoundedQueryShapedPartitions()
    {
        MarketDataSchemaSql.Create.ShouldContain("futures_iti_signal_ordered");
        MarketDataSchemaSql.Create.ShouldContain("PRIMARY KEY (contractId, timePeriod, calendarBucketStart)");
        MarketDataSchemaSql.Create.ShouldContain("timeFrameStartValueDate date");
        MarketDataSchemaSql.Create.ShouldContain("bandPercentage double precision");
        MarketDataDbSql.GetFuturesItiTimeFrameState.ShouldContain("calendarBucketStart = $3");
    }

    [Fact]
    public void LiveProjectionWrites_AreScopedAndTickBatchesAreExplicitlyBounded()
    {
        GetCql("BeginMarketDataProjectionScopeOperationV3")
            .ShouldContain("market_data_projection_scope_state");
        GetCql("BeginMarketDataProjectionScopeOperationV3")
            .ShouldContain("activeOperations = activeOperations + :activeOperations");
        GetCql("CompleteMarketDataProjectionScopeOperationV3")
            .ShouldContain("blocked = false");
        GetCql("MarkMarketDataProjectionScopeAtomicWriteV3")
            .ShouldContain("SET generation = :generation");
        GetCql("MarkMarketDataProjectionScopeAtomicWriteV3")
            .ShouldContain("scopeKey = :scopeKey");
        GetCql("RegisterMarketDataProjectionGuardOperationV3")
            .ShouldContain("activeOperations = activeOperations + :activeOperations");
        GetCql("CompleteMarketDataProjectionGuardOperationV3")
            .ShouldContain("IF blocked = false");
        GetCql("CompleteMarketDataProjectionGuardOperationV3")
            .ShouldContain("activeOperations = :expectedActiveOperations");
        GetCql("GetMarketDataProjectionScopeStatesV3")
            .ShouldContain("activeOperations AS \"ActiveOperations\"");
        MarketDataSchemaSql.Create.ShouldContain("market_data.market_data_projection_scope_state");
        MarketDataSchemaSql.Create.ShouldContain("market_data.market_data_projection_scope_mutation");
        Assert.DoesNotContain("CREATE TABLE IF NOT EXISTS market_data.futures_tick_data (", MarketDataSchemaSql.Create);

        var batchBound = (int)typeof(MarketDataDbContext)
            .GetField("TickAtomicBatchRowCount", BindingFlags.NonPublic | BindingFlags.Static)!
            .GetRawConstantValue()!;
        Assert.InRange(batchBound, 1, 24);
        Assert.InRange(batchBound * 2 + 1, 1, 50);
    }

    [Fact]
    public void ProjectionCutover_UsesBucketedGuardsWithoutExpandingTheGlobalHotRow()
    {
        var flags = BindingFlags.NonPublic | BindingFlags.Static;
        var guardCount = (int)typeof(MarketDataDbContext)
            .GetField("ProjectionGuardScopeCount", flags)!
            .GetRawConstantValue()!;
        var addGuards = typeof(MarketDataDbContextExtensions)
            .GetMethod("AddProjectionGuardScopes", flags)!;
        var getGuard = typeof(MarketDataDbContextExtensions)
            .GetMethod("GetProjectionGuardScopeKey", flags)!;
        var guardedScopes = (string[])addGuards.Invoke(
            null,
            new object[] { new[] { "scope-a", "scope-a" } })!;

        Assert.Equal(32, guardCount);
        Assert.Equal(2, guardedScopes.Length);
        Assert.Contains("scope-a", guardedScopes);
        Assert.Single(
            guardedScopes,
            scope => scope.StartsWith("$guard:", StringComparison.Ordinal));
        Assert.Equal("$guard:28", getGuard.Invoke(null, new object[] { "scope-a" }));
        Assert.Equal("$guard:30", getGuard.Invoke(null, new object[] { "12:ESU2047:12345" }));
        Assert.Equal("$guard:16", getGuard.Invoke(null, new object[] { "204712" }));
        Assert.Equal("$guard:7", getGuard.Invoke(null, new object[] { "7" }));
        GetCql("RegisterMarketDataProjectionGuardOperationV3")
            .ShouldContain("market_data_projection_scope_state");
        GetCql("RegisterMarketDataProjectionGuardOperationV3")
            .ShouldContain("SET activeOperations = activeOperations + :activeOperations");
    }

    [Fact]
    public void ScopedProjectionParameters_FollowCqlPlaceholderOrder()
    {
        var generation = Guid.NewGuid();
        var activeOperations = new HashSet<Guid> { generation };
        var completedOn = DateTime.UtcNow;

        AssertBind(
            new BeginMarketDataProjectionScopeOperationV3(
                "projection", "scope", generation, activeOperations),
            generation, activeOperations, "projection", "scope");
        AssertBind(
            new CompleteMarketDataProjectionScopeOperationV3(
                "projection", "scope", generation, activeOperations, completedOn, activeOperations),
            activeOperations, completedOn, "projection", "scope", generation, activeOperations);
        AssertBind(
            new RemoveMarketDataProjectionScopeOperationV3("projection", "scope", generation),
            generation, "projection", "scope");
        AssertBind(
            new MarkMarketDataProjectionScopeAtomicWriteV3("projection", "scope", generation),
            generation, "projection", "scope");
        AssertBind(
            new RegisterMarketDataProjectionGuardOperationV3(
                "projection", "scope", activeOperations),
            activeOperations, "projection", "scope");
        AssertBind(
            new CompleteMarketDataProjectionGuardOperationV3(
                "projection", "scope", generation, activeOperations, completedOn, activeOperations),
            generation, activeOperations, completedOn, "projection", "scope", activeOperations);
    }

    [Fact]
    public void MonthScopeHelpers_HandleDateOnlyMaximumWithoutOverflow()
    {
        var flags = BindingFlags.NonPublic | BindingFlags.Static;
        var monthEnd = (DateOnly)typeof(MarketDataDbContextExtensions)
            .GetMethod("GetMonthEnd", flags)!
            .Invoke(null, new object[] { 999912 })!;
        var months = (IEnumerable<int>)typeof(MarketDataDbContextExtensions)
            .GetMethod("GetYearMonths", flags)!
            .Invoke(null, new object[] { new DateOnly(9999, 12, 1), DateOnly.MaxValue })!;

        Assert.Equal(DateOnly.MaxValue, monthEnd);
        Assert.Equal(new[] { 999912 }, months.ToArray());
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
    public void TickGuardRecovery_DoesNotAutoReclaimAnAmbiguousDataBatch()
    {
        Assert.False(MarketDataDbContextExtensions.IsTickGuardFailureAutomaticallyRecoverable(
            TickProjectionGuardFailureStage.RegistrationResponseUnknown));
        Assert.True(MarketDataDbContextExtensions.IsTickGuardFailureAutomaticallyRecoverable(
            TickProjectionGuardFailureStage.RegisteredBeforeDataSubmission));
        Assert.False(MarketDataDbContextExtensions.IsTickGuardFailureAutomaticallyRecoverable(
            TickProjectionGuardFailureStage.DataBatchResponseUnknown));
        Assert.True(MarketDataDbContextExtensions.IsTickGuardFailureAutomaticallyRecoverable(
            TickProjectionGuardFailureStage.AfterDataAcknowledged));
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

    static string GetCql(string fieldName)
        => (string)CqlType.GetField(
            fieldName,
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)!
            .GetRawConstantValue()!;


    static void AssertBind(IBindValue bindValue, params object?[] expected)
        => Assert.Equal(expected, (object?[])bindValue.Bind());
}

file static class StringAssertions
{
    public static void ShouldContain(this string actual, string expected)
        => Assert.Contains(expected, actual, StringComparison.OrdinalIgnoreCase);
}
