using TomasAI.IFM.Domain.Reference.ParameterSets.Startup.Command.Model;
using TomasAI.IFM.Domain.Reference.ParameterSets.Assignment.Command.State;
using TomasAI.IFM.Domain.Reference.ParameterSets.Assignment.Command.Model;
using TomasAI.IFM.Domain.Reference.ParameterSets.Model;
using TomasAI.IFM.Application.Storage.ConfigurationDb;
using TomasAI.IFM.Domain.Reference.Shared.ParameterSets;
using TomasAI.IFM.Domain.Reference.ParameterSets.Command.State;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Identity;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
namespace TomasAI.IFM.Domain.Reference.ParameterSets.Preparation.Query.Model;

/// <summary>Reads one coherent generation from authoritative streams under the existing writer lease.</summary>
public static class ParameterStartupReader
{
    public static async Task<ParameterStartupSnapshotModel> ReadAsync(Guid runId, IConfigurationDbContext db,
     IEventSourceActorStateRepository<ParameterAssignmentCommandState> assignmentRepository,
     IEventSourceActorStateRepository<ParameterSetCommandState> setRepository, CancellationToken token)
    {
        ArgumentNullException.ThrowIfNull(db);
        var lease = await db.AcquireParameterWriteLeaseAsync(token).ConfigureAwait(false);
        await using var configuredLease = lease.ConfigureAwait(false);
        return await (ReadUnderLeaseAsync(runId, assignmentRepository, setRepository, token)).ConfigureAwait(false);
    }
    public static async Task<ParameterStartupSnapshotModel> ReadUnderLeaseAsync(Guid runId,
     IEventSourceActorStateRepository<ParameterAssignmentCommandState> assignmentRepository,
     IEventSourceActorStateRepository<ParameterSetCommandState> setRepository, CancellationToken token)
    {
        ArgumentNullException.ThrowIfNull(assignmentRepository);
        ArgumentNullException.ThrowIfNull(setRepository);
        var assignments = await ReadAssignmentsUnderLeaseAsync(assignmentRepository, token).ConfigureAwait(false);
        var versions = new Dictionary<ParameterVersionRef, ParameterSetVersion>();
        foreach (var assignment in assignments)
        {
            token.ThrowIfCancellationRequested();
            if (!assignment.Enabled || versions.ContainsKey(assignment.Reference)) continue;
            var setId = new ParameterSetEntityId(assignment.Reference.SetId);
            var setAddress = new CreateParameterSetCommand { EntityId = setId, Subject = new ActorSubject(ActorType.Command, CreateParameterSetCommand.Actor, CreateParameterSetCommand.Verb, setId.Format()) };
            var set = await (setRepository.LoadStateAsync(setAddress, token)).ConfigureAwait(false);
            if (!set.Versions.TryGetValue(assignment.Reference.Version, out var version)) throw new InvalidOperationException("PARAM.EXACT_VERSION_MISSING");
            versions.Add(assignment.Reference, version);
        }
        token.ThrowIfCancellationRequested(); return new(runId, assignments, versions);
    }

    /// <summary>Reads current references for every supported assignment scope under the caller's writer lease.</summary>
    public static async Task<IReadOnlyList<ParameterAssignmentRevision>> ReadAssignmentsUnderLeaseAsync(
        IEventSourceActorStateRepository<ParameterAssignmentCommandState> assignmentRepository, CancellationToken token)
    {
        ArgumentNullException.ThrowIfNull(assignmentRepository);
        var assignments = new List<ParameterAssignmentRevision>();
        var scopes = new[] { TimeFrameType.Daily, TimeFrameType.Weekly, TimeFrameType.Monthly }
            .Select(horizon => WorkflowParameterScopeModel.Create(IntrinsicTimeStrategyWorkflowDefinition.Id, horizon))
            .Append(OptionSpreadStrategyParameterScopeModel.IronCondor())
            .Append(OptionSpreadStrategyParameterScopeModel.VerticalSpread());
        foreach (var scope in scopes)
        {
            token.ThrowIfCancellationRequested();
            var id = new ParameterAssignmentEntityId(ParameterAssignmentPolicyModel.AssignmentId(scope));
            var address = new AssignParameterVersionCommand { EntityId = id, Scope = scope,
                Subject = new(ActorType.Command, AssignParameterVersionCommand.Actor, AssignParameterVersionCommand.Verb, id.Format()) };
            var state = await assignmentRepository.LoadStateAsync(address, token).ConfigureAwait(false);
            if (state.Assignment is { } assignment) assignments.Add(assignment);
        }
        return assignments;
    }

}
