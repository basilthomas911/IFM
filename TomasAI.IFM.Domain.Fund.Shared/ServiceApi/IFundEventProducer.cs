using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TomasAI.IFM.Domain.Fund.Shared.Events;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Fund.Shared.ServiceApi
{
    public interface IFundEventProducer : IEventProducer
    {
    }
}
