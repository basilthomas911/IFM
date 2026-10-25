using MessagePack;
using NSubstitute;
using TomasAI.IFM.Application.MarketData.Contracts;
using TomasAI.IFM.Application.MarketData.Databento.Resiliency;
using TomasAI.IFM.Application.MarketData.Databento.Workers;
using TomasAI.IFM.Application.MarketData.MarketOutlook;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation.Events;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Framework.MarketData.Contracts.TickAggregation;

namespace TomasAI.IFM.Application.MarketData.UnitTests;

public sealed class OptionObservationCatchUpTests
{
    static readonly DateTimeOffset At = new(2026, 10, 23, 18, 0, 0, TimeSpan.Zero);
    static readonly Guid Generation = Guid.NewGuid(), Worker = Guid.NewGuid();
    static readonly DateOnly Date = new(2026, 10, 23);
    static DatasetWorkerAdmission Identity => new("GLBX.MDP3", Date, Worker, Generation, 1);

    [Fact]
    public async Task Early_quote_is_caught_up_after_admission_even_when_another_contract_advanced_and_first_contract_goes_quiet()
    {
        var retained = new LatestOptionObservations(); var admissions = new DatasetWorkerAdmissionRegistry();
        var published = new List<FuturesTickTradeDataChangedEvent>();
        var publisher = Substitute.For<ITickAggregationEventPublisher>();
        publisher.PublishAsync(Arg.Any<FuturesTickTradeDataChangedEvent>(), Arg.Any<CancellationToken>())
            .Returns(call => { published.Add(call.Arg<FuturesTickTradeDataChangedEvent>()); return ValueTask.CompletedTask; });
        var ingress = new DatasetPublicationIngress(admissions, publisher, Substitute.For<IMarketDataOperationsRecorder>());
        var early = Frame("quiet-C8100", 1, At);
        retained.Retain(early); Assert.False(await ingress.AcceptAsync(early));
        admissions.Admit(Identity);
        Assert.True(await ingress.AcceptAsync(Frame("active-P8000", 20, At.AddSeconds(1))));
        var snapshot = new MarketDataReadSnapshot(new(true, Date, []), [], [], [], OptionObservations: retained.Capture());
        snapshot = MessagePackSerializer.Deserialize<MarketDataReadSnapshot>(MessagePackSerializer.Serialize(snapshot));
        Assert.True(await ingress.AcceptAsync(Assert.Single(snapshot.OptionObservations!)));
        Assert.False(await ingress.AcceptAsync(early));
        Assert.Equal(2, published.Count);
        var quiet = published.Single(value => value.OptionMarketPriceObservation!.OptionTickData.ContractId == "quiet-C8100").OptionMarketPriceObservation!.OptionTickData;
        Assert.Equal(At, quiet.ObservedAtUtc); Assert.Equal(At, quiet.CalculatedAtUtc); Assert.Equal(Generation, quiet.SourceGeneration);
    }

    [Fact]
    public async Task Duplicate_old_quote_and_retired_generation_cannot_overwrite_newer_contract_value()
    {
        var retained = new LatestOptionObservations(); var admissions = new DatasetWorkerAdmissionRegistry(); admissions.Admit(Identity);
        var ingress = new DatasetPublicationIngress(admissions, Substitute.For<ITickAggregationEventPublisher>(), Substitute.For<IMarketDataOperationsRecorder>());
        var newest = Frame("C8100", 2, At.AddSeconds(1));
        Assert.True(retained.Retain(newest)); Assert.True(await ingress.AcceptAsync(newest));
        var stale = Frame("C8100", 3, At);
        Assert.False(retained.Retain(stale)); Assert.False(await ingress.AcceptAsync(stale));
        Assert.False(await ingress.AcceptAsync(newest));
        Assert.False(await ingress.AcceptAsync(newest with { GenerationId = Guid.NewGuid(), PublicationSequence = 4 }));
        Assert.Equal(newest, Assert.Single(retained.Capture()));
    }

