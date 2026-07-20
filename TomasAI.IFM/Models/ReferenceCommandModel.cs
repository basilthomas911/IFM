using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TomasAI.IFM.Shared.Reference;
using TomasAI.IFM.Shared.Reference.ViewModels;
using TomasAI.IFM.Shared.Reference.ServiceApi;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Models
{
    public class ReferenceCommandModel : BaseModel<ReferenceCommandModel>
    {
        readonly IReferenceCommandApi _commandApi;

        /// <summary>
        /// create reference model
        /// </summary>
        /// <param name="commandApi"></param>
        public ReferenceCommandModel(IReferenceCommandApi commandApi)
        {
            _commandApi = commandApi ?? throw new ArgumentNullException(nameof(commandApi));
        }

        /// <summary>
        /// add economic calendar
        /// </summary>
        /// <param name="economicCalendar"></param>
        public async Task<Guid> AddEconomicCalendarAsync(EconomicCalendarReadModel economicCalendar)
            => await ExecuteCommandAsync(() => _commandApi.AddEconomicCalendarAsync(economicCalendar));

        /// <summary>
        /// change economic calendar
        /// </summary>
        /// <param name="economicCalendarId"></param>
        /// <param name="economicCalendar"></param>
        public async Task<Guid> ChangeEconomicCalendarAsync(EconomicCalendarId economicCalendarId, EconomicCalendarReadModel economicCalendar)
            => await ExecuteCommandAsync(() => _commandApi.ChangeEconomicCalendarAsync(economicCalendarId, economicCalendar));

        /// <summary>
        /// remove economic calendar
        /// </summary>
        /// <param name="economicCalendarId"></param>
        public async Task<Guid> RemoveEconomicCalendarAsync(EconomicCalendarId economicCalendarId)
            => await ExecuteCommandAsync(() => _commandApi.RemoveEconomicCalendarAsync(economicCalendarId));

        /// <summary>
        /// import economic calendars
        /// </summary>
        /// <param name="importDate"></param>
        /// <param name="economicCalendars"></param>
        /// <param name="onCompleted"></param>
        public async Task ImportEconomicCalendarsAsync(DateTime importDate, EconomicCalendarReadModel[] economicCalendars, Action onCompleted)
            => await ExecuteCommandAsync(() => _commandApi.ImportEconomicCalendarsAsync(importDate, economicCalendars), onCompleted);

        /// <summary>
        /// add lookup type
        /// </summary>
        /// <param name="lookupType"></param>
        public async Task<Guid> AddLookupTypeAsync(LookupTypeReadModel lookupType)
            => await ExecuteCommandAsync(() => _commandApi.AddLookupTypeAsync(lookupType));

        /// <summary>
        /// change lookup type
        /// </summary>
        /// <param name="lookupTypeId"></param>
        /// <param name="lookupType"></param>
        public async Task<Guid> ChangeLookupTypeAsync(LookupTypeId lookupTypeId, LookupTypeReadModel lookupType)
            => await ExecuteCommandAsync(() => _commandApi.ChangeLookupTypeAsync(lookupTypeId, lookupType));

        /// <summary>
        /// remove lookup type
        /// </summary>
        /// <param name="lookupTypeId"></param>
        public async Task<Guid> RemoveLookupTypeAsync(LookupTypeId lookupTypeId)
            => await ExecuteCommandAsync(() => _commandApi.RemoveLookupTypeAsync(lookupTypeId));

    }
}
