using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using NATS.Client.Core;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Events;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.Events;
using TomasAI.IFM.Domain.MarketData.Shared.Events;
using TomasAI.IFM.Framework.Messaging.NatsJetStream;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.StatusConsole.Events;

namespace TomasAI.IFM.UI.Net.SystemTests.Infrastructure;

public sealed record G0ObservedEvent(
    DateTimeOffset ObservedUtc,
    string Family,
    string Verb,
    string EntityId,
    Guid CommandId,
    bool? Success,
    int? RecordCount,
    string Message);

public sealed class G0EventObserver : IAsyncDisposable
{
    readonly string _url;
    readonly NatsConnectionManager _connectionManager = new();
    readonly List<NatsActorEventListener> _listeners = [];
    readonly ConcurrentQueue<G0ObservedEvent> _events = new();
    bool _started;

    public G0EventObserver(Uri natsUri) => _url = natsUri.ToString();

    public IReadOnlyList<G0ObservedEvent> Events => _events.ToArray();

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (_started)
            return;

        try
        {
            await StartListenerAsync(
                "G0-YieldCurve",
                YieldCurveRatesImportedEvent.Actor,
                [YieldCurveRatesImportedEvent.Verb, YieldCurveRatesImportedCompleteEvent.Verb, YieldCurveRatesImportedFailEvent.Verb],
                HandleYieldCurveAsync).ConfigureAwait(false);
            await StartListenerAsync(
                "G0-EconomicCalendar",
                EconomicCalendarsImportedEvent.Actor,
                [EconomicCalendarsImportedEvent.Verb, EconomicCalendarsImportedCompleteEvent.Verb, EconomicCalendarsImportedFailEvent.Verb],
                HandleEconomicCalendarAsync).ConfigureAwait(false);
            await StartListenerAsync(
                "G0-Rsi",
                FuturesRsiSignalStartedEvent.Actor,
                [FuturesRsiSignalStartedEvent.Verb, FuturesRsiSignalStoppedEvent.Verb],
                HandleRsiAsync).ConfigureAwait(false);
            await StartListenerAsync(
                "G0-Atr",
                FuturesAtrSignalStartedEvent.Actor,
                [FuturesAtrSignalStartedEvent.Verb, FuturesAtrSignalStoppedEvent.Verb],
                HandleAtrAsync).ConfigureAwait(false);
            await StartListenerAsync(
                "G0-Adx",
                FuturesAdxSignalStartedEvent.Actor,
                [FuturesAdxSignalStartedEvent.Verb, FuturesAdxSignalStoppedEvent.Verb],
                HandleAdxAsync).ConfigureAwait(false);
            await StartListenerAsync(
                "G0-Macd",
                FuturesMacdSignalStartedEvent.Actor,
                [FuturesMacdSignalStartedEvent.Verb, FuturesMacdSignalStoppedEvent.Verb],
                HandleMacdAsync).ConfigureAwait(false);
            await StartListenerAsync(
                "G0-Status",
                StatusConsoleLoggedEvent.Actor,
                [StatusConsoleLoggedEvent.Verb],
                HandleStatusAsync,
                ActorType.Notify).ConfigureAwait(false);
            await StartListenerAsync(
                "G0-MarketDataFeed",
                MarketDataFeedStartedEvent.Actor,
                [
                    MarketDataFeedStartedEvent.Verb,
                    MarketDataFeedStartedCompleteEvent.Verb,
                    MarketDataFeedStartedFailEvent.Verb,
                    MarketDataFeedStoppedEvent.Verb,
                    MarketDataFeedStoppedCompleteEvent.Verb,
                    MarketDataFeedStoppedFailEvent.Verb
                ],
                HandleMarketDataFeedAsync).ConfigureAwait(false);

            using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutSource.CancelAfter(TimeSpan.FromSeconds(10));
            while (_listeners.Any(listener => listener.State != EventListenerState.Running))
                await Task.Delay(TimeSpan.FromMilliseconds(50), timeoutSource.Token).ConfigureAwait(false);
            _started = true;
        }
        catch
        {
            await StopAsync().ConfigureAwait(false);
            throw;
        }
    }

    public async Task<IReadOnlyList<G0ObservedEvent>> WaitForAsync(
        Func<IReadOnlyList<G0ObservedEvent>, bool> predicate,
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
        throw new TimeoutException($"Expected NATS evidence was not observed within {timeout}.");
    }

    public async Task WriteEvidenceAsync(G0EvidenceWriter evidence, CancellationToken cancellationToken)
        => await evidence.WriteTextAsync(
            Path.Combine("network", "nats-events.json"),
            JsonSerializer.Serialize(Events, new JsonSerializerOptions { WriteIndented = true }),
            cancellationToken).ConfigureAwait(false);

    public async Task StopAsync()
    {
        foreach (var listener in _listeners.AsEnumerable().Reverse())
        {
            try { await listener.StopAsync().ConfigureAwait(false); }
            catch { /* Preserve the primary audit result; cleanup state is checked by the caller. */ }
        }
        _listeners.Clear();
        _started = false;
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync().ConfigureAwait(false);
        await _connectionManager.DisposeAsync().ConfigureAwait(false);
    }

    async Task StartListenerAsync(
        string id,
        string actor,
        List<string> verbs,
        Func<string, NatsMsg<byte[]>, ValueTask> handler,
        ActorType actorType = ActorType.Event)
    {
        var listener = new NatsActorEventListener(
            new NatsEventListenerOptions { Url = _url },
            NullLogger.Instance,
            _connectionManager);
        _listeners.Add(listener);
        await listener.StartAsync(
            id,
            new Dictionary<ActorMailboxId, List<string>>
            {
                [new ActorMailboxId(actorType, actor)] = verbs
            },
            handler).ConfigureAwait(false);
    }

    ValueTask HandleYieldCurveAsync(string verb, NatsMsg<byte[]> message)
    {
        if (verb == YieldCurveRatesImportedEvent.Verb)
        {
            var value = message.AsEvent<YieldCurveRatesImportedEvent>()!;
            Add("YieldCurve", verb, value.Subject.EntityId, value.CommandId, null, null, "Import accepted");
        }
        else if (verb == YieldCurveRatesImportedCompleteEvent.Verb)
        {
            var value = message.AsEvent<YieldCurveRatesImportedCompleteEvent>()!;
            Add("YieldCurve", verb, value.Subject.EntityId, value.CommandId, true, value.YieldCurveRates.Length, "Import completed");
        }
        else
        {
            var value = message.AsEvent<YieldCurveRatesImportedFailEvent>()!;
            Add("YieldCurve", verb, value.Subject.EntityId, value.CommandId, false, null, value.ErrorMessage);
        }
        return ValueTask.CompletedTask;
    }

    ValueTask HandleEconomicCalendarAsync(string verb, NatsMsg<byte[]> message)
    {
        if (verb == EconomicCalendarsImportedEvent.Verb)
        {
            var value = message.AsEvent<EconomicCalendarsImportedEvent>()!;
            Add("EconomicCalendar", verb, value.Subject.EntityId, value.CommandId, null, null, "Import accepted");
        }
        else if (verb == EconomicCalendarsImportedCompleteEvent.Verb)
        {
            var value = message.AsEvent<EconomicCalendarsImportedCompleteEvent>()!;
            Add("EconomicCalendar", verb, value.Subject.EntityId, value.CommandId, true, value.EconomicCalendars.Length, "Import completed");
        }
        else
        {
            var value = message.AsEvent<EconomicCalendarsImportedFailEvent>()!;
            Add("EconomicCalendar", verb, value.Subject.EntityId, value.CommandId, false, null, value.ErrorMessage);
        }
        return ValueTask.CompletedTask;
    }

    ValueTask HandleRsiAsync(string verb, NatsMsg<byte[]> message)
    {
        if (verb == FuturesRsiSignalStartedEvent.Verb)
        {
            var value = message.AsEvent<FuturesRsiSignalStartedEvent>()!;
            AddSignal("RSI", verb, value.EntityId.Format(), value.CommandId);
        }
        else
        {
            var value = message.AsEvent<FuturesRsiSignalStoppedEvent>()!;
            AddSignal("RSI", verb, value.EntityId.Format(), value.CommandId);
        }
        return ValueTask.CompletedTask;
    }

    ValueTask HandleAtrAsync(string verb, NatsMsg<byte[]> message)
    {
        if (verb == FuturesAtrSignalStartedEvent.Verb)
        {
            var value = message.AsEvent<FuturesAtrSignalStartedEvent>()!;
            AddSignal("ATR", verb, value.EntityId.Format(), value.CommandId);
        }
        else
        {
            var value = message.AsEvent<FuturesAtrSignalStoppedEvent>()!;
            AddSignal("ATR", verb, value.EntityId.Format(), value.CommandId);
        }
        return ValueTask.CompletedTask;
    }

    ValueTask HandleAdxAsync(string verb, NatsMsg<byte[]> message)
    {
        if (verb == FuturesAdxSignalStartedEvent.Verb)
        {
            var value = message.AsEvent<FuturesAdxSignalStartedEvent>()!;
            AddSignal("ADX", verb, value.EntityId.Format(), value.CommandId);
        }
        else
        {
            var value = message.AsEvent<FuturesAdxSignalStoppedEvent>()!;
            AddSignal("ADX", verb, value.EntityId.Format(), value.CommandId);
        }
        return ValueTask.CompletedTask;
    }

    ValueTask HandleMacdAsync(string verb, NatsMsg<byte[]> message)
    {
        if (verb == FuturesMacdSignalStartedEvent.Verb)
        {
            var value = message.AsEvent<FuturesMacdSignalStartedEvent>()!;
            AddSignal("MACD", verb, value.EntityId.Format(), value.CommandId);
        }
        else
        {
            var value = message.AsEvent<FuturesMacdSignalStoppedEvent>()!;
            AddSignal("MACD", verb, value.EntityId.Format(), value.CommandId);
        }
        return ValueTask.CompletedTask;
    }

    ValueTask HandleStatusAsync(string verb, NatsMsg<byte[]> message)
    {
        var value = message.AsEvent<StatusConsoleLoggedEvent>()!;
        Add("Status", verb, value.Subject.EntityId, value.CommandId, null, null, value.StatusConsoleLog.Message);
        return ValueTask.CompletedTask;
    }

    ValueTask HandleMarketDataFeedAsync(string verb, NatsMsg<byte[]> message)
    {
        if (verb == MarketDataFeedStartedEvent.Verb)
        {
            var value = message.AsEvent<MarketDataFeedStartedEvent>()!;
            Add("MarketDataFeed", verb, value.Subject.EntityId, value.CommandId, null, value.FuturesContracts?.Length, "Feed start accepted");
        }
        else if (verb == MarketDataFeedStartedCompleteEvent.Verb)
        {
            var value = message.AsEvent<MarketDataFeedStartedCompleteEvent>()!;
            Add("MarketDataFeed", verb, value.Subject.EntityId, value.CommandId, true, value.FuturesContracts?.Length, "Feed start completed");
        }
        else if (verb == MarketDataFeedStartedFailEvent.Verb)
        {
            var value = message.AsEvent<MarketDataFeedStartedFailEvent>()!;
            Add("MarketDataFeed", verb, value.Subject.EntityId, value.CommandId, false, null, value.ErrorMessage);
        }
        else if (verb == MarketDataFeedStoppedEvent.Verb)
        {
            var value = message.AsEvent<MarketDataFeedStoppedEvent>()!;
            Add("MarketDataFeed", verb, value.Subject.EntityId, value.CommandId, null, null, "Feed stop accepted");
        }
        else if (verb == MarketDataFeedStoppedCompleteEvent.Verb)
        {
            var value = message.AsEvent<MarketDataFeedStoppedCompleteEvent>()!;
            Add("MarketDataFeed", verb, value.Subject.EntityId, value.CommandId, true, null, "Feed stop completed");
        }
        else
        {
            var value = message.AsEvent<MarketDataFeedStoppedFailEvent>()!;
            Add("MarketDataFeed", verb, value.Subject.EntityId, value.CommandId, false, null, value.ErrorMessage);
        }
        return ValueTask.CompletedTask;
    }

    void AddSignal(string family, string verb, string entityId, Guid commandId)
        => Add(family, verb, entityId, commandId, true, null, $"{family} {verb}");

    void Add(
        string family,
        string verb,
        string entityId,
        Guid commandId,
        bool? success,
        int? recordCount,
        string message)
        => _events.Enqueue(new G0ObservedEvent(
            DateTimeOffset.UtcNow,
            family,
            verb,
            entityId,
            commandId,
            success,
            recordCount,
            message));
}
