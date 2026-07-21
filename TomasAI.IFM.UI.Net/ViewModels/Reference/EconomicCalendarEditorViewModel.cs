using TomasAI.IFM.UI.Net.Contracts;
using TomasAI.IFM.UI.Net.Models;
using TomasAI.IFM.Shared.Reference;
using TomasAI.IFM.Shared.Reference.ViewModels;
using TomasAI.IFM.Shared.StatusConsole;

namespace TomasAI.IFM.UI.Net.ViewModels.Reference;

/// <summary>
/// Represents the view model for managing and editing economic calendar data, including country codes and economic
/// calendar entries.
/// </summary>
/// <remarks>This class provides functionality to load, add, modify, and remove economic calendar entries, as well
/// as to manage associated country codes. It also includes actions for handling UI-related events, such as cursor
/// changes and data loading notifications.</remarks>
/// <param name="appRoot"></param>
public class EconomicCalendarEditorViewModel(IAppRoot appRoot) : BaseEditorViewModel(appRoot)
{
    List<EconomicCalendarCountryCodeReadModel> _countryCodes = [];
    List<EconomicCalendarReadModel> _economicCalendars = [];

    public ICollection<EconomicCalendarCountryCodeReadModel> CountryCodes => _countryCodes;
    public ICollection<EconomicCalendarReadModel> EconomicCalendars => _economicCalendars;

    public Action OnCountryCodesLoaded = () => { };
    public Action OnEconomicCalendarsLoaded = () => { };
    public Action OnWaitCursor = () => { };
    public Action OnDefaultCursor = () => { };

    /// <summary>
    /// Retrieves the country code at the specified index.
    /// </summary>
    /// <param name="index">The zero-based index of the country code to retrieve. Must be within the valid range of the collection.</param>
    /// <returns>The country code as a string if the specified index is valid; otherwise, <see langword="null"/>.</returns>
    public string GetCountryCode(int index)
        => index >= 0 && index < _countryCodes.Count ? _countryCodes[index].CountryCode: null!;

    /// <summary>
    /// Retrieves the economic calendar at the specified index.
    /// </summary>
    /// <param name="index">The zero-based index of the economic calendar to retrieve. Must be within the range of available calendars.</param>
    /// <returns>An <see cref="EconomicCalendarReadModel"/> representing the economic calendar at the specified index,  or <see
    /// langword="null"/> if the index is out of range.</returns>
    public EconomicCalendarReadModel GetEconomicCalendar(int index) 
        => index >= 0 && index < _economicCalendars.Count ? _economicCalendars[index] : null!;
    
    /// <summary>
    /// Retrieves the index of the specified country code in the list of country codes.
    /// </summary>
    /// <remarks>This method performs a linear search through the list of country codes. If the same country
    /// code  appears multiple times, the index of the first occurrence is returned. The search is
    /// case-sensitive.</remarks>
    /// <param name="countryCode">The country code to search for. This value cannot be null.</param>
    /// <returns>The zero-based index of the specified country code if found; otherwise, -1.</returns>
    public int GetCountryCodeIndex(string countryCode)
    {
        for(var index = 0; index < _countryCodes.Count; index++)
            if (_countryCodes[index].CountryCode == countryCode)
                return index;
        return -1;
    }

    /// <summary>
    /// Adds a new economic calendar entry and performs the specified completion action upon success.
    /// </summary>
    /// <remarks>This method adds the specified economic calendar entry to the system and triggers the
    /// provided completion action, if any.  It handles errors internally and logs the operation status to the
    /// console.</remarks>
    /// <param name="economicCalendar">The economic calendar entry to be added. This parameter must not be <see langword="null"/>.</param>
    /// <param name="onCompleted">An action to be executed after the economic calendar entry is successfully added. This parameter is optional and
    /// can be <see langword="null"/>.</param>
    public void AddEconomicCalendar(EconomicCalendarReadModel economicCalendar, Action onCompleted)
        => AppRoot.GetModel<ReferenceCommandModel>().Execute(async model => {
            model.OnError((errorCode, errorMsg) => OnError(errorCode, errorMsg));
            await model.AddEconomicCalendarAsync(economicCalendar);
            LoadEconomicCalendars(DateOnly.FromDateTime(economicCalendar.EventDate), economicCalendar.CountryCode);
            WriteStatusConsole(LogSourceType.Reference, $"Economic Calendar {economicCalendar.Id} Added");
            onCompleted?.Invoke();
        });

