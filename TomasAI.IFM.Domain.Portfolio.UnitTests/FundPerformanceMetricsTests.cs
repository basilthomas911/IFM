using FluentAssertions;
using MessagePack;
using TomasAI.IFM.Domain.Portfolio.GeneralLedger.Query.Model;
using TomasAI.IFM.Domain.Portfolio.Shared.Financial;

namespace TomasAI.IFM.Domain.Portfolio.UnitTests;

public class FundPerformanceMetricsTests
{
    [Fact]
    public void Query_round_trips_scope_dates_and_correlation()
    {
        var query = new GetFundPerformanceMetricsQuery { Parameters=new(Day.AddDays(-5),Day),
            EntityId=new(1), Scope=new() { PortfolioId=1,FundId=2 }, CorrelationId=Guid.NewGuid(),
            RequestedAtUtc=new(2026,10,23,12,0,0,DateTimeKind.Utc) };
        MessagePackSerializer.Deserialize<GetFundPerformanceMetricsQuery>(MessagePackSerializer.Serialize(query)).Should().BeEquivalentTo(query);
    }
    static readonly DateOnly Day = new(2026, 10, 23);
    static FinancialTransactionRow Row(int ordinal, LedgerTransactionKind kind, decimal amount, int day = 0) =>
        new(ordinal, Guid.NewGuid(), ordinal, ordinal, new() { TransactionKind = kind, Amount = amount, AccountingDate = Day.AddDays(day) }, null);

    [Fact]
    public void Preserves_posting_basis_and_includes_cash_movements_in_drawdown()
    {
        var result = FundPerformanceMetricsCalculator.Calculate([
            Row(1, LedgerTransactionKind.OpeningBalance, 1000),
            Row(2, LedgerTransactionKind.RealizedPnl, 200),
            Row(3, LedgerTransactionKind.RealizedPnl, -100),
            Row(4, LedgerTransactionKind.Commission, 10),
            Row(5, LedgerTransactionKind.WithdrawalSettled, 100)], new(Day, Day));
        result.WinRate.Should().Be(.5m); result.LossRate.Should().Be(.5m);
        result.AverageProfit.Should().Be(200); result.AverageLoss.Should().Be(100);
        result.WinLossRatio.Should().Be(2); result.PnlAmount.Should().Be(100); result.PnlPercent.Should().Be(.1m);
        result.TradeCommission.Should().Be(10); result.MaximumDrawdownAmount.Should().Be(210);
        result.MaximumDrawdownPercent.Should().Be(.175m);
        // Returns .2 and -.1: sample standard deviation sqrt(.045), mean .05.
        ((double)result.ActualSharpeRatio).Should().BeApproximately(Math.Sqrt(14), 1e-10);
        result.CalculationBasis.Should().Contain("not daily returns").And.Contain("no cash-flow adjustment");
    }

    [Fact]
    public void Filters_inclusive_accounting_range_and_orders_by_revision()
    {
        var result = FundPerformanceMetricsCalculator.Calculate([
            Row(3, LedgerTransactionKind.RealizedPnl, -50),
            Row(1, LedgerTransactionKind.OpeningBalance, 1000),
            Row(2, LedgerTransactionKind.RealizedPnl, 100),
            Row(4, LedgerTransactionKind.RealizedPnl, 999, 1)], new(Day, Day));
        result.PnlAmount.Should().Be(50); result.MaximumDrawdownAmount.Should().Be(50);
    }

    [Fact]
    public void Empty_and_zero_capital_results_preserve_unavailable_drawdown()
    {
        var empty = FundPerformanceMetricsCalculator.Calculate([], new(Day, Day));
        empty.HasHistory.Should().BeFalse(); empty.MaximumDrawdownPercent.Should().BeNull();
        var loss = FundPerformanceMetricsCalculator.Calculate([Row(1, LedgerTransactionKind.RealizedPnl, -10)], new(Day, Day));
        loss.PnlPercent.Should().Be(0); loss.ActualSharpeRatio.Should().Be(0);
        loss.MaximumDrawdownPercent.Should().BeNull(); loss.MaximumDrawdownAmount.Should().Be(10);
        MessagePackSerializer.Deserialize<FundPerformanceMetrics>(MessagePackSerializer.Serialize(loss)).Should().Be(loss);
    }
}
