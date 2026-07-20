using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Reference.Events;
using TomasAI.IFM.Shared.Reference.ServiceApi;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Application.Storage.ReferenceDb;

namespace TomasAI.IFM.Application.Event.Denormalizer
{
    public class ReferenceEventDenormalizer : BaseEventDenormalizer,
        IAsyncEventHandler<LookupTypeCreatedEvent>,
        IAsyncEventHandler<LookupTypeDeletedEvent>,
        IAsyncEventHandler<StrikePriceVolatilityAddedEvent>,
        IAsyncEventHandler<StrikePriceVolatilityChangedEvent>,
        IAsyncEventHandler<StrikePriceVolatilityRemovedEvent>,
        IAsyncEventHandler<EconomicCalendarAddedEvent>,
        IAsyncEventHandler<EconomicCalendarChangedEvent>,
        IAsyncEventHandler<EconomicCalendarRemovedEvent>
    {
        private const int Err_ReferenceEventDenormalizer = 5005;
        private readonly IReferenceDbContext _dbReference;
        private readonly IReferenceEventProducer _eventProducer;

        public ReferenceEventDenormalizer(IReferenceDbContext dbReference, IReferenceEventProducer eventProducer, ILogger<ReferenceEventDenormalizer> logger):base(logger)
        { 
            _dbReference = dbReference;
            _eventProducer = eventProducer;
            SetEventProducer(e => _eventProducer.PostEventAsync((dynamic)e));
        }

        public async Task ExecuteAsync(LookupTypeCreatedEvent e) => await DenormalizeAsync(e, Err_ReferenceEventDenormalizer, () => _dbReference.DbWriter.InsertLookupTypesAsync(e.LookupTypes));

        public async Task ExecuteAsync(LookupTypeDeletedEvent e) => await DenormalizeAsync(e, Err_ReferenceEventDenormalizer, () => _dbReference.DbWriter.DeleteLookupTypeAsync(e.LookupTypeName));

        public async Task ExecuteAsync(StrikePriceVolatilityAddedEvent e) => await DenormalizeAsync(e, Err_ReferenceEventDenormalizer, () => _dbReference.DbWriter.InsertStrikePriceVolatilityAsync(e.StrikePriceVolatility));

        public async Task ExecuteAsync(StrikePriceVolatilityChangedEvent e) => await DenormalizeAsync(e, Err_ReferenceEventDenormalizer, () => _dbReference.DbWriter.UpdateStrikePriceVolatilityAsync(e.OriginalId, e.StrikePriceVolatility));

        public async Task ExecuteAsync(StrikePriceVolatilityRemovedEvent e) => await DenormalizeAsync(e, Err_ReferenceEventDenormalizer, () => _dbReference.DbWriter.DeleteStrikePriceVolatilityAsync(e.StrikePriceVolatilityId));
        
        public async Task ExecuteAsync(EconomicCalendarAddedEvent e) => await DenormalizeAsync(e, Err_ReferenceEventDenormalizer, () => _dbReference.DbWriter.InsertEconomicCalendarAsync(e.EconomicCalendar));

        public async Task ExecuteAsync(EconomicCalendarChangedEvent e) => await DenormalizeAsync(e, Err_ReferenceEventDenormalizer, () => _dbReference.DbWriter.UpdateEconomicCalendarAsync(e.EconomicCalendarId, e.EconomicCalendar));

        public async Task ExecuteAsync(EconomicCalendarRemovedEvent e) => await DenormalizeAsync(e, Err_ReferenceEventDenormalizer, () => _dbReference.DbWriter.DeleteEconomicCalendarAsync(e.EconomicCalendarId));

    }
}
