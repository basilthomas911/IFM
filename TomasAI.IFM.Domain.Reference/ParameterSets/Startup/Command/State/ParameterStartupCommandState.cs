using TomasAI.IFM.Domain.Reference.ParameterSets.Startup.Command.Model;
using System.Text.Json;
using TomasAI.IFM.Domain.Reference.Shared.ParameterSets;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
namespace TomasAI.IFM.Domain.Reference.ParameterSets.Startup.Command.State;

public sealed class ParameterStartupCommandState : BaseEventSourceActorState<ParameterStartupCommandState>, IEventSourceActorState<ParameterStartupCommandState>
{
    public override ActorThreadId Id { get; set; } = default!;
    public long Revision { get; private set; }
    public Guid RunId { get; private set; }
    public bool Released { get; private set; }
    internal ParameterStartupCreatedEvent? RegistryCreation { get; set; }
    // Compatibility collection surface; replay admits exactly one RunId in this aggregate.
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "MA0016", Justification = "Actor reducers and command models share this mutable state dictionary; preserve the published state contract during the persistence refactor.")]
    public Dictionary<Guid, ParameterStartupRun> ActiveRuns { get; } = [];
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "MA0016", Justification = "Actor reducers and command models share this mutable state dictionary; preserve the published state contract during the persistence refactor.")]
    public Dictionary<Guid, ParameterSignalStartupReport> Reports { get; } = [];
    /// <summary>Requires every accepted source fact to participate in ordered replay.</summary>
    protected override bool ApplyReplayEvent(IEvent domainEvent)
    {
        var applied = base.ApplyReplayEvent(domainEvent);
        if (domainEvent is ParameterStartupChangedEvent && !applied) throw new InvalidDataException("PARAM.STARTUP_REPLAY_GAP");
        return applied;
    }

    internal ParameterStartupStateSnapshottedEvent CaptureSnapshot(ICommand command) => new()
    {
        Subject = new(ActorType.Event, "ParameterStartupEvent", "StateSnapshotted", command.Subject.EntityId),
        Id = Guid.NewGuid(),
        CommandId = command.CommandId,
        EventSource = command.EventSource,

        RunId = RunId,
        Revision = Revision,
        Released = Released,
        ActiveRun = ActiveRuns.GetValueOrDefault(RunId),
        Report = Reports.GetValueOrDefault(RunId)
    };

    internal static bool IsCompleteSnapshot(ParameterStartupStateSnapshottedEvent snapshot) =>
        snapshot.RunId != Guid.Empty && snapshot.Revision > 0 &&
        (snapshot.Released
            ? snapshot.ActiveRun is null && snapshot.Report is null
            : snapshot.ActiveRun is { } run && run.RunId == snapshot.RunId);

    private bool RestoreSnapshot(ParameterStartupStateSnapshottedEvent snapshot)
    {
        if (!IsCompleteSnapshot(snapshot))
            return false;
        if (RunId != Guid.Empty && RunId != snapshot.RunId ||
            !string.IsNullOrEmpty(Id.EntityId) && (!Guid.TryParse(Id.EntityId, out var owner) || owner != snapshot.RunId))
            throw new InvalidDataException("PARAM.STARTUP_OWNER_MISMATCH");
        ActiveRuns.Clear();
        Reports.Clear();
        RunId = snapshot.RunId;
        Revision = snapshot.Revision;
        Released = snapshot.Released;
        if (snapshot.ActiveRun is { } run)
            ActiveRuns.Add(RunId, run);
        if (snapshot.Report is { } report)
            Reports.Add(RunId, report);
        return true;
    }

    protected override bool Apply(IEvent domainEvent)
    {
        if (domainEvent is ParameterStartupStateSnapshottedEvent snapshot)
            return RestoreSnapshot(snapshot);
        if (domainEvent is not ParameterStartupChangedEvent fact || fact.Revision != Revision + 1 || fact.RunId == Guid.Empty) return false;
        if (!string.IsNullOrEmpty(Id.EntityId) && (!Guid.TryParse(Id.EntityId, out var owner) || owner != fact.EntityId.Id))
            throw new InvalidDataException("PARAM.STARTUP_OWNER_MISMATCH");
        if (RunId != Guid.Empty && RunId != fact.RunId) throw new InvalidDataException("PARAM.STARTUP_OWNER_MISMATCH");
        if (fact.EntityId != ParameterStartupEntityId.Registry && fact.EntityId.Id != fact.RunId)
            throw new InvalidDataException("PARAM.STARTUP_OWNER_MISMATCH");
        if (fact is ParameterStartupCreatedEvent creation)
        {
            if (Revision != 0 || fact.Revision != 1 || creation.Released || creation.EntityId.Id != creation.RunId
                || (!creation.ImportedFromRegistry && (creation.ReleasedAtCreation || creation.ReadReport() is not null)) || creation.ReadRun() is not { } createdRun || createdRun.RunId != fact.RunId || createdRun.Plan.StartupRunId != fact.RunId)
                throw new InvalidDataException("PARAM.STARTUP_INVALID");
            RunId = fact.RunId;
            Released = creation.ReleasedAtCreation;
            if (!Released) ActiveRuns.Add(RunId, createdRun);
            if (!Released && creation.ReadReport() is { } initialReport)
            {
                TomasAI.IFM.Domain.Reference.ParameterSets.Startup.Command.Model.ParameterSignalStartupReportModel.Validate(createdRun, initialReport);
                Reports[RunId] = initialReport;
            }
            Revision = fact.Revision;
            return true;
        }
        RunId = fact.RunId;
        if (fact.ReadReport() is {} report)
        {
            if (!ActiveRuns.TryGetValue(fact.RunId, out var run)) throw new InvalidDataException("PARAM.STARTUP_NOT_FOUND");
            TomasAI.IFM.Domain.Reference.ParameterSets.Startup.Command.Model.ParameterSignalStartupReportModel.Validate(run, report);
            Reports[fact.RunId] = report;
        }
        else if (fact.Released) { ActiveRuns.Remove(fact.RunId); Reports.Remove(fact.RunId); Released = true; }
        else
        {
            var run = fact.ReadRun() ?? throw new InvalidDataException("PARAM.STARTUP_INVALID");
            if (run.RunId != fact.RunId || run.Plan.StartupRunId != fact.RunId || ActiveRuns.ContainsKey(run.RunId))
                throw new InvalidDataException("PARAM.STARTUP_INVALID");
            if (Released) throw new InvalidDataException("PARAM.STARTUP_RELEASED");
            ActiveRuns.Add(run.RunId, run);
        }
        Revision = fact.Revision; return true;
    }
}
