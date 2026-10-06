using TomasAI.IFM.Domain.Reference.Shared.Commands;
using TomasAI.IFM.Domain.Reference.Shared.Events;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Domain.Reference.LookupType.Command.Exceptions;
using TomasAI.IFM.Domain.Reference.LookupType.Command.State;

using TomasAI.IFM.Domain.Reference.LookupType.Command.Model;
namespace TomasAI.IFM.Domain.Reference.LookupType.Command;

public static class AddLookupType
{
    /// <summary>
    /// Executes the <see cref="AddLookupTypeCommand"/> against the provided <see cref="LookupTypeCommandState"/>.
    /// </summary>
    /// <param name="e">The add lookup type command to execute.</param>
    /// <param name="state">The current state of the lookup type.</param>
    /// <returns>The command acceptance or business failure.</returns>
    public static ServiceResult<GuidResult> Execute(this AddLookupTypeCommand e, LookupTypeCommandState state)
    {
        var errorMsg = "LookupType.STATE.APPLY_FAILED: unable to apply added lookup event";
        var updated = e.Compute(state, out var lookupChange) switch
        {
            _ when lookupChange.RejectionReason is not null
                => e.UpdateFailed(ref errorMsg, lookupChange.RejectionReason),
            _ => state.Update(e.CreateLookupTypeAddedEvent(lookupChange), e)
        };
        return updated
            ? new ServiceOk<GuidResult>(new GuidResult(e.CommandId))
            : e.UpdateFailed(errorMsg);
    }

    /// <summary>
    /// Creates a <see cref="LookupTypeAddedEvent"/> from an <see cref="AddLookupTypeCommand"/>.
    /// </summary>
    /// <param name="e">The source add command containing entity identifiers, payload, and origin metadata.</param>
    /// <param name="lookupChange">The accepted lookup definition.</param>
    /// <returns>A fully-populated added event ready to be applied to actor state.</returns>
    internal static LookupTypeAddedEvent CreateLookupTypeAddedEvent(this AddLookupTypeCommand e, LookupTypeChange lookupChange)
        => new()
        {
            CommandId = e.CommandId,
            Subject = new ActorSubject(ActorType.Event, LookupTypeAddedEvent.Actor, LookupTypeAddedEvent.Verb, e.EntityId.Format()),
            EntityId = e.EntityId,
            LookupType = lookupChange.LookupType!,
            AddedOn = e.OriginatedOn,
            AddedBy = e.OriginatedBy
        };
    /// <summary>Computes the lookup business change without mutating state.</summary>
    /// <param name="e">The concrete lookup command.</param>
    /// <param name="state">The authoritative lookup state.</param>
    /// <param name="lookupChange">The proposed lookup definition or business rejection.</param>
    /// <returns>True when the lookup transition is valid.</returns>
    internal static bool Compute(this AddLookupTypeCommand e, LookupTypeCommandState state, out LookupTypeChange lookupChange)
    {
        lookupChange = state.LookupTypeExists(e.EntityId)
            ? new(null, $"{e.CommandName}: lookupType {e.EntityId} already exists")
            : e.LookupType is null
                ? new(null, "LookupType: the lookup definition is required.")
                : new(e.LookupType with { });
        return lookupChange.RejectionReason is null;
    }
}
