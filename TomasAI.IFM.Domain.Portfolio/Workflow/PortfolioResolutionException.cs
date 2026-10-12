using TomasAI.IFM.Domain.Portfolio.Shared.Contracts;
using TomasAI.IFM.Domain.Portfolio.Shared.ViewModels;

namespace TomasAI.IFM.Domain.Portfolio.Workflow;

[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1032", Justification = "This typed failure requires immutable outcome or reason-code evidence; generic constructors would create an invalid failure contract.")]
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "RCS1194", Justification = "This failure requires its typed immutable evidence; generic exception constructors cannot represent a valid domain outcome.")]
public sealed class PortfolioResolutionException(string reasonCode, string message) : InvalidOperationException(message)
{
    public string ReasonCode { get; } = reasonCode;
}
