using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.Storage.FundDb;
using TomasAI.IFM.Application.Storage.FundDb.Schema;
using TomasAI.IFM.Domain.Fund.Shared;
using TomasAI.IFM.Domain.Fund.Shared.ViewModels;
using TomasAI.IFM.Domain.Trade.Shared;
using Xunit;

namespace TomasAI.IFM.Application.Storage.IntegrationTests.FundDb;

public sealed class FundTransactionProjectionTests
{
    [Fact]
    public void MonthRanges_AreBoundedAtCalendarMonthEdges()
    {
        var ranges = FundTransactionProjection.GetMonthRanges(
            new DateOnly(2026, 1, 30),
            new DateOnly(2026, 3, 2));

        Assert.Equal(3, ranges.Count);
        Assert.Equal(new FundTransactionMonthRange(new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 30), new DateOnly(2026, 1, 31)), ranges[0]);
        Assert.Equal(new FundTransactionMonthRange(new DateOnly(2026, 2, 1), new DateOnly(2026, 2, 1), new DateOnly(2026, 2, 28)), ranges[1]);
        Assert.Equal(new FundTransactionMonthRange(new DateOnly(2026, 3, 1), new DateOnly(2026, 3, 1), new DateOnly(2026, 3, 2)), ranges[2]);
    }

    [Fact]
    public void AmountSign_PutsZeroWithNonNegativeAmounts()
    {
        Assert.Equal(-1, FundTransactionProjection.GetAmountSign(-0.01m));
        Assert.Equal(1, FundTransactionProjection.GetAmountSign(0m));
        Assert.Equal(1, FundTransactionProjection.GetAmountSign(0.01m));
    }

    [Fact]
    public void LogicalDuplicates_RejectDivergentPayloadsButAllowIdenticalRetries()
    {
        var transaction = new FundTransactionReadModel(
            transactionId: 10,
            transactionDate: new DateTime(2026, 1, 2, 3, 4, 5, DateTimeKind.Utc),
            transactionType: FundTransactionType.RealizedTradePnl,
            fundId: 1,
            orderId: 2,
            tradeId: 3,
            tradeType: TradeType.LongIronCondor,
            valueDate: new DateOnly(2026, 1, 2),
            tradeStatus: TradeStatus.Close,
            description: "retry",
            amount: 10m,
            balance: 100m);

        FundTransactionProjection.ValidateLogicalDuplicates([transaction, transaction]);
        Assert.Throws<ArgumentException>(() =>
            FundTransactionProjection.ValidateLogicalDuplicates(
                [transaction, transaction with { Amount = 11m }]));
    }

    [Fact]
    public void SubMillisecondTransactionDates_MapToOneStorageLogicalKeyAndRejectDivergentInputs()
    {
        var transaction = new FundTransactionReadModel(
            transactionId: 10,
            transactionDate: new DateTime(2026, 1, 2, 3, 4, 5, 123, DateTimeKind.Utc),
            transactionType: FundTransactionType.RealizedTradePnl,
            fundId: 1,
            orderId: 2,
            tradeId: 3,
            tradeType: TradeType.LongIronCondor,
            valueDate: new DateOnly(2026, 1, 2),
            tradeStatus: TradeStatus.Close,
            description: "millisecond collision",
            amount: 10m,
            balance: 100m);
        var sameScyllaTimestamp = transaction with
        {
            TransactionDate = transaction.TransactionDate.AddTicks(1),
            Amount = 11m
        };

        Assert.Equal(
            FundTransactionLogicalKey.From(transaction),
            FundTransactionLogicalKey.From(sameScyllaTimestamp));
        Assert.Equal(
            FundTransactionProjection.NormalizeTransactionDate(transaction.TransactionDate),
            FundTransactionProjection.NormalizeTransactionDate(
                DateTime.SpecifyKind(transaction.TransactionDate, DateTimeKind.Unspecified)));
        Assert.Throws<ArgumentException>(() =>
            FundTransactionProjection.ValidateLogicalDuplicates([transaction, sameScyllaTimestamp]));

        var firstFingerprint = new FundTransactionKeyFingerprint();
        firstFingerprint.Add(new FundTransactionProjectionKey(
            1, transaction.ValueDate, 2, 3, "Long", "RealizedTradePnl", transaction.TransactionDate, 10));
        var secondFingerprint = new FundTransactionKeyFingerprint();
        secondFingerprint.Add(new FundTransactionProjectionKey(
            1, transaction.ValueDate, 2, 3, "Long", "RealizedTradePnl", sameScyllaTimestamp.TransactionDate, 10));
        Assert.Equal(firstFingerprint.Value, secondFingerprint.Value);
    }

    [Fact]
    public void BackfillResult_ReportsProjectionCountMismatches()
    {
        var result = new FundTransactionProjectionBackfillResult(
            TransactionsRead: 10,
            TransactionsProjected: 10,
            BatchesExecuted: 2,
            TimelineRows: 10,
            StatusBalanceRows: 9,
            TransactionAmountRows: 12,
            SourceFingerprint: "source",
            TimelineFingerprint: "source",
            StatusBalanceFingerprint: "source",
            TransactionAmountFingerprint: "source",
            CompletedMonths: 1,
            TotalMonths: 1);

        Assert.Equal(0, result.TimelineMismatchCount);
        Assert.Equal(1, result.StatusBalanceMismatchCount);
        Assert.Equal(2, result.TransactionAmountMismatchCount);
        Assert.False(result.IsReconciled);
    }

    [Fact]
    public void BackfillResult_RejectsMissingAndStaleKeysEvenWhenCountsMatch()
    {
        var result = new FundTransactionProjectionBackfillResult(
            TransactionsRead: 2,
            TransactionsProjected: 2,
            BatchesExecuted: 1,
            TimelineRows: 2,
            StatusBalanceRows: 2,
            TransactionAmountRows: 2,
            SourceFingerprint: "canonical-keys",
            TimelineFingerprint: "canonical-keys",
            StatusBalanceFingerprint: "missing-plus-stale",
            TransactionAmountFingerprint: "canonical-keys",
            CompletedMonths: 1,
            TotalMonths: 1);

        Assert.Equal(0, result.StatusBalanceMismatchCount);
        Assert.False(result.KeysMatch);
        Assert.False(result.IsReconciled);
    }

    [Fact]
    public void KeyFingerprint_IsOrderIndependentAndSensitiveToKeyReplacement()
    {
        var first = new FundTransactionProjectionKey(1, new DateOnly(2026, 1, 2), 3, 4, "Long", "Opening", new DateTime(2026, 1, 2, 3, 4, 5), 6);
        var second = first with { TransactionId = 7 };
        var stale = first with { TransactionId = 8 };
        var canonical = new FundTransactionKeyFingerprint();
        canonical.Add(first);
        canonical.Add(second);
        var reordered = new FundTransactionKeyFingerprint();
        reordered.Add(second);
        reordered.Add(first);
        var missingAndStale = new FundTransactionKeyFingerprint();
        missingAndStale.Add(first);
        missingAndStale.Add(stale);

        Assert.Equal(canonical.Value, reordered.Value);
        Assert.NotEqual(canonical.Value, missingAndStale.Value);
    }

    [Fact]
    public void FundCql_DoesNotUseAllowFiltering()
    {
        var statements = typeof(FundDbCql)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(field => field.FieldType == typeof(string))
            .Select(field => Assert.IsType<string>(field.GetValue(null)));

        Assert.All(statements, statement =>
            Assert.DoesNotContain("ALLOW FILTERING", statement, StringComparison.OrdinalIgnoreCase));
    }

    [Theory]
    [InlineData(nameof(FundSchemaCql.CreateFundTransactionTimelineV3Table), "PRIMARY KEY ((fundId, monthBucket)")]
    [InlineData(nameof(FundSchemaCql.CreateFundBalanceByStatusDayV3Table), "PRIMARY KEY ((fundId, monthBucket)")]
    [InlineData(nameof(FundSchemaCql.CreateFundTransactionAmountV3Table), "PRIMARY KEY ((fundId, monthBucket)")]
    [InlineData(nameof(FundSchemaCql.CreateFundTransactionProjectionStateV3Table), "PRIMARY KEY ((fundId, monthBucket)")]
    [InlineData(nameof(FundSchemaCql.CreateFundTransactionProjectionMutationV3Table), "PRIMARY KEY ((fundId, monthBucket)")]
    [InlineData(nameof(FundSchemaCql.CreateFundTransactionWriteMutationV3Table), "PRIMARY KEY ((fundId), mutationId)")]
    public void ProjectionSchemas_UseBoundedQueryPartitions(string fieldName, string expectedPartitionKey)
    {
        var field = typeof(FundSchemaCql).GetField(fieldName, BindingFlags.Public | BindingFlags.Static);
        var statement = Assert.IsType<string>(field?.GetValue(null));

        Assert.Contains(expectedPartitionKey, statement, StringComparison.Ordinal);
    }

    [Fact]
    public void ProjectionCutover_IsGenerationConditionalAndMutationsAreExplicit()
    {
        Assert.Contains("IF generation = :generation", FundDbCql.MarkFundTransactionProjectionCompleteV3, StringComparison.Ordinal);
        Assert.Contains("fund_transaction_projection_mutation_v3", FundDbCql.InsertFundTransactionProjectionMutationV3, StringComparison.Ordinal);
        Assert.Contains("fund_transaction_projection_mutation_v3", FundDbCql.GetFundTransactionProjectionMutationsV3, StringComparison.Ordinal);
    }

    [Fact]
    public void WriteOwnership_UsesLwtConflictPoisonAndConditionalSafeRelease()
    {
        Assert.Contains("IF NOT EXISTS", FundDbCql.ClaimFundTransactionWriteOwnershipV3, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("conflicted = true", FundDbCql.FlagFundTransactionWriteOwnershipConflictV3, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("IF EXISTS", FundDbCql.FlagFundTransactionWriteOwnershipConflictV3, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("ownerMutationId = :mutationId", FundDbCql.ReleaseFundTransactionWriteOwnershipIfSafeV3, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("conflicted = false", FundDbCql.ReleaseFundTransactionWriteOwnershipIfSafeV3, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("fundId int PRIMARY KEY", FundSchemaCql.CreateFundTransactionWriteOwnershipV3Table, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TransactionIdentity_UsesExactLogicalKeyLwtWithoutFundWideSerialization()
    {
        Assert.Contains(
            "PRIMARY KEY ((fundId, valueDate, orderId, tradeId, tradeType, transactionType, transactionDate))",
            FundSchemaCql.CreateFundTransactionIdentityV4Table,
            StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(
            "transactionId))",
            FundSchemaCql.CreateFundTransactionIdentityV4Table,
            StringComparison.OrdinalIgnoreCase);
        Assert.Contains("IF NOT EXISTS", FundDbCql.ReserveFundTransactionIdentityV4, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("FROM fund_transaction_identity_v4", FundDbCql.GetFundTransactionIdentityV4, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("ALLOW FILTERING", FundDbCql.GetFundTransactionIdentityV4, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void IdentityExpectation_ChoosesMinimumIdIndependentOfInputOrderAndReportsDuplicates()
    {
        var expectation = new FundTransactionIdentityExpectation(900, CanonicalRows: 1)
            .Add(100)
            .Add(500);

        Assert.Equal(100, expectation.TransactionId);
        Assert.Equal(2, expectation.DuplicateCanonicalRows);
    }

    [Fact]
    public void FundOrderIdLookup_UsesQueryShapedProjectionAndCanonicalFallbackScan()
    {
        Assert.Contains("orderId int PRIMARY KEY", FundSchemaCql.CreateFundOrderByOrderIdV3Table, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("reservationToken uuid", FundSchemaCql.CreateFundOrderByOrderIdV3Table, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("FROM fund_order_by_order_id_v3", FundDbCql.GetFundIdFromOrderId, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("WHERE orderId = :orderId", FundDbCql.GetFundIdFromOrderId, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("IF NOT EXISTS", FundDbCql.InsertFundOrderByOrderIdV3, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("reservationToken = :expectedReservationToken", FundDbCql.RotateFundOrderByOrderIdV3Reservation, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("operationId uuid", FundSchemaCql.CreateFundOrderWriteOwnershipV3Table, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("IF NOT EXISTS", FundDbCql.ClaimFundOrderWriteOwnershipV3, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("IF operationId = :operationId", FundDbCql.ReleaseFundOrderWriteOwnershipV3, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("ALLOW FILTERING", FundDbCql.GetFundIdFromOrderId, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("WHERE orderId", FundDbCql.GetFundOrders, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ReservationRelease_RunsOnlyAfterCanonicalMutationIsAcknowledged()
    {
        var operations = new List<string>();

        await ProjectionMutationSafety.ExecuteCanonicalMutationThenReleaseReservationAsync(
            () =>
            {
                operations.Add("canonical");
                return Task.CompletedTask;
            },
            () =>
            {
                operations.Add("release");
                return Task.CompletedTask;
            });

        Assert.Equal(["canonical", "release"], operations);

        operations.Clear();
        await Assert.ThrowsAsync<TimeoutException>(() =>
            ProjectionMutationSafety.ExecuteCanonicalMutationThenReleaseReservationAsync(
                () =>
                {
                    operations.Add("canonical");
                    throw new TimeoutException("ambiguous canonical response");
                },
                () =>
                {
                    operations.Add("release");
                    return Task.CompletedTask;
                }));

        Assert.Equal(["canonical"], operations);
    }

    [Fact]
    public void FundOrderBackfillResult_AllowsHistoricalRowsButRejectsMissingConflictingOrTokenlessRows()
    {
        Assert.True(new FundOrderProjectionBackfillResult(10, 10, 0, 0).IsReconciled);
        Assert.True(new FundOrderProjectionBackfillResult(10, 12, 0, 0).IsReconciled);
        Assert.False(new FundOrderProjectionBackfillResult(10, 10, 1, 1).IsReconciled);
        Assert.False(new FundOrderProjectionBackfillResult(10, 10, 0, 0, TokenlessRows: 1).IsReconciled);
    }

    [Fact]
    public void OverlappingSuccessfulWriters_CannotRepublishProjectionReadiness()
    {
        var ownerCanPublish = ProjectionMutationSafety.CanPublishReady(
            operationSucceeded: true,
            ownsWriteEpoch: true,
            wasReadyOrExactlyReconciled: true,
            markerIsExclusive: true,
            generationStillMatches: true,
            ownershipReleasedWithoutConflict: false);
        var contenderCanPublish = ProjectionMutationSafety.CanPublishReady(
            operationSucceeded: true,
            ownsWriteEpoch: false,
            wasReadyOrExactlyReconciled: true,
            markerIsExclusive: true,
            generationStillMatches: true,
            ownershipReleasedWithoutConflict: true);

        Assert.False(ownerCanPublish);
        Assert.False(contenderCanPublish);
    }

    [Theory]
    [InlineData(false, true, true, true)]
    [InlineData(true, true, true, false)]
    [InlineData(false, false, true, false)]
    [InlineData(false, true, false, false)]
    public void MutationJournalCleanup_RequiresNoTargetSubmissionAndConfirmedOwnershipResolution(
        bool targetMutationSubmissionStarted,
        bool ownershipResolved,
        bool activationConfirmed,
        bool expected)
    {
        Assert.Equal(
            expected,
            ProjectionMutationSafety.CanRemoveMutationJournalAfterFailure(
                targetMutationSubmissionStarted,
                ownershipResolved,
                activationConfirmed));
        Assert.Contains(
            "IF ownerMutationId = :mutationId",
            FundDbCql.ReleaseFundTransactionWriteOwnershipV3,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void StaleRecovery_RequiresExplicitNonFutureUtcCutoffAndTimestampedJournals()
    {
        ProjectionMutationSafety.ValidateStaleOperationCutoffUtc(null, "cutoff");
        ProjectionMutationSafety.ValidateStaleOperationCutoffUtc(
            DateTime.UtcNow.AddMinutes(-1),
            "cutoff");
        Assert.Throws<ArgumentException>(() =>
            ProjectionMutationSafety.ValidateStaleOperationCutoffUtc(
                DateTime.SpecifyKind(DateTime.UtcNow.AddMinutes(-1), DateTimeKind.Unspecified),
                "cutoff"));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            ProjectionMutationSafety.ValidateStaleOperationCutoffUtc(
                DateTime.UtcNow.AddMinutes(1),
                "cutoff"));
        Assert.Contains("startedOn", FundDbCql.GetFundTransactionWriteMutationJournalV3, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("startedOn", FundDbCql.GetFundTransactionProjectionMutationJournalV3All, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(nameof(FundDbCql.DeleteFundTransactionTimelinePartitionV3))]
    [InlineData(nameof(FundDbCql.DeleteFundBalanceByStatusMonthPartitionV3))]
    [InlineData(nameof(FundDbCql.DeleteFundTransactionAmountPartitionV3))]
    public void BackfillDeletesWholeMonthlyProjectionPartitions(string fieldName)
    {
        var field = typeof(FundDbCql).GetField(fieldName, BindingFlags.Public | BindingFlags.Static);
        var statement = Assert.IsType<string>(field?.GetValue(null));

        Assert.Contains("DELETE FROM", statement, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("fundId = :fundId AND monthBucket = :monthBucket", statement, StringComparison.Ordinal);
    }
}
