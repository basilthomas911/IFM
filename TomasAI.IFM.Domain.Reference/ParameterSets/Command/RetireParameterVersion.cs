using System.Text.Json;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Domain.Reference.Shared.ParameterSets;
using TomasAI.IFM.Domain.Reference.ParameterSets.Model;
using TomasAI.IFM.Domain.Reference.ParameterSets.Command.State;
using TomasAI.IFM.Domain.Reference.ParameterSets.Command.Actor;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Domain.Reference.ParameterSets.Command.Model;
namespace TomasAI.IFM.Domain.Reference.ParameterSets.Command;

public static class RetireParameterVersion
{
    /// <summary>Authorizes the request, checks replay, and applies its computed source event.</summary>
    /// <param name="command">The concrete lifecycle intent.</param>
    /// <param name="context">The authorized command services and repositories.</param>
    /// <param name="state">The authoritative actor state.</param>
    /// <param name="logger">The actor logger.</param>
    /// <returns>The command acceptance or business failure.</returns>
    public static async Task<ServiceResult<GuidResult>> ExecuteAsync(this RetireParameterVersionCommand command, IParameterSetCommandContext context, ParameterSetCommandState state, ILogger<ParameterSetCommandActor> logger)
    {
        ArgumentNullException.ThrowIfNull(context); ArgumentNullException.ThrowIfNull(logger);
        context.AccessPolicy.Demand(ParameterCapability.Retire);
        var hash = ParameterMutationModel.RequestHash(command);
        if (state.Operations.TryGetValue(command.CommandId, out var previous))
        {
            if (previous != hash) throw new InvalidOperationException("PARAM.OPERATION_IDENTITY_MISMATCH");
            return new ServiceOk<GuidResult>(new(command.CommandId));
        }
        if (!state.Versions.TryGetValue(command.Version, out var selected)) throw new InvalidOperationException("PARAM.NOT_FOUND");
        var assigned = false;
        // All currently registered assignment scopes are checked under the shared writer lease held by the actor.
        foreach (var horizon in new[] { TomasAI.IFM.Domain.MarketData.Analytics.Shared.TimeFrameType.Daily, TomasAI.IFM.Domain.MarketData.Analytics.Shared.TimeFrameType.Weekly, TomasAI.IFM.Domain.MarketData.Analytics.Shared.TimeFrameType.Monthly })
        {
            var scope = WorkflowParameterScopeModel.Create(TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Identity.IntrinsicTimeStrategyWorkflowDefinition.Id, horizon);
            var identity = new ParameterAssignmentEntityId(WorkflowParameterScopeModel.AssignmentId(scope));
            var address = new AssignParameterVersionCommand { EntityId = identity, Scope = scope, Subject = new ActorSubject(ActorType.Command, AssignParameterVersionCommand.Actor, AssignParameterVersionCommand.Verb, identity.Format()) };
            var usage = await context.Assignments.LoadStateAsync(address);
            assigned |= usage.Assignment is { Enabled: true } current && current.Reference == selected.Reference;
        }
        var errorMsg = "ParameterSet.STATE.APPLY_FAILED: unable to apply RetireParameterVersion event";
        var updated = command.Compute(state, hash, assigned, out var parameterChange) switch
        {
            _ when parameterChange.RejectionReason is not null
                => command.UpdateFailed(ref errorMsg, parameterChange.RejectionReason),
            _ => state.Update(command.CreateParameterVersionRetiredEvent(parameterChange), command)
        };
        var result = updated
            ? new ServiceOk<GuidResult>(new GuidResult(command.CommandId))
            : command.UpdateFailed(errorMsg);
        return result;
    }
    /// <summary>Computes the proposed business change without changing actor state.</summary>
    /// <param name="command">The concrete lifecycle intent.</param>
    /// <param name="state">The authoritative state, read without mutation.</param>
    /// <param name="hash">The request identity hash.</param>
    /// <param name="assigned">Whether the selected version is currently assigned.</param>
    /// <param name="parameterChange">The proposed business change or rejection reason.</param>
    /// <returns>True when computation succeeds; otherwise false with its business rejection.</returns>
    internal static bool Compute(this RetireParameterVersionCommand command, ParameterSetCommandState state, string hash, bool assigned, out ParameterVersionChange parameterChange)
    {
        try
        {
            var parameterVersion = ParameterMutationModel.Decide(command, state.CatalogRevision, state.Versions, DateTime.UtcNow, assigned);
            parameterChange = new(parameterVersion, state.CatalogRevision + 1, hash, ParameterAuditModel.ForSet(command, state.Versions, parameterVersion));
            return true;
        }
        catch (Exception rejection) when (rejection is ArgumentException or InvalidOperationException or KeyNotFoundException)
        {
            parameterChange = new(null, state.CatalogRevision, hash, "", rejection.Message);
            return false;
        }
    }

    /// <summary>Creates the source event with the originating command identity.</summary>
    /// <param name="command">The originating command.</param>
    /// <param name="parameterChange">The accepted immutable business change.</param>
    /// <returns>The source event ready for state application.</returns>
    internal static ParameterVersionRetiredEvent CreateParameterVersionRetiredEvent(this RetireParameterVersionCommand command, ParameterVersionChange parameterChange)
        => new()
        {
            CommandId = command.CommandId,
            Subject = new ActorSubject(ActorType.Event, ParameterVersionRetiredEvent.Actor, ParameterVersionRetiredEvent.Verb, command.EntityId.Format()),
            EntityId = command.EntityId,
            Revision = parameterChange.CatalogRevision,
            RequestHash = parameterChange.RequestHash,
            VersionJson = JsonSerializer.Serialize(parameterChange.ParameterVersion),
            AuditJson = parameterChange.ParameterAuditJson,
        };
}
