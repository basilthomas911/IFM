using TomasAI.IFM.Domain.Application.Actor.Command.State;
using TomasAI.IFM.Domain.Application.Shared.Commands;
using TomasAI.IFM.Domain.Application.Shared.Events;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Application.Actor.Command;

internal static class ShutdownApplication
{
    /// <summary>
    /// Handle a <see cref="ShutdownApplicationCommand"/> by building the corresponding
    /// <see cref="ApplicationShutdownEvent"/> and updating the actor state.
    /// </summary>
    /// <param name="e">The shutdown command to execute.</param>
    /// <param name="state">The current actor command state to update.</param>
    /// <returns>The standard command service result containing the command identity.</returns>
    public static ServiceResult<GuidResult> Execute(this ShutdownApplicationCommand e, ApplicationCommandState state)
        => e.UpdateResult(() => state.Update(e.CreateApplicationShutdownEvent(), e));

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
