using NSubstitute;
using TomasAI.IFM.Application.MarketData.Databento.Resiliency;
using TomasAI.IFM.Application.MarketData.Pricing;
using TomasAI.IFM.Framework.MarketData.Contracts.Pricing;
using static TomasAI.IFM.Application.MarketData.UnitTests.OrderCompositionPricingPrerequisiteTests;

namespace TomasAI.IFM.Application.MarketData.UnitTests;

public sealed class CompositionMarketPreparationTests
{
    [Theory]
    [InlineData("Daily")] [InlineData("Weekly")] [InlineData("Monthly")]
    public async Task Outright_preparation_and_restart_need_neither_Treasury_nor_options(string horizon)
    {
        var clock = new Clock(At); var api = Substitute.For<ICompositionMarketDataApi>();
        var contexts = Substitute.For<IOptionPricingContextProvider>();
        await using var discovery = new QualifiedCompositionDiscovery(new(Substitute.For<IOptionPricingConventionStore>()), contexts, api, clock);
        var store = Substitute.For<ICompositionPreparationStore>(); CompositionPreparation? saved = null;
        store.ReadAsync(Arg.Any<CompositionPreparationKey>(), Arg.Any<CancellationToken>()).Returns(_ => saved);
        store.CommitAsync(Arg.Any<CompositionPreparation>(), Arg.Any<CancellationToken>()).Returns(call => saved ??= call.Arg<CompositionPreparation>());
        var admissions = new DatasetWorkerAdmissionRegistry();
        admissions.Admit(new("GLBX.MDP3", new(2026, 9, 8), Guid.NewGuid(), Generation, 1));
        var future = new CompositionFutureDefinition("ES-future", "ES", "GLBX.MDP3", "XCME", "USD", At.AddDays(10), 50, .25m, new('a', 64));
        var source = Substitute.For<ICompositionMarketSource>();
        source.ReadAsync(Arg.Any<CompositionSnapshotRequest>(), null, Arg.Any<CancellationToken>()).Returns(
            new CompositionMarketPage("complete", Generation, 1, [new("ES-future", Quote("ES-future", 5000), null, null, null, null, future)], null));
        api.CaptureAsync("GLBX.MDP3", Arg.Any<CompositionSnapshotRequest>(), Arg.Any<CancellationToken>()).Returns(call =>
            new MarketCompositionSnapshotProvider(source, clock).CaptureAsync(call.Arg<CompositionSnapshotRequest>() with { EvaluatedAtUtc = At }, call.Arg<CancellationToken>()));
        var prepare = new CompositionMarketPreparation(discovery, new(api, store, clock), store, admissions, clock);
        var key = OrderCompositionPreparationTests.Key();
        var first = await prepare.PrepareAsync(key, new() { ScopeComplete = true, ValueDate = new(2026, 9, 8), Futures = [future] }, horizon, At.AddSeconds(2), default);
        Assert.Null(first.Failure); Assert.NotNull(first.Preparation);
        admissions.Close("GLBX.MDP3", Generation);
        var replay = await new CompositionMarketPreparation(discovery, new(api, store, clock), store, admissions, clock)
            .PrepareAsync(key, new(), horizon, At.AddSeconds(2), default);
        Assert.Equal(first.Preparation, replay.Preparation);
        await api.Received(1).CaptureAsync("GLBX.MDP3", Arg.Any<CompositionSnapshotRequest>(), Arg.Any<CancellationToken>());
        Assert.Empty(contexts.ReceivedCalls());
        await api.DidNotReceive().AcquireAsync(Arg.Any<string>(), Arg.Any<WorkerOptionChainRequest>(), Arg.Any<CancellationToken>());
    }
}
