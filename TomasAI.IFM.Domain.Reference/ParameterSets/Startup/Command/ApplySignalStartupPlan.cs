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

public static class ApplySignalStartupPlan
{

    /// <param name="command">
    /// The concrete startup intent.
    /// The originating command.
    /// </param>
    extension(ApplySignalStartupPlanCommand command)
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
            if (state.RunId != Guid.Empty && state.RunId == command.RunId || state.ActiveRuns.ContainsKey(command.RunId)) return Task.FromResult<ServiceResult<GuidResult>>(new ServiceOk<GuidResult>(new(command.CommandId)));
            var evidence = command.Evidence ?? throw new InvalidOperationException("PARAM.EVIDENCE_REQUIRED");
            if (evidence.Assignments.IsDefault || evidence.Versions.IsDefault) throw new InvalidOperationException("PARAM.EVIDENCE_REQUIRED");
            var snapshot = new ParameterStartupSnapshotModel(command.RunId, evidence.Assignments,
                evidence.Versions.ToDictionary(version => version.Reference));
            var errorMsg = "ParameterStartup.STATE.APPLY_FAILED: unable to apply ApplySignalStartupPlan event";
            var updated = command.Compute(state, snapshot, out var startupChange) switch
            {
                _ when startupChange.RejectionReason is not null
                    => command.UpdateFailed(ref errorMsg, startupChange.RejectionReason),
                _ => state.Update(command.CreateParameterStartupCreatedEvent(startupChange), command)
            };
            var result = updated
                ? new ServiceOk<GuidResult>(new GuidResult(command.CommandId))
                : command.UpdateFailed(errorMsg);
            return Task.FromResult(result);
        }

        /// <summary>Computes a startup change without mutating active runs or reports.</summary>
        ///
        /// <param name="state">The authoritative startup state.</param>
        /// <param name="snapshot">The exact parameter assignments frozen under the startup lease.</param>
        /// <param name="startupChange">The proposed startup change or rejection.</param>
        /// <returns>True when the startup change is valid.</returns>
        internal bool Compute(ParameterStartupCommandState state, ParameterStartupSnapshotModel snapshot, out ParameterStartupChange startupChange)
        {
            try
            {
                // Generic parameter activation freezes exact assignments only. Domain consumers such as
                // Regime Discovery derive their own startup requirements when their actor workflow starts.
                var plan = new ParameterSignalStartupPlan(command.RunId, snapshot.Fingerprint,
                    snapshot.Fingerprint, [], null);
                if (command.ExpectedFingerprint.Length != 0 && !string.Equals(command.ExpectedFingerprint, plan.Fingerprint, StringComparison.Ordinal)) throw new InvalidOperationException("PARAM.STARTUP_PLAN_CHANGED");
                var run = new ParameterStartupRun(command.RunId, snapshot.Scopes.ToArray(), snapshot.Assignments.Select(x => x.Version).DistinctBy(x => x.Reference).ToArray(), plan, DateTime.UtcNow, command.OriginatedBy);
                startupChange = new(state.Revision + 1, StartupRun: run);
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
        internal ParameterStartupCreatedEvent CreateParameterStartupCreatedEvent(ParameterStartupChange startupChange)
            => new()
            {
                CommandId = command.CommandId,
                Subject = new ActorSubject(ActorType.Event, ParameterStartupCreatedEvent.Actor, ParameterStartupCreatedEvent.Verb, command.EntityId.Format()),
                EntityId = command.EntityId,
                RunId = command.RunId,
                Revision = startupChange.Revision,
                Run = startupChange.StartupRun,
                Report = startupChange.StartupReport,
                Released = startupChange.Released
            };
    }
}
