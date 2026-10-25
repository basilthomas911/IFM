using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
namespace TomasAI.IFM.Domain.Reference.Shared.ParameterSets;
public interface IParameterRuntimeSnapshot
{
    bool Enabled { get; }
    Guid? RunId { get; }
    void Apply(ParameterStartupRun run);
    void Clear();
    ParameterRuntimeResolution Resolve(string workflowDefinitionId, TimeFrameType horizon);
    ParameterRuntimeResolution Resolve(ParameterAssignmentScope scope);
}
