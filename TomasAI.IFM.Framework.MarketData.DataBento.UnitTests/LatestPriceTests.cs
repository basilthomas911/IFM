namespace TomasAI.IFM.Framework.MarketData.DataBento.UnitTests;

public sealed class LatestPriceTests
{
    private const string Dataset = "GLBX.MDP3";

    [Theory]
    [InlineData(LatestPricePolicy.LastTrade, LatestPriceResultFlags.TradeValid)]
    [InlineData(
        LatestPricePolicy.QuoteMidpoint,
        LatestPriceResultFlags.BidValid | LatestPriceResultFlags.AskValid)]
    [InlineData(LatestPricePolicy.Bid, LatestPriceResultFlags.BidValid)]
    [InlineData(LatestPricePolicy.Ask, LatestPriceResultFlags.AskValid)]
    public void EveryPricePolicyReturnsOnlyAQualifyingResult(
        LatestPricePolicy policy,
        LatestPriceResultFlags flags)
    {
        var control = new LatestPriceAdmissionControl();
        var sut = new DatabentoLatestPriceClient(
            Dataset,
            control,
            (_, _, _) => Result(policy, flags));

        var result = sut.GetLatestPrice(Request(policy), TimeSpan.FromSeconds(1));

        Assert.Equal(policy, result.SelectedPolicy);
        Assert.Equal(123, result.SelectedPrice);
        Assert.Equal(0, control.GetActiveSessionCount(Dataset));
    }

    [Fact]
    public void NativeFailureAlwaysReleasesDatasetSessionPermit()
    {
        var control = new LatestPriceAdmissionControl();
        var sut = new DatabentoLatestPriceClient(
            Dataset,
            control,
            (_, _, _) => throw new DatabentoFeedTimeoutException("expected"));

        Assert.Throws<DatabentoFeedTimeoutException>(() =>
            sut.GetLatestPrice(Request(), TimeSpan.FromSeconds(1)));
        Assert.Equal(0, control.GetActiveSessionCount(Dataset));
    }

    [Fact]
    public void CrossedMidpointResultIsRejectedAsAbiMismatch()
    {
        var sut = new DatabentoLatestPriceClient(
            Dataset,
            new LatestPriceAdmissionControl(),
            (_, _, _) => new LatestPriceResult64(
                1,
                2,
                LatestPricePolicy.QuoteMidpoint,
                LatestPriceResultFlags.BidValid | LatestPriceResultFlags.AskValid,
                101,
                bidPrice: 102,
                askPrice: 100));

        var exception = Assert.Throws<DatabentoFeedException>(() =>
            sut.GetLatestPrice(
                Request(LatestPricePolicy.QuoteMidpoint),
                TimeSpan.FromSeconds(1)));

        Assert.Equal(DatabentoFeedStatus.AbiMismatch, exception.Status);
    }

    [Fact]
    public void ReplayFreshnessPassesReplayResultWithoutPolicyFallback()
    {
        LatestPriceRequest? observed = null;
        var sut = new DatabentoLatestPriceClient(
            Dataset,
            new LatestPriceAdmissionControl(),
            (_, request, _) =>
            {
                observed = request;
                return Result(
                    LatestPricePolicy.Bid,
                    LatestPriceResultFlags.BidValid
                    | LatestPriceResultFlags.ReplayContributed);
            });
        var request = Request(LatestPricePolicy.Bid) with
        {
            FreshnessPolicy = LatestPriceFreshnessPolicy.ReplayLookbackThenLive,
            ReplayLookback = TimeSpan.FromMinutes(5)
        };

        var result = sut.GetLatestPrice(request, TimeSpan.FromSeconds(1));

        Assert.Same(request, observed);
        Assert.True(result.UsedReplay);
        Assert.False(result.IsLive);
    }

    [Theory]
    [InlineData((LatestPricePolicy)0, LatestPriceFreshnessPolicy.NextObserved)]
    [InlineData(LatestPricePolicy.LastTrade, (LatestPriceFreshnessPolicy)0)]
    public void InvalidPoliciesAreRejectedBeforeOpeningSession(
        LatestPricePolicy pricePolicy,
        LatestPriceFreshnessPolicy freshnessPolicy)
    {
        var called = false;
        var sut = new DatabentoLatestPriceClient(
            Dataset,
            new LatestPriceAdmissionControl(),
            (_, _, _) =>
            {
                called = true;
                return Result();
            });
        var request = Request(pricePolicy) with { FreshnessPolicy = freshnessPolicy };

        Assert.Throws<ArgumentException>(() =>
            sut.GetLatestPrice(request, TimeSpan.FromSeconds(1)));
        Assert.False(called);
    }

