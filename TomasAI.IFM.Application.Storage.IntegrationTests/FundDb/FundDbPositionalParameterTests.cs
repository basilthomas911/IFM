using System;
using TomasAI.IFM.Application.Storage.FundDb;
using TomasAI.IFM.Framework.Storage;
using Xunit;

namespace TomasAI.IFM.Application.Storage.IntegrationTests.FundDb;

public sealed class FundDbPositionalParameterTests
{
    static readonly DateOnly StartDate = new(2026, 1, 2);
    static readonly DateOnly EndDate = new(2026, 1, 31);
    static readonly DateOnly MonthBucket = new(2026, 1, 1);
    static readonly DateTime CreatedOn = new(2026, 1, 2, 3, 4, 5, DateTimeKind.Utc);
    static readonly DateTime UpdatedOn = new(2026, 1, 3, 4, 5, 6, DateTimeKind.Utc);
    static readonly Guid ReservationToken = Guid.Parse("73c27988-d257-475f-af4e-428e8ab3dc39");

    [Fact]
    public void QueryAndDeleteBindValues_UseCqlMarkerOrder()
    {
        AssertValues("DeleteFund", new DeleteFund(11), 11);
        AssertValues("DeleteFundOrder", new DeleteFundOrder(11, 12), 11, 12);
        AssertValues("ReleaseFundOrderWriteOwnershipV3", new ReleaseFundOrderWriteOwnershipV3(12, ReservationToken), 12, ReservationToken);
        AssertValues("ClaimFundOrderWriteOwnershipV3", new ClaimFundOrderWriteOwnershipV3(12, ReservationToken, CreatedOn), 12, ReservationToken, CreatedOn);
        AssertValues("DeleteFundOrderByOrderIdV3ForOfflineRepair", new DeleteFundOrderByOrderIdV3ForOfflineRepair(12), 12);
        AssertValues("DeleteFundOrderTrade", new DeleteFundOrderTrade(11, 12, 13), 11, 12, 13);
        AssertValues("DeleteFundTransaction", new DeleteFundTransaction(11, StartDate, 12, 13, "Long", "Opening", CreatedOn),
            11, StartDate, 12, 13, "Long", "Opening", CreatedOn);
        AssertValues("DeleteFundTransactionTimelineV3", new DeleteFundTransactionTimelineV3(11, MonthBucket, StartDate, 12, 13, "Long", "Opening", CreatedOn, 14L),
            11, MonthBucket, StartDate, 12, 13, "Long", "Opening", CreatedOn, 14L);
        AssertValues("DeleteFundBalanceByStatusDayV3", new DeleteFundBalanceByStatusDayV3(11, MonthBucket, StartDate, "Open", CreatedOn, 14L, 12, 13, "Long", "Opening"),
            11, MonthBucket, StartDate, "Open", CreatedOn, 14L, 12, 13, "Long", "Opening");
        AssertValues("DeleteFundTransactionAmountV3", new DeleteFundTransactionAmountV3(11, MonthBucket, "Opening", -1, StartDate, CreatedOn, 14L, 12, 13, "Long"),
            11, MonthBucket, "Opening", -1, StartDate, CreatedOn, 14L, 12, 13, "Long");
        AssertValues("FundTransactionProjectionPartition", new FundTransactionProjectionPartition(11, MonthBucket),
            11, MonthBucket);
        AssertValues("GetFundByFundId", new GetFundByFundId(11), 11);
        AssertValues("GetFundOrder", new GetFundOrder(11, 12), 11, 12);
        AssertValues("GetFundOrderTrade", new GetFundOrderTrade(11, 12, 13), 11, 12, 13);
        AssertValues("GetFundTransaction", new GetFundTransaction(11, StartDate, 12, 13, "Long", "Opening", CreatedOn),
            11, StartDate, 12, 13, "Long", "Opening", CreatedOn);
        AssertValues("GetFundTransactionIdentityV4", new GetFundTransactionIdentityV4(11, StartDate, 12, 13, "Long", "Opening", CreatedOn),
            11, StartDate, 12, 13, "Long", "Opening", CreatedOn);
        AssertValues("GetFundTransactions", new GetFundTransactions(11, StartDate, EndDate),
            11, StartDate, EndDate);
        AssertValues("GetFundTransactionTimelineV3", new GetFundTransactionTimelineV3(11, MonthBucket, StartDate, EndDate),
            11, MonthBucket, StartDate, EndDate);
        AssertValues("GetFundTransactionAmountsV3", new GetFundTransactionAmountsV3(11, MonthBucket, "TradeCommission", -1, StartDate, EndDate),
            11, MonthBucket, "TradeCommission", -1, StartDate, EndDate);
        AssertValues("GetOpeningFundBalanceV3", new GetOpeningFundBalanceV3(11, MonthBucket, StartDate, "Open"),
            11, MonthBucket, StartDate, "Open");
        AssertValues("GetClosingFundBalanceV3", new GetClosingFundBalanceV3(11, MonthBucket, EndDate, "Close"),
            11, MonthBucket, EndDate, "Close");
        AssertValues("GetFundTransactionProjectionStateV3", new GetFundTransactionProjectionStateV3(11, MonthBucket),
            11, MonthBucket);
        var generation = Guid.Parse("58c4d32e-4388-4f0d-9f6a-21dc2b8a1624");
        AssertValues("MarkFundTransactionProjectionIncompleteV3", new MarkFundTransactionProjectionIncompleteV3(generation, 11, MonthBucket),
            generation, 11, MonthBucket);
        AssertValues("MarkFundTransactionProjectionCompleteV3", new MarkFundTransactionProjectionCompleteV3(4, "fingerprint", UpdatedOn, 11, MonthBucket, generation),
            4L, "fingerprint", UpdatedOn, 11, MonthBucket, generation);
        AssertValues("InsertFundTransactionProjectionMutationV3", new InsertFundTransactionProjectionMutationV3(11, MonthBucket, generation, CreatedOn),
            11, MonthBucket, generation, CreatedOn);
        AssertValues("DeleteFundTransactionProjectionMutationV3", new DeleteFundTransactionProjectionMutationV3(11, MonthBucket, generation),
            11, MonthBucket, generation);
        AssertValues("GetFundTransactionProjectionMutationsV3", new GetFundTransactionProjectionMutationsV3(11, MonthBucket),
            11, MonthBucket);
        AssertValues("GetFundTransactionWriteMutationsV3", new GetFundTransactionWriteMutationsV3(11), 11);
        AssertValues("DeleteFundTransactionWriteMutationV3", new DeleteFundTransactionWriteMutationV3(11, generation),
            11, generation);
        AssertValues("FlagFundTransactionWriteOwnershipConflictV3", new FlagFundTransactionWriteOwnershipConflictV3(11), 11);
        AssertValues("ReleaseFundTransactionWriteOwnershipV3", new ReleaseFundTransactionWriteOwnershipV3(11, generation),
            11, generation);
        AssertValues("GetFirstFundTransactionValueDate", new GetFirstFundTransactionValueDate(11, StartDate),
            11, StartDate);
        AssertValues("GetLastFundTransactionValueDate", new GetLastFundTransactionValueDate(11, EndDate),
            11, EndDate);
        AssertValues("GetFundBalance", new GetFundBalance(11), 11);
        AssertValues("GetFundIdFromOrderId", new GetFundIdFromOrderId(12), 12);
        AssertValues("GetFundOrderReservationV3", new GetFundOrderReservationV3(12), 12);
    }

