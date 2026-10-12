using TomasAI.IFM.Domain.Portfolio.Shared.Common;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using TomasAI.IFM.Domain.Portfolio.Shared.Commands;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Portfolio.Operations;

public sealed class PortfolioAuthorizationException : UnauthorizedAccessException
{
    public PortfolioAuthorizationException() { }
    public PortfolioAuthorizationException(string message) : base(message) { }
    public PortfolioAuthorizationException(string message, Exception innerException) : base(message, innerException) { }
}
