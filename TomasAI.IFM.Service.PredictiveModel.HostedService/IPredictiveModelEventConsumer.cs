using System;
using System.Collections.Generic;
using System.Text;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Service.PredictiveModel.HostedService
{
    public interface IPredictiveModelEventConsumer : IEventConsumer
    {
    }
}
