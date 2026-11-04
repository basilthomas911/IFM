using TomasAI.IFM.Domain.Reference.Configuration.Strategy.Command.Model;
using TomasAI.IFM.Domain.Reference.Configuration.Strategy.Command.State;
using TomasAI.IFM.Domain.Reference.Configuration.Strategy.Command.Events;
using TomasAI.IFM.Domain.Reference.Shared.Configuration.Strategy;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
namespace TomasAI.IFM.Domain.Reference.Configuration.Strategy.Command;
/// <summary>Handles the retired Regime Discovery lifecycle.</summary>
public static class RetireRegimeDiscoveryParameterSet
{

    /// <param name="command">
    /// The concrete configuration command.
    /// The concrete configuration intent.
    /// The originating command.
    /// </param>
    extension(RetireRegimeDiscoveryParameterSetCommand command)
    {
        /// <summary>Checks the lifecycle and applies one computed source event.</summary>
        ///
        /// <param name="context">The command actor context.</param>
        /// <param name="state">The authoritative configuration state.</param>
        /// <returns>The command acceptance or business failure.</returns>
        public Task<ServiceResult<GuidResult>> ExecuteAsync(ICommandActorContext context, RegimeDiscoveryConfigurationCommandState state)
        {
            ArgumentNullException.ThrowIfNull(command);
            ArgumentNullException.ThrowIfNull(state);
            if (string.Equals(state.Status, "Retired", StringComparison.Ordinal)) return Task.FromResult<ServiceResult<GuidResult>>(new ServiceOk<GuidResult>(new GuidResult(command.CommandId)));
            var errorMsg = "RegimeDiscoveryConfiguration.STATE.APPLY_FAILED: unable to apply retired event";
            var updated = command.Compute(state, out var configurationChange) switch
            {
                _ when configurationChange.RejectionReason is not null
                    => command.UpdateFailed(ref errorMsg, configurationChange.RejectionReason),
                _ => state.Update(command.CreateRegimeDiscoveryParameterSetRetiredEvent(configurationChange) with
                {
                    ParameterSet = state.ParameterSet,
                    EffectiveFromUtc = state.EffectiveFromUtc
                }, command)
            };
            return Task.FromResult(updated
                ? new ServiceOk<GuidResult>(new GuidResult(command.CommandId))
                : command.UpdateFailed(errorMsg));
        }

        /// <summary>Computes a lifecycle change without mutating configuration state.</summary>
        ///
        /// <param name="state">The current configuration lifecycle.</param>
        /// <param name="configurationChange">The proposed configuration change or rejection.</param>
        /// <returns>True when the transition is valid.</returns>
        internal bool Compute(RegimeDiscoveryConfigurationCommandState state, out RegimeDiscoveryConfigurationChange configurationChange)
        {
            configurationChange = !string.Equals(state.Status, "Published", StringComparison.Ordinal)
                ? new(RejectionReason: "RegimeDiscoveryConfiguration: only a Published version can be retired.")
                : new(RetiredAtUtc: command.RetiredAtUtc);
            return configurationChange.RejectionReason is null;
        }

        /// <summary>Creates the source event with its originating command identity.</summary>
        ///
        /// <param name="configurationChange">The accepted immutable business change.</param>
        /// <returns>The source event ready for state application.</returns>
        internal RegimeDiscoveryConfigurationParameterSetRetiredEvent CreateRegimeDiscoveryParameterSetRetiredEvent(RegimeDiscoveryConfigurationChange configurationChange)
            => new()
            {
                CommandId = command.CommandId,
                Subject = new ActorSubject(ActorType.Event, RegimeDiscoveryConfigurationParameterSetRetiredEvent.Actor, RegimeDiscoveryConfigurationParameterSetRetiredEvent.Verb, command.EntityId.Format()),
                EntityId = command.EntityId,
                RetiredAtUtc = configurationChange.RetiredAtUtc,
            };
    }
}