    [Fact]
    public void InsertAndUpdateBindValues_UseCqlMarkerOrder()
    {
        AssertValues("InsertFund", new InsertFund(11, "Fund", "Description", 15.5m, true, CreatedOn, "creator"),
            11, "Fund", "Description", 15.5m, true, CreatedOn, "creator");
        AssertValues("InsertFundOrder",
            new InsertFundOrder(11, 12, CreatedOn, "Open", "ES", StartDate, EndDate, "reference", CreatedOn, "creator", UpdatedOn, "updater"),
            11, 12, CreatedOn, "Open", "ES", StartDate, EndDate, "reference", CreatedOn, "creator", UpdatedOn, "updater");
        AssertValues("InsertFundOrderByOrderIdV3", new InsertFundOrderByOrderIdV3(12, 11, ReservationToken), 12, 11, ReservationToken);
        var replacementToken = Guid.Parse("b462dcc8-f423-411e-919b-7a55819616d3");
        AssertValues(
            "RotateFundOrderByOrderIdV3Reservation",
            new RotateFundOrderByOrderIdV3Reservation(replacementToken, 12, 11, ReservationToken),
            replacementToken, 12, 11, ReservationToken);
        var mutationId = Guid.Parse("58c4d32e-4388-4f0d-9f6a-21dc2b8a1624");
        AssertValues("InsertFundTransactionWriteMutationV3", new InsertFundTransactionWriteMutationV3(11, mutationId, CreatedOn),
            11, mutationId, CreatedOn);
        AssertValues("ClaimFundTransactionWriteOwnershipV3", new ClaimFundTransactionWriteOwnershipV3(11, mutationId, CreatedOn),
            11, mutationId, CreatedOn);
        AssertValues("ReserveFundTransactionIdentityV4", new ReserveFundTransactionIdentityV4(11, StartDate, 12, 13, "Long", "Opening", CreatedOn, 14L),
            11, StartDate, 12, 13, "Long", "Opening", CreatedOn, 14L);
        AssertValues("InsertFundOrderTrade",
            new InsertFundOrderTrade(11, 12, 13, "Long", StartDate, EndDate, "New", "Buy", "reference", true, "ES", CreatedOn, "creator"),
            11, 12, 13, "Long", StartDate, EndDate, "New", "Buy", "reference", true, "ES", CreatedOn, "creator", null, null);
        AssertValues("InsertFundTransaction",
            new InsertFundTransaction(14L, CreatedOn, "Opening", 11, 12, 13, "Long", StartDate, "Open", "description", 16.5m, 17.5m),
            14L, CreatedOn, "Opening", 11, 12, 13, "Long", StartDate, "Open", "description", 16.5m, 17.5m);
        AssertValues("InsertFundTransactionTimelineV3",
            new InsertFundTransactionTimelineV3(11, MonthBucket, StartDate, CreatedOn, 14L, "Opening", 12, 13, "Long", "Open", "description", 16.5m, 17.5m),
            11, MonthBucket, StartDate, CreatedOn, 14L, "Opening", 12, 13, "Long", "Open", "description", 16.5m, 17.5m);
        AssertValues("InsertFundBalanceByStatusDayV3",
            new InsertFundBalanceByStatusDayV3(11, MonthBucket, StartDate, "Open", CreatedOn, 14L, "Opening", 12, 13, "Long", 17.5m),
            11, MonthBucket, StartDate, "Open", CreatedOn, 14L, "Opening", 12, 13, "Long", 17.5m);
        AssertValues("InsertFundTransactionAmountV3",
            new InsertFundTransactionAmountV3(11, MonthBucket, "Opening", 1, StartDate, CreatedOn, 14L, 12, 13, "Long", 16.5m),
            11, MonthBucket, "Opening", 1, StartDate, CreatedOn, 14L, 12, 13, "Long", 16.5m);

        AssertValues("UpdateFundBalance", new UpdateFundBalance(11, 15.5m), 15.5m, 11);
        AssertValues("UpdateFundOrderTradeState", new UpdateFundOrderTradeState(11, 12, 13, "Closed", UpdatedOn, "updater"),
            "Closed", UpdatedOn, "updater", 11, 12, 13);
        AssertValues("UpdateFundOrderStatus", new UpdateFundOrderStatus(11, 12, "Closed"), "Closed", 11, 12);
    }

    static void AssertValues(string parameterName, IBindValue bindValue, params object?[] expected)
    {
        var actual = Assert.IsType<object?[]>(bindValue.Bind());
        Assert.Equal(expected.Length, actual.Length);

        for (var index = 0; index < expected.Length; index++)
            Assert.True(Equals(expected[index], actual[index]),
                $"{parameterName} value {index} was '{actual[index]}' instead of '{expected[index]}'.");
    }
}
