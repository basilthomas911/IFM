using System.Text.Json;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Domain.Reference.Shared.ParameterSets;
using TomasAI.IFM.Domain.Reference.ParameterSets.Command.Actor;
using TomasAI.IFM.Domain.Reference.ParameterSets.Command.State;
using TomasAI.IFM.Domain.Reference.ParameterSets.Model;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Domain.Reference.ParameterSets.Command.Model;
namespace TomasAI.IFM.Domain.Reference.ParameterSets.Command;

public static class ApplySignalStartupPlan
{
    /// <summary>Authorizes the startup request and applies its computed source event.</summary>
    /// <param name="command">The concrete startup intent.</param>
    /// <param name="context">The authorized startup services.</param>
    /// <param name="state">The authoritative startup state.</param>
    /// <param name="logger">The actor logger.</param>
    /// <returns>The command acceptance or failure.</returns>
    public static async Task<ServiceResult<GuidResult>> ExecuteAsync(this ApplySignalStartupPlanCommand command, IParameterStartupCommandContext context, ParameterStartupCommandState state, ILogger<ParameterStartupCommandActor> logger)
    {
        context.AccessPolicy.Demand(ParameterCapability.Assign);
        if (state.ActiveRuns.ContainsKey(command.RunId)) return new ServiceOk<GuidResult>(new(command.CommandId));
        var snapshot = await ParameterStartupReader.ReadUnderLeaseAsync(command.RunId, context.Assignments, context.ParameterSets, CancellationToken.None);
        var errorMsg = "ParameterStartup.STATE.APPLY_FAILED: unable to apply ApplySignalStartupPlan event";
        var updated = command.Compute(state, snapshot, out var startupChange) switch
        {
            _ when startupChange.RejectionReason is not null
                => command.UpdateFailed(ref errorMsg, startupChange.RejectionReason),
            _ => state.Update(command.CreateParameterStartupChangedEvent(startupChange), command)
        };
        var result = updated
            ? new ServiceOk<GuidResult>(new GuidResult(command.CommandId))
            : command.UpdateFailed(errorMsg);
        return result;
    }

    /// <summary>Computes a startup change without mutating active runs or reports.</summary>
    /// <param name="command">The concrete startup intent.</param>
    /// <param name="state">The authoritative startup state.</param>
    /// <param name="snapshot">The exact parameter assignments frozen under the startup lease.</param>
    /// <param name="startupChange">The proposed startup change or rejection.</param>
    /// <returns>True when the startup change is valid.</returns>
    internal static bool Compute(this ApplySignalStartupPlanCommand command, ParameterStartupCommandState state, ParameterStartupSnapshotModel snapshot, out ParameterStartupChange startupChange)
    {
        try
        {
            // Generic parameter activation freezes exact assignments only. Domain consumers such as
            // Regime Discovery derive their own startup requirements when their actor workflow starts.
            var plan = new ParameterSignalStartupPlan(command.RunId, snapshot.Fingerprint,
                snapshot.Fingerprint, [], null);
            if (command.ExpectedFingerprint.Length != 0 && command.ExpectedFingerprint != plan.Fingerprint) throw new InvalidOperationException("PARAM.STARTUP_PLAN_CHANGED");
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
    /// <param name="command">The originating command.</param>
    /// <param name="startupChange">The accepted startup change.</param>
    /// <returns>The source event ready for state application.</returns>
    internal static ParameterStartupChangedEvent CreateParameterStartupChangedEvent(this ApplySignalStartupPlanCommand command, ParameterStartupChange startupChange)
        => new()
        {
            CommandId = command.CommandId,
            Subject = new ActorSubject(ActorType.Event, ParameterStartupChangedEvent.Actor, ParameterStartupChangedEvent.Verb, command.EntityId.Format()),
            EntityId = command.EntityId,
            RunId = command.RunId,
            Revision = startupChange.Revision,
            RunJson = startupChange.StartupRun is null ? "" : JsonSerializer.Serialize(startupChange.StartupRun),
            ReportJson = startupChange.StartupReport is null ? "" : JsonSerializer.Serialize(startupChange.StartupReport),
            Released = startupChange.Released
        };
}
