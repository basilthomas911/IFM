using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.Reference.Shared.ParameterSets;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Identity;
namespace TomasAI.IFM.Domain.Reference.ParameterSets.Model;

/// <summary>Workflow scope identity is stable across the historical document and typed contract migration.</summary>
public static class WorkflowParameterScopeModel
{
    public const string ConsumerKind="strategy-workflow";
    public const string Role="regime-discovery";
    public static ParameterAssignmentScope Create(string workflowDefinitionId,TimeFrameType horizon)
    {
        if(workflowDefinitionId!=IntrinsicTimeStrategyWorkflowDefinition.Id)throw new ArgumentException("PARAM.WORKFLOW_UNSUPPORTED");
        if(horizon is not(TimeFrameType.Daily or TimeFrameType.Weekly or TimeFrameType.Monthly))throw new ArgumentException("PARAM.HORIZON_UNSUPPORTED");
        var hash=ParameterBinaryFingerprint.Hash((ConsumerKind,workflowDefinitionId,Role,RegimeDiscoveryParameterModel.ComponentCode,horizon));
        return new(ConsumerKind,workflowDefinitionId,Role,RegimeDiscoveryParameterModel.ComponentCode,string.Empty,hash){TargetHorizon=horizon};
    }
    public static TimeFrameType Validate(ParameterAssignmentScope scope)
    {
        var value=ParameterHistoryReader.UpgradeScope(scope);
        var horizon=value.TargetHorizon??throw new ArgumentException("PARAM.HORIZON_UNSUPPORTED");
        var expected=Create(value.ConsumerId,horizon);
        var hash=value.Codec==ParameterValueCodec.Codec?expected.ScopeSha256:LegacyHash(horizon);
        if(value.ConsumerKindCode!=ConsumerKind||value.Role!=Role||value.ComponentCode!=expected.ComponentCode||value.Strategy is not null||value.ScopeSha256!=hash)
            throw new ArgumentException("PARAM.SCOPE_INVALID");
        return horizon;
    }
    public static Guid AssignmentId(ParameterAssignmentScope scope)=>Validate(scope) switch
    {
        TimeFrameType.Daily=>Guid.Parse("7b28a658-2bb1-dbf2-a19c-9238e1e91266"),
        TimeFrameType.Weekly=>Guid.Parse("5bd6b993-2904-e8a2-957e-97bfb373494a"),
        TimeFrameType.Monthly=>Guid.Parse("c6c69b08-1e80-2943-f9d1-a630d01b6295"),
        _=>throw new ArgumentException("PARAM.HORIZON_UNSUPPORTED")
    };
    static string LegacyHash(TimeFrameType horizon)=>horizon switch
    {
        TimeFrameType.Daily=>"76060e366b906d60ef496ef6248f384a7e897cd935885e86c7898d7391c7991f",
        TimeFrameType.Weekly=>"f95124b0ca442b9f4e83b314ffbc22141395f88ca26bd4720518c50b673d12f4",
        TimeFrameType.Monthly=>"8d07dfee1e68875b98294b6771db5d5de18265a377ca512e460a4c46a6198c7e",
        _=>throw new ArgumentException("PARAM.HORIZON_UNSUPPORTED")
    };
}
