using FluentAssertions;
using TomasAI.IFM.Domain.MarketData.Analytics.OptionVolatility;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.OptionVolatility;

namespace TomasAI.IFM.Domain.MarketData.Analytics.UnitTests.OptionVolatility;

public sealed class VolatilitySampleCoordinatorTests
{
    [Fact]
    public void Admit_DeduplicatesDailySlotAndAcceptsOnlyNewerCorrection()
    {
        var coordinator = new VolatilitySampleCoordinator(TimeSpan.FromMinutes(5), 2);
        var original = OptionVolatilityTestData.Observation("original");
        var duplicate = original with { ObservationId = "duplicate" };
        var correction = original with { ObservationId = "correction", Revision = 2,
            SupersedesObservationId = original.ObservationId };

        coordinator.Admit(original, VolatilitySampleKind.DailyFinal).Accepted.Should().BeTrue();
        coordinator.Admit(duplicate, VolatilitySampleKind.DailyFinal).Accepted.Should().BeFalse();
        var corrected = coordinator.Admit(correction, VolatilitySampleKind.DailyFinal);
        corrected.Accepted.Should().BeTrue();
        corrected.ReplacedCoalescedCheckpoint.Should().BeTrue();
    }

    [Fact]
    public void Admit_CoalescesIntradayObservationsInSameIntervalByRevision()
    {
        var coordinator = new VolatilitySampleCoordinator(TimeSpan.FromMinutes(5), 2);
        var first = OptionVolatilityTestData.Observation("first") with
        { ObservedAtUtc = OptionVolatilityTestData.Now.AddMinutes(-4) };
        var corrected = first with { ObservationId = "corrected", Revision = 2,
            SupersedesObservationId = first.ObservationId, ObservedAtUtc = first.ObservedAtUtc.AddMinutes(1) };

        coordinator.Admit(first, VolatilitySampleKind.IntradayCheckpoint).Accepted.Should().BeTrue();
        var result = coordinator.Admit(corrected, VolatilitySampleKind.IntradayCheckpoint);

        result.Accepted.Should().BeTrue();
        result.ReplacedCoalescedCheckpoint.Should().BeTrue();
        result.ReasonCode.Should().Be("IntradayCheckpointCoalesced");
    }

    [Fact]
    public void Admit_EnforcesPerValueDateIntradayCheckpointCap()
    {
        var coordinator = new VolatilitySampleCoordinator(TimeSpan.FromMinutes(5), 2);
        var baseline = OptionVolatilityTestData.Observation("one") with
        { ObservedAtUtc = OptionVolatilityTestData.Now };

        coordinator.Admit(baseline, VolatilitySampleKind.IntradayCheckpoint).Accepted.Should().BeTrue();
        coordinator.Admit(baseline with { ObservationId = "two", ObservedAtUtc = baseline.ObservedAtUtc.AddMinutes(5) },
            VolatilitySampleKind.IntradayCheckpoint).Accepted.Should().BeTrue();
        var rejected = coordinator.Admit(
            baseline with { ObservationId = "three", ObservedAtUtc = baseline.ObservedAtUtc.AddMinutes(10) },
            VolatilitySampleKind.IntradayCheckpoint);

        rejected.Accepted.Should().BeFalse();
        rejected.ReasonCode.Should().Be("IntradayCheckpointLimitReached");
    }
}
