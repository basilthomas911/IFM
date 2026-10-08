using NSubstitute;
using TomasAI.IFM.Application.MarketData.Databento.Resiliency;
using TomasAI.IFM.Application.MarketData.Pricing;
using static TomasAI.IFM.Application.MarketData.UnitTests.OrderCompositionPricingPrerequisiteTests;

namespace TomasAI.IFM.Application.MarketData.UnitTests;

public sealed class IndividualOptionRiskReaderTests
{
    [Fact]
    public async Task Monitoring_requests_worker_frozen_time_without_changing_the_owned_scope_or_deadline()
    {
        var date = new DateOnly(2026, 9, 8);
        var admissions = new DatasetWorkerAdmissionRegistry();
        admissions.Admit(new("GLBX.MDP3", date, Guid.NewGuid(), Generation, 1));
        var market = Substitute.For<ICompositionMarketDataApi>();
        var reader = new IndividualOptionRiskReader(market, admissions);
        reader.Register(new("owned-leg", Guid.NewGuid(), Generation, date, new(2026, 10, 2),
            DateTimeOffset.UtcNow.AddMinutes(1), [new(Context(), 5000, true)], SeparateContractConnection: true));
        var now = DateTimeOffset.UtcNow;
        var valuation = Black76PricingModel.Calculate(Context(), Quote("ES-future", 5000), Quote("ES-option-call", 100), 5000, true, At).Value;
        var snapshot = new MarketCompositionSnapshot(1, Guid.NewGuid(), "owned-leg", "scope/v1", "Daily", Generation,
            now, now.AddSeconds(2), [new(new("ES-option-call", Quote("ES-option-call", 100), Context(), 5000, true, Quote("ES-future", 5000)), valuation)], "digest");
        market.CaptureAsync("GLBX.MDP3", Arg.Any<CompositionSnapshotRequest>(), Arg.Any<CancellationToken>())
            .Returns(new CompositionSnapshotResult(snapshot, null));

        Assert.Same(snapshot, await reader.CaptureAsync("ES-option-call", date, default));
        await market.Received(1).CaptureAsync("GLBX.MDP3", Arg.Is<CompositionSnapshotRequest>(request =>
            request.EvaluatedAtUtc == default && request.ScopeId == "owned-leg"
            && request.GenerationId == Generation && request.MaximumContracts == 1
            && request.DeadlineUtc >= now && request.DeadlineUtc <= now.AddSeconds(4)), Arg.Any<CancellationToken>());
        Assert.DoesNotContain(market.ReceivedCalls(), call => call.GetMethodInfo().Name == nameof(ICompositionMarketDataApi.AcquireAsync));
    }
}
