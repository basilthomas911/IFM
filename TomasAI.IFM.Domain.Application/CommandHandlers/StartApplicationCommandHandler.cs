using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TomasAI.IFM.Shared.Application.Commands;
using TomasAI.IFM.Shared.Application.Events;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Application.CommandHandlers;

public class StartApplicationCommandHandler 
    : BaseBoundedContextCommandHandler<ApplicationBoundedContextState>,
    IBoundedContextCommandHandler<StartApplicationCommand, ApplicationBoundedContextState>
{
    /// <summary>
    /// Handles the StartApplicationCommand by applying the corresponding event to the application state.
    /// </summary>
    /// <param name="e"></param>
    /// <param name="state"></param>
    /// <returns></returns>
    public bool Execute(StartApplicationCommand e, ApplicationBoundedContextState state)
        => state.Update(new ApplicationStartupEvent
        {
            CreatedOn = e.OriginatedOn,
            CreatedBy = e.OriginatedBy
        }, e);
}
