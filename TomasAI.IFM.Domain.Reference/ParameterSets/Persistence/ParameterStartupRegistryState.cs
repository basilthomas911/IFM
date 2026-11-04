using TomasAI.IFM.Domain.Reference.ParameterSets.Startup.Command.Model;
using System.Text.Json;
using TomasAI.IFM.Domain.Reference.Shared.ParameterSets;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
namespace TomasAI.IFM.Domain.Reference.ParameterSets.Persistence;

public sealed class ParameterStartupRegistryState : BaseEventSourceActorState<ParameterStartupRegistryState>, IEventSourceActorState<ParameterStartupRegistryState>
{
    public override ActorThreadId Id { get; set; } = default!;
    public long Revision { get; private set; }
    readonly Dictionary<Guid, ParameterStartupRun> allRuns = [];
    public IReadOnlyDictionary<Guid, ParameterStartupRun> AllRuns => allRuns;
    // Compatibility reader only. New commands never append to this registry.
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "MA0016", Justification = "Actor reducers and command models share this mutable state dictionary; preserve the published state contract during the persistence refactor.")]
    public Dictionary<Guid, ParameterStartupRun> ActiveRuns { get; } = [];
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "MA0016", Justification = "Actor reducers and command models share this mutable state dictionary; preserve the published state contract during the persistence refactor.")]
    public Dictionary<Guid, ParameterSignalStartupReport> Reports { get; } = [];
    protected override bool Apply(IEvent domainEvent)
    {
        if (domainEvent is not ParameterStartupChangedEvent fact || fact.Revision != Revision + 1 || fact.RunId == Guid.Empty) return false;
        if (fact.ReadReport() is {} report)
        {
            if (!ActiveRuns.TryGetValue(fact.RunId, out var run)) throw new InvalidDataException("PARAM.STARTUP_NOT_FOUND");
            TomasAI.IFM.Domain.Reference.ParameterSets.Startup.Command.Model.ParameterSignalStartupReportModel.Validate(run, report);
            Reports[fact.RunId] = report;
        }
        else if (fact.Released) { ActiveRuns.Remove(fact.RunId); }
        else
        {
            var run = fact.ReadRun() ?? throw new InvalidDataException("PARAM.STARTUP_INVALID");
            if (run.RunId != fact.RunId || run.Plan.StartupRunId != fact.RunId || ActiveRuns.ContainsKey(run.RunId))
                throw new InvalidDataException("PARAM.STARTUP_INVALID");
            allRuns[run.RunId] = run;
            ActiveRuns.Add(run.RunId, run);
        }
        Revision = fact.Revision; return true;
    }
}
