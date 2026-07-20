using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TomasAI.IFM.Shared.Reference.Events;

namespace TomasAI.IFM.Shared.Reference.ServiceApi
{
    public interface IReferenceEventDenormalizerApi
    {
        Task CreateLookupTypeAsync(LookupTypeAddedEvent e);
        Task DeleteLookupTypeAsync(LookupTypeRemovedEvent e);
        Task AddScheduledJobAsync(ScheduledJobAddedEvent e);
        Task ChangeScheduledJobAsync(ScheduledJobChangedEvent e);
        Task RemoveScheduledJobAsync(ScheduledJobRemovedEvent e);
    }
}
