using System;
using System.Collections.Generic;
using System.Text;

namespace TomasAI.IFM.Shared.EventSourcing
{
    public interface IEventConsumerOptions
    {
        string GroupId { get; }
        string BootstrapServers { get;  }
        bool EnableAutoCommit { get; }
    }
}
