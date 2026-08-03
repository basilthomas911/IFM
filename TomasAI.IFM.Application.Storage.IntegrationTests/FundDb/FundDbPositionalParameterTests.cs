using System;
using TomasAI.IFM.Application.Storage.FundDb;
using TomasAI.IFM.Framework.Storage;
using Xunit;

namespace TomasAI.IFM.Application.Storage.IntegrationTests.FundDb;

public sealed class FundDbPositionalParameterTests
{
    static readonly DateOnly StartDate = new(2026, 1, 2);
    static readonly DateOnly EndDate = new(2026, 1, 31);
    static readonly DateTime CreatedOn = new(2026, 1, 2, 3, 4, 5, DateTimeKind.Utc);
    static readonly DateTime UpdatedOn = new(2026, 1, 3, 4, 5, 6, DateTimeKind.Utc);

    [Fact]
    public void QueryAndDeleteBindValues_UseCqlMarkerOrder()
    {
        AssertValues("DeleteFund", new DeleteFund(11), 11);
        AssertValues("DeleteFundOrder", new DeleteFundOrder(11, 12), 11, 12);
        AssertValues("DeleteFundOrderTrade", new DeleteFundOrderTrade(11, 12, 13), 11, 12, 13);
        AssertValues("DeleteFundTransaction", new DeleteFundTransaction(11, StartDate, 12, 13, "Long", "Opening", CreatedOn),
            11, StartDate, 12, 13, "Long", "Opening", CreatedOn);
        AssertValues("GetFundByFundId", new GetFundByFundId(11), 11);
        AssertValues("GetFundOrder", new GetFundOrder(11, 12), 11, 12);
        AssertValues("GetFundOrderTrade", new GetFundOrderTrade(11, 12, 13), 11, 12, 13);
        AssertValues("GetFundTransaction", new GetFundTransaction(11, StartDate, 12, 13, "Long", "Opening", CreatedOn),
            11, StartDate, 12, 13, "Long", "Opening", CreatedOn);
        AssertValues("GetFundTransactions", new GetFundTransactions(11, StartDate, EndDate),
            11, StartDate, EndDate);
        AssertValues("GetFundPnl", new GetFundPnl(11, StartDate, EndDate),
            11, StartDate, EndDate);
        AssertValues("GetFundBalance", new GetFundBalance(11), 11);
        AssertValues("GetFundTradeCommission", new GetFundTradeCommission(11, StartDate, EndDate),
            11, StartDate, EndDate);
        AssertValues("GetFundBalanceByTransactionId", new GetFundBalanceByTransactionId(11, 14L), 11, 14L);
        AssertValues("GetFundMinTransactionId", new GetFundMinTransactionId(11, StartDate), 11, StartDate);
        AssertValues("GetFundMaxTransactionId", new GetFundMaxTransactionId(11, EndDate), 11, EndDate);
        AssertValues("GetFundTransactionDateByTradeStatus", new GetFundTransactionDateByTradeStatus(11, StartDate, "Open"),
            11, StartDate, "Open");
        AssertValues("GetFundBalanceByTransactionDate", new GetFundBalanceByTransactionDate(11, CreatedOn), 11, CreatedOn);
        AssertValues("GetFundLossOrders", new GetFundLossOrders(11, StartDate, EndDate),
            11, StartDate, EndDate);
        AssertValues("GetFundProfitOrders", new GetFundProfitOrders(11, StartDate, EndDate),
            11, StartDate, EndDate);
        AssertValues("GetFundDailyBalance", new GetFundDailyBalance(11, StartDate, EndDate),
            11, StartDate, EndDate);
        AssertValues("GetFundIdFromOrderId", new GetFundIdFromOrderId(12), 12);
    }

    [Fact]
    public void InsertAndUpdateBindValues_UseCqlMarkerOrder()
    {
        AssertValues("InsertFund", new InsertFund(11, "Fund", "Description", 15.5m, true, CreatedOn, "creator"),
            11, "Fund", "Description", 15.5m, true, CreatedOn, "creator");
        AssertValues("InsertFundOrder",
            new InsertFundOrder(11, 12, CreatedOn, "Open", "ES", StartDate, EndDate, "reference", CreatedOn, "creator", UpdatedOn, "updater"),
            11, 12, CreatedOn, "Open", "ES", StartDate, EndDate, "reference", CreatedOn, "creator", UpdatedOn, "updater");
        AssertValues("InsertFundOrderTrade",
            new InsertFundOrderTrade(11, 12, 13, "Long", StartDate, EndDate, "New", "Buy", "reference", true, "ES", CreatedOn, "creator"),
            11, 12, 13, "Long", StartDate, EndDate, "New", "Buy", "reference", true, "ES", CreatedOn, "creator", null, null);
        AssertValues("InsertFundTransaction",
            new InsertFundTransaction(14L, CreatedOn, "Opening", 11, 12, 13, "Long", StartDate, "Open", "description", 16.5m, 17.5m),
            14L, CreatedOn, "Opening", 11, 12, 13, "Long", StartDate, "Open", "description", 16.5m, 17.5m);

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
