using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.Reference.Commands;
using TomasAI.IFM.Shared.Reference.Events;
using TomasAI.IFM.Domain.Reference.LookupType.Command.Exceptions;
using TomasAI.IFM.Domain.Reference.LookupType.Command.State;

namespace TomasAI.IFM.Domain.Reference.LookupType.Command;

public static class AddLookupType
{
    /// <summary>
    /// Executes the <see cref="AddLookupTypeCommand"/> against the provided <see cref="LookupTypeCommandState"/>.
    /// </summary>
    /// <param name="e">The add lookup type command to execute.</param>
    /// <param name="state">The current state of the lookup type.</param>
    /// <returns>true if the lookup type was successfully added; otherwise, false.</returns>
    /// <exception cref="AddLookupTypeException">Thrown if the lookup type already exists in the state.</exception>
    public static bool Execute(this AddLookupTypeCommand e, LookupTypeCommandState state)
    {
        return e switch
        {
            _ when state.LookupTypeExists(e.EntityId) => throw new AddLookupTypeException($"{e.CommandName}: lookupType {e.EntityId} already exists"),
            _ => state.Update(e.CreateLookupTypeAddedEvent(), e)
        };
    }

    /// <summary>
    /// Creates a <see cref="LookupTypeAddedEvent"/> from an <see cref="AddLookupTypeCommand"/>.
    /// </summary>
    /// <param name="e">The source add command containing entity identifiers, payload, and origin metadata.</param>
    /// <returns>A fully-populated added event ready to be applied to actor state.</returns>
    internal static LookupTypeAddedEvent CreateLookupTypeAddedEvent(this AddLookupTypeCommand e)
        => new()
        {
            CommandId = e.CommandId,
            Subject = new ActorSubject(ActorType.Event, LookupTypeAddedEvent.Actor, LookupTypeAddedEvent.Verb, e.EntityId.Format()),
            EntityId = e.EntityId,
            LookupType = e.LookupType,
            AddedOn = e.OriginatedOn,
            AddedBy = e.OriginatedBy
        };
}
