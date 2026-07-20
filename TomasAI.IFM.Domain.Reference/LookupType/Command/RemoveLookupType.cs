using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.Reference.Commands;
using TomasAI.IFM.Shared.Reference.Events;
using TomasAI.IFM.Domain.Reference.LookupType.Command.Exceptions;
using TomasAI.IFM.Domain.Reference.LookupType.Command.State;

namespace TomasAI.IFM.Domain.Reference.LookupType.Command;

public static class RemoveLookupType
{
    /// <summary>
    /// Executes the <see cref="RemoveLookupTypeCommand"/> against the provided <see cref="LookupTypeCommandState"/>.
    /// </summary>
    /// <param name="e">The remove command to execute. Cannot be null.</param>
    /// <param name="state">The current state of the lookup type. Cannot be null.</param>
    /// <returns>true if the lookup type was successfully removed; otherwise, false.</returns>
    /// <exception cref="RemoveLookupTypeException">Thrown if the lookup type does not exist in the state.</exception>
    public static bool Execute(this RemoveLookupTypeCommand e, LookupTypeCommandState state)
    {
        return e switch
        {
            _ when !state.LookupTypeExists(e.EntityId) && !e.Overwrite => throw new RemoveLookupTypeException($"{e.CommandName}: lookupType {e.EntityId} does not exist"),
            _ => state.Update(e.CreateLookupTypeRemovedEvent(), e)
        };
    }

    /// <summary>
    /// Creates a <see cref="LookupTypeRemovedEvent"/> from a <see cref="RemoveLookupTypeCommand"/>.
    /// </summary>
    /// <param name="e">The source remove command containing entity identifiers and origin metadata.</param>
    /// <returns>A fully-populated removed event ready to be applied to actor state.</returns>
    internal static LookupTypeRemovedEvent CreateLookupTypeRemovedEvent(this RemoveLookupTypeCommand e)
        => new()
        {
            CommandId = e.CommandId,
            Subject = new ActorSubject(ActorType.Event, LookupTypeRemovedEvent.Actor, LookupTypeRemovedEvent.Verb, e.EntityId.Format()),
            EntityId = e.EntityId,
            LookupTypeId = e.LookupTypeId,
            RemovedOn = e.OriginatedOn,
            RemovedBy = e.OriginatedBy
        };
}
