using TomasAI.IFM.Domain.Application.Command.State;
using TomasAI.IFM.Domain.Application.Shared.Commands;
using TomasAI.IFM.Domain.Application.Shared.Events;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Application.Command;

internal static class ShutdownApplication
{

    /// <param name="e">
    /// The shutdown command to execute.
    /// The command containing the application shutdown details.
    /// </param>
    extension(ShutdownApplicationCommand e)
    {
        /// <summary>
        /// Handle a <see cref="ShutdownApplicationCommand"/> by building the corresponding
        /// <see cref="ApplicationShutdownEvent"/> and updating the actor state.
        /// </summary>
        ///
        /// <param name="state">The current actor command state to update.</param>
        /// <returns>The standard command service result containing the command identity.</returns>
        public ServiceResult<GuidResult> Execute(ApplicationCommandState state)
            => e.UpdateResult(() => state.Update(e.CreateApplicationShutdownEvent(), e));

        /// <summary>
        /// Creates an <see cref="ApplicationShutdownEvent"/> from a <see cref="ShutdownApplicationCommand"/>.
        /// </summary>
        ///
        /// <returns>A fully populated <see cref="ApplicationShutdownEvent"/>.</returns>
        ApplicationShutdownEvent CreateApplicationShutdownEvent()
            => new()
            {
                CommandId = e.CommandId,
                Subject = new ActorSubject(ActorType.Event, ApplicationShutdownEvent.Actor, ApplicationShutdownEvent.Verb, e.EntityId.Format()),
                EntityId = e.EntityId,
                CreatedOn = e.OriginatedOn,
                CreatedBy = e.OriginatedBy
            };
    }
}
