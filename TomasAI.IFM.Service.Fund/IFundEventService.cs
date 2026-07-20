using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Service.Fund
{
    public interface IFundEventService
    {
        Task ExecuteAsync(IEvent e);
    }
}
