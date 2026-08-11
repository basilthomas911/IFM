using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using TomasAI.IFM.Framework.SequenceId;
using TomasAI.IFM.Framework.SequenceId.Postgres;
using Xunit;

namespace TomasAI.IFM.Application.Storage.IntegrationTests.SequenceIdDb;

public class PostgresSequenceIdGeneratorTests
{
    [Theory]
    [InlineData("FundId", SequenceName.Fund_FundId)]
    [InlineData("OrderId", SequenceName.Trade_OrderId)]
    [InlineData("TradeId", SequenceName.Trade_TradeId)]
    [InlineData("ScheduledJobId", SequenceName.ScheduledJob_JobId)]
    public void MapsLegacyAndTypedSequenceNames(string value, SequenceName expected)
        => SequenceNameExtensions.ParseSequenceName(value).Should().Be(expected);

    [Fact]
    public void RejectsAnUnregisteredSequenceName()
    {
        var parse = () => SequenceNameExtensions.ParseSequenceName("arbitrary-counter");

        parse.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public async Task UsesOnePostgresReservationPerAllocationRange()
    {
        var db = new FakeSequenceIdDbContext();
        var generator = new PostgresSequenceIdGenerator(db);

        var sequenceIds = new List<long>(SequenceIdSettings.AllocationSize + 1);
        for (var index = 0; index <= SequenceIdSettings.AllocationSize; index++)
            sequenceIds.Add(await generator.GetSequenceIdAsync(SequenceName.TradePlan_SequenceId));

        sequenceIds.Should().Equal(Enumerable.Range(1, SequenceIdSettings.AllocationSize + 1)
            .Select(static value => (long)value));
        db.GetReservationCount(SequenceName.TradePlan_SequenceId).Should().Be(2);
    }

    [Fact]
    public async Task AllocatesUniqueIdsUnderHighConcurrency()
    {
        const int requestCount = 20_000;
        var db = new FakeSequenceIdDbContext();
        var generator = new PostgresSequenceIdGenerator(db);

        var tasks = Enumerable.Range(0, requestCount)
            .Select(async _ => await generator.GetSequenceIdAsync(
                SequenceName.FuturesTickData_TickId))
            .ToArray();
        var sequenceIds = await Task.WhenAll(tasks);

        sequenceIds.Should().OnlyHaveUniqueItems();
        sequenceIds.Min().Should().Be(1);
        sequenceIds.Max().Should().Be(requestCount);
        db.GetReservationCount(SequenceName.FuturesTickData_TickId)
            .Should().Be(requestCount / SequenceIdSettings.AllocationSize);
    }

    [Fact]
    public async Task MultipleGeneratorInstancesReceiveDisjointRanges()
    {
        const int generatorCount = 4;
        const int requestsPerGenerator = 2_500;
        var db = new FakeSequenceIdDbContext();
        PostgresSequenceIdGenerator[] generators =
            [.. Enumerable.Range(0, generatorCount)
                .Select(_ => new PostgresSequenceIdGenerator(db))];

        var tasks = generators.SelectMany(generator =>
                Enumerable.Range(0, requestsPerGenerator)
                    .Select(async _ => await generator.GetSequenceIdAsync(
                        SequenceName.OptionTradeSpreadData_SequenceId)))
            .ToArray();
        var sequenceIds = await Task.WhenAll(tasks);

        sequenceIds.Should().HaveCount(generatorCount * requestsPerGenerator);
        sequenceIds.Should().OnlyHaveUniqueItems();
        db.GetReservationCount(SequenceName.OptionTradeSpreadData_SequenceId)
            .Should().Be(generatorCount * requestsPerGenerator / SequenceIdSettings.AllocationSize);
    }

    [Fact]
    public async Task MaintainsIndependentRangesForEachNamedSequence()
    {
        var db = new FakeSequenceIdDbContext();
        var generator = new PostgresSequenceIdGenerator(db);

        var tradePlanId = await generator.GetSequenceIdAsync(SequenceName.TradePlan_SequenceId);

        tradePlanId.Should().Be(1);
        db.GetReservationCount(SequenceName.TradePlan_SequenceId).Should().Be(1);
    }

    [Fact]
    public async Task ReportsThePostgresReservedHighWatermark()
    {
        var db = new FakeSequenceIdDbContext();
        var generator = new PostgresSequenceIdGenerator(db);

        await generator.GetSequenceIdAsync(SequenceName.SpreadDistribution_Id);

        var highWatermark = await generator.GetHighWatermarkAsync(
            SequenceName.SpreadDistribution_Id);
        highWatermark.Should().Be(SequenceIdSettings.AllocationSize);
    }

    [Fact]
    public async Task DatabaseFailureDoesNotPoisonSequenceState()
    {
        var attempts = 0;
        var db = new FakeSequenceIdDbContext((sequenceName, cancellationToken) =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (Interlocked.Increment(ref attempts) == 1)
                return Task.FromException<long>(new InvalidOperationException("database unavailable"));
            return Task.FromResult(1L);
        });
        var generator = new PostgresSequenceIdGenerator(db);

        var failedAllocation = async () =>
            await generator.GetSequenceIdAsync(SequenceName.TelemetryLog_SequenceId);
        await failedAllocation.Should().ThrowAsync<InvalidOperationException>();

        var sequenceId = await generator.GetSequenceIdAsync(SequenceName.TelemetryLog_SequenceId);
        sequenceId.Should().Be(1);
    }

