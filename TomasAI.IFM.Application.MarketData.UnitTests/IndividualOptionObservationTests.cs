using MessagePack;
using TomasAI.IFM.Application.MarketData.Databento.Resiliency;
using NSubstitute;
using TomasAI.IFM.Application.MarketData.Databento;
using TomasAI.IFM.Application.MarketData.Databento.Workers;
using TomasAI.IFM.Application.MarketData.MarketOutlook;
using TomasAI.IFM.Application.MarketData.Pricing;
using TomasAI.IFM.Application.MarketData.Worker;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation.Events;
using TomasAI.IFM.Framework.MarketData.Contracts.LastPrice;
using TomasAI.IFM.Framework.MarketData.Contracts.TickAggregation;
using TomasAI.IFM.Framework.MarketData.DataBento.OptionChain;
using static TomasAI.IFM.Application.MarketData.UnitTests.OrderCompositionPricingPrerequisiteTests;

namespace TomasAI.IFM.Application.MarketData.UnitTests;

public sealed class IndividualOptionObservationTests
{
    [Fact]
    public async Task Real_quote_crosses_worker_transport_and_generation_fence_without_inventing_a_trade()
    {
        using var bytes = new MemoryStream();
        await using var pipe = new PipeDatasetWorkerPublisher(bytes, "GLBX.MDP3", new(2026,9,8), Guid.NewGuid(), 1);
        await pipe.StartAsync(); await pipe.BindGenerationAsync(Generation, default);
        var inputs = new OptionChainPricingInputStore(); inputs.Set(new(Context(), Quote("ES-future", 5000)));
        var observations = new IndividualOptionObservationPublisher(Generation, inputs, pipe, _ => true);
        await observations.PublishAsync(QuoteEvent());
        bytes.Position = 0;
        var envelope = await DatasetPublicationFrameCodec.ReadAsync(bytes, default);
        Assert.Equal(DatasetPublicationKind.OptionQuoteObservation, envelope.Kind);
        var changed = MessagePackSerializer.Deserialize<FuturesTickTradeDataChangedEvent>(envelope.Payload);
        Assert.Equal(default, changed.TradeData);
        Assert.Equal(100, changed.OptionMarketPriceObservation!.OptionTickData.OptionPrice);
        Assert.Equal(OptionMarketPriceBasis.QuoteMidpoint, changed.OptionMarketPriceObservation.PriceBasis);
        Assert.False(changed.OptionMarketPriceObservation.OptionTickData.GreeksAvailable);
        Assert.Equal(At.UtcDateTime, changed.OptionMarketPriceObservation.EventAtUtc);
        using var mirror = new DatasetWorkerCurrentValues();
        var admission = new DatasetWorkerAdmission(envelope.Dataset, envelope.ValueDate, envelope.WorkerInstanceId, Generation, 1);
        mirror.ActivateDataset(admission, [new DatabentoContractRegistration
        { DomainContractId = "ES-future", ProviderContractName = "ESZ6", Dataset = "GLBX.MDP3", AssetTypeId = AssetTypeId.Futures }]);
        var admissions = new DatasetWorkerAdmissionRegistry(); admissions.Admit(admission);
        var downstream = Substitute.For<ITickAggregationEventPublisher>();
        var ingress = new DatasetPublicationIngress(admissions, downstream, Substitute.For<IMarketDataOperationsRecorder>(), mirror);
        Assert.True(await ingress.AcceptAsync(envelope));
        await downstream.Received(1).PublishAsync(Arg.Is<FuturesTickTradeDataChangedEvent>(value =>
            value.Id == changed.Id && value.SourceGenerationId == Generation && value.OptionMarketPriceObservation == changed.OptionMarketPriceObservation), Arg.Any<CancellationToken>());
        Assert.False(mirror.GetFuturesReader("ES-future").TryGetLastTrade(out _));
        Assert.False(await ingress.AcceptAsync(envelope));
        Assert.False(await ingress.AcceptAsync(envelope with { GenerationId = Guid.NewGuid(), PublicationSequence = 2 }));
    }

