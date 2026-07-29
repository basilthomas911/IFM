using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TomasAI.IFM.Domain.Reference.Shared.Events;

namespace TomasAI.IFM.Domain.Reference.Shared.ServiceApi
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
