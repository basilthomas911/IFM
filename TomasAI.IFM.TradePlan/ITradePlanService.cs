using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Service.TradePlan
{
    public interface ITradePlanService
    {
        Task ExecuteAsync(IEvent e);
    }
}
