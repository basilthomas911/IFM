using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
namespace TomasAI.IFM.Domain.Reference.Shared.ParameterSets;
public sealed record ParameterRuntimeResolution(bool IsConfigured,bool IsDisabled,AppliedParameterAssignment? Applied);
public interface IParameterRuntimeSnapshot
{
 bool Enabled{get;}
 Guid? RunId{get;}
 ParameterSignalStartupPlan? Plan{get;}
 void Apply(ParameterStartupRun run);
 void Clear();
 ParameterRuntimeResolution Resolve(string workflowDefinitionId,TimeFrameType horizon);
}
