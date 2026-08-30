using FluentAssertions;
using TomasAI.IFM.Domain.Portfolio.Identity;
using TomasAI.IFM.Framework.SequenceId;

namespace TomasAI.IFM.Domain.Portfolio.UnitTests.Identity;

public sealed class PortfolioBusinessIdAllocatorTests
{
    [Fact]
    [Trait("Gate", "PF-02")]
    [Trait("Category", "Portfolio")]
    public async Task Uses_the_four_authoritative_named_sequences()
    {
        var generator = new RecordingSequenceIdGenerator();
        var allocator = new PortfolioBusinessIdAllocator(generator);

        (await allocator.AllocatePortfolioIdAsync()).Id.Should().Be(1);
        (await allocator.AllocateFundIdAsync()).Should().Be(2);
        (await allocator.AllocateOrderIdAsync()).Should().Be(3);
        (await allocator.AllocateTradeIdAsync()).Should().Be(4);

        generator.Requested.Should().Equal(
            SequenceName.Portfolio_PortfolioId,
            SequenceName.Fund_FundId,
            SequenceName.Trade_OrderId,
            SequenceName.Trade_TradeId);
    }

    [Theory]
    [InlineData(0L, typeof(InvalidOperationException))]
    [InlineData(-1L, typeof(InvalidOperationException))]
    [InlineData(2147483648L, typeof(OverflowException))]
    [Trait("Gate", "PF-02")]
    [Trait("Category", "Portfolio")]
    public async Task Rejects_values_outside_positive_Int32(long value, Type exceptionType)
    {
        var allocator = new PortfolioBusinessIdAllocator(new FixedSequenceIdGenerator(value));

        var action = async () => await allocator.AllocatePortfolioIdAsync();

        await action.Should().ThrowAsync<Exception>().Where(e => e.GetType() == exceptionType);
    }

    [Fact]
    [Trait("Gate", "PF-02")]
    [Trait("Category", "Portfolio")]
    public void Maps_Portfolio_legacy_name_to_typed_sequence()
    {
        SequenceNameExtensions.ParseSequenceName("PortfolioId")
            .Should().Be(SequenceName.Portfolio_PortfolioId);
        SequenceName.Portfolio_PortfolioId.ToStringFast()
            .Should().Be("Portfolio_PortfolioId");
    }

    sealed class RecordingSequenceIdGenerator : ISequenceIdGenerator
    {
        long _next;
        public List<SequenceName> Requested { get; } = [];
        public ValueTask<long> GetSequenceIdAsync(SequenceName name, CancellationToken cancellationToken = default)
        {
            Requested.Add(name);
            return ValueTask.FromResult(Interlocked.Increment(ref _next));
        }
        public ValueTask<long> GetHighWatermarkAsync(SequenceName name, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(_next);
    }

    sealed class FixedSequenceIdGenerator(long value) : ISequenceIdGenerator
    {
        public ValueTask<long> GetSequenceIdAsync(SequenceName name, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(value);
        public ValueTask<long> GetHighWatermarkAsync(SequenceName name, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(value);
    }
}
