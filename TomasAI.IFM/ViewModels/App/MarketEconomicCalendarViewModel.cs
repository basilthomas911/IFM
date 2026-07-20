using System;
using System.Collections.Generic;
using System.Linq;
using TomasAI.IFM.Contracts;
using TomasAI.IFM.Models;
using TomasAI.IFM.Shared.Reference;
using TomasAI.IFM.Shared.Reference.ViewModels;

namespace TomasAI.IFM.ViewModels.App
{
    public class MarketEconomicCalendarReadModel
    {
        IAppRoot _appRoot;
        List<EconomicCalendarReadModel> _economicCalendar;
        List<string> _countryCodes;
        string _selectedCountryCode;
        ReferenceQueryModel _referenceQueryModel;
        EconomicCalendarEventModel _eventModel;

        /// <summary>
        /// create IFM app root view model
        /// </summary>
        /// <param name="appRoot"></param>
        public MarketEconomicCalendarReadModel(IAppRoot appRoot)
        {
            _appRoot = appRoot;
            _referenceQueryModel = _appRoot.GetModel<ReferenceQueryModel>();
            _eventModel = _appRoot.GetModel<EconomicCalendarEventModel>();
        }

        public IReadOnlyCollection<string> CountryCodes => _countryCodes;

        public Action<EconomicCalendarReadModel[]> OnModelUpdate;
        public Action<string, EconomicCalendarReadModel> OnCalendarDateUpdate;
        public Action<string> OnErrorMessage;
        public Action OnCountryCodesLoaded;

        public void LoadCountryCodes()
            => _referenceQueryModel.Execute(async model => {
                model.OnError((_, errorMsg) => OnErrorMessage?.Invoke(errorMsg));
                await model.LoadEconomicCalendarCountryCodesAsync(countryCodes => {
                    _countryCodes = new();
                    if (countryCodes is not null && countryCodes.Length > 0)
                        _countryCodes.AddRange(countryCodes.Select(e => e.CountryCode).ToList());
                    OnCountryCodesLoaded?.Invoke();
                });
            });

        public void UpdateModel(DateTime todaysDate, EconomicCalendarViewType calendarViewType)
            => _referenceQueryModel.Execute(async model => {
                model.OnError((_, errorMsg) => OnErrorMessage?.Invoke(errorMsg));
                await model.LoadEconomicCalendarAsync(todaysDate, calendarViewType, _selectedCountryCode, async economicCalendar => {
                    await model.LoadEconomicCalendarDateAsync(todaysDate, calendarViewType, calendarDate => {
                        _economicCalendar = new();
                        if (economicCalendar is not null && economicCalendar.Length > 0)
                            _economicCalendar.AddRange(economicCalendar);
                        if (!string.IsNullOrWhiteSpace(calendarDate))
                            OnCalendarDateUpdate?.Invoke(calendarDate, _economicCalendar.Count > 0 ? _economicCalendar[0] : null);
                        OnModelUpdate?.Invoke(economicCalendar);
                    });
                });
            });

        public void SetSelectedCountryCode(int index)
            => _selectedCountryCode = (index > -1 && index < _countryCodes.Count)
                    ? _countryCodes[index]
                    : string.Empty;
        
        public DateTime? GetCalendarDate(int index)
        {
            if ((_economicCalendar?.Count ?? 0 ) > 0)
            {
                if (index >= 0 && index < _economicCalendar.Count)
                    return _economicCalendar[index].EventDate;
            }
            return null;
        }

        public EconomicCalendarReadModel GetEconomicCalendar(int index)
        {
            if ((_economicCalendar?.Count ?? 0 ) > 0)
            {
                if (index >= 0 && index < _economicCalendar.Count)
                    return _economicCalendar[index];
            }
            return null;
        }

        public EconomicCalendarViewType GetEconomicCalendarViewType(string calendarType)
            => calendarType switch {
                "Today" => EconomicCalendarViewType.Today,
                "Yesterday" => EconomicCalendarViewType.Yesterday,
                "Tomorrow" => EconomicCalendarViewType.Tomorrow,
                "This Week" => EconomicCalendarViewType.ThisWeek,
                "Next Week" => EconomicCalendarViewType.NextWeek,
                _ => EconomicCalendarViewType.Today
            };

        public void StartEventListeners(Action refreshView)
            => _eventModel.Execute(async model => {
                model.OnError((_, errorMsg) => OnErrorMessage?.Invoke(errorMsg));
                await model.StartEconomicCalendarEventListenersAsync(
                    addedAction: e => refreshView?.Invoke(),
                    changedAction: e => refreshView?.Invoke(),
                    removedAction: e => refreshView?.Invoke(),
                    importedAction: e => refreshView?.Invoke());
            });

        public void StopEventListeners()
            => _eventModel.Execute(async model => {
                model.OnError((_, errorMsg) => OnErrorMessage?.Invoke(errorMsg));
                await model.StopEconomicCalendarEventListenersAsync();
            });
    }
}
