using System;
using System.Threading.Tasks;
using TomasAI.IFM.Shared.Telemetry.ViewModels;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Shared.Telemetry.ServiceApi
{
    public interface ITelemetryCommandApi
    {
        Task<ServiceResult<Guid>> AddLogEventsAsync(LogEventsReadModel logEvents);
    }

}