    [Fact]
    public async Task Failed_delivery_does_not_consume_the_catch_up_value()
    {
        var admissions = new DatasetWorkerAdmissionRegistry(); admissions.Admit(Identity);
        var publisher = Substitute.For<ITickAggregationEventPublisher>(); var attempts = 0;
        publisher.PublishAsync(Arg.Any<FuturesTickTradeDataChangedEvent>(), Arg.Any<CancellationToken>())
            .Returns(_ => ++attempts == 1 ? ValueTask.FromException(new IOException("Injected unavailable publisher")) : ValueTask.CompletedTask);
        var ingress = new DatasetPublicationIngress(admissions, publisher, Substitute.For<IMarketDataOperationsRecorder>());
        var frame = Frame("C8100", 1, At);
        await Assert.ThrowsAsync<IOException>(async () => await ingress.AcceptAsync(frame));
        Assert.True(await ingress.AcceptAsync(frame)); Assert.False(await ingress.AcceptAsync(frame)); Assert.Equal(2, attempts);
    }

    [Fact]
    public void Replacement_generation_clears_old_legs_and_late_old_frames_cannot_replace_new_values()
    {
        var retained = new LatestOptionObservations(); var old = Frame("C8100", 1, At);
        retained.Retain(old); retained.Retain(Frame("old-only", 2, At));
        var next = Guid.NewGuid(); var tick = Tick(old) with { SourceGeneration = next };
        var changed = MessagePackSerializer.Deserialize<FuturesTickTradeDataChangedEvent>(old.Payload) with {
            OptionMarketPriceObservation = new("ES-future", OptionMarketPriceBasis.QuoteMidpoint, tick, 3, At.UtcDateTime) };
        var replacement = old with { GenerationId = next, PublicationSequence = 3, Payload = MessagePackSerializer.Serialize(changed) };
        Assert.True(retained.Retain(replacement)); Assert.False(retained.Retain(old with { PublicationSequence = 4 }));
        Assert.Equal(replacement, Assert.Single(retained.Capture()));
    }

    [Fact]
    public void Retention_replaces_values_and_rotating_snapshots_cover_more_than_one_page()
    {
        var retained = new LatestOptionObservations();
        for (var i = 0; i < 200; i++) Assert.True(retained.Retain(Frame("contract-" + i, i + 1, At)));
        Assert.True(retained.Retain(Frame("contract-0", 1000, At.AddSeconds(1))));
        var pages = retained.Capture().Concat(retained.Capture()).ToArray();
        Assert.Equal(200, pages.Select(value => Tick(value).ContractId).Distinct().Count());
        Assert.All(pages.Where(value => Tick(value).ContractId == "contract-0"), value => Assert.Equal(1000, value.PublicationSequence));
    }

    static FuturesOptionTickDataV2ReadModel Tick(DatasetPublicationEnvelope frame) => MessagePackSerializer.Deserialize<FuturesTickTradeDataChangedEvent>(frame.Payload).OptionMarketPriceObservation!.OptionTickData;
    static DatasetPublicationEnvelope Frame(string contract, long sequence, DateTimeOffset at)
    {
        var tick = new FuturesOptionTickDataV2ReadModel { ContractId = contract, ValueDate = Date, SourceGeneration = Generation,
            ObservedAtUtc = at, CalculatedAtUtc = at, OptionPrice = 10, BidPrice = 9, AskPrice = 11, GreeksAvailable = true };
        var changed = new FuturesTickTradeDataChangedEvent { Id = Guid.NewGuid(), AssetTypeId = AssetTypeId.FuturesOption,
            OptionMarketPriceObservation = new("ES-future", OptionMarketPriceBasis.QuoteMidpoint, tick, sequence, at.UtcDateTime) };
        return new() { Dataset = "GLBX.MDP3", ValueDate = Date, WorkerInstanceId = Worker, GenerationId = Generation,
            ManifestRevision = 1, PublicationSequence = sequence, Kind = DatasetPublicationKind.OptionQuoteObservation,
            Payload = MessagePackSerializer.Serialize(changed) };
    }
}
