using TomasAI.IFM.Domain.Application.Actor.Command.State;
using TomasAI.IFM.Shared.Application.Commands;
using TomasAI.IFM.Shared.Application.Events;
using TomasAI.IFM.Shared.EventModelActor;

namespace TomasAI.IFM.Domain.Application.Actor.Command.Handlers;

internal static class StartApplicationCommandHandler
{

    /// <summary>
    /// Handle a <see cref="StartApplicationCommand"/> by building the corresponding
    /// <see cref="ApplicationStartupEvent"/> and updating the actor state.
    /// </summary>
    /// <param name="e">The start command to execute.</param>
    /// <param name="state">The current actor command state to update.</param>
    /// <returns><see langword="true"/> if the state was updated successfully; otherwise <see langword="false"/>.</returns>
    public static bool Execute(this StartApplicationCommand e, ApplicationCommandState state)
        => state.Update(e.CreateApplicationStartupEvent(), e);

    /// <summary>
    /// Creates an <see cref="ApplicationStartupEvent"/> from a <see cref="StartApplicationCommand"/>.
    /// </summary>
    /// <param name="e">The command containing the application startup details.</param>
    /// <returns>A fully populated <see cref="ApplicationStartupEvent"/>.</returns>
    static ApplicationStartupEvent CreateApplicationStartupEvent(this StartApplicationCommand e)
        => new()
        {
            CommandId = e.CommandId,
            Subject = new ActorSubject(ActorType.Event, ApplicationStartupEvent.Actor, ApplicationStartupEvent.Verb, e.EntityId.Format()),
            EntityId = e.EntityId,
            CreatedOn = e.OriginatedOn,
            CreatedBy = e.OriginatedBy
        };
}
