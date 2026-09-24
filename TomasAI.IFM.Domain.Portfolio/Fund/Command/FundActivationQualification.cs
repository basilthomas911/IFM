using TomasAI.IFM.Domain.Portfolio.Command.State;
using TomasAI.IFM.Domain.Portfolio.Persistence;
using TomasAI.IFM.Domain.Portfolio.Shared.Contracts;
using TomasAI.IFM.Domain.Portfolio.Shared.Identities;
using TomasAI.IFM.Domain.Reference.Shared.ServiceApi;

namespace TomasAI.IFM.Domain.Portfolio.Fund.Command;

/// <summary>Qualifies Fund activation against current Portfolio and catalog authority.</summary>
internal static class FundActivationQualification
{
    /// <summary>Returns the current parent-Portfolio and assignment evidence for one Fund transition.</summary>
    internal static async ValueTask<FundActivationContext> EvaluateAsync(
        PortfolioFundId id,
        PortfolioFundAggregate aggregate,
        IPortfolioEventStore events,
        IReferenceQueryApi? referenceQueries,
        bool qualifyCatalog,
        CancellationToken cancellationToken)
    {
        var currentAssignments = aggregate.Assignments.Where(x => x.FundMandateVersion == aggregate.Current?.FundMandateVersion).ToArray();
        if (qualifyCatalog && referenceQueries is not null)
            foreach (var assignment in currentAssignments.Where(x => x.Enabled))
            {
                var deployment = assignment.TradeStrategyFamily?.CatalogDeployment ?? throw new InvalidOperationException("Legacy assignments must be replaced before activating a Fund.");
                await TomasAI.IFM.Domain.Reference.Shared.StrategyCatalog.StrategyCatalogPermissionValidation.ValidateDeploymentAsync(referenceQueries, deployment, true, cancellationToken);
            }

        var portfolio = await events.LoadPortfolioAsync(new PortfolioId(id.PortfolioId), cancellationToken).ConfigureAwait(false);
        var enabled = currentAssignments.Count(x => x.Enabled);
        return new(portfolio.Current?.OperatingState == PortfolioOperatingState.Active, enabled,
            currentAssignments.Any(x => x.Enabled && x.TradeSelectionHintProfileId != Guid.Empty),
            currentAssignments.Any(x => x.Enabled && x.OrderCompositionProfileId != Guid.Empty));
    }
}
