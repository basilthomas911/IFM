using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.UI.EventConsumer
{
    public interface ISpreadDistributionJobUIEventConsumer : IEventConsumer
    {
        ValueTask StartAsync();
    }
}


