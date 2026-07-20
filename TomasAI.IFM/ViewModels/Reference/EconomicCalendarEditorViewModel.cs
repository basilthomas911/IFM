using System;
using System.Collections.Generic;
using TomasAI.IFM.Contracts;
using TomasAI.IFM.Models;
using TomasAI.IFM.Shared.Reference;
using TomasAI.IFM.Shared.Reference.ViewModels;
using TomasAI.IFM.Shared.Log;

namespace TomasAI.IFM.ViewModels.Reference
{
    public class EconomicCalendarEditorViewModel : BaseEditorViewModel
    {
        List<EconomicCalendarCountryCodeReadModel> _countryCodes;
        List<EconomicCalendarReadModel> _economicCalendars;
 
        public EconomicCalendarEditorViewModel(IAppRoot appRoot)
            :base(appRoot)
        {
        }

        public ICollection<EconomicCalendarCountryCodeReadModel> CountryCodes => _countryCodes;
        public ICollection<EconomicCalendarReadModel> EconomicCalendars => _economicCalendars;

        public Action OnCountryCodesLoaded;
        public Action OnEconomicCalendarsLoaded;
 
        public string GetCountryCode(int index) => index >= 0 && index < _countryCodes.Count ? _countryCodes[index].CountryCode: null;
        public EconomicCalendarReadModel GetEconomicCalendar(int index) => index >= 0 && index < _economicCalendars.Count ? _economicCalendars[index] : null;
        
        public int GetCountryCodeIndex(string countryCode)
        {
            for(var index = 0; index < _countryCodes.Count; index++)
                if (_countryCodes[index].CountryCode == countryCode)
                    return index;
            return -1;
        }

        /// <summary>
        /// add economic calendar
        /// </summary>
        /// <param name="economicCalendar"></param>
        public void AddEconomicCalendar(EconomicCalendarReadModel economicCalendar, Action onCompleted)
            => AppRoot.GetModel<ReferenceCommandModel>().Execute(async model => {
                model.OnError((errorCode, errorMsg) => OnError(errorCode, errorMsg));
                await model.AddEconomicCalendarAsync(economicCalendar);
                LoadEconomicCalendars();
                WriteStatusConsole(LogSourceType.Reference, $"Economic Calendar {economicCalendar.Id} Added");
                onCompleted?.Invoke();
            });
        
        /// <summary>
        /// change economic calendar
        /// </summary>
        /// <param name="economicCalendarId"></param>
        /// <param name="economicCalendar"></param>
        public void ChangeEconomicCalendar(EconomicCalendarId economicCalendarId, EconomicCalendarReadModel economicCalendar, Action onCompleted)
            => AppRoot.GetModel<ReferenceCommandModel>().Execute(async model => {
                model.OnError((errorCode, errorMsg) => OnError(errorCode, errorMsg));
                await model.ChangeEconomicCalendarAsync(economicCalendarId, economicCalendar);
                LoadEconomicCalendars();
                WriteStatusConsole(LogSourceType.Reference, $"Economic Calendar {economicCalendarId} Changed");
                onCompleted?.Invoke();
            });

        /// <summary>
        /// remove economic calendar
        /// </summary>
        /// <param name="economicCalendarId"></param>
        public void RemoveEconomicCalendar(EconomicCalendarId economicCalendarId)
            => AppRoot.GetModel<ReferenceCommandModel>().Execute(async model => {
                model.OnError((errorCode, errorMsg) => OnError(errorCode, errorMsg));
                await model.RemoveEconomicCalendarAsync(economicCalendarId);
                LoadEconomicCalendars();
                WriteStatusConsole(LogSourceType.Reference, $"Economic Calendar {economicCalendarId} Removed");
            });

        /// <summary>
        /// import economic calendars
        /// </summary>
        /// <param name="importDate"></param>
        public void ImportEconomicCalendars(DateTime importDate)
         => AppRoot.GetModel<ReferenceCommandModel>()
                .Execute(async model => {
                    model.OnError((errorCode, errorMsg) => OnError(errorCode, errorMsg));
                    var economicCalendars = default(EconomicCalendarReadModel[]);
                    await AppRoot.GetModel<ReferenceQueryModel>().GetExternalEconomicCalendarsAsync(e => economicCalendars = e);
                    await model.ImportEconomicCalendarsAsync(importDate, economicCalendars,
                        () => {
                            LoadEconomicCalendars();
                            WriteStatusConsole(LogSourceType.Reference, $"Economic Calendars For: {importDate:yyyy-MM-dd} Imported");
                        });
                });
        
        /// <summary>
        /// load country codes
        /// </summary>
        public void LoadCountryCodes()
            => AppRoot.GetModel<ReferenceQueryModel>().Execute(async model => {
                model.OnError((errorCode, errorMsg) => OnError(errorCode, errorMsg));
                _countryCodes = new ();
                await model.LoadEconomicCalendarCountryCodesAsync(countryCodes => {
                    if ((countryCodes?.Length ?? 0) > 0)
                        _countryCodes.AddRange(countryCodes);
                    OnCountryCodesLoaded?.Invoke();
                });
            });

        /// <summary>
        /// load economic calendars
        /// </summary>
        public void LoadEconomicCalendars()
            => AppRoot.GetModel<ReferenceQueryModel>().Execute(async model => {
                model.OnError((errorCode, errorMsg) => OnError(errorCode, errorMsg));
                _economicCalendars = new List<EconomicCalendarReadModel>();
                await model.LoadEconomicCalendarsAsync(economicCalendars => {
                    if ((economicCalendars?.Length ?? 0) > 0)
                        _economicCalendars.AddRange(economicCalendars);
                    OnEconomicCalendarsLoaded?.Invoke();
                });
            });
   
    }
}
