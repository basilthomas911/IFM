using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.Reference.Shared.ParameterSets;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Identity;

namespace TomasAI.IFM.Domain.Reference.ParameterSets.Model;

/// <summary>Regime Discovery belongs to the workflow definition and target horizon, before deployment selection.</summary>
public static class WorkflowParameterScopeModel
{
    public const string ConsumerKind = "strategy-workflow";
    public const string Role = "regime-discovery";

    public static ParameterAssignmentScope Create(string workflowDefinitionId, TimeFrameType horizon)
    {
        if (workflowDefinitionId != IntrinsicTimeStrategyWorkflowDefinition.Id)
            throw new ArgumentException("PARAM.WORKFLOW_UNSUPPORTED");
        if (horizon is not (TimeFrameType.Daily or TimeFrameType.Weekly or TimeFrameType.Monthly))
            throw new ArgumentException("PARAM.HORIZON_UNSUPPORTED");
        var json = ParameterCanonicalPayloadModel.Canonicalize(JsonSerializer.Serialize(new { TargetHorizon = horizon.ToString() }));
        return new(ConsumerKind, workflowDefinitionId, Role, RegimeDiscoveryParameterModel.ComponentCode,
            json, ParameterCanonicalPayloadModel.Hash(json));
    }

    public static TimeFrameType Validate(ParameterAssignmentScope scope)
    {
        using var json = JsonDocument.Parse(scope.ScopeJson);
        if (!Enum.TryParse<TimeFrameType>(json.RootElement.GetProperty("TargetHorizon").GetString(), out var horizon))
            throw new ArgumentException("PARAM.HORIZON_UNSUPPORTED");
        if (scope != Create(scope.ConsumerId, horizon))
            throw new ArgumentException("PARAM.SCOPE_INVALID");
        return horizon;
    }

    public static Guid AssignmentId(ParameterAssignmentScope scope)
    {
        Validate(scope);
        var identity = JsonSerializer.Serialize(new { scope.ConsumerKindCode, scope.ConsumerId, scope.Role, scope.ComponentCode, scope.ScopeSha256 });
        return new Guid(SHA256.HashData(Encoding.UTF8.GetBytes(identity)).AsSpan(0, 16));
    }
}
