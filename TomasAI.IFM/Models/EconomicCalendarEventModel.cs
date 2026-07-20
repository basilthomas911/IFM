using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TomasAI.IFM.Contracts;
using TomasAI.IFM.Shared.MarketData.ViewModels;
using TomasAI.IFM.Shared.Reference.Events;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.UI.EventConsumer;

namespace TomasAI.IFM.Models
{
    public class EconomicCalendarEventModel : BaseModel<EconomicCalendarEventModel>
    {
        readonly IEconomicCalendarUIEventConsumer _economicCalendarEventConsumer;
 
        public EconomicCalendarEventModel(IEconomicCalendarUIEventConsumer economicCalendarEventConsumer)
        {
            _economicCalendarEventConsumer = economicCalendarEventConsumer ?? throw new ArgumentNullException(nameof(economicCalendarEventConsumer));
        }

        /// <summary>
        /// start listening for economic calendar complete events
        /// </summary>
        /// <param name="listenerAction"></param>
        public async Task StartEconomicCalendarEventListenersAsync( 
            Action<EconomicCalendarAddedCompleteEvent> addedAction,
            Action<EconomicCalendarChangedCompleteEvent> changedAction,
            Action<EconomicCalendarRemovedCompleteEvent> removedAction,
            Action<EconomicCalendarsImportedCompleteEvent> importedAction) 
            => await _economicCalendarEventConsumer.StartAsync(addedAction, changedAction, removedAction, importedAction);

        /// <summary>
        /// stop listening for economic calendar complete events
        /// </summary>
        public async Task StopEconomicCalendarEventListenersAsync() 
            => await _economicCalendarEventConsumer.StopAsync();
        
    }
}