    /// <summary>
    /// change economic calendar
    /// </summary>
    /// <param name="economicCalendarId"></param>
    /// <param name="economicCalendar"></param>
    /// <param name="overwrite"></param>
    public void ChangeEconomicCalendar(EconomicCalendarId economicCalendarId, EconomicCalendarReadModel economicCalendar, bool overwrite,  Action onCompleted)
        => AppRoot.GetModel<ReferenceCommandModel>().Execute(async model => {
            model.OnError((errorCode, errorMsg) => OnError(errorCode, errorMsg));
            await model.ChangeEconomicCalendarAsync(economicCalendarId, economicCalendar, overwrite);
            LoadEconomicCalendars(DateOnly.FromDateTime(economicCalendar.EventDate), economicCalendar.CountryCode);
            WriteStatusConsole(LogSourceType.Reference, $"Economic Calendar {economicCalendarId} Changed");
            onCompleted?.Invoke();
        });

    /// <summary>
    /// remove economic calendar
    /// </summary>
    /// <param name="economicCalendarId"></param>
    /// <param name="overwrite"></param>
    public void RemoveEconomicCalendar(EconomicCalendarId economicCalendarId, bool overwrite)
        => AppRoot.GetModel<ReferenceCommandModel>().Execute(async model => {
            model.OnError((errorCode, errorMsg) => OnError(errorCode, errorMsg));
            await model.RemoveEconomicCalendarAsync(economicCalendarId, overwrite);
            LoadEconomicCalendars(DateOnly.FromDateTime(economicCalendarId.EventDate), economicCalendarId.CountryCode);
            WriteStatusConsole(LogSourceType.Reference, $"Economic Calendar {economicCalendarId} Removed");
        });

    /// <summary>
    /// import economic calendars
    /// </summary>
    /// <param name="importDate"></param>
    public void ImportEconomicCalendars(DateTime importDate, string countryCode)
     => AppRoot.GetModel<ReferenceCommandModel>()
            .Execute(async model => {
                model.OnError((errorCode, errorMsg) => OnError(errorCode, errorMsg));
                var economicCalendars = default(EconomicCalendarReadModel[]);
                await AppRoot.GetModel<ReferenceQueryModel>().GetExternalEconomicCalendarsAsync(e => economicCalendars = e);
                await model.ImportEconomicCalendarsAsync(importDate, economicCalendars!,
                    () => {
                        LoadEconomicCalendars(DateOnly.FromDateTime(importDate), countryCode);
                        WriteStatusConsole(LogSourceType.Reference, $"Economic Calendars For: {importDate:yyyy-MM-dd} Imported");
                    });
            });
    
    /// <summary>
    /// load country codes
    /// </summary>
    public void LoadCountryCodes()
        => AppRoot.GetModel<ReferenceQueryModel>().Execute(async model => {
            model.OnError((errorCode, errorMsg) => OnError(errorCode, errorMsg));
            _countryCodes = [];
            await model.LoadEconomicCalendarCountryCodesAsync(countryCodes => {
                if (countryCodes?.Length  > 0)
                    _countryCodes.AddRange(countryCodes!);
                OnCountryCodesLoaded?.Invoke();
            });
        });

    /// <summary>
    /// load economic calendars
    /// </summary>
    public void LoadEconomicCalendars(DateOnly eventDate, string countryCode)
        => AppRoot.GetModel<ReferenceQueryModel>().Execute(async model => {
            model.OnError((errorCode, errorMsg) => OnError(errorCode, errorMsg));
            _economicCalendars = [];
            await model.LoadEconomicCalendarsAsync(eventDate, countryCode, economicCalendars => {
                if (economicCalendars?.Length> 0)
                    _economicCalendars.AddRange(economicCalendars.OrderBy(e => e.EventDate));
                OnEconomicCalendarsLoaded?.Invoke();
            });
        });

}
