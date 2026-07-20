using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Service.ErrorConsole
{
    public interface IErrorConsoleService
    {
        Task ExecuteAsync(CommandExceptionEvent e);
        Task ExecuteAsync(QueryExceptionEvent e);
        Task ExecuteAsync(EventServiceExceptionEvent e);
    }
}
