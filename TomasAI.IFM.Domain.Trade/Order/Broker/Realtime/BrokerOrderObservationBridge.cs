using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.TradeBroker.Contracts;
using TomasAI.IFM.Domain.Trade.Order.Broker.Logging;
using TomasAI.IFM.Domain.Trade.Order.Broker.Event.Actor;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Order.Broker;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Order.Broker.Realtime;

/// <summary>Owns the single application broker observation subscription and routes facts through actor mailboxes.</summary>
public sealed class BrokerOrderObservationBridge(ITradeBroker broker, IActorSupervisor supervisor,
    ILogger<BrokerOrderObservationBridge> logger) : IAsyncDisposable, IDisposable
{
    private CancellationTokenSource? _stop;
    private Task? _run;

    /// <summary>Starts exactly one order-observation reader.</summary>
    public ValueTask StartAsync()
    {
        if (_run is not null) return ValueTask.CompletedTask;
        _stop = new CancellationTokenSource();
        _run = RunAsync(_stop.Token);
        return ValueTask.CompletedTask;
    }

    /// <summary>Stops the observation reader and waits for a clean exit.</summary>
    public async ValueTask StopAsync()
    {
        if (_stop is null || _run is null) return;
        await _stop.CancelAsync().ConfigureAwait(false);
        try { await _run.ConfigureAwait(false); }
        catch (Exception) when (_stop.IsCancellationRequested) { }
        _stop.Dispose();
        _stop = null;
        _run = null;
    }

    private async Task RunAsync(CancellationToken cancellationToken)
    {
        await foreach (var observation in broker.ObserveOrdersAsync(cancellationToken).ConfigureAwait(false))
        {
            try
            {
                if (!TryParse(observation.BrokerOrderId, out var id))
                    throw new InvalidOperationException("BO.OBSERVATION.IDENTITY_INVALID");
                await RouteAsync(id, observation).ConfigureAwait(false);
                logger.Routed(observation.Kind.ToString(), observation.BrokerOrderId, observation.ObservationId);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                logger.Failed(exception, "BO.OBSERVATION.ROUTE_FAILED", observation.BrokerOrderId, observation.ObservationId);
            }
        }
    }

    private async ValueTask RouteAsync(BrokerOrderId brokerOrderId, BrokerObservation observation)
    {
        if (observation.Kind is BrokerObservationKind.AccountSnapshot or
            BrokerObservationKind.GateChanged or BrokerObservationKind.ConnectionChanged)
            return;

        var evidence = new BrokerOrderObservationEvidence
        {
            ObservationId = observation.ObservationId,
            Kind = observation.Kind switch
            {
                BrokerObservationKind.Acknowledged => BrokerOrderObservationKind.Acknowledged,
                BrokerObservationKind.Execution => BrokerOrderObservationKind.Execution,
                BrokerObservationKind.Commission => BrokerOrderObservationKind.Commission,
                BrokerObservationKind.Rejected => BrokerOrderObservationKind.Rejected,
                BrokerObservationKind.Cancelled => BrokerOrderObservationKind.Cancelled,
                BrokerObservationKind.OrderCompleted => BrokerOrderObservationKind.OrderCompleted,
                _ => BrokerOrderObservationKind.Unknown
            },
            AccountAlias = observation.AccountAlias,
            OperationId = observation.OperationId,
            ComponentId = observation.ComponentId,
            LegId = observation.LegId,
            ContractId = observation.ContractId,
            ExternalExecutionId = observation.ExternalExecutionId,
            SignedQuantity = observation.SignedQuantity,
            Price = observation.Price,
            Commission = observation.Commission,
            OrderRevision = observation.OrderRevision,
            SourceEpoch = observation.SourceEpoch,
            SourceSequence = observation.SourceSequence,
            OccurredAtUtc = observation.OccurredAtUtc,
            Category = observation.Category,
            Detail = observation.Detail,
            ContentHash = ContentHash(observation)
        };
        var eventId = DeterministicId("broker-observation", observation.ObservationId.ToString("N"));
        var domainEvent = new BrokerOrderObservationReceivedEvent
        {
            Id = eventId,
            CommandId = eventId,
            Subject = new(ActorType.Event, BrokerOrderEventActor.ActorName,
                BrokerOrderObservationReceivedEvent.Verb, brokerOrderId.Format()),
            EntityId = brokerOrderId,
            AggregateId = brokerOrderId.Format(),
            EventSource = nameof(BrokerOrderObservationBridge),
            ReceivedOn = observation.OccurredAtUtc,
            Observation = evidence
        };
        var producer = supervisor.GetJSProducer(new ActorMailboxId(ActorType.Event, BrokerOrderEventActor.ActorName));
        await producer.SendAsync<BrokerOrderObservationReceivedEvent, BrokerOrderId>(
            domainEvent.Subject, domainEvent).ConfigureAwait(false);
    }

    private static bool TryParse(string value, out BrokerOrderId id)
    {
        id = default;
        var fields = value.Split('.');
        if (fields.Length != 5 || !int.TryParse(fields[0], out var portfolioId) ||
            !int.TryParse(fields[1], out var fundId) || !int.TryParse(fields[2], out var orderId) ||
            !Guid.TryParseExact(fields[3], "N", out var attempt) || !Guid.TryParseExact(fields[4], "N", out var component))
            return false;
        id = new(new(new(portfolioId, fundId, orderId), attempt), component);
        return id.IsValid;
    }

    private static Guid DeterministicId(params string[] parts) => new(SHA256.HashData(Encoding.UTF8.GetBytes(string.Join('|', parts)))[..16]);

    private static string ContentHash(BrokerObservation observation) => Convert.ToHexString(SHA256.HashData(
        Encoding.UTF8.GetBytes(string.Join('|', observation.Kind, observation.AccountAlias,
            observation.BrokerOrderId, observation.OperationId, observation.ComponentId,
            observation.LegId, observation.ContractId, observation.ExternalExecutionId,
            observation.SignedQuantity, observation.Price, observation.Commission,
            observation.OrderRevision, observation.SourceEpoch, observation.SourceSequence,
            observation.OccurredAtUtc.Ticks, observation.Category, observation.Detail))));

    /// <summary>Stops and releases the bridge.</summary>
    public async ValueTask DisposeAsync() => await StopAsync().ConfigureAwait(false);

    /// <summary>Stops the order observation bridge during synchronous container disposal.</summary>
    public void Dispose() => StopAsync().AsTask().GetAwaiter().GetResult();
}
