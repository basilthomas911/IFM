using TomasAI.IFM.Domain.Portfolio.Command.State;
using TomasAI.IFM.Domain.Portfolio.Shared.ViewModels;

namespace TomasAI.IFM.Domain.Portfolio.Fund.Command;

/// <summary>Qualifies Fund activation against current Portfolio and catalog authority.</summary>
internal static class FundActivationQualification
{
    internal static FundTradeTemplateAssignmentReadModel[] EnabledAssignments(PortfolioFundAggregate aggregate)
        => aggregate.Assignments.Where(assignment => assignment.Enabled &&
            assignment.FundMandateVersion == aggregate.Current?.FundMandateVersion).ToArray();

    internal static FundActivationContext ActiveParentContext(PortfolioFundAggregate aggregate) => BuildContext(true, EnabledAssignments(aggregate));

    static FundActivationContext BuildContext(bool parentActive, FundTradeTemplateAssignmentReadModel[] assignments)
        => new(parentActive, assignments.Length, assignments.Any(x => x.TradeSelectionHintProfileId != Guid.Empty),
            assignments.Any(x => x.OrderCompositionProfileId != Guid.Empty));
}
