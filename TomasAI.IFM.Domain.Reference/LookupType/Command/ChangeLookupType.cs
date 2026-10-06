using TomasAI.IFM.Domain.Reference.Shared.Commands;
using TomasAI.IFM.Domain.Reference.Shared.Events;
using TomasAI.IFM.Domain.Reference.Shared;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Domain.Reference.LookupType.Command.Exceptions;
using TomasAI.IFM.Domain.Reference.LookupType.Command.State;

using TomasAI.IFM.Domain.Reference.LookupType.Command.Model;
namespace TomasAI.IFM.Domain.Reference.LookupType.Command;

public static class ChangeLookupType
{
    /// <summary>
    /// Executes the <see cref="ChangeLookupTypeCommand"/> against the provided <see cref="LookupTypeCommandState"/>.
    /// </summary>
    /// <param name="e">The change command to execute. Cannot be null.</param>
    /// <param name="state">The current state of the lookup type. Cannot be null.</param>
    /// <returns>The command acceptance or business failure.</returns>
    public static ServiceResult<GuidResult> Execute(this ChangeLookupTypeCommand e, LookupTypeCommandState state)
    {
        var errorMsg = "LookupType.STATE.APPLY_FAILED: unable to apply changed lookup event";
        var updated = e.Compute(state, out var lookupChange) switch
        {
            _ when lookupChange.RejectionReason is not null
                => e.UpdateFailed(ref errorMsg, lookupChange.RejectionReason),
            _ => state.Update(e.CreateLookupTypeChangedEvent(lookupChange), e)
        };
        return updated
            ? new ServiceOk<GuidResult>(new GuidResult(e.CommandId))
            : e.UpdateFailed(errorMsg);
    }

    /// <summary>
    /// Creates a <see cref="LookupTypeChangedEvent"/> from a <see cref="ChangeLookupTypeCommand"/>.
    /// </summary>
    /// <param name="e">The source change command containing entity identifiers, payload, and origin metadata.</param>
    /// <param name="lookupChange">The accepted lookup definition.</param>
    /// <returns>A fully-populated changed event ready to be applied to actor state.</returns>
    internal static LookupTypeChangedEvent CreateLookupTypeChangedEvent(this ChangeLookupTypeCommand e, LookupTypeChange lookupChange)
        => new()
        {
            CommandId = e.CommandId,
            Subject = new ActorSubject(ActorType.Event, LookupTypeChangedEvent.Actor, LookupTypeChangedEvent.Verb, e.EntityId.Format()),
            EntityId = e.EntityId,
            LookupTypeId = e.LookupTypeId,
            LookupType = lookupChange.LookupType!,
            ChangedOn = e.OriginatedOn,
            ChangedBy = e.OriginatedBy
        };
    /// <summary>Computes the lookup business change without mutating state.</summary>
    /// <param name="e">The concrete lookup command.</param>
    /// <param name="state">The authoritative lookup state.</param>
    /// <param name="lookupChange">The proposed lookup definition or business rejection.</param>
    /// <returns>True when the lookup transition is valid.</returns>
    internal static bool Compute(this ChangeLookupTypeCommand e, LookupTypeCommandState state, out LookupTypeChange lookupChange)
    {
        lookupChange = !state.LookupTypeExists(e.EntityId) && !e.Overwrite
            ? new(null, $"{e.CommandName}: lookupType {e.EntityId} does not exist")
            : e.LookupType is null
                ? new(null, "LookupType: the lookup definition is required.")
                : new(e.LookupType with { });
        return lookupChange.RejectionReason is null;
    }
}
