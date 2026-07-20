using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Reference.Events;
using TomasAI.IFM.Application.Storage.ReferenceDb;
using System.Threading.Tasks;

namespace TomasAI.IFM.Application.Event
{
    public class ReferenceEvents:
        IAsyncEventHandler<LookupTypeCreatedEvent>,
        IAsyncEventHandler<LookupTypeDeletedEvent>,
        IAsyncEventHandler<ScheduledJobAddedEvent>,
        IAsyncEventHandler<ScheduledJobChangedEvent>,
        IAsyncEventHandler<ScheduledJobRemovedEvent>
    {
        private readonly IReferenceDbContext _dbReference;

        public ReferenceEvents(IReferenceDbContext dbReference)
            => _dbReference = dbReference;

        public async Task ExecuteAsync(LookupTypeCreatedEvent e) => await _dbReference.DbWriter.InsertLookupTypesAsync(e.LookupTypes);

        public async Task ExecuteAsync(LookupTypeDeletedEvent e) => await _dbReference.DbWriter.DeleteLookupTypeAsync(e.LookupTypeName);

        public async Task ExecuteAsync(ScheduledJobAddedEvent e) => await _dbReference.DbWriter.InsertScheduledJobAsync(e.ScheduledJob);

        public async Task ExecuteAsync(ScheduledJobChangedEvent e) => await _dbReference.DbWriter.UpdateScheduledJobAsync(e.ScheduledJob);

        public async Task ExecuteAsync(ScheduledJobRemovedEvent e)
        {
            var jobId = await _dbReference.DbReader.GetScheduledJobIdAsync(e.ScheduledJobName);
            await _dbReference.DbWriter.DeleteScheduledJobAsync(jobId);
        }
    }

}
