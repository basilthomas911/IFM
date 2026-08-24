using TomasAI.IFM.UI.Net.Models.Operations;
using TomasAI.IFM.UI.Net.Models.Reference;
using TomasAI.IFM.UI.Net.Services.Operations;
using TomasAI.IFM.UI.Net.Services.Subscriptions;

namespace TomasAI.IFM.UI.Net.Services.Reference;

/// <summary>Defines economic-calendar maintenance and notifications used by presentation workflows.</summary>
public interface IEconomicCalendarService
{
    /// <summary>Loads economic-calendar country-code selectors.</summary>
    ValueTask<UiOperationResult<IReadOnlyList<EconomicCalendarCountryCodeUiModel>>> GetCountryCodesAsync(
        CancellationToken cancellationToken = default);

    /// <summary>Loads economic-calendar entries for one Eastern date and country.</summary>
    ValueTask<UiOperationResult<IReadOnlyList<EconomicCalendarUiModel>>> GetCalendarsAsync(
        DateOnly eventDate,
        string countryCode,
        CancellationToken cancellationToken = default);

    /// <summary>Adds an economic-calendar entry.</summary>
    ValueTask<UiOperationResult<Guid>> AddAsync(
        EconomicCalendarUiModel calendar,
        CancellationToken cancellationToken = default);

    /// <summary>Changes an economic-calendar entry.</summary>
    ValueTask<UiOperationResult<Guid>> ChangeAsync(
        DateTime originalEventDate,
        string originalCountryCode,
        string originalEventName,
        EconomicCalendarUiModel calendar,
        bool overwrite,
        CancellationToken cancellationToken = default);

    /// <summary>Removes an economic-calendar entry.</summary>
    ValueTask<UiOperationResult<Guid>> RemoveAsync(
        DateTime eventDate,
        string countryCode,
        string eventName,
        bool overwrite,
        CancellationToken cancellationToken = default);

    /// <summary>Imports economic-calendar entries for the selected date and countries.</summary>
    ValueTask<UiOperationResult<Guid>> ImportAsync(
        DateTime importDate,
        IReadOnlyList<string> countryCodes,
        CancellationToken cancellationToken = default);

    /// <summary>Creates an independently owned calendar terminal-event subscription.</summary>
    IUiEventSubscription CreateSubscription(
        Action<TerminalNotificationUiModel> handler);
}
