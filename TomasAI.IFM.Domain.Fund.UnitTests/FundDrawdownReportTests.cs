using FluentAssertions;
using MessagePack;
using NSubstitute;
using TomasAI.IFM.Application.Storage.FundDb;
using TomasAI.IFM.Domain.Fund.Query;
using TomasAI.IFM.Domain.Fund.Shared.ViewModels;
using TomasAI.IFM.Domain.Trade.Shared;

namespace TomasAI.IFM.Domain.Fund.UnitTests;

public sealed class FundDrawdownReportTests
{
    [Fact]
    public void Drawdown_retains_the_largest_decline_after_recovery_and_uses_the_prior_peak()
    {
        var result = FundQueryCalculations.CalculateMaximumDrawdown([120m, 90m, 150m, 130m, 160m], 100m);
        result.Amount.Should().Be(30m);
        result.Percent.Should().Be(.25);
    }

    [Fact]
    public void Drawdown_handles_initial_loss_empty_history_and_non_positive_capital()
    {
        FundQueryCalculations.CalculateMaximumDrawdown([80m], 100m).Should().Be((20m, .2));
        FundQueryCalculations.CalculateMaximumDrawdown([], 100m).Should().Be(((decimal?)null, (double?)null));
        FundQueryCalculations.CalculateMaximumDrawdown([0m, -10m], 0m).Should().Be((10m, (double?)null));
        FundQueryCalculations.CalculateMaximumDrawdown([110m, 130m], 100m).Should().Be((0m, 0d));
    }

    [Fact]
    public async Task Report_uses_chronological_transactions_including_same_day_trough_and_round_trips_new_fields()
    {
        var date = new DateOnly(2026, 1, 2);
        FundTransactionReadModel Transaction(int id, decimal balance) => FundTransactionReadModel
            .AsRealizedTradePnlTransaction(7, 1, id, default, date, "test", 0m)
            with { TransactionId = id, TransactionDate = date.ToDateTime(new TimeOnly(10, id)), Balance = balance };
        var db = Substitute.For<IFundDbContext>();
        db.GetFundStartingBalanceAsync(7, date).Returns(100m);
        db.GetFundTransactionsAsync(7, date, date.AddDays(1)).Returns(new[] { Transaction(3, 130m), Transaction(1, 120m), Transaction(2, 90m) });
        var report = await FundQueryCalculations.GetPnlReportAsync(db, 7, date, date.AddDays(1));
        report.HasHistory.Should().BeTrue();
        report.MaximumDrawdownAmount.Should().Be(30m);
        report.MaximumDrawdownPercent.Should().Be(.25);
        var restored = MessagePackSerializer.Deserialize<FundPnlReportReadModel>(MessagePackSerializer.Serialize(report));
        restored.Should().BeEquivalentTo(report);
    }

    [Fact]
    public async Task Empty_report_marks_metrics_unavailable()
    {
        var report = await FundQueryCalculations.GetPnlReportAsync(Substitute.For<IFundDbContext>(), 7, new(2026, 1, 1), new(2026, 2, 1));
        report.HasHistory.Should().BeFalse();
        report.MaximumDrawdownPercent.Should().BeNull();
    }

    [Fact]
    public async Task Report_includes_the_loss_on_the_first_transaction_of_the_period()
    {
        var date = new DateOnly(2026, 1, 2);
        var first = FundTransactionReadModel.AsRealizedTradePnlTransaction(7, 1, 1, default, date, "loss", -20m) with { Balance = 80m };
        var db = Substitute.For<IFundDbContext>();
        db.GetFundStartingBalanceAsync(7, date).Returns(80m);
        db.GetFundTransactionsAsync(7, date, date).Returns(new[] { first });
        var report = await FundQueryCalculations.GetPnlReportAsync(db, 7, date, date);
        report.MaximumDrawdownAmount.Should().Be(20m);
        report.MaximumDrawdownPercent.Should().Be(.2);
    }
}
