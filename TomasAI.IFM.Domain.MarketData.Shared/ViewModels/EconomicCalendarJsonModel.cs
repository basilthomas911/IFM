using MessagePack;
using Newtonsoft.Json;

namespace TomasAI.IFM.Domain.MarketData.Shared.ViewModels;

/// <summary>
/// Raw JSON import model (external feed) for an economic calendar item.
/// </summary>
public record EconomicCalendarJsonModel(
    string Event,
    DateTime Date,
    string Country,
    string Actual,
    string Previous,
    string Change,
    string ChangePercentage,
    string Estimate,
    string Impact)
{
    /// <summary>
    /// Maps the external JSON model to the internal <see cref="EconomicCalendarReadModel"/>.
    /// </summary>
    public EconomicCalendarReadModel ToViewModel()
        => new(
            eventDate: new DateTime(Date.Year, Date.Month, Date.Day, Date.Hour, Date.Minute, Date.Second, DateTimeKind.Utc).ToLocalTime(),
            countryCode: GetCountryCode().Length >= 2 ? GetCountryCode()[..2] : GetCountryCode(),
            eventName: GetEventName(),
            actual: GetActual(),
            forecast: GetEstimate(),
            prior: GetPrevious(),
            createdOn: DateTime.UtcNow,
            createdBy: string.Empty);

    string GetCountryCode() => string.IsNullOrWhiteSpace(Country) ? string.Empty : Country.Trim();
    string GetEventName() => string.IsNullOrWhiteSpace(Event) ? string.Empty : Event.Trim();
    string GetActual() => string.IsNullOrWhiteSpace(Actual) ? "0" : Actual.Trim();
    string GetEstimate() => string.IsNullOrWhiteSpace(Estimate) ? "0" : Estimate.Trim();
    string GetPrevious() => string.IsNullOrWhiteSpace(Previous) ? "0" : Previous.Trim();
}
