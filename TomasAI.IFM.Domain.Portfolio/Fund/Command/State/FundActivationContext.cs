using TomasAI.IFM.Domain.Portfolio.Fund.Command.Model;
using TomasAI.IFM.Domain.Portfolio.Command.Model;
using TomasAI.IFM.Domain.Portfolio.Shared.Events;
using TomasAI.IFM.Domain.Portfolio.Shared.Fund.Events;
using TomasAI.IFM.Domain.Portfolio.Shared.FinancialPolicy.Events;
using TomasAI.IFM.Domain.Portfolio.Shared.Contracts;
using TomasAI.IFM.Domain.Portfolio.Shared.ViewModels;
using TomasAI.IFM.Domain.Portfolio.Workflow;

namespace TomasAI.IFM.Domain.Portfolio.Command.State;

[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
public readonly record struct FundActivationContext(
    bool ParentPortfolioIsActive,
    int EnabledCompatibleAssignmentCount,
    bool HasSelectionHintProfile,
    bool HasCompositionProfile)
{
    public bool IsValid => ParentPortfolioIsActive && EnabledCompatibleAssignmentCount > 0 &&
                           HasSelectionHintProfile && HasCompositionProfile;
}
