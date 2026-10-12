using TomasAI.IFM.Domain.Portfolio.Shared.Identities;
using TomasAI.IFM.Framework.SequenceId;

namespace TomasAI.IFM.Domain.Portfolio.Identity;

public interface IPortfolioBusinessIdAllocator : IPortfolioBusinessIdHighWatermark
{
    ValueTask<PortfolioId> AllocatePortfolioIdAsync(CancellationToken cancellationToken = default);
    ValueTask<int> AllocateFundIdAsync(CancellationToken cancellationToken = default);
    ValueTask<int> AllocateOrderIdAsync(CancellationToken cancellationToken = default);
    ValueTask<int> AllocateTradeIdAsync(CancellationToken cancellationToken = default);
    ValueTask<int> AllocatePolicyIdAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
    ValueTask<int> IPortfolioBusinessIdHighWatermark.GetPortfolioHighWatermarkAsync(CancellationToken cancellationToken) => throw new NotSupportedException();
}
