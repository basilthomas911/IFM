using TomasAI.IFM.Domain.Application.Actor.Command.State;
using TomasAI.IFM.Shared.Application.Commands;
using TomasAI.IFM.Shared.Application.Events;
using TomasAI.IFM.Shared.EventModelActor;

namespace TomasAI.IFM.Domain.Application.Actor.Command.Handlers;

internal static class ShutdownApplicationCommandHandler
{
    /// <summary>
    /// Handle a <see cref="ShutdownApplicationCommand"/> by building the corresponding
    /// <see cref="ApplicationShutdownEvent"/> and updating the actor state.
    /// </summary>
    /// <param name="e">The shutdown command to execute.</param>
    /// <param name="state">The current actor command state to update.</param>
    /// <returns><see langword="true"/> if the state was updated successfully; otherwise <see langword="false"/>.</returns>
    public static bool Execute(this ShutdownApplicationCommand e, ApplicationCommandState state)
        => state.Update(e.CreateApplicationShutdownEvent(), e);

    /// <summary>
    /// Creates an <see cref="ApplicationShutdownEvent"/> from a <see cref="ShutdownApplicationCommand"/>.
    /// </summary>
    /// <param name="e">The command containing the application shutdown details.</param>
    /// <returns>A fully populated <see cref="ApplicationShutdownEvent"/>.</returns>
    static ApplicationShutdownEvent CreateApplicationShutdownEvent(this ShutdownApplicationCommand e)
        => new()
        {
            CommandId = e.CommandId,
            Subject = new ActorSubject(ActorType.Event, ApplicationShutdownEvent.Actor, ApplicationShutdownEvent.Verb, e.EntityId.Format()),
            EntityId = e.EntityId,
            CreatedOn = e.OriginatedOn,
            CreatedBy = e.OriginatedBy
        };
}
