using FluentAssertions;
using TomasAI.IFM.Domain.Portfolio.Identity;
using TomasAI.IFM.Framework.SequenceId;

namespace TomasAI.IFM.Domain.Portfolio.VerificationTests.Identity;

public sealed class PortfolioAllocationVerificationTests
{
    [Fact]
    [Trait("Gate", "PF-02")]
    [Trait("Category", "Portfolio")]
    public async Task Production_allocator_preserves_representative_business_ids()
    {
        var allocator = new PortfolioBusinessIdAllocator(new NamedGenerator(new Dictionary<SequenceName, long>()
        {
            [SequenceName.Portfolio_PortfolioId] = 101,
            [SequenceName.Fund_FundId] = 205,
            [SequenceName.Trade_OrderId] = 3001,
            [SequenceName.Trade_TradeId] = 4001,
        }));

        (await allocator.AllocatePortfolioIdAsync()).Id.Should().Be(101);
        (await allocator.AllocateFundIdAsync()).Should().Be(205);
        (await allocator.AllocateOrderIdAsync()).Should().Be(3001);
        (await allocator.AllocateTradeIdAsync()).Should().Be(4001);
    }

    sealed class NamedGenerator(IReadOnlyDictionary<SequenceName, long> ids) : ISequenceIdGenerator
    {
        public ValueTask<long> GetSequenceIdAsync(SequenceName name, CancellationToken cancellationToken = default) => ValueTask.FromResult(ids[name]);
        public ValueTask<long> GetHighWatermarkAsync(SequenceName name, CancellationToken cancellationToken = default) => ValueTask.FromResult(ids[name]);
    }
}
