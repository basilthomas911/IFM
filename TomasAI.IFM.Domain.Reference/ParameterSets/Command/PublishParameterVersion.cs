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

public static class PublishParameterVersion
{
    /// <summary>Authorizes the request, checks replay, and applies its computed source event.</summary>
    /// <param name="command">The concrete lifecycle intent.</param>
    /// <param name="context">The authorized command services and repositories.</param>
    /// <param name="state">The authoritative actor state.</param>
    /// <param name="logger">The actor logger.</param>
    /// <returns>The command acceptance or business failure.</returns>
    public static Task<ServiceResult<GuidResult>> ExecuteAsync(this PublishParameterVersionCommand command, IParameterSetCommandContext context, ParameterSetCommandState state, ILogger<ParameterSetCommandActor> logger)
    {
        ArgumentNullException.ThrowIfNull(context); ArgumentNullException.ThrowIfNull(logger);
        context.AccessPolicy.Demand(ParameterCapability.Publish);
        var hash = ParameterMutationModel.RequestHash(command);
        if (state.Operations.TryGetValue(command.CommandId, out var previous))
        {
            if (previous != hash) throw new InvalidOperationException("PARAM.OPERATION_IDENTITY_MISMATCH");
            return Task.FromResult<ServiceResult<GuidResult>>(new ServiceOk<GuidResult>(new(command.CommandId)));
        }
        var errorMsg = "ParameterSet.STATE.APPLY_FAILED: unable to apply PublishParameterVersion event";
        var updated = command.Compute(state, hash, out var parameterChange) switch
        {
            _ when parameterChange.RejectionReason is not null
                => command.UpdateFailed(ref errorMsg, parameterChange.RejectionReason),
            _ => state.Update(command.CreateParameterVersionPublishedEvent(parameterChange), command)
        };
        var result = updated
            ? new ServiceOk<GuidResult>(new GuidResult(command.CommandId))
            : command.UpdateFailed(errorMsg);
        return Task.FromResult<ServiceResult<GuidResult>>(result);
    }
    /// <summary>Computes the proposed business change without changing actor state.</summary>
    /// <param name="command">The concrete lifecycle intent.</param>
    /// <param name="state">The authoritative state, read without mutation.</param>
    /// <param name="hash">The request identity hash.</param>
    /// <param name="parameterChange">The proposed business change or rejection reason.</param>
    /// <returns>True when computation succeeds; otherwise false with its business rejection.</returns>
    internal static bool Compute(this PublishParameterVersionCommand command, ParameterSetCommandState state, string hash, out ParameterVersionChange parameterChange)
    {
        try
        {
            var parameterVersion = ParameterMutationModel.Decide(command, state.CatalogRevision, state.Versions, DateTime.UtcNow);
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
    internal static ParameterVersionPublishedEvent CreateParameterVersionPublishedEvent(this PublishParameterVersionCommand command, ParameterVersionChange parameterChange)
        => new()
        {
            CommandId = command.CommandId,
            Subject = new ActorSubject(ActorType.Event, ParameterVersionPublishedEvent.Actor, ParameterVersionPublishedEvent.Verb, command.EntityId.Format()),
            EntityId = command.EntityId,
            Revision = parameterChange.CatalogRevision,
            RequestHash = parameterChange.RequestHash,
            VersionJson = JsonSerializer.Serialize(parameterChange.ParameterVersion),
            AuditJson = parameterChange.ParameterAuditJson,
        };
}
