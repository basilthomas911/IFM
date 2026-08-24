using TomasAI.IFM.Domain.MarketData.Shared;
using TomasAI.IFM.Domain.MarketData.Shared.Events;
using TomasAI.IFM.Domain.MarketData.Shared.ServiceApi;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.UI.EventConsumer;
using TomasAI.IFM.UI.Net.Models;
using TomasAI.IFM.UI.Net.Models.Operations;
using TomasAI.IFM.UI.Net.Models.Reference;
using TomasAI.IFM.UI.Net.Services.Operations;
using TomasAI.IFM.UI.Net.Services.Subscriptions;

namespace TomasAI.IFM.UI.Net.Services.Reference;

/// <summary>Maps typed Market Data APIs and calendar events into UI-owned operations and models.</summary>
public sealed class EconomicCalendarService(
    IMarketDataCommandApi commandApi,
    IMarketDataQueryApi queryApi,
    IEconomicCalendarUIEventConsumer eventConsumer) : IEconomicCalendarService
{
    readonly IMarketDataCommandApi _commandApi =
        commandApi ?? throw new ArgumentNullException(nameof(commandApi));
    readonly IMarketDataQueryApi _queryApi =
        queryApi ?? throw new ArgumentNullException(nameof(queryApi));
    readonly IEconomicCalendarUIEventConsumer _eventConsumer =
        eventConsumer ?? throw new ArgumentNullException(nameof(eventConsumer));

    /// <inheritdoc />
    public async ValueTask<UiOperationResult<IReadOnlyList<EconomicCalendarCountryCodeUiModel>>>
        GetCountryCodesAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return (await _queryApi.GetEconomicCalendarCountryCodesAsync().ConfigureAwait(false))
            .ToUiResult(values => (IReadOnlyList<EconomicCalendarCountryCodeUiModel>)values
                .Select(value => new EconomicCalendarCountryCodeUiModel(value.CountryCode))
                .ToArray());
    }

    /// <inheritdoc />
    public async ValueTask<UiOperationResult<IReadOnlyList<EconomicCalendarUiModel>>> GetCalendarsAsync(
        DateOnly eventDate,
        string countryCode,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var result = await _queryApi.GetEconomicCalendarsAsync(
            EasternTime.ToUtc(eventDate.ToDateTime(TimeOnly.MinValue)),
            EconomicCalendarViewType.Today,
            countryCode).ConfigureAwait(false);
        return result.ToUiResult(values => (IReadOnlyList<EconomicCalendarUiModel>)values
            .Select(ToUi)
            .ToArray());
    }

    /// <inheritdoc />
    public async ValueTask<UiOperationResult<Guid>> AddAsync(
        EconomicCalendarUiModel calendar,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Map(await _commandApi.AddEconomicCalendarAsync(ToBackend(calendar)).ConfigureAwait(false));
    }

    /// <inheritdoc />
    public async ValueTask<UiOperationResult<Guid>> ChangeAsync(
        DateTime originalEventDate,
        string originalCountryCode,
        string originalEventName,
        EconomicCalendarUiModel calendar,
        bool overwrite,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Map(await _commandApi.ChangeEconomicCalendarAsync(
            new EconomicCalendarId(originalEventDate, originalCountryCode, originalEventName),
            ToBackend(calendar),
            overwrite).ConfigureAwait(false));
    }

    /// <inheritdoc />
    public async ValueTask<UiOperationResult<Guid>> RemoveAsync(
        DateTime eventDate,
        string countryCode,
        string eventName,
        bool overwrite,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Map(await _commandApi.RemoveEconomicCalendarAsync(
            new EconomicCalendarId(eventDate, countryCode, eventName),
            overwrite).ConfigureAwait(false));
    }

    /// <inheritdoc />
    public async ValueTask<UiOperationResult<Guid>> ImportAsync(
        DateTime importDate,
        IReadOnlyList<string> countryCodes,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Map(await _commandApi.ImportEconomicCalendarsAsync(
            EasternTime.DateToUtc(importDate),
            countryCodes.ToArray()).ConfigureAwait(false));
    }

    /// <inheritdoc />
    public IUiEventSubscription CreateSubscription(
        Action<TerminalNotificationUiModel> handler)
    {
        ArgumentNullException.ThrowIfNull(handler);
        return new OwnedUiEventSubscription(
            cancellationToken =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                return _eventConsumer.StartAsync(
                    value => Publish(value),
                    value => Publish(value),
                    value => Publish(value),
                    value => Publish(value),
                    value => Publish(value),
                    value => Publish(value),
                    value => Publish(value),
                    value => Publish(value));
            },
            _eventConsumer.StopAsync);

        void Publish(IEvent value) => handler(ToTerminal(value));
    }

    static UiOperationResult<Guid> Map(ServiceResult<Guid> result)
        => result.ToUiResult(value => value);

    static EconomicCalendarUiModel ToUi(EconomicCalendarReadModel value)
        => new(
            value.EventDate,
            value.CountryCode,
            value.EventName,
            value.Actual,
            value.Forecast,
            value.Prior,
            value.CreatedOn,
            value.CreatedBy,
            value.Impact,
            value.Unit,
            value.Change,
            value.ChangePercentage);

    static EconomicCalendarReadModel ToBackend(EconomicCalendarUiModel value)
        => new(
            value.EventDate,
            value.CountryCode,
            value.EventName,
            value.Actual,
            value.Forecast,
            value.Prior,
            value.CreatedOn,
            value.CreatedBy,
            value.Impact,
            value.Unit,
            value.Change,
            value.ChangePercentage);

    static TerminalNotificationUiModel ToTerminal(IEvent value)
        => value is IErrorEvent error
            ? new TerminalNotificationUiModel(
                value.CommandId,
                error.ErrorCode,
                error.ErrorMessage,
                GetKind(value))
            : new TerminalNotificationUiModel(value.CommandId, Kind: GetKind(value));

    static TerminalNotificationKind GetKind(IEvent value)
        => value switch
        {
            EconomicCalendarAddedCompleteEvent or EconomicCalendarAddedFailEvent
                => TerminalNotificationKind.Added,
            EconomicCalendarChangedCompleteEvent or EconomicCalendarChangedFailEvent
                => TerminalNotificationKind.Changed,
            EconomicCalendarRemovedCompleteEvent or EconomicCalendarRemovedFailEvent
                => TerminalNotificationKind.Removed,
            EconomicCalendarsImportedCompleteEvent or EconomicCalendarsImportedFailEvent
                => TerminalNotificationKind.Imported,
            _ => TerminalNotificationKind.Unknown
        };
}
