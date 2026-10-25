using TomasAI.IFM.Domain.Portfolio.Shared.Identities;

namespace TomasAI.IFM.Domain.Portfolio.Identity;

public interface IPortfolioBusinessIdHighWatermark
{
    /// <summary>Returns the highest Portfolio ID reserved by the authoritative PostgreSQL sequence without allocating an ID.</summary>
    ValueTask<int> GetPortfolioHighWatermarkAsync(CancellationToken cancellationToken = default);
}
