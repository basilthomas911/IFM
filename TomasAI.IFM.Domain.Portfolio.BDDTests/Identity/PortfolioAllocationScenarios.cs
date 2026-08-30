using FluentAssertions;
using TomasAI.IFM.Domain.Portfolio.Identity;
using TomasAI.IFM.Framework.SequenceId;

namespace TomasAI.IFM.Domain.Portfolio.BDDTests.Identity;

public sealed class PortfolioAllocationScenarios
{
    [Fact]
    [Trait("Gate", "PF-02")]
    [Trait("Category", "Portfolio")]
    public async Task Given_a_new_Portfolio_when_an_id_is_allocated_then_it_is_positive_and_operator_facing()
    {
        var allocator = new PortfolioBusinessIdAllocator(new StubGenerator(7001));

        var id = await allocator.AllocatePortfolioIdAsync();

        id.Id.Should().Be(7001);
        id.Format().Should().Be("7001");
    }

    [Fact]
    [Trait("Gate", "PF-02")]
    [Trait("Category", "Portfolio")]
    public async Task Given_a_consumed_gap_when_the_next_id_is_allocated_then_no_previous_id_is_reused()
    {
        var allocator = new PortfolioBusinessIdAllocator(new QueueGenerator(1001, 1101));

        (await allocator.AllocatePortfolioIdAsync()).Id.Should().Be(1001);
        (await allocator.AllocatePortfolioIdAsync()).Id.Should().Be(1101);
    }

    sealed class StubGenerator(long id) : ISequenceIdGenerator
    {
        public ValueTask<long> GetSequenceIdAsync(SequenceName name, CancellationToken cancellationToken = default) => ValueTask.FromResult(id);
        public ValueTask<long> GetHighWatermarkAsync(SequenceName name, CancellationToken cancellationToken = default) => ValueTask.FromResult(id);
    }

    sealed class QueueGenerator(params long[] ids) : ISequenceIdGenerator
    {
        readonly Queue<long> _ids = new(ids);
        public ValueTask<long> GetSequenceIdAsync(SequenceName name, CancellationToken cancellationToken = default) => ValueTask.FromResult(_ids.Dequeue());
        public ValueTask<long> GetHighWatermarkAsync(SequenceName name, CancellationToken cancellationToken = default) => ValueTask.FromResult(_ids.LastOrDefault());
    }
}
