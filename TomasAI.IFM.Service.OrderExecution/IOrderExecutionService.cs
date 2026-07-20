using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Service.OrderExecution
{
    public interface IOrderExecutionService
    {
        Task ExecuteAsync(IEvent e);

    }
}
