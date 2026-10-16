using System.Text.Json;
using TomasAI.IFM.Domain.Reference.Shared.ParameterSets;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Configuration.RegimeDiscovery;

namespace TomasAI.IFM.Domain.Reference.ParameterSets.Model;

/// <summary>Dispatches component-specific assignment identity, scope, and payload validation.</summary>
public static class ParameterAssignmentPolicyModel
{
    public static Guid AssignmentId(ParameterAssignmentScope scope) 
    {
        ArgumentNullException.ThrowIfNull(scope);
        return scope.ComponentCode switch
    {
        RegimeDiscoveryParameterModel.ComponentCode => WorkflowParameterScopeModel.AssignmentId(scope),
        IronCondorMarketSelectionParameterModel.ComponentCode or VerticalSpreadMarketSelectionParameterModel.ComponentCode
            => OptionSpreadStrategyParameterScopeModel.AssignmentId(scope),
#pragma warning disable MA0015 // The published rejection protocol requires an exact reason code in Exception.Message.
        _ => throw new ArgumentException("PARAM.COMPONENT_UNSUPPORTED")
#pragma warning restore MA0015
    };
    }

    public static void Validate(ParameterAssignmentScope scope, ParameterSetVersion version)
    {
        ArgumentNullException.ThrowIfNull(scope);
        ArgumentNullException.ThrowIfNull(version);
#pragma warning disable MA0015 // The published rejection protocol requires an exact reason code in Exception.Message.
        if (!string.Equals(version.Reference.ComponentCode,scope.ComponentCode,StringComparison.Ordinal)) throw new ArgumentException("PARAM.REFERENCE_INVALID");
#pragma warning restore MA0015
        var descriptor = ParameterComponentModelRegistry.Get(scope.ComponentCode);
        if (descriptor.ValidateTyped(version.Values(), version.SchemaVersion)
            .Any(issue => issue.Severity == ParameterIssueSeverity.Error))
#pragma warning disable MA0015 // The published rejection protocol requires an exact reason code in Exception.Message.
            throw new ArgumentException("PARAM.VERSION_INVALID");
#pragma warning restore MA0015
        var values=version.Values();
        if(values.SetId!=version.Reference.SetId||values.Version!=version.Reference.Version)
#pragma warning disable MA0015 // The published rejection protocol requires an exact reason code in Exception.Message.
            throw new ArgumentException("PARAM.REFERENCE_INVALID");
#pragma warning restore MA0015
        if (string.Equals(scope.ComponentCode,RegimeDiscoveryParameterModel.ComponentCode,StringComparison.Ordinal))
        {
            var horizon = WorkflowParameterScopeModel.Validate(scope);
            var payload = version.Values().Get<RegimeDiscoveryParameterSet>();
#pragma warning disable MA0015 // The published rejection protocol requires an exact reason code in Exception.Message.
            if (payload.TargetHorizon != horizon) throw new ArgumentException("PARAM.HORIZON_MISMATCH");
#pragma warning restore MA0015
        }
        else OptionSpreadStrategyParameterScopeModel.Validate(scope);
    }
}
