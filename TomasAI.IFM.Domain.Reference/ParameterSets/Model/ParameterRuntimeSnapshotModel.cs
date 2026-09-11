using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.Reference.Shared.ParameterSets;
namespace TomasAI.IFM.Domain.Reference.ParameterSets.Model;
/// <summary>One process consumes an immutable, durably applied startup generation. No polling or recovery worker.</summary>
public sealed class ParameterRuntimeSnapshotModel(bool enabled):IParameterRuntimeSnapshot
{
 sealed record State(ParameterStartupSnapshotModel Snapshot,ParameterSignalStartupPlan Plan);
 State? current;
 public bool Enabled=>enabled;
 public Guid? RunId=>Volatile.Read(ref current)?.Snapshot.StartupRunId;
 public ParameterSignalStartupPlan? Plan
 {
  get{var plan=Volatile.Read(ref current)?.Plan;return plan is null?null:System.Text.Json.JsonSerializer.Deserialize<ParameterSignalStartupPlan>(System.Text.Json.JsonSerializer.Serialize(plan));}
 }
 public void Apply(ParameterStartupRun run)
 {
  var snapshot=new ParameterStartupSnapshotModel(run.RunId,run.Scopes,run.Versions.ToDictionary(x=>x.Reference));
  var plan=SignalStartupPlanModel.Create(snapshot,SignalStartupPlanModel.ExistingIntradayConsumers());
  if(plan.Fingerprint!=run.Plan.Fingerprint)throw new InvalidDataException("PARAM.STARTUP_FINGERPRINT_MISMATCH");
  var previous=Interlocked.CompareExchange(ref current,new(snapshot,plan),null);
  if(previous is not null&&(previous.Snapshot.StartupRunId!=run.RunId||previous.Plan.Fingerprint!=plan.Fingerprint))
   throw new InvalidOperationException("PARAM.STARTUP_ALREADY_APPLIED");
 }
 public void Clear()=>Interlocked.Exchange(ref current,null);
 public ParameterRuntimeResolution Resolve(string workflowDefinitionId,TimeFrameType horizon)
 {
  if(!Enabled)return new(false,false,null);
  var value=Volatile.Read(ref current)??throw new InvalidOperationException("PARAM.STARTUP_NOT_APPLIED");
  var scope=WorkflowParameterScopeModel.Create(workflowDefinitionId,horizon);
  var applied=value.Snapshot.Resolve(scope,value.Snapshot.StartupRunId);
  return new(value.Snapshot.HasScope(scope),value.Snapshot.HasScope(scope)&&applied is null,applied);
 }
}
