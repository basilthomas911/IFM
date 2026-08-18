using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using NATS.Client.Core;
using TomasAI.IFM.Domain.Fund.Shared.Events;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.Events;
using TomasAI.IFM.Domain.MarketData.Shared.Events;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Domain.Reference.Shared.Events;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Events.Domain;
using TomasAI.IFM.Framework.Messaging.NatsJetStream;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.UI.Net.SystemTests.Infrastructure;

public sealed record G2ObservedCommandEvent(
    DateTimeOffset ObservedUtc,
    string Family,
    string EventName,
    string Subject,
    Guid CommandId,
    bool? Success,
    string ErrorMessage,
    DateOnly? ImportDate,
    YieldCurveRateReadModel[]? ImportedYieldCurveRates);

public sealed record G2CommandListenerRegistration(
    string Family,
    string Actor,
    string Verb,
    string EventType,
    bool? Success);

public sealed class G2CommandEventObserver : IAsyncDisposable
{
    static readonly G2EventRoute[] Routes =
    [
        Route<MarketDataFeedStartedEvent>("MarketDataFeed", MarketDataFeedStartedEvent.Actor, MarketDataFeedStartedEvent.Verb, null),
        Route<MarketDataFeedStartedCompleteEvent>("MarketDataFeed", MarketDataFeedStartedCompleteEvent.Actor, MarketDataFeedStartedCompleteEvent.Verb, true),
        Route<MarketDataFeedStartedFailEvent>("MarketDataFeed", MarketDataFeedStartedFailEvent.Actor, MarketDataFeedStartedFailEvent.Verb, false),
        Route<MarketDataFeedStoppedEvent>("MarketDataFeed", MarketDataFeedStoppedEvent.Actor, MarketDataFeedStoppedEvent.Verb, null),
        Route<MarketDataFeedStoppedCompleteEvent>("MarketDataFeed", MarketDataFeedStoppedCompleteEvent.Actor, MarketDataFeedStoppedCompleteEvent.Verb, true),
        Route<MarketDataFeedStoppedFailEvent>("MarketDataFeed", MarketDataFeedStoppedFailEvent.Actor, MarketDataFeedStoppedFailEvent.Verb, false),

        Route<FuturesContractAddedEvent>("FuturesContract", FuturesContractAddedEvent.Actor, FuturesContractAddedEvent.Verb, null),
        Route<FuturesContractAddedCompleteEvent>("FuturesContract", FuturesContractAddedCompleteEvent.Actor, FuturesContractAddedCompleteEvent.Verb, true),
        Route<FuturesContractAddedFailEvent>("FuturesContract", FuturesContractAddedFailEvent.Actor, FuturesContractAddedFailEvent.Verb, false),
        Route<FuturesContractChangedEvent>("FuturesContract", FuturesContractChangedEvent.Actor, FuturesContractChangedEvent.Verb, null),
        Route<FuturesContractChangedCompleteEvent>("FuturesContract", FuturesContractChangedCompleteEvent.Actor, FuturesContractChangedCompleteEvent.Verb, true),
        Route<FuturesContractChangedFailEvent>("FuturesContract", FuturesContractChangedFailEvent.Actor, FuturesContractChangedFailEvent.Verb, false),
        Route<FuturesContractRemovedEvent>("FuturesContract", FuturesContractRemovedEvent.Actor, FuturesContractRemovedEvent.Verb, null),
        Route<FuturesContractRemovedCompleteEvent>("FuturesContract", FuturesContractRemovedCompleteEvent.Actor, FuturesContractRemovedCompleteEvent.Verb, true),
        Route<FuturesContractRemovedFailEvent>("FuturesContract", FuturesContractRemovedFailEvent.Actor, FuturesContractRemovedFailEvent.Verb, false),

        Route<FuturesOptionContractAddedEvent>("FuturesOptionContract", FuturesOptionContractAddedEvent.Actor, FuturesOptionContractAddedEvent.Verb, null),
        Route<FuturesOptionContractAddedCompleteEvent>("FuturesOptionContract", FuturesOptionContractAddedCompleteEvent.Actor, FuturesOptionContractAddedCompleteEvent.Verb, true),
        Route<FuturesOptionContractAddedFailEvent>("FuturesOptionContract", FuturesOptionContractAddedFailEvent.Actor, FuturesOptionContractAddedFailEvent.Verb, false),
        Route<FuturesOptionContractChangedEvent>("FuturesOptionContract", FuturesOptionContractChangedEvent.Actor, FuturesOptionContractChangedEvent.Verb, null),
        Route<FuturesOptionContractChangedCompleteEvent>("FuturesOptionContract", FuturesOptionContractChangedCompleteEvent.Actor, FuturesOptionContractChangedCompleteEvent.Verb, true),
        Route<FuturesOptionContractChangedFailEvent>("FuturesOptionContract", FuturesOptionContractChangedFailEvent.Actor, FuturesOptionContractChangedFailEvent.Verb, false),
        Route<FuturesOptionContractRemovedEvent>("FuturesOptionContract", FuturesOptionContractRemovedEvent.Actor, FuturesOptionContractRemovedEvent.Verb, null),
        Route<FuturesOptionContractRemovedCompleteEvent>("FuturesOptionContract", FuturesOptionContractRemovedCompleteEvent.Actor, FuturesOptionContractRemovedCompleteEvent.Verb, true),
        Route<FuturesOptionContractRemovedFailEvent>("FuturesOptionContract", FuturesOptionContractRemovedFailEvent.Actor, FuturesOptionContractRemovedFailEvent.Verb, false),

        Route<YieldCurveRateAddedEvent>("YieldCurve", YieldCurveRateAddedEvent.Actor, YieldCurveRateAddedEvent.Verb, null),
        Route<YieldCurveRateAddedCompleteEvent>("YieldCurve", YieldCurveRateAddedCompleteEvent.Actor, YieldCurveRateAddedCompleteEvent.Verb, true),
        Route<YieldCurveRateAddedFailEvent>("YieldCurve", YieldCurveRateAddedFailEvent.Actor, YieldCurveRateAddedFailEvent.Verb, false),
        Route<YieldCurveRateChangedEvent>("YieldCurve", YieldCurveRateChangedEvent.Actor, YieldCurveRateChangedEvent.Verb, null),
        Route<YieldCurveRateChangedCompleteEvent>("YieldCurve", YieldCurveRateChangedCompleteEvent.Actor, YieldCurveRateChangedCompleteEvent.Verb, true),
        Route<YieldCurveRateChangedFailEvent>("YieldCurve", YieldCurveRateChangedFailEvent.Actor, YieldCurveRateChangedFailEvent.Verb, false),
        Route<YieldCurveRateRemovedEvent>("YieldCurve", YieldCurveRateRemovedEvent.Actor, YieldCurveRateRemovedEvent.Verb, null),
        Route<YieldCurveRateRemovedCompleteEvent>("YieldCurve", YieldCurveRateRemovedCompleteEvent.Actor, YieldCurveRateRemovedCompleteEvent.Verb, true),
        Route<YieldCurveRateRemovedFailEvent>("YieldCurve", YieldCurveRateRemovedFailEvent.Actor, YieldCurveRateRemovedFailEvent.Verb, false),
        Route<YieldCurveRatesImportedEvent>("YieldCurve", YieldCurveRatesImportedEvent.Actor, YieldCurveRatesImportedEvent.Verb, null),
        Route<YieldCurveRatesImportedCompleteEvent>("YieldCurve", YieldCurveRatesImportedCompleteEvent.Actor, YieldCurveRatesImportedCompleteEvent.Verb, true),
        Route<YieldCurveRatesImportedFailEvent>("YieldCurve", YieldCurveRatesImportedFailEvent.Actor, YieldCurveRatesImportedFailEvent.Verb, false),

        Route<EconomicCalendarAddedCompleteEvent>("EconomicCalendar", EconomicCalendarAddedCompleteEvent.Actor, EconomicCalendarAddedCompleteEvent.Verb, true),
        Route<EconomicCalendarAddedFailEvent>("EconomicCalendar", EconomicCalendarAddedFailEvent.Actor, EconomicCalendarAddedFailEvent.Verb, false),
        Route<EconomicCalendarChangedCompleteEvent>("EconomicCalendar", EconomicCalendarChangedCompleteEvent.Actor, EconomicCalendarChangedCompleteEvent.Verb, true),
        Route<EconomicCalendarChangedFailEvent>("EconomicCalendar", EconomicCalendarChangedFailEvent.Actor, EconomicCalendarChangedFailEvent.Verb, false),
        Route<EconomicCalendarRemovedCompleteEvent>("EconomicCalendar", EconomicCalendarRemovedCompleteEvent.Actor, EconomicCalendarRemovedCompleteEvent.Verb, true),
        Route<EconomicCalendarRemovedFailEvent>("EconomicCalendar", EconomicCalendarRemovedFailEvent.Actor, EconomicCalendarRemovedFailEvent.Verb, false),
        Route<EconomicCalendarsImportedCompleteEvent>("EconomicCalendar", EconomicCalendarsImportedCompleteEvent.Actor, EconomicCalendarsImportedCompleteEvent.Verb, true),
        Route<EconomicCalendarsImportedFailEvent>("EconomicCalendar", EconomicCalendarsImportedFailEvent.Actor, EconomicCalendarsImportedFailEvent.Verb, false),

        Route<LookupTypeAddedCompleteEvent>("LookupType", LookupTypeAddedCompleteEvent.Actor, LookupTypeAddedCompleteEvent.Verb, true),
        Route<LookupTypeAddedFailEvent>("LookupType", LookupTypeAddedFailEvent.Actor, LookupTypeAddedFailEvent.Verb, false),
        Route<LookupTypeChangedCompleteEvent>("LookupType", LookupTypeChangedCompleteEvent.Actor, LookupTypeChangedCompleteEvent.Verb, true),
        Route<LookupTypeChangedFailEvent>("LookupType", LookupTypeChangedFailEvent.Actor, LookupTypeChangedFailEvent.Verb, false),
        Route<LookupTypeRemovedCompleteEvent>("LookupType", LookupTypeRemovedCompleteEvent.Actor, LookupTypeRemovedCompleteEvent.Verb, true),
        Route<LookupTypeRemovedFailEvent>("LookupType", LookupTypeRemovedFailEvent.Actor, LookupTypeRemovedFailEvent.Verb, false),

        Route<FundCreatedCompleteEvent>("Fund", FundCreatedCompleteEvent.Actor, FundCreatedCompleteEvent.Verb, true),
        Route<FundCreatedFailEvent>("Fund", FundCreatedFailEvent.Actor, FundCreatedFailEvent.Verb, false),
        Route<FundTransactionCreatedCompleteEvent>("FundTransaction", FundTransactionCreatedCompleteEvent.Actor, FundTransactionCreatedCompleteEvent.Verb, true),
        Route<FundTransactionCreatedFailEvent>("FundTransaction", FundTransactionCreatedFailEvent.Actor, FundTransactionCreatedFailEvent.Verb, false),

        Route<OrderAddedToFundCompleteEvent>("FundOrder", OrderAddedToFundCompleteEvent.Actor, OrderAddedToFundCompleteEvent.Verb, true),
        Route<OrderAddedToFundFailEvent>("FundOrder", OrderAddedToFundFailEvent.Actor, OrderAddedToFundFailEvent.Verb, false),
        Route<OrderRemovedFromFundCompleteEvent>("FundOrder", OrderRemovedFromFundCompleteEvent.Actor, OrderRemovedFromFundCompleteEvent.Verb, true),
        Route<OrderRemovedFromFundFailEvent>("FundOrder", OrderRemovedFromFundFailEvent.Actor, OrderRemovedFromFundFailEvent.Verb, false),
        Route<TradeAddedToFundOrderCompleteEvent>("FundOrder", TradeAddedToFundOrderCompleteEvent.Actor, TradeAddedToFundOrderCompleteEvent.Verb, true),
        Route<TradeAddedToFundOrderFailEvent>("FundOrder", TradeAddedToFundOrderFailEvent.Actor, TradeAddedToFundOrderFailEvent.Verb, false),
        Route<TradeRemovedFromFundOrderCompleteEvent>("FundOrder", TradeRemovedFromFundOrderCompleteEvent.Actor, TradeRemovedFromFundOrderCompleteEvent.Verb, true),
        Route<TradeRemovedFromFundOrderFailEvent>("FundOrder", TradeRemovedFromFundOrderFailEvent.Actor, TradeRemovedFromFundOrderFailEvent.Verb, false),
        Route<FundOrderTradeStateChangedCompleteEvent>("FundOrder", FundOrderTradeStateChangedCompleteEvent.Actor, FundOrderTradeStateChangedCompleteEvent.Verb, true),
        Route<FundOrderTradeStateChangedFailEvent>("FundOrder", FundOrderTradeStateChangedFailEvent.Actor, FundOrderTradeStateChangedFailEvent.Verb, false),

        Route<EndOfDayFundTransactionProcessedCompleteEvent>("EndOfDay", EndOfDayFundTransactionProcessedCompleteEvent.Actor, EndOfDayFundTransactionProcessedCompleteEvent.Verb, true),
        Route<EndOfDayFundTransactionProcessedFailEvent>("EndOfDay", EndOfDayFundTransactionProcessedFailEvent.Actor, EndOfDayFundTransactionProcessedFailEvent.Verb, false),

        Route<DatabaseBackupRequestedDomainEvent>("DatabaseBackup", "DatabaseBackupEvent", "BackupRequested", null),
        Route<DatabaseOperationCompletedEvent>("DatabaseBackup", "DatabaseBackupEvent", "OperationCompleted", true),
        Route<DatabaseOperationFailedEvent>("DatabaseBackup", "DatabaseBackupEvent", "OperationFailed", false),
        Route<DatabaseOperationCancelledEvent>("DatabaseBackup", "DatabaseBackupEvent", "OperationCancelled", false)
    ];