    [Fact]
    public async Task CancellationBeforeAllocationDoesNotReserveARange()
    {
        var db = new FakeSequenceIdDbContext();
        var generator = new PostgresSequenceIdGenerator(db);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        var allocation = async () => await generator.GetSequenceIdAsync(
            SequenceName.FundTransaction_TransactionId,
            cancellation.Token);

        await allocation.Should().ThrowAsync<OperationCanceledException>();
        db.GetReservationCount(SequenceName.FundTransaction_TransactionId).Should().Be(0);
    }

    [Fact]
    public async Task RejectsARangeThatWouldOverflowInt64()
    {
        var db = new FakeSequenceIdDbContext((_, _) =>
            Task.FromResult(long.MaxValue - SequenceIdSettings.AllocationSize + 2L));
        var generator = new PostgresSequenceIdGenerator(db);

        var allocation = async () => await generator.GetSequenceIdAsync(
            SequenceName.StreamingRequest_RequestId);

        await allocation.Should().ThrowAsync<OverflowException>();
    }

    [Fact]
    public async Task FinalValidInt64RangeEndsWithoutWrappingNegative()
    {
        var reservations = 0;
        var db = new FakeSequenceIdDbContext((_, _) =>
            Interlocked.Increment(ref reservations) == 1
                ? Task.FromResult(long.MaxValue - SequenceIdSettings.AllocationSize + 1L)
                : Task.FromException<long>(new OverflowException("sequence exhausted")));
        var generator = new PostgresSequenceIdGenerator(db);

        long last = 0;
        for (var index = 0; index < SequenceIdSettings.AllocationSize; index++)
            last = await generator.GetSequenceIdAsync(SequenceName.TelemetryLog_SequenceId);

        last.Should().Be(long.MaxValue);
        var exhausted = async () => await generator.GetSequenceIdAsync(
            SequenceName.TelemetryLog_SequenceId);
        await exhausted.Should().ThrowAsync<OverflowException>();
    }

    [Fact]
    public async Task RejectsAPostgresSequenceWithTheWrongAllocationSize()
    {
        var db = new FakeSequenceIdDbContext { AllocationSize = 1 };
        var generator = new PostgresSequenceIdGenerator(db);

        var allocation = async () => await generator.GetSequenceIdAsync(
            SequenceName.TradePlacementSignal_SequenceId);

        await allocation.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*increment 1*requires 100*");
        db.GetReservationCount(SequenceName.TradePlacementSignal_SequenceId).Should().Be(0);
    }

    sealed class FakeSequenceIdDbContext : ISequenceIdDbContext
    {
        readonly Func<SequenceName, CancellationToken, Task<long>>? _reservation;
        readonly ConcurrentDictionary<SequenceName, long> _rangeStarts = new();
        readonly ConcurrentDictionary<SequenceName, int> _reservationCounts = new();

        internal FakeSequenceIdDbContext(
            Func<SequenceName, CancellationToken, Task<long>>? reservation = null)
            => _reservation = reservation;

        internal long AllocationSize { get; init; } = SequenceIdSettings.AllocationSize;

        public Task<long> GetSequenceAllocationSizeAsync(
            SequenceName sequenceName,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(AllocationSize);
        }

        public Task<long> GetNextSequenceIdAsync(
            SequenceName sequenceName,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _reservationCounts.AddOrUpdate(sequenceName, 1, static (_, count) => count + 1);
            if (_reservation is not null)
                return _reservation(sequenceName, cancellationToken);

            var rangeStart = _rangeStarts.AddOrUpdate(
                sequenceName,
                1L,
                static (_, current) => checked(current + SequenceIdSettings.AllocationSize));
            return Task.FromResult(rangeStart);
        }

        public Task<long> GetCurrentSequenceIdAsync(
            SequenceName sequenceName,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(_rangeStarts.TryGetValue(sequenceName, out var rangeStart)
                ? checked(rangeStart + SequenceIdSettings.AllocationSize - 1L)
                : 0L);
        }

        internal int GetReservationCount(SequenceName sequenceName)
            => _reservationCounts.GetValueOrDefault(sequenceName);
    }
}