    [Fact]
    public void ReplayLookbackMustMatchFreshnessPolicy()
    {
        var sut = new DatabentoLatestPriceClient(
            Dataset,
            new LatestPriceAdmissionControl(),
            (_, _, _) => Result());

        Assert.Throws<ArgumentException>(() => sut.GetLatestPrice(
            Request() with { ReplayLookback = TimeSpan.FromSeconds(1) },
            TimeSpan.FromSeconds(1)));
        Assert.Throws<ArgumentOutOfRangeException>(() => sut.GetLatestPrice(
            Request() with
            {
                FreshnessPolicy = LatestPriceFreshnessPolicy.ReplayLookbackThenLive
            },
            TimeSpan.FromSeconds(1)));
    }

    [Fact]
    public void DatasetSessionBudgetAllowsOnlyOneTemporarySession()
    {
        var clock = new FakeAdmissionClock();
        var control = new LatestPriceAdmissionControl(clock: clock);
        var first = control.Acquire(Dataset, TimeSpan.FromSeconds(1));
        clock.OnWait = first.Dispose;

        using var second = control.Acquire(Dataset, TimeSpan.FromSeconds(1));

        Assert.Equal(1, control.GetActiveSessionCount(Dataset));
    }

    [Fact]
    public void DatasetSessionBudgetTimesOutWithoutLeakingAPermit()
    {
        var clock = new FakeAdmissionClock();
        var control = new LatestPriceAdmissionControl(clock: clock);
        using var first = control.Acquire(Dataset, TimeSpan.FromSeconds(1));

        Assert.Throws<DatabentoFeedTimeoutException>(() =>
            control.Acquire(Dataset, TimeSpan.FromMilliseconds(10)));
        Assert.Equal(1, control.GetActiveSessionCount(Dataset));
    }

    [Fact]
    public void ConnectionStartGovernorAdmitsAtMostFiveStartsPerSecond()
    {
        var clock = new FakeAdmissionClock();
        var control = new LatestPriceAdmissionControl(clock: clock);
        for (var index = 0; index < 5; ++index)
        {
            using var lease = control.Acquire(
                $"DATASET-{index}", TimeSpan.FromSeconds(2));
        }

        using var sixth = control.Acquire("DATASET-5", TimeSpan.FromSeconds(2));

        Assert.Equal(TimeSpan.FromSeconds(1), clock.Elapsed);
    }

    [Fact]
    public void FactoryCreatesDedicatedLatestPriceClient()
    {
        var factory = new DatabentoFeedFactory(new LatestPriceAdmissionControl());
        var options = DatabentoFeedOptions.ForProfile(
            FeedDeploymentProfile.Development,
            Dataset);

        Assert.IsAssignableFrom<IDatabentoLatestPriceClient>(
            factory.CreateLatestPriceClient(options));
    }

    private static LatestPriceRequest Request(
        LatestPricePolicy policy = LatestPricePolicy.LastTrade) => new()
        {
            Dataset = Dataset,
            Symbol = "ESU6",
            PricePolicy = policy,
            FreshnessPolicy = LatestPriceFreshnessPolicy.NextObserved
        };

    private static LatestPriceResult64 Result(
        LatestPricePolicy policy = LatestPricePolicy.LastTrade,
        LatestPriceResultFlags flags = LatestPriceResultFlags.TradeValid) =>
        new(
            1,
            2,
            policy,
            flags,
            123,
            bidPrice: 122,
            askPrice: 124,
            lastTradePrice: 123,
            eventTimestampNanoseconds: 10,
            receiveTimestampNanoseconds: 11,
            bidSize: 3,
            askSize: 4);

    private sealed class FakeAdmissionClock : ILatestPriceAdmissionClock
    {
        private long _timestamp;

        internal Action? OnWait { get; set; }
        internal TimeSpan Elapsed => TimeSpan.FromTicks(_timestamp);

        public long GetTimestamp() => _timestamp;

        public TimeSpan GetElapsedTime(long startingTimestamp, long endingTimestamp) =>
            TimeSpan.FromTicks(endingTimestamp - startingTimestamp);

        public void Wait(object gate, TimeSpan timeout)
        {
            Monitor.Exit(gate);
            try
            {
                var onWait = OnWait;
                OnWait = null;
                _timestamp += onWait is null ? timeout.Ticks : 1;
                onWait?.Invoke();
            }
            finally
            {
                Monitor.Enter(gate);
            }
        }
    }
}