    readonly NatsConnectionManager _connectionManager = new();
    readonly NatsActorEventListener _listener;
    readonly ConcurrentQueue<G2ObservedCommandEvent> _events = new();
    readonly IReadOnlyDictionary<(string Actor, string Verb), G2EventRoute> _routes;

    public G2CommandEventObserver(Uri natsUri)
    {
        _listener = new NatsActorEventListener(
            new NatsEventListenerOptions { Url = natsUri.ToString() },
            NullLogger.Instance,
            _connectionManager);
        _routes = Routes.ToDictionary(route => (route.Actor, route.Verb));
    }

    public EventListenerState State => _listener.State;
    public IReadOnlyList<G2ObservedCommandEvent> Events => _events.ToArray();
    public static IReadOnlyList<G2CommandListenerRegistration> Registrations
        => Routes.Select(route => new G2CommandListenerRegistration(
                route.Family,
                route.Actor,
                route.Verb,
                route.EventType.Name,
                route.Success))
            .ToArray();

    public async Task StartAsync(string runId, CancellationToken cancellationToken)
    {
        var eventMap = Routes
            .GroupBy(route => new ActorMailboxId(ActorType.Event, route.Actor))
            .ToDictionary(
                group => group.Key,
                group => group.Select(route => route.Verb).Distinct(StringComparer.Ordinal).ToList());
        await _listener.StartAsync($"IFM.UI.G2.CommandEvidence.{runId}", eventMap, HandleAsync)
            .ConfigureAwait(false);

        using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutSource.CancelAfter(TimeSpan.FromSeconds(10));
        while (_listener.State != EventListenerState.Running)
            await Task.Delay(TimeSpan.FromMilliseconds(50), timeoutSource.Token).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<G2ObservedCommandEvent>> WaitForAsync(
        Func<IReadOnlyList<G2ObservedCommandEvent>, bool> predicate,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutSource.CancelAfter(timeout);
        while (!timeoutSource.IsCancellationRequested)
        {
            var snapshot = Events;
            if (predicate(snapshot))
                return snapshot;
            try
            {
                await Task.Delay(TimeSpan.FromMilliseconds(100), timeoutSource.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                break;
            }
        }
        throw new TimeoutException($"Expected G2 command evidence was not observed within {timeout}.");
    }

    public async Task WriteEvidenceAsync(G0EvidenceWriter evidence, CancellationToken cancellationToken)
    {
        await evidence.WriteTextAsync(
            Path.Combine("network", "g2-command-listener-catalog.json"),
            JsonSerializer.Serialize(Registrations, new JsonSerializerOptions { WriteIndented = true }),
            cancellationToken).ConfigureAwait(false);
        await evidence.WriteTextAsync(
            Path.Combine("network", "g2-command-events.json"),
            JsonSerializer.Serialize(Events, new JsonSerializerOptions { WriteIndented = true }),
            cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask DisposeAsync()
    {
        await _listener.StopAsync().ConfigureAwait(false);
        await _connectionManager.DisposeAsync().ConfigureAwait(false);
    }

    ValueTask HandleAsync(string verb, NatsMsg<byte[]> message)
    {
        var subject = message.Subject.ToSubject();
        if (!_routes.TryGetValue((subject.Name, verb), out var route))
            throw new InvalidOperationException($"No G2 command-evidence route exists for {subject.Name}.{verb}.");
        var domainEvent = route.Deserialize(message)
            ?? throw new InvalidOperationException($"Could not deserialize {route.EventType.Name}.");
        var importDate = domainEvent switch
        {
            YieldCurveRatesImportedEvent value => DateOnly.FromDateTime(value.ImportDate),
            YieldCurveRatesImportedCompleteEvent value => DateOnly.FromDateTime(value.ImportDate),
            YieldCurveRatesImportedFailEvent value => DateOnly.FromDateTime(value.ImportDate),
            _ => null as DateOnly?
        };
        var importedRates = domainEvent is YieldCurveRatesImportedCompleteEvent completed
            ? completed.YieldCurveRates
            : null;
        _events.Enqueue(new G2ObservedCommandEvent(
            DateTimeOffset.UtcNow,
            route.Family,
            domainEvent.EventName,
            domainEvent.Subject.ToString(),
            domainEvent.CommandId,
            route.Success,
            domainEvent is IErrorEvent error ? error.ErrorMessage : string.Empty,
            importDate,
            importedRates));
        return ValueTask.CompletedTask;
    }

    static G2EventRoute Route<TEvent>(string family, string actor, string verb, bool? success)
        where TEvent : class, IEvent
        => new(
            family,
            actor,
            verb,
            typeof(TEvent),
            success,
            message => message.AsEvent<TEvent>());

    sealed record G2EventRoute(
        string Family,
        string Actor,
        string Verb,
        Type EventType,
        bool? Success,
        Func<NatsMsg<byte[]>, IEvent?> Deserialize);
}
