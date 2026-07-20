using System;
using System.Collections.Generic;
using System.Text;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Service.SystemAdmin.HostedService
{
    public interface IDatabaseBackupEventConsumer : IEventConsumer
    {
    }
}
