using TomasAI.IFM.Domain.Portfolio.Shared.Common;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using TomasAI.IFM.Domain.Portfolio.Shared.Commands;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Portfolio.Operations;

public sealed class PortfolioOperationalOptions
{
    public const string SectionName = "Portfolio:Operations";
    public bool Enabled { get; init; } = true;
    public bool QueriesEnabled { get; init; } = true;
    public bool MutationsEnabled { get; init; } = true;
    public bool AuthorizationRequired { get; init; } = true;

    public PortfolioOperationalOptions Validate()
    {
        if (Enabled && !QueriesEnabled && !MutationsEnabled)
            throw new InvalidOperationException("At least one Portfolio operational path must be enabled when Portfolio is enabled.");
        return this;
    }
}
