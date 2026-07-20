using System;
using System.Collections.Generic;
using System.Text;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Application.OptionPricer.HostedService
{
    public interface IOptionPricerEventConsumer : IEventConsumer
    {
    }
}
