using TomasAI.IFM.Domain.Reference.Shared.ViewModels;
using TomasAI.IFM.UI.Net.ViewModels.Lifecycle;
using TomasAI.IFM.Domain.Reference.Shared;
using System;
using System.Collections.Generic;
using System.Linq;
using TomasAI.IFM.UI.Net.Contracts;
using TomasAI.IFM.UI.Net.Models;
using TomasAI.IFM.Domain.Reference.Shared.ViewModels;

namespace TomasAI.IFM.UI.Net.ViewModels.App
{
    public class MarketEconomicCalendarReadModel : IAsyncLifecycle, IAsyncDisposable
    {
        readonly AsyncLifecycleCoordinator _lifecycle;
        Action _refreshView = null!;
        IAppRoot _appRoot;
        List<EconomicCalendarReadModel> _economicCalendars = null!;
        List<string> _countryCodes = null!;
        string _selectedCountryCode = null!;
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
            _lifecycle = new AsyncLifecycleCoordinator(StartListenersCoreAsync, StopListenersCoreAsync);
        }

        public IReadOnlyCollection<string> CountryCodes => _countryCodes;

        public Action<EconomicCalendarReadModel[]> OnModelUpdate = null!;
        public Action<string, EconomicCalendarReadModel> OnCalendarDateUpdate = null!;
        public Action<string> OnErrorMessage = null!;
        public Action OnCountryCodesLoaded = null!;

        public Task LoadCountryCodes()
            => _referenceQueryModel.ExecuteAsync(async model => {
                model.OnError((_, errorMsg) => OnErrorMessage?.Invoke(errorMsg));
                await model.LoadEconomicCalendarCountryCodesAsync(countryCodes => {
                    _countryCodes = new();
                    if (countryCodes is not null && countryCodes.Length > 0)
                        _countryCodes.AddRange(countryCodes.Select(e => e.CountryCode).ToList());
                    OnCountryCodesLoaded?.Invoke();
                });
            });

        public Task UpdateModel(DateTime todaysDate, EconomicCalendarViewType calendarViewType)
            => _referenceQueryModel.ExecuteAsync(async model => {
                model.OnError((_, errorMsg) => OnErrorMessage?.Invoke(errorMsg));
                await model.LoadEconomicCalendarAsync(todaysDate, calendarViewType, _selectedCountryCode, async economicCalendar => {
                    await model.LoadEconomicCalendarDateAsync(todaysDate, calendarViewType, calendarDate => {
                        _economicCalendars = [];
                        if (economicCalendar is not null && economicCalendar.Length > 0)
                            _economicCalendars.AddRange(economicCalendar);
                        if (!string.IsNullOrWhiteSpace(calendarDate))
                            OnCalendarDateUpdate?.Invoke(calendarDate, _economicCalendars.Count > 0 ? _economicCalendars[0] : null!);
                        OnModelUpdate?.Invoke(economicCalendar!);
                    });
                });
            });

        public void SetSelectedCountryCode(int index)
            => _selectedCountryCode = (index > -1 && index < _countryCodes.Count)
                    ? _countryCodes[index]
                    : string.Empty;

        public DateTime? GetCalendarDate(int index)
        {
            if ((_economicCalendars?.Count ?? 0 ) > 0)
            {
                if (index >= 0 && index < _economicCalendars!.Count)
                    return _economicCalendars[index].EventDate;
            }
            return null;
        }

        public EconomicCalendarReadModel GetEconomicCalendar(int index)
        {
            if ((_economicCalendars?.Count ?? 0 ) > 0)
            {
                if (index >= 0 && index < _economicCalendars!.Count)
                    return _economicCalendars[index];
            }
            return null!;
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

        public Task StartEventListeners(Action refreshView)
        {
            _refreshView = refreshView;
            return InitializeAsync(CancellationToken.None);
        }

        Task StartListenersCoreAsync(CancellationToken cancellationToken)
            => _eventModel.ExecuteAsync(async model => {
                cancellationToken.ThrowIfCancellationRequested();
                model.OnError((_, errorMsg) => OnErrorMessage?.Invoke(errorMsg));
                await model.StartEconomicCalendarEventListenersAsync(
                    addedAction: e => _refreshView?.Invoke(),
                    changedAction: e => _refreshView?.Invoke(),
                    removedAction: e => _refreshView?.Invoke(),
                    importedAction: e => _refreshView?.Invoke());
            });

        public Task StopEventListeners()
            => StopAsync(CancellationToken.None);

        Task StopListenersCoreAsync(CancellationToken cancellationToken)
            => _eventModel.ExecuteAsync(async model => {
                cancellationToken.ThrowIfCancellationRequested();
                model.OnError((_, errorMsg) => OnErrorMessage?.Invoke(errorMsg));
                await model.StopEconomicCalendarEventListenersAsync();
            });

        public Task InitializeAsync(CancellationToken cancellationToken) => _lifecycle.InitializeAsync(cancellationToken);
        public Task StopAsync(CancellationToken cancellationToken) => _lifecycle.StopAsync(cancellationToken);
        public ValueTask DisposeAsync() => _lifecycle.DisposeAsync();
    }
}
