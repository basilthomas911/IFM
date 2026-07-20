using System;
using System.Collections.Generic;
using System.Text;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Application.PredictiveModel.Server.HostedService
{
    public interface IPredictiveModelServerEventConsumer : IEventConsumer
    {
    }
}
