using System.Text.Json;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Domain.Reference.Shared.ParameterSets;
using TomasAI.IFM.Domain.Reference.ParameterSets.Model;
using TomasAI.IFM.Domain.Reference.ParameterSets.Command.Actor;
using TomasAI.IFM.Domain.Reference.ParameterSets.Command.State;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Domain.Reference.ParameterSets.Command.Model;
namespace TomasAI.IFM.Domain.Reference.ParameterSets.Command;

public static class AssignParameterVersion
{
    /// <summary>Authorizes the request, checks replay, and applies its computed source event.</summary>
    /// <param name="command">The concrete lifecycle intent.</param>
    /// <param name="context">The authorized command services and repositories.</param>
    /// <param name="state">The authoritative actor state.</param>
    /// <param name="logger">The actor logger.</param>
    /// <returns>The command acceptance or business failure.</returns>
    public static async Task<ServiceResult<GuidResult>> ExecuteAsync(this AssignParameterVersionCommand command, IParameterAssignmentCommandContext context, ParameterAssignmentCommandState state, ILogger<ParameterAssignmentCommandActor> logger)
    {
        ArgumentNullException.ThrowIfNull(context); ArgumentNullException.ThrowIfNull(logger);
        context.AccessPolicy.Demand(ParameterCapability.Assign);
        if (command.Scope is null || command.Reference is null || command.EntityId.AssignmentId != ParameterAssignmentPolicyModel.AssignmentId(command.Scope)) throw new ArgumentException("PARAM.IDENTITY_INVALID");
        var hash = ParameterCanonicalPayloadModel.Hash(JsonSerializer.Serialize(new { command.CommandName, command.EntityId, command.ExpectedRevision, command.Scope, command.Reference }));
        if (state.Operations.TryGetValue(command.CommandId, out var prior))
        { if (prior != hash) throw new InvalidOperationException("PARAM.OPERATION_IDENTITY_MISMATCH"); return new ServiceOk<GuidResult>(new(command.CommandId)); }
        var identity = new ParameterSetEntityId(command.Reference.SetId);
        var address = new CreateParameterSetCommand { EntityId = identity, Subject = new ActorSubject(ActorType.Command, CreateParameterSetCommand.Actor, CreateParameterSetCommand.Verb, identity.Format()) };
        var set = await context.ParameterSets.LoadStateAsync(address);
        if (!set.Versions.TryGetValue(command.Reference.Version, out var version) || version.Reference != command.Reference) throw new InvalidOperationException("PARAM.EXACT_VERSION_MISSING");
        var errorMsg = "ParameterAssignment.STATE.APPLY_FAILED: unable to apply AssignParameterVersion event";
        var updated = command.Compute(state, hash, version, out var parameterChange) switch
        {
            _ when parameterChange.RejectionReason is not null
                => command.UpdateFailed(ref errorMsg, parameterChange.RejectionReason),
            _ => state.Update(command.CreateParameterAssignmentChangedEvent(parameterChange), command)
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
    /// <param name="version">The exact committed parameter version.</param>
    /// <param name="parameterChange">The proposed business change or rejection reason.</param>
    /// <returns>True when computation succeeds; otherwise false with its business rejection.</returns>
    internal static bool Compute(this AssignParameterVersionCommand command, ParameterAssignmentCommandState state, string hash, ParameterSetVersion version, out ParameterAssignmentChange parameterChange)
    {
        try
        {
            var parameterAssignment = ParameterAssignmentModel.Assign(command.Scope, version, state.Assignment, command.ExpectedRevision, DateTime.UtcNow, command.OriginatedBy);
            parameterChange = new(parameterAssignment, hash, ParameterAuditModel.ForAssignment(command.CommandId, command.CommandName, command.OriginatedBy, state.Assignment, parameterAssignment));
            return true;
        }
        catch (Exception rejection) when (rejection is ArgumentException or InvalidOperationException or KeyNotFoundException)
        {
            parameterChange = new(null, hash, "", rejection.Message);
            return false;
        }
    }

    /// <summary>Creates the source event with the originating command identity.</summary>
    /// <param name="command">The originating command.</param>
    /// <param name="parameterChange">The accepted immutable business change.</param>
    /// <returns>The source event ready for state application.</returns>
    internal static ParameterAssignmentChangedEvent CreateParameterAssignmentChangedEvent(this AssignParameterVersionCommand command, ParameterAssignmentChange parameterChange)
        => new()
        {
            CommandId = command.CommandId,
            Subject = new ActorSubject(ActorType.Event, ParameterAssignmentChangedEvent.Actor, ParameterAssignmentChangedEvent.Verb, command.EntityId.Format()),
            EntityId = command.EntityId,
            Revision = parameterChange.ParameterAssignment!.Revision,
            RequestHash = parameterChange.RequestHash,
            AssignmentJson = JsonSerializer.Serialize(parameterChange.ParameterAssignment),
            AuditJson = parameterChange.ParameterAuditJson,
        };
}
