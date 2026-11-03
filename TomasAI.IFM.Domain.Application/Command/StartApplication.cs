using TomasAI.IFM.Domain.Application.Command.State;
using TomasAI.IFM.Domain.Application.Shared.Commands;
using TomasAI.IFM.Domain.Application.Shared.Events;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Application.Command;

internal static class StartApplication
{

    /// <param name="e">
    /// The start command to execute.
    /// The command containing the application startup details.
    /// </param>
    extension(StartApplicationCommand e)
    {
        /// <summary>
        /// Handle a <see cref="StartApplicationCommand"/> by building the corresponding
        /// <see cref="ApplicationStartupEvent"/> and updating the actor state.
        /// </summary>
        ///
        /// <param name="state">The current actor command state to update.</param>
        /// <returns>The standard command service result containing the command identity.</returns>
        public ServiceResult<GuidResult> Execute(ApplicationCommandState state)
            => e.UpdateResult(() => state.Update(e.CreateApplicationStartupEvent(), e));

        /// <summary>
        /// Creates an <see cref="ApplicationStartupEvent"/> from a <see cref="StartApplicationCommand"/>.
        /// </summary>
        ///
        /// <returns>A fully populated <see cref="ApplicationStartupEvent"/>.</returns>
        ApplicationStartupEvent CreateApplicationStartupEvent()
            => new()
            {
                CommandId = e.CommandId,
                Subject = new ActorSubject(ActorType.Event, ApplicationStartupEvent.Actor, ApplicationStartupEvent.Verb, e.EntityId.Format()),
                EntityId = e.EntityId,
                CreatedOn = e.OriginatedOn,
                CreatedBy = e.OriginatedBy
            };
    }
}
