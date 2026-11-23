using FluentAssertions;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Runtime;

namespace TomasAI.IFM.Domain.Trade.UnitTests.Strategy.Workflow.IntrinsicTime.Runtime;

public sealed class PreparedDecisionInputTests
{
    [Fact]
    public void Acquisition_is_immediate_and_preserves_the_producer_reference_and_expiry()
    {
        var cache = new PreparedDecisionInput<object>();
        var now = DateTime.UtcNow;
        cache.TryAcquire(now, out _, out _).Should().BeFalse();
        var snapshot = new object();
        cache.Publish(snapshot, 1, now.AddSeconds(5));
        cache.TryAcquire(now, out var acquired, out var revision).Should().BeTrue();
        acquired.Should().BeSameAs(snapshot);
        revision.Should().Be(1);
        cache.TryAcquire(now.AddSeconds(5), out _, out _).Should().BeFalse();
    }

    [Fact]
    public void Invalidation_cannot_discard_a_newer_generation_or_resurrect_an_old_publication()
    {
        var cache = new PreparedDecisionInput<object>();
        var now = DateTime.UtcNow;
        cache.Publish(new object(), 2, now.AddSeconds(5));
        cache.Invalidate(1);
        cache.TryAcquire(now, out _, out _).Should().BeTrue();
        cache.Invalidate(2);
        cache.Publish(new object(), 1, now.AddSeconds(5));
        cache.Publish(new object(), 2, now.AddSeconds(5));
        cache.TryAcquire(now, out _, out var revision).Should().BeFalse();
        revision.Should().Be(2);
        cache.Publish(new object(), 3, now.AddSeconds(5));
        cache.TryAcquire(now, out _, out revision).Should().BeTrue();
        revision.Should().Be(3);
    }
}
