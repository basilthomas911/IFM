using System.Text.Json;
using TomasAI.IFM.Domain.Reference.Shared.ParameterSets;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
namespace TomasAI.IFM.Domain.Reference.ParameterSets.Command.State;
public sealed class ParameterStartupCommandState:BaseEventSourceActorState<ParameterStartupCommandState>,IEventSourceActorState<ParameterStartupCommandState>
{
 public override ActorThreadId Id{get;set;}=default!;
 public long Revision{get;private set;}
 // Recent inspection cache only; immutable events and SQL retain the full startup history.
 // Cache eviction never releases a runtime snapshot or changes an assignment.
 public Dictionary<Guid,ParameterStartupRun> ActiveRuns{get;}=[];
 public Dictionary<Guid,ParameterSignalStartupReport> Reports{get;}=[];
 protected override bool Apply(IEvent e)
 {
  if(e is not ParameterStartupChangedEvent fact||fact.Revision!=Revision+1||fact.RunId==Guid.Empty)return false;
  if(!string.IsNullOrEmpty(fact.ReportJson))
  {
   var report=JsonSerializer.Deserialize<ParameterSignalStartupReport>(fact.ReportJson)??throw new InvalidDataException("PARAM.STARTUP_REPORT_INVALID");
   if(!ActiveRuns.TryGetValue(fact.RunId,out var run))throw new InvalidDataException("PARAM.STARTUP_NOT_FOUND");
   TomasAI.IFM.Domain.Reference.ParameterSets.Model.ParameterSignalStartupReportModel.Validate(run,report);
   Reports[fact.RunId]=report;
  }
  else if(fact.Released){ActiveRuns.Remove(fact.RunId);Reports.Remove(fact.RunId);}
  else
  {
   var run=JsonSerializer.Deserialize<ParameterStartupRun>(fact.RunJson)??throw new InvalidDataException("PARAM.STARTUP_INVALID");
   if(run.RunId!=fact.RunId||run.Plan.StartupRunId!=fact.RunId||ActiveRuns.ContainsKey(run.RunId))
    throw new InvalidDataException("PARAM.STARTUP_INVALID");
   if(ActiveRuns.Count>=100){var oldest=ActiveRuns.First().Key;ActiveRuns.Remove(oldest);Reports.Remove(oldest);}
   ActiveRuns.Add(run.RunId,run);
  }
  Revision=fact.Revision;return true;
 }
}
