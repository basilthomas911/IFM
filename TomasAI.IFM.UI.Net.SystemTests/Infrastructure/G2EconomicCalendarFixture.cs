using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Domain.Reference.Shared.ViewModels;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.UI.Net.Models;

namespace TomasAI.IFM.UI.Net.SystemTests.Infrastructure;

/// <summary>
/// Isolated manual economic-calendar row and provider parameters used by G2-020 through G2-023.
/// </summary>
public sealed record G2EconomicCalendarFixture(
    DateOnly ManualDate,
    DateOnly ImportDate,
    string CountryCode,
    EconomicCalendarReadModel AddedCalendar,
    EconomicCalendarReadModel ChangedCalendar,
    string DefinitionDescription)
{
    public static async Task<G2EconomicCalendarFixture> CreateAsync(
        G0QuerySession queries,
        G2Configuration configuration,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(queries);
        ArgumentNullException.ThrowIfNull(configuration);

        var definitions = Require(
                await queries.Reference.GetReferenceDataDefinitionTypesAsync()
                    .WaitAsync(timeout, cancellationToken),
                "ReferenceDataDefinitionType lookup")
            .ToArray();
        var description = definitions.SingleOrDefault(value => string.Equals(
                value.ShortCode,
                "EconomicCalendar",
                StringComparison.OrdinalIgnoreCase))?.Description
            ?? throw new G0DependencyException(
                "The ReferenceDataDefinitionType lookup does not contain required short code 'EconomicCalendar'.");
        var countryCode = configuration.ImportCountryCodes[0];
        var localDate = configuration.EconomicCalendarManualDate.ToDateTime(TimeOnly.MinValue);
        var eventDateUtc = EasternTime.ToUtc(localDate);
        var eventName = $"{configuration.RunPrefix}-Calendar";
        var createdOn = DateTime.UtcNow;

        return new G2EconomicCalendarFixture(
            configuration.EconomicCalendarManualDate,
            configuration.ImportDate,
            countryCode,
            Calendar(eventDateUtc, countryCode, eventName, "1.01", "1.02", "1.03", createdOn),
            Calendar(eventDateUtc, countryCode, eventName, "2.01", "2.02", "2.03", createdOn),
            description);
    }

    static EconomicCalendarReadModel Calendar(
        DateTime eventDateUtc,
        string countryCode,
        string eventName,
        string actual,
        string forecast,
        string prior,
        DateTime createdOn)
        => new(
            eventDateUtc,
            countryCode,
            eventName,
            actual,
            forecast,
            prior,
            createdOn,
            "G2 UI system test");

    static T Require<T>(ServiceResult<T> result, string queryName)
        where T : class
    {
        if (!result.Success || result.Value is null)
            throw new G0DependencyException(
                $"Typed {queryName} query failed: code={result.ErrorCode}; message={result.ErrorMessage}");
        return result.Value;
    }
}
