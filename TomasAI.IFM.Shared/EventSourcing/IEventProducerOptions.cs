using System;
using System.Collections.Generic;
using System.Text;

namespace TomasAI.IFM.Shared.EventSourcing
{
    public interface IEventProducerOptions
    {
        string BootstrapServers { get; }
    }
}