    [Theory]
    [InlineData(false, 99, 101)]
    [InlineData(true, 0, 101)]
    [InlineData(true, 102, 101)]
    public async Task Chain_wide_missing_or_crossed_quotes_do_not_enter_position_monitoring(bool individual, int bid, int ask)
    {
        var inputs = new OptionChainPricingInputStore(); inputs.Set(new(Context(), Quote("ES-future", 5000)));
        var downstream = Substitute.For<ITickAggregationEventPublisher>();
        var observations = new IndividualOptionObservationPublisher(Generation, inputs, downstream, _ => individual);
        var quote = QuoteEvent();
        await observations.PublishAsync(quote with { Tick = quote.Tick with { BidPrice = bid, AskPrice = ask } });
        Assert.Empty(downstream.ReceivedCalls());
    }

    [Fact]
    public async Task Option_quote_requires_an_admitted_underlying_and_matching_session()
    {
        using var bytes = new MemoryStream();
        await using var pipe = new PipeDatasetWorkerPublisher(bytes, "GLBX.MDP3", new(2026,9,8), Guid.NewGuid(), 1);
        await pipe.StartAsync(); await pipe.BindGenerationAsync(Generation, default);
        var inputs = new OptionChainPricingInputStore(); inputs.Set(new(Context(), Quote("ES-future", 5000)));
        await new IndividualOptionObservationPublisher(Generation, inputs, pipe, _ => true).PublishAsync(QuoteEvent());
        bytes.Position = 0;
        var envelope = await DatasetPublicationFrameCodec.ReadAsync(bytes, default);
        using var mirror = new DatasetWorkerCurrentValues();
        mirror.ActivateDataset(new(envelope.Dataset, envelope.ValueDate, envelope.WorkerInstanceId, Generation, 1),
            [new DatabentoContractRegistration { DomainContractId = "OTHER", ProviderContractName = "OTHER", Dataset = "GLBX.MDP3", AssetTypeId = AssetTypeId.Futures }]);
        Assert.False(mirror.AcceptPublication(envelope));
        Assert.False(mirror.AcceptPublication(envelope with { ValueDate = envelope.ValueDate.AddDays(1) }));
    }

    [Fact]
    public async Task Repeated_depth_updates_do_not_build_position_backlog_but_actual_quotes_and_freshness_are_preserved()
    {
        var clock = new QuoteClock();
        var inputs = new OptionChainPricingInputStore(); inputs.Set(new(Context(), Quote("ES-future", 5000)));
        var downstream = Substitute.For<ITickAggregationEventPublisher>();
        var observations = new IndividualOptionObservationPublisher(Generation, inputs, downstream, _ => true, clock);
        var quote = QuoteEvent();
        await observations.PublishAsync(quote);
        for (var index = 0; index < 1000; index++)
            await observations.PublishAsync(quote with { EventId = Guid.NewGuid(), Tick = quote.Tick with
                { SourceSequence = 43 + index, BidSize = (uint)(index + 1) } });
        await downstream.Received(1).PublishAsync(Arg.Any<FuturesTickTradeDataChangedEvent>());
        var changedQuote = quote with { EventId = Guid.NewGuid(), Tick = quote.Tick with { BidPrice = 100 } };
        await observations.PublishAsync(changedQuote);
        await downstream.Received(2).PublishAsync(Arg.Any<FuturesTickTradeDataChangedEvent>());
        clock.Advance(TimeSpan.FromMilliseconds(999));
        await observations.PublishAsync(changedQuote);
        await downstream.Received(2).PublishAsync(Arg.Any<FuturesTickTradeDataChangedEvent>());
        clock.Advance(TimeSpan.FromMilliseconds(1));
        await observations.PublishAsync(changedQuote);
        await downstream.Received(3).PublishAsync(Arg.Any<FuturesTickTradeDataChangedEvent>());
        await downstream.Received().PublishAsync(Arg.Is<FuturesTickTradeDataChangedEvent>(e =>
            e.ReceivedOn == At.AddSeconds(1).UtcDateTime && e.OptionMarketPriceObservation!.OptionTickData.OptionPrice == 100.5));
    }

    sealed class QuoteClock : TimeProvider
    {
        DateTimeOffset now = At;
        public override DateTimeOffset GetUtcNow() => now;
        public override long TimestampFrequency => TimeSpan.TicksPerSecond;
        public override long GetTimestamp() => (now-At).Ticks;
        public void Advance(TimeSpan interval) => now += interval;
    }

    static FuturesOptionChainQuoteChangedServiceEvent QuoteEvent() => new(Guid.NewGuid(), "ES-future", "ES-option-call",
        new(2026,9,8), new(2026,10,2), new LastQuoteTickSnapshot("ES-option-call", new(2026,9,8), 99, 2, 1, 101, 3, 1, 42, At, At), default);
}
