using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using TomasAI.IFM.Application.Storage.MarketDataDb;
using TomasAI.IFM.Framework.Storage;
using Xunit;

namespace TomasAI.IFM.Application.Storage.IntegrationTests.MarketDataDb;

public sealed class MarketDataProjectionPolicyTests
{
    static readonly Type CqlType = typeof(MarketDataDbContext).Assembly.GetType(
        "TomasAI.IFM.Application.Storage.MarketDataDb.MarketDataDbCql",
        throwOnError: true)!;
    static readonly Type SchemaCqlType = typeof(MarketDataDbContext).Assembly.GetType(
        "TomasAI.IFM.Application.Storage.MarketDataDb.Schema.MarketDataSchemaCql",
        throwOnError: true)!;

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
        Assert.Null(SchemaCqlType.GetField(
            "CreateVixFuturesEodDataByMonthTable",
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static));
    }

    [Fact]
    public void ProjectionCutover_HasDurableStateAndInFlightMutationQueries()
    {
        GetCql("GetMarketDataProjectionState").ShouldContain("market_data_projection_state_v2");
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
        GetSchemaCql("CreateFuturesItiSignalByContractDayV2Table")
            .ShouldContain("PRIMARY KEY ((contractId, valueDate), intrinsicTimeMode, sequenceId");
        GetSchemaCql("CreateFuturesItiSignalByContractMonthV2Table")
            .ShouldContain("PRIMARY KEY ((contractId, yearMonth), valueDate, sequenceId");
        GetSchemaCql("CreateFuturesItiSignalByTrendModeMonthV2Table")
            .ShouldContain("PRIMARY KEY ((contractId, intrinsicTimeTrend, intrinsicTimeMode, yearMonth)");
        GetCql("GetFuturesItiSignalsByContractMonthV2")
            .ShouldContain("contractId = :contractId AND yearMonth = :yearMonth");
        GetCql("GetLastFuturesItiSignalByTrendModeMonthV2")
            .ShouldContain("AND yearMonth = :yearMonth");
        GetCql("GetFuturesItiSignalsByContractDayModeAfterSequenceV2")
            .ShouldContain("intrinsicTimeMode = :intrinsicTimeMode AND sequenceId > :sequenceId");
    }

    [Fact]
    public void LiveProjectionWrites_AreScopedAndTickBatchesAreExplicitlyBounded()
    {
        GetCql("BeginMarketDataProjectionScopeOperationV3")
            .ShouldContain("market_data_projection_scope_state_v3");
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
        GetSchemaCql("CreateMarketDataProjectionScopeStateV3Table")
            .ShouldContain("blocked boolean");
        GetSchemaCql("CreateMarketDataProjectionScopeMutationV3Table")
            .ShouldContain("PRIMARY KEY ((projectionName, scopeKey), mutationId)");

        // Tick canonical and query tables deliberately route differently. The hot path
        // therefore uses a tiny logged batch, not a claimed same-partition optimization.
        GetSchemaCql("CreateFuturesTickDataTable")
            .ShouldContain("PRIMARY KEY (contractId, valueDate, tickId)");
        GetSchemaCql("CreateFuturesTickDataByTimeTable")
            .ShouldContain("PRIMARY KEY ((contractId, valueDate), tickTime, tickId)");

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
        var addGuards = typeof(MarketDataDbContext)
            .GetMethod("AddProjectionGuardScopes", flags)!;
        var getGuard = typeof(MarketDataDbContext)
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
            .ShouldContain("market_data_projection_scope_state_v3");
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
        var monthEnd = (DateOnly)typeof(MarketDataDbContext)
            .GetMethod("GetMonthEnd", flags)!
            .Invoke(null, new object[] { 999912 })!;
        var months = (IEnumerable<int>)typeof(MarketDataDbContext)
            .GetMethod("GetYearMonths", flags)!
            .Invoke(null, new object[] { new DateOnly(9999, 12, 1), DateOnly.MaxValue })!;

        Assert.Equal(DateOnly.MaxValue, monthEnd);
        Assert.Equal(new[] { 999912 }, months.ToArray());
    }

    [Fact]
    public void ScopedProjectionReadiness_FailsClosedWhileAnyOperationIsActive()
    {
        var ready = new MarketDataProjectionScopeStateData(
            "projection", "scope", Guid.NewGuid(), IsReady: true, Blocked: false,
            ActiveOperationsEmpty: true);
        var active = ready with { ActiveOperationsEmpty = false };

        Assert.True(ready.CanRead);
        Assert.False(active.CanRead);
    }

    [Fact]
    public void TickGuardRecovery_DoesNotAutoReclaimAnAmbiguousDataBatch()
    {
        Assert.False(MarketDataDbContext.IsTickGuardFailureAutomaticallyRecoverable(
            TickProjectionGuardFailureStage.RegistrationResponseUnknown));
        Assert.True(MarketDataDbContext.IsTickGuardFailureAutomaticallyRecoverable(
            TickProjectionGuardFailureStage.RegisteredBeforeDataSubmission));
        Assert.False(MarketDataDbContext.IsTickGuardFailureAutomaticallyRecoverable(
            TickProjectionGuardFailureStage.DataBatchResponseUnknown));
        Assert.True(MarketDataDbContext.IsTickGuardFailureAutomaticallyRecoverable(
            TickProjectionGuardFailureStage.AfterDataAcknowledged));
    }

    [Fact]
    public void Reconciliation_RejectsEqualCountsWithDifferentRowIdentities()
    {
        var result = new MarketDataProjectionBackfillResult(
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

    static string GetSchemaCql(string fieldName)
        => (string)SchemaCqlType.GetField(
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
