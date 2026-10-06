using TomasAI.IFM.Domain.Reference.Configuration.Strategy.Command.Model;
using TomasAI.IFM.Domain.Reference.Configuration.Strategy.Command.State;
using TomasAI.IFM.Domain.Reference.Configuration.Strategy.Command.Events;
using TomasAI.IFM.Domain.Reference.Shared.Configuration.Strategy;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
namespace TomasAI.IFM.Domain.Reference.Configuration.Strategy.Command;
/// <summary>Handles the created Regime Discovery lifecycle.</summary>
public static class CreateRegimeDiscoveryParameterSet
{
    /// <summary>Checks the lifecycle and applies one computed source event.</summary>
    /// <param name="command">The concrete configuration command.</param>
    /// <param name="context">The command actor context.</param>
    /// <param name="state">The authoritative configuration state.</param>
    /// <returns>The command acceptance or business failure.</returns>
    public static Task<ServiceResult<GuidResult>> ExecuteAsync(this CreateRegimeDiscoveryParameterSetCommand command, ICommandActorContext context, RegimeDiscoveryConfigurationCommandState state)
    {
        if (state.ParameterSet is not null) return Task.FromResult<ServiceResult<GuidResult>>(new ServiceOk<GuidResult>(new GuidResult(command.CommandId)));
        var errorMsg = "RegimeDiscoveryConfiguration.STATE.APPLY_FAILED: unable to apply created event";
        var updated = command.Compute(state, out var configurationChange) switch
        {
            _ when configurationChange.RejectionReason is not null
                => command.UpdateFailed(ref errorMsg, configurationChange.RejectionReason),
            _ => state.Update(command.CreateRegimeDiscoveryParameterSetCreatedEvent(configurationChange), command)
        };
        return Task.FromResult(updated
            ? new ServiceOk<GuidResult>(new GuidResult(command.CommandId))
            : command.UpdateFailed(errorMsg));
    }
    /// <summary>Computes a lifecycle change without mutating configuration state.</summary>
    /// <param name="command">The concrete configuration intent.</param>
    /// <param name="state">The current configuration lifecycle.</param>
    /// <param name="configurationChange">The proposed configuration change or rejection.</param>
    /// <returns>True when the transition is valid.</returns>
    internal static bool Compute(this CreateRegimeDiscoveryParameterSetCommand command, RegimeDiscoveryConfigurationCommandState state, out RegimeDiscoveryConfigurationChange configurationChange)
    {
        configurationChange = state.Status != "Empty"
            ? new(RejectionReason: "RegimeDiscoveryConfiguration: only a Empty version can be created.")
            : new(command.ParameterSet with { }, command.Description, command.CreatedBy);
        return configurationChange.RejectionReason is null;
    }
    /// <summary>Creates the source event with its originating command identity.</summary>
    /// <param name="command">The originating command.</param>
    /// <param name="configurationChange">The accepted immutable business change.</param>
    /// <returns>The source event ready for state application.</returns>
    internal static RegimeDiscoveryParameterSetCreatedEvent CreateRegimeDiscoveryParameterSetCreatedEvent(this CreateRegimeDiscoveryParameterSetCommand command, RegimeDiscoveryConfigurationChange configurationChange)
        => new()
        {
            CommandId = command.CommandId,
            Subject = new ActorSubject(ActorType.Event, RegimeDiscoveryParameterSetCreatedEvent.Actor, RegimeDiscoveryParameterSetCreatedEvent.Verb, command.EntityId.Format()),
            EntityId = command.EntityId,
            ParameterSet = configurationChange.ParameterSet!,
            Description = configurationChange.Description,
            CreatedBy = configurationChange.CreatedBy,
        };
}
