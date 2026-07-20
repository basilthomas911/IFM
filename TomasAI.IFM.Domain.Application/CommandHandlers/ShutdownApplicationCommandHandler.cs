using TomasAI.IFM.Shared.Application.Commands;
using TomasAI.IFM.Shared.Application.Events;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Application.CommandHandlers;

public class ShutdownApplicationCommandHandler
    : BaseBoundedContextCommandHandler<ApplicationBoundedContextState>,
    IBoundedContextCommandHandler<ShutdownApplicationCommand, ApplicationBoundedContextState>
{
    /// <summary>
    /// Handles the shutdown application command by applying the corresponding event to the state.
    /// </summary>
    /// <param name="e"></param>
    /// <param name="state"></param>
    /// <returns></returns>
    public bool Execute(ShutdownApplicationCommand e, ApplicationBoundedContextState state)
        => state.Update(new ApplicationShutdownEvent
        {
            CreatedOn = e.OriginatedOn,
            CreatedBy = e.OriginatedBy
        }, e);
}
