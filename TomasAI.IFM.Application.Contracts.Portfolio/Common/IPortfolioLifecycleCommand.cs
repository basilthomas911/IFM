using TomasAI.IFM.Domain.Portfolio.Shared.Common;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Portfolio.Shared.Common;

/// <summary>Exposes the existing caller and correlation fields of Portfolio lifecycle commands.</summary>
public interface IPortfolioLifecycleCommand : ICommandRetryIdentity
{
    Guid CorrelationId { get; }
    DateTime RequestedOnUtc { get; }
    PortfolioAccessContext Access { get; }
}
