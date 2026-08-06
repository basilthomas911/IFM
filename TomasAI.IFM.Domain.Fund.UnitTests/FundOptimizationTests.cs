using FluentAssertions;
using NSubstitute;
using TomasAI.IFM.Application.Storage.FundDb;
using TomasAI.IFM.Domain.Fund.Query;
using TomasAI.IFM.Domain.Fund.Shared;
using TomasAI.IFM.Domain.Fund.Shared.Commands;
using TomasAI.IFM.Domain.Fund.Shared.Events;
using TomasAI.IFM.Domain.Fund.Shared.ViewModels;
using TomasAI.IFM.Domain.Fund.Transaction.Command;
using TomasAI.IFM.Domain.Fund.Transaction.Command.State;
using TomasAI.IFM.Domain.Trade.Shared;

namespace TomasAI.IFM.Domain.Fund.UnitTests;

public class FundOptimizationTests
{
    static readonly DateOnly StartDate = new(2026, 1, 1);
    static readonly DateOnly EndDate = new(2026, 1, 31);

    [Fact]
    public async Task GetPnlReportAsync_ExecutesIndependentReadsConcurrently_AndReadsBalancesOnce()
    {
        var db = Substitute.For<IFundDbContext>();
        var probe = new ConcurrencyProbe();
        ICollection<FundOrderAmountReadModel> orders =
        [
            new FundOrderAmountReadModel(1, StartDate, 1, 10m)
        ];
        ICollection<FundDailyBalanceReadModel> balances =
        [
            new FundDailyBalanceReadModel(1, StartDate, 100m),
            new FundDailyBalanceReadModel(1, StartDate.AddDays(1), 101m),
            new FundDailyBalanceReadModel(1, StartDate.AddDays(2), 99m)
        ];

        db.GetFundLossOrdersAsync(1, StartDate, EndDate).Returns(_ => probe.TrackAsync(orders));
        db.GetFundProfitOrdersAsync(1, StartDate, EndDate).Returns(_ => probe.TrackAsync(orders));
        db.GetFundStartingBalanceAsync(1, StartDate).Returns(_ => probe.TrackAsync(100m));
        db.GetFundEndingBalanceAsync(1, EndDate).Returns(_ => probe.TrackAsync(110m));
        db.GetFundTradeCommissionAsync(1, StartDate, EndDate).Returns(_ => probe.TrackAsync(2m));
        db.GetFundDailyBalancesAsync(1, StartDate, EndDate).Returns(_ => probe.TrackAsync(balances));

        var result = await FundQueryCalculations.GetPnlReportAsync(db, 1, StartDate, EndDate);

        result.PnlAmount.Should().Be(10m);
        result.TargetSharpeRatio.Should().Be(result.ActualSharpeRatio);
        probe.MaximumConcurrency.Should().BeGreaterThan(1);
        await db.Received(1).GetFundDailyBalancesAsync(1, StartDate, EndDate);
    }

    [Fact]
    public void CalculateSharpeRatio_ReturnsZero_WhenPreviousBalanceIsZero()
    {
        ICollection<FundDailyBalanceReadModel> balances =
        [
            new FundDailyBalanceReadModel(1, StartDate, 100m),
            new FundDailyBalanceReadModel(1, StartDate.AddDays(1), 0m),
            new FundDailyBalanceReadModel(1, StartDate.AddDays(2), 90m)
        ];

        FundQueryCalculations.CalculateSharpeRatio(balances).Should().Be(0);
    }

    [Fact]
    public void CalculateSharpeRatio_MatchesSampleVarianceSemantics()
    {
        ICollection<FundDailyBalanceReadModel> balances =
        [
            new FundDailyBalanceReadModel(1, StartDate, 100m),
            new FundDailyBalanceReadModel(1, StartDate.AddDays(1), 103m),
            new FundDailyBalanceReadModel(1, StartDate.AddDays(2), 101m),
            new FundDailyBalanceReadModel(1, StartDate.AddDays(3), 105m),
            new FundDailyBalanceReadModel(1, StartDate.AddDays(4), 104m)
        ];
        var returns = new[]
        {
            (100d - 103d) / 103d,
            (103d - 101d) / 101d,
            (101d - 105d) / 105d,
            (105d - 104d) / 104d
        };
        var mean = returns.Average();
        var variance = returns.Sum(value => (value - mean) * (value - mean)) / (returns.Length - 1);
        var expected = mean / Math.Sqrt(variance) * Math.Sqrt(252);

        FundQueryCalculations.CalculateSharpeRatio(balances).Should().BeApproximately(expected, 1e-12);
    }

    [Fact]
    public async Task CreateFundTransaction_CalculatesBalanceBeforeConstructingEvent()
    {
        var db = Substitute.For<IFundDbContext>();
        db.GetFundBalanceAsync(1).Returns(100m);
        var state = new FundTransactionCommandState(db);
        var transaction = CreateTransaction(balance: 0m) with
        {
            TransactionType = FundTransactionType.CashDeposit,
            TradeStatus = TradeStatus.Open,
            Amount = 5m
        };
        var command = new CreateFundTransactionCommand(transaction)
        {
            CommandId = Guid.NewGuid()
        };

        var result = await command.ExecuteAsync(state);

        result.Success.Should().BeTrue();
        var domainEvent = state.Events.Should().ContainSingle().Subject.Should().BeOfType<FundTransactionEvent>().Subject;
        domainEvent.FundTransaction.Balance.Should().Be(105m);
    }

    [Fact]
    public void ReplayEndOfDayEvent_DoesNotMutateEventPayload()
    {
        var db = Substitute.For<IFundDbContext>();
        var state = new FundTransactionCommandState(db);
        var transaction = CreateTransaction(balance: 125m);
        var domainEvent = new EndOfDayFundTransactionProcessedEvent
        {
            EntityId = transaction.EntityId,
            FundTransaction = transaction
        };

        state.Update(domainEvent, addEvent: false).Should().BeTrue();

        domainEvent.FundTransaction.Should().BeSameAs(transaction);
        domainEvent.FundTransaction.Balance.Should().Be(125m);
    }

    static FundTransactionReadModel CreateTransaction(decimal balance)
        => new(
            transactionId: 1,
            transactionDate: DateTime.UtcNow,
            transactionType: FundTransactionType.UnrealizedTradePnl,
            fundId: 1,
            orderId: 1,
            tradeId: 1,
            tradeType: TradeType.ShortPut,
            valueDate: StartDate,
            tradeStatus: TradeStatus.EndOfDay,
            description: string.Empty,
            amount: 5m,
            balance: balance);

    sealed class ConcurrencyProbe
    {
        int _active;
        int _maximum;

        public int MaximumConcurrency => Volatile.Read(ref _maximum);

        public async Task<T> TrackAsync<T>(T result)
        {
            var active = Interlocked.Increment(ref _active);
            UpdateMaximum(active);
            try
            {
                await Task.Delay(20).ConfigureAwait(false);
                return result;
            }
            finally
            {
                Interlocked.Decrement(ref _active);
            }
        }

        void UpdateMaximum(int value)
        {
            var current = Volatile.Read(ref _maximum);
            while (value > current)
            {
                var observed = Interlocked.CompareExchange(ref _maximum, value, current);
                if (observed == current)
                    return;
                current = observed;
            }
        }
    }
}
