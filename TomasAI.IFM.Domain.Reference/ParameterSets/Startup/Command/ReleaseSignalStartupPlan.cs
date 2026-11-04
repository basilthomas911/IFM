using TomasAI.IFM.Domain.Reference.ParameterSets.Startup.Command.State;
using TomasAI.IFM.Domain.Reference.ParameterSets.Startup.Command.Model;
using TomasAI.IFM.Domain.Reference.ParameterSets.Startup.Command.Actor;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Domain.Reference.Shared.ParameterSets;
using TomasAI.IFM.Domain.Reference.ParameterSets.Command.Actor;
using TomasAI.IFM.Domain.Reference.ParameterSets.Command.State;
using TomasAI.IFM.Domain.Reference.ParameterSets.Model;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Domain.Reference.ParameterSets.Command.Model;
namespace TomasAI.IFM.Domain.Reference.ParameterSets.Startup.Command;

public static class ReleaseSignalStartupPlan
{

    /// <param name="command">
    /// The concrete startup intent.
    /// The originating command.
    /// </param>
    extension(ReleaseSignalStartupPlanCommand command)
    {
        /// <summary>Authorizes the startup request and applies its computed source event.</summary>
        ///
        /// <param name="context">The authorized startup services.</param>
        /// <param name="state">The authoritative startup state.</param>
        /// <param name="logger">The actor logger.</param>
        /// <returns>The command acceptance or failure.</returns>
        public Task<ServiceResult<GuidResult>> ExecuteAsync(IParameterStartupCommandContext context, ParameterStartupCommandState state, ILogger<ParameterStartupCommandActor> logger)
        {
            ArgumentNullException.ThrowIfNull(command);
            ArgumentNullException.ThrowIfNull(context);
            ArgumentNullException.ThrowIfNull(state);
            context.AccessPolicy.Demand(ParameterCapability.Assign);
            if (!state.ActiveRuns.ContainsKey(command.RunId)) return Task.FromResult<ServiceResult<GuidResult>>(new ServiceOk<GuidResult>(new(command.CommandId)));
            var errorMsg = "ParameterStartup.STATE.APPLY_FAILED: unable to apply ReleaseSignalStartupPlan event";
            var updated = command.Compute(state, out var startupChange) switch
            {
                _ when startupChange.RejectionReason is not null
                    => command.UpdateFailed(ref errorMsg, startupChange.RejectionReason),
                _ => state.Update(command.CreateParameterStartupChangedEvent(startupChange), command)
            };
            var result = updated
                ? new ServiceOk<GuidResult>(new GuidResult(command.CommandId))
                : command.UpdateFailed(errorMsg);
            return Task.FromResult<ServiceResult<GuidResult>>(result);
        }

        /// <summary>Computes a startup change without mutating active runs or reports.</summary>
        ///
        /// <param name="state">The authoritative startup state.</param>
        /// <param name="startupChange">The proposed startup change or rejection.</param>
        /// <returns>True when the startup change is valid.</returns>
        internal bool Compute(ParameterStartupCommandState state, out ParameterStartupChange startupChange)
        {
            try
            {
                if (command.ExpectedFingerprint.Length != 0 && state.ActiveRuns.TryGetValue(command.RunId, out var run)
                    && !string.Equals(command.ExpectedFingerprint, run.Plan.Fingerprint, StringComparison.Ordinal))
                    throw new InvalidOperationException("PARAM.STARTUP_PLAN_CHANGED");
                startupChange = new(state.Revision + 1, Released: true);
                return true;
            }
            catch (Exception rejection) when (rejection is ArgumentException or InvalidOperationException)
            {
                startupChange = new(state.Revision, RejectionReason: rejection.Message);
                return false;
            }
        }

        /// <summary>Creates the startup source event with the originating command identity.</summary>
        ///
        /// <param name="startupChange">The accepted startup change.</param>
        /// <returns>The source event ready for state application.</returns>
        internal ParameterStartupChangedEvent CreateParameterStartupChangedEvent(ParameterStartupChange startupChange)
            => new()
            {
                CommandId = command.CommandId,
                Subject = new ActorSubject(ActorType.Event, ParameterStartupChangedEvent.Actor, ParameterStartupChangedEvent.Verb, command.EntityId.Format()),
                EntityId = command.EntityId,
                RunId = command.RunId,
                Revision = startupChange.Revision,
                Run = startupChange.StartupRun,
                Report = startupChange.StartupReport,
                Released = startupChange.Released
            };
    }
}
