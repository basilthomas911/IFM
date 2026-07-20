using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.Reference.Commands;
using TomasAI.IFM.Shared.Reference.Events;
using TomasAI.IFM.Domain.Reference.LookupType.Command.Exceptions;
using TomasAI.IFM.Domain.Reference.LookupType.Command.State;

namespace TomasAI.IFM.Domain.Reference.LookupType.Command;

public static class ChangeLookupType
{
    /// <summary>
    /// Executes the <see cref="ChangeLookupTypeCommand"/> against the provided <see cref="LookupTypeCommandState"/>.
    /// </summary>
    /// <param name="e">The change command to execute. Cannot be null.</param>
    /// <param name="state">The current state of the lookup type. Cannot be null.</param>
    /// <returns>true if the lookup type was successfully changed; otherwise, false.</returns>
    /// <exception cref="ChangeLookupTypeException">Thrown if the lookup type does not exist in the state.</exception>
    public static bool Execute(this ChangeLookupTypeCommand e, LookupTypeCommandState state)
    {
        return e switch
        {
            _ when !state.LookupTypeExists(e.EntityId) && !e.Overwrite => throw new ChangeLookupTypeException($"{e.CommandName}: lookupType {e.EntityId} does not exist"),
            _ => state.Update(e.CreateLookupTypeChangedEvent(), e)
        };
    }

    /// <summary>
    /// Creates a <see cref="LookupTypeChangedEvent"/> from a <see cref="ChangeLookupTypeCommand"/>.
    /// </summary>
    /// <param name="e">The source change command containing entity identifiers, payload, and origin metadata.</param>
    /// <returns>A fully-populated changed event ready to be applied to actor state.</returns>
    internal static LookupTypeChangedEvent CreateLookupTypeChangedEvent(this ChangeLookupTypeCommand e)
        => new()
        {
            CommandId = e.CommandId,
            Subject = new ActorSubject(ActorType.Event, LookupTypeChangedEvent.Actor, LookupTypeChangedEvent.Verb, e.EntityId.Format()),
            EntityId = e.EntityId,
            LookupTypeId = e.LookupTypeId,
            LookupType = e.LookupType,
            ChangedOn = e.OriginatedOn,
            ChangedBy = e.OriginatedBy
        };
}
