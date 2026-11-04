using TomasAI.IFM.Domain.Reference.ParameterSets.Assignment.Command.Model;
using TomasAI.IFM.Domain.Reference.ParameterSets.Model;
using TomasAI.IFM.Domain.Reference.Shared.ParameterSets;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Identity;
namespace TomasAI.IFM.Domain.Reference.ParameterSets.Preparation.Query.Model;

/// <summary>Defines the supported startup assignment scopes.</summary>
public static class ParameterStartupReader
{
    internal static IEnumerable<ParameterAssignmentScope> SupportedScopes() =>
        new[] { TimeFrameType.Daily, TimeFrameType.Weekly, TimeFrameType.Monthly }
            .Select(horizon => WorkflowParameterScopeModel.Create(IntrinsicTimeStrategyWorkflowDefinition.Id, horizon))
            .Append(OptionSpreadStrategyParameterScopeModel.IronCondor())
            .Append(OptionSpreadStrategyParameterScopeModel.VerticalSpread());
}
