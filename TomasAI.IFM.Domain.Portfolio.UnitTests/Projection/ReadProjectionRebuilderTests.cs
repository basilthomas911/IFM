using FluentAssertions;
using TomasAI.IFM.Application.EventProjector;
using TomasAI.IFM.Application.EventProjector.Contracts;
using TomasAI.IFM.Domain.Portfolio.Shared.Financial;
using TomasAI.IFM.Shared.EventProjector;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Portfolio.UnitTests.Projection;

public sealed class ReadProjectionRebuilderTests
{
    static EventProjectionDescriptor Descriptor() => new(typeof(LedgerPostingCompletedEvent),
        EventProjectionIdempotencyStrategy.NaturalKeyMutation,
        (_, _) => throw new InvalidOperationException("Live workflow must never execute during reconstruction."),
        _ => null, (_, _) => null);

    [Fact]
    public async Task Rebuild_uses_only_registered_materialization_and_never_the_live_workflow()
    {
        var applied = new List<long>();
        var descriptor = Descriptor().WithReadModelRebuild((source, _) => { applied.Add(source.EventId); return Task.CompletedTask; });
        var result = await new ReadProjectionRebuilder([descriptor]).RebuildAsync(Sources(11, 12));
        result.Should().Be(2); applied.Should().Equal(11, 12);
    }

    [Fact]
    public void Missing_rebuild_action_is_rejected_before_any_source_is_applied()
        => FluentActions.Invoking(() => new ReadProjectionRebuilder([Descriptor()])).Should().Throw<InvalidOperationException>();

    [Fact]
    public async Task Out_of_order_or_uncommitted_source_is_not_applied()
    {
        var applied = new List<long>();
        var descriptor = Descriptor().WithReadModelRebuild((source, _) => { applied.Add(source.EventId); return Task.CompletedTask; });
        await FluentActions.Awaiting(() => new ReadProjectionRebuilder([descriptor]).RebuildAsync(Sources(12, 11)))
            .Should().ThrowAsync<InvalidDataException>();
        applied.Should().Equal(12);
        await FluentActions.Awaiting(() => new ReadProjectionRebuilder([descriptor]).RebuildAsync(Sources(0)))
            .Should().ThrowAsync<InvalidDataException>();
    }

    static async IAsyncEnumerable<IEvent> Sources(params long[] ids)
    {
        foreach (var id in ids)
        { yield return new LedgerPostingCompletedEvent { EventId = id }; await Task.Yield(); }
    }
}
