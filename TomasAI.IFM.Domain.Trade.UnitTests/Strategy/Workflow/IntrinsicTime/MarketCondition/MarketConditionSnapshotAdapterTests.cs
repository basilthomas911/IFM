using FluentAssertions;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.MarketCondition.Model;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Configuration.MarketCondition;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.MarketCondition.Model;
using Xunit;

namespace TomasAI.IFM.Domain.Trade.UnitTests.Strategy.Workflow.IntrinsicTime.MarketCondition;

public sealed class MarketConditionSnapshotAdapterTests
{
    [Fact]
    public void Provider_native_floating_point_values_are_normalized_once()
    {
        var observation = Observation("FuturesQuote", new DateTime(2026, 8, 28, 14, 0, 0, DateTimeKind.Utc));
        var value = MarketConditionSnapshotAssembler.Normalize(new MarketConditionRawFuturesQuote
        {
            BidPrice = 6500d, AskPrice = 6500.25d, BidSize = 12d, AskSize = 13d,
            LastPrice = 6500.25d, OneMinuteMoveAtr = 0.125d,
            QuoteObservation = observation, TradeObservation = observation with { SourceId = "FuturesTrade" }
        });

        value.BidPrice.Should().Be(6500m);
        value.AskPrice.Should().Be(6500.25m);
        value.OneMinuteMoveAtr.Should().Be(0.125m);
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void Non_finite_provider_values_fail_capture_normalization(double value)
    {
        var action = () => MarketConditionSnapshotAssembler.Normalize(new MarketConditionRawFuturesQuote
            { BidPrice = value });

        action.Should().Throw<MarketConditionCalculationException>()
            .Which.Category.Should().Be(MarketConditionFailureCategory.RequiredInputInvalid);
    }

    [Fact]
    public void Option_universe_is_bounded_by_authoritative_metadata_and_aggregated_deterministically()
    {
        var at = new DateTime(2026, 8, 28, 14, 0, 0, DateTimeKind.Utc);
        var date = DateOnly.FromDateTime(at);
        var values = Enumerable.Range(0, 14).Select(index => new MarketConditionRawOptionContract
        {
            ContractId = $"ES-{index:00}", InstrumentRoot = index == 13 ? "NQ" : "ES",
            ExpirationDate = index == 12 ? date.AddDays(60) : date.AddDays(7),
            OptionType = index % 2 == 0 ? "Call" : "Put",
            StrikePrice = index == 11 ? 106d : 100d,
            BidPrice = index == 0 ? 0d : 1d, AskPrice = 1.2d,
            BidSize = 2d, AskSize = 4d, UnderlyingPrice = 100.1d,
            Observation = Observation("OptionFeed", at.AddSeconds(-1)) with { SequenceId = index + 1 }
        }).Reverse().ToArray();

        var result = MarketConditionSnapshotAssembler.AggregateOptions(values, 100m, date,
            MarketConditionV1Tests.Parameters(Domain.MarketData.Analytics.Shared.TimeFrameType.Weekly).OptionLiquidity);

        result.CandidateContractCount.Should().Be(11);
        result.ValidQuoteCount.Should().Be(10);
        result.ValidQuoteCoverage.Should().Be(0.909091m);
        result.EligibleExpirationCount.Should().Be(1);
        result.HasCalls.Should().BeTrue();
        result.HasPuts.Should().BeTrue();
        result.MedianRelativeSpread.Should().Be(0.181818m);
        result.P90RelativeSpread.Should().Be(0.181818m);
        result.MedianBidSize.Should().Be(2m);
        result.MedianAskSize.Should().Be(4m);
        result.UnderlyingMismatch.Should().Be(0.001m);
        result.Observation.SourceTimestampUtc.Should().Be(at.AddSeconds(-1));
        result.Observation.SequenceId.Should().Be(11);

        var shuffled = MarketConditionSnapshotAssembler.AggregateOptions(values.OrderBy(_ => Guid.NewGuid()),
            100m, date, MarketConditionV1Tests.Parameters(Domain.MarketData.Analytics.Shared.TimeFrameType.Weekly).OptionLiquidity);
        shuffled.Should().BeEquivalentTo(result);
    }

    [Fact]
    public void Missing_individual_option_quotes_degrade_coverage_without_invalidating_the_chain()
    {
        var at = new DateTime(2026, 8, 28, 14, 0, 0, DateTimeKind.Utc);
        var values = Enumerable.Range(0, 12).Select(index => new MarketConditionRawOptionContract
        {
            ContractId = $"ES-{index}", InstrumentRoot = "ES",
            ExpirationDate = DateOnly.FromDateTime(at).AddDays(7),
            OptionType = index % 2 == 0 ? "Call" : "Put", StrikePrice = 100d,
            BidPrice = index == 0 ? 0d : 1d, AskPrice = index == 0 ? 0d : 1.1d,
            BidSize = 2d, AskSize = 2d, UnderlyingPrice = index == 0 ? 0d : 100d,
            Observation = index == 0
                ? new MarketSourceObservation
                {
                    SourceId = "FuturesOptionFeed", Availability = MarketSourceAvailability.Unavailable,
                    Validity = MarketSourceValidity.Valid
                }
                : Observation("FuturesOptionFeed", at.AddSeconds(-1))
        });

        var result = MarketConditionSnapshotAssembler.AggregateOptions(values, 100m, DateOnly.FromDateTime(at),
            MarketConditionV1Tests.Parameters(Domain.MarketData.Analytics.Shared.TimeFrameType.Weekly).OptionLiquidity);

        result.ValidQuoteCount.Should().Be(11);
        result.ValidQuoteCoverage.Should().Be(0.916667m);
        result.Observation.Availability.Should().Be(MarketSourceAvailability.Degraded);
        result.Observation.Validity.Should().Be(MarketSourceValidity.Valid);
    }

    [Fact]
    public void Snapshot_and_result_array_contracts_are_defensively_copied()
    {
        var health = new[] { new MarketConditionOperationalHealthItem { SourceId = "PrimaryFuturesFeed" } };
        var snapshot = new MarketConditionSnapshot { OperationalHealth = health };
        health[0] = health[0] with { SourceId = "mutated-input" };
        var returned = snapshot.OperationalHealth;
        returned[0] = returned[0] with { SourceId = "mutated-output" };
        snapshot.OperationalHealth[0].SourceId.Should().Be("PrimaryFuturesFeed");

        var reasons = new[] { "A" };
        var result = new MarketConditionResult { Reasons = reasons };
        reasons[0] = "B";
        var resultReturned = result.Reasons;
        resultReturned[0] = "C";
        result.Reasons.Should().Equal("A");
    }

    [Fact]
    public async Task Coordinator_reads_each_adapter_once_and_returns_one_sealed_atomic_snapshot()
    {
        var at = new DateTime(2026, 8, 28, 14, 0, 0, DateTimeKind.Utc);
        var adapters = new AdapterFixture(at);
        var coordinator = new MarketConditionSnapshotAdapterCoordinator(
            adapters, adapters, adapters, adapters, adapters, adapters);
        var command = MarketConditionFunctionExecutionTests.Command(at.AddSeconds(4));

        var result = await coordinator.PublishAsync(command, new MarketConditionWorkflowEligibilityState
        {
            EntriesEnabled = true, RegimeProducedAtUtc = at.AddSeconds(-2),
            TriggerProducedAtUtc = at.AddSeconds(-1)
        }, at);

        adapters.Calls.Values.Should().OnlyContain(x => x == 1);
        adapters.Calls.Should().HaveCount(6);
        result.OptionChainQuality.CandidateContractCount.Should().Be(12);
        result.DataQualityItems.Should().HaveCount(10);
        result.SnapshotSha256.Should().Be(MarketConditionSnapshotHash.Compute(result));
    }

    static MarketSourceObservation Observation(string source, DateTime at) => new()
    {
        SourceId = source, SourceTimestampUtc = at, ReceivedAtUtc = at, SequenceId = 1,
        Availability = MarketSourceAvailability.Available, Validity = MarketSourceValidity.Valid
    };

    sealed class AdapterFixture(DateTime at) : IMarketConditionFuturesQuoteAdapter,
        IMarketConditionOptionUniverseAdapter, IMarketConditionSessionAdapter,
        IMarketConditionEventRiskAdapter, IMarketConditionVolatilityAdapter,
        IMarketConditionOperationalHealthAdapter
    {
        public Dictionary<string, int> Calls { get; } = [];
        void Called(string name) => Calls[name] = Calls.GetValueOrDefault(name) + 1;

        ValueTask<MarketConditionRawFuturesQuote> IMarketConditionFuturesQuoteAdapter.ReadOnceAsync(
            string instrumentRoot, DateTime evaluationTimestampUtc, CancellationToken cancellationToken)
        {
            Called("futures");
            return ValueTask.FromResult(new MarketConditionRawFuturesQuote
            {
                BidPrice = 6500d, AskPrice = 6500.25d, BidSize = 10d, AskSize = 10d,
                LastPrice = 6500d, OneMinuteMoveAtr = 0.1d,
                QuoteObservation = Observation("FuturesQuote", at.AddSeconds(-1)),
                TradeObservation = Observation("FuturesTrade", at.AddSeconds(-1))
            });
        }

        ValueTask<IReadOnlyCollection<MarketConditionRawOptionContract>>
            IMarketConditionOptionUniverseAdapter.ReadOnceAsync(string instrumentRoot,
                decimal futuresUnderlyingPrice, MarketConditionOptionLiquidityConfiguration configuration,
                DateTime evaluationTimestampUtc, CancellationToken cancellationToken)
        {
            Called("options");
            IReadOnlyCollection<MarketConditionRawOptionContract> values = Enumerable.Range(0, 12).Select(index => new MarketConditionRawOptionContract
            {
                ContractId = $"ES-{index}", InstrumentRoot = "ES",
                ExpirationDate = DateOnly.FromDateTime(at).AddDays(7),
                OptionType = index % 2 == 0 ? "Call" : "Put", StrikePrice = 6500d,
                BidPrice = 1d, AskPrice = 1.1d, BidSize = 2d, AskSize = 2d,
                UnderlyingPrice = 6500d, Observation = Observation("OptionFeed", at.AddSeconds(-1))
            }).ToArray();
            return ValueTask.FromResult(values);
        }

        public ValueTask<MarketConditionSessionState> ReadOnceAsync(string instrumentRoot,
            MarketConditionSessionConfiguration configuration, DateTime evaluationTimestampUtc,
            CancellationToken cancellationToken)
        {
            Called("session");
            return ValueTask.FromResult(new MarketConditionSessionState
            {
                Status = MarketSessionStatus.Open, IsEntryWindow = true,
                ExchangeLocalTime = new TimeSpan(11, 0, 0), ExchangeLocalWeekday = DayOfWeek.Friday,
                Observation = Observation("Session", at.AddSeconds(-1))
            });
        }

        public ValueTask<MarketConditionEventRiskState> ReadOnceAsync(
            MarketConditionEventRiskConfiguration configuration, DateTime evaluationTimestampUtc,
            CancellationToken cancellationToken)
        {
            Called("events");
            return ValueTask.FromResult(new MarketConditionEventRiskState
                { Status = MarketEventRiskStatus.Clear, Observation = Observation("EventRisk", at.AddSeconds(-1)) });
        }

        public ValueTask<MarketConditionVolatilityShockState> ReadOnceAsync(string instrumentRoot,
            DateTime evaluationTimestampUtc, CancellationToken cancellationToken)
        {
            Called("volatility");
            return ValueTask.FromResult(new MarketConditionVolatilityShockState
                { Observation = Observation("Volatility", at.AddSeconds(-1)) });
        }

        public ValueTask<IReadOnlyCollection<MarketConditionOperationalHealthItem>> ReadOnceAsync(
            IReadOnlyCollection<string> requiredSources, DateTime evaluationTimestampUtc,
            CancellationToken cancellationToken)
        {
            Called("health");
            IReadOnlyCollection<MarketConditionOperationalHealthItem> values = requiredSources.Select(source =>
                new MarketConditionOperationalHealthItem
                {
                    SourceId = source, Status = MarketOperationalStatus.Healthy,
                    Observation = Observation(source, at.AddSeconds(-1))
                }).ToArray();
            return ValueTask.FromResult(values);
        }
    }
}
