using System.Security.Cryptography;
using System.Text;
using System.Threading.Channels;
using Microsoft.Extensions.Logging;
using NATS.Client.Core;
using NATS.Client.JetStream;
using NATS.Client.JetStream.Models;
using NATS.Net;
using TomasAI.IFM.Framework.Messaging.NatsJetStream.Contracts;
using TomasAI.IFM.Framework.Messaging.NatsJetStream.Serializers;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Framework.Messaging.NatsJetStream;

/// <summary>
/// Receives actor events through stable, explicit-acknowledgement JetStream consumers.
/// </summary>
/// <remarks>
/// A message is acknowledged only after the configured handler returns successfully.
/// Handler failures are negatively acknowledged and therefore remain eligible for redelivery.
/// </remarks>
public sealed class NatsJetStreamEventListener(
    INatsJetStreamEventListenerOptions options,
    ILogger logger,
    NatsConnectionManager? connectionManager = null) : IJSActorEventListener
{
    const string ServiceId = nameof(NatsJetStreamEventListener);
    readonly INatsJetStreamEventListenerOptions _options = options ?? throw new ArgumentNullException(nameof(options));
    readonly ILogger _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    NatsConnectionManager? _connectionManager = connectionManager ?? new NatsConnectionManager();
    readonly bool _ownsConnectionManager = connectionManager is null;
    readonly NatsByteArrayMessageSerializer _deserializer = new();
    readonly SemaphoreSlim _lifecycleGate = new(1, 1);
    readonly List<MailboxRuntime> _mailboxes = [];
    NatsClient? _client;
    CancellationTokenSource? _consumerCancellation;
    EventListenerState _state = EventListenerState.Unknown;
    string _eventListenerId = string.Empty;
    Func<string, NatsMsg<byte[]>, ValueTask> _eventHandler = static (_, _) => ValueTask.CompletedTask;
    int _messageCount;

    /// <inheritdoc />
    public EventListenerState State => _state;

    /// <inheritdoc />
    public int MessageCount => Volatile.Read(ref _messageCount);

    /// <summary>
    /// Gets the server stream selected for this listener after startup.
    /// </summary>
    internal string StreamName { get; private set; } = string.Empty;

    /// <inheritdoc />
    public async ValueTask StartAsync(
        string eventListenerId,
        Dictionary<ActorMailboxId, List<string>> eventMap,
        Func<string, NatsMsg<byte[]>, ValueTask> eventHandler)
    {
        await _lifecycleGate.WaitAsync().ConfigureAwait(false);
        try
        {
            if (_client is not null)
                return;

            ValidateStartArguments(eventListenerId, eventMap, eventHandler);
            GetValidatedOptions();

            ActorExtensions.DataSerializer ??= new NatsMessagePackDataSerializer();
            ActorExtensions.MsgSerializer ??= new NatsByteArrayMessageSerializer();
            _eventListenerId = eventListenerId;
            _eventHandler = eventHandler;
            Interlocked.Exchange(ref _messageCount, 0);
            _consumerCancellation = new CancellationTokenSource();
            var cancellationToken = _consumerCancellation.Token;
            _connectionManager ??= new NatsConnectionManager();
            _client = await _connectionManager.GetClientAsync(_options.Url, cancellationToken).ConfigureAwait(false);
            var jetStream = await _connectionManager.GetJetStreamContextAsync(_options.Url, cancellationToken).ConfigureAwait(false);
            _state = EventListenerState.Started;

            foreach (var entry in eventMap)
                await StartMailboxAsync(jetStream, entry.Key, entry.Value, eventMap.Count, cancellationToken).ConfigureAwait(false);

            _state = EventListenerState.Running;
            _logger.LogInformationEvent(
                ServiceId,
                "JetStream event listener {EventListenerId} started with {MailboxCount} durable consumers.",
                eventListenerId,
                _mailboxes.Count);
        }
        catch
        {
            await StopFailedStartAsync().ConfigureAwait(false);
            throw;
        }
        finally
        {
            _lifecycleGate.Release();
        }
    }

    /// <inheritdoc />
    public async ValueTask StopAsync()
    {
        await _lifecycleGate.WaitAsync().ConfigureAwait(false);
        try
        {
            if (_client is null)
                return;

            _consumerCancellation!.Cancel();
            await AwaitExpectedCancellationAsync(_mailboxes.Select(runtime => runtime.ConsumerTask)).ConfigureAwait(false);
            foreach (var runtime in _mailboxes)
                foreach (var channel in runtime.Channels)
                    channel.Writer.TryComplete();
            await Task.WhenAll(_mailboxes.SelectMany(runtime => runtime.DispatcherTasks)).ConfigureAwait(false);
            _mailboxes.Clear();
            _consumerCancellation.Dispose();
            _consumerCancellation = null;
            _client = null;
            if (_ownsConnectionManager)
            {
                await _connectionManager!.DisposeAsync().ConfigureAwait(false);
                _connectionManager = null;
            }
            _state = EventListenerState.Stopped;
            _logger.LogInformationEvent(
                ServiceId,
                "JetStream event listener {EventListenerId} stopped after {MessageCount} deliveries.",
                _eventListenerId,
                MessageCount);
        }
        finally
        {
            _lifecycleGate.Release();
        }
    }

    async ValueTask StartMailboxAsync(
        INatsJSContext jetStream,
        ActorMailboxId mailboxId,
        List<string> verbs,
        int mailboxCount,
        CancellationToken cancellationToken)
    {
        var filterSubject = ResolveFilterSubject(mailboxId, mailboxCount);
        var stream = await ResolveStreamAsync(jetStream, filterSubject, cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrEmpty(StreamName))
            StreamName = stream.Info.Config.Name;
        else if (!string.Equals(StreamName, stream.Info.Config.Name, StringComparison.Ordinal))
            throw new InvalidOperationException("All mailbox filters for one listener must be stored by the same stream.");
        var durableName = CreateDurableConsumerName(
            _options.DurableConsumerNamePrefix,
            _eventListenerId,
            mailboxId);
        var consumer = await jetStream.CreateOrUpdateConsumerAsync(
            stream.Info.Config.Name,
            new ConsumerConfig(durableName)
            {
                FilterSubject = filterSubject,
                AckPolicy = ConsumerConfigAckPolicy.Explicit,
                DeliverPolicy = _options.DeliverPolicy == NatsJetStreamEventDeliverPolicy.New
                    ? ConsumerConfigDeliverPolicy.New
                    : ConsumerConfigDeliverPolicy.All,
                AckWait = _options.AckWait,
                MaxDeliver = _options.MaxDeliver,
                MaxAckPending = GetOutstandingLimit()
            },
            cancellationToken).ConfigureAwait(false);

        var channels = new Channel<PendingDelivery>[_options.DispatcherCount];
        var dispatcherTasks = new Task[_options.DispatcherCount];
        var acceptedVerbs = new HashSet<string>(verbs, StringComparer.OrdinalIgnoreCase);
        for (var index = 0; index < channels.Length; index++)
        {
            channels[index] = Channel.CreateBounded<PendingDelivery>(new BoundedChannelOptions(_options.DispatcherCapacity)
            {
                SingleReader = true,
                SingleWriter = true,
                FullMode = BoundedChannelFullMode.Wait
            });
            dispatcherTasks[index] = DispatchLoopAsync(channels[index].Reader);
        }

        var consumeOptions = new NatsJSConsumeOpts
        {
            MaxMsgs = GetMaxMessages(),
            ThresholdMsgs = GetThresholdMessages(),
            DrainOnCancel = false
        };
        var consumerTask = ConsumeAsync(
            consumer,
            mailboxId,
            acceptedVerbs,
            channels,
            consumeOptions,
            cancellationToken);
        _mailboxes.Add(new MailboxRuntime(channels, dispatcherTasks, consumerTask));
    }

    async Task ConsumeAsync(
        INatsJSConsumer consumer,
        ActorMailboxId mailboxId,
        HashSet<string> acceptedVerbs,
        Channel<PendingDelivery>[] channels,
        NatsJSConsumeOpts consumeOptions,
        CancellationToken cancellationToken)
    {
        try
        {
            await foreach (var message in consumer.ConsumeAsync(
                opts: consumeOptions,
                serializer: _deserializer,
                cancellationToken: cancellationToken).ConfigureAwait(false))
            {
                try
                {
                    message.EnsureSuccess();
                    Interlocked.Increment(ref _messageCount);
                    NatsMessagingMetrics.Received.Add(1);
                    if (message.Metadata?.NumDelivered > 1)
                        NatsMessagingMetrics.RecordJetStreamRedelivery(mailboxId.ActorType);

                    if (message.Data is null)
                    {
                        await message.AckAsync(cancellationToken: CancellationToken.None).ConfigureAwait(false);
                        continue;
                    }

                    var subject = message.Subject.ToSubject();
                    if (!acceptedVerbs.Contains(subject.Verb))
                    {
                        await message.AckAsync(cancellationToken: CancellationToken.None).ConfigureAwait(false);
                        continue;
                    }

                    var stripe = (subject.ThreadId.GetHashCode() & 0x7fff_ffff) % channels.Length;
                    NatsMessagingMetrics.JetStreamListenerPending.Add(1);
                    try
                    {
                        await channels[stripe].Writer.WriteAsync(
                            new PendingDelivery(message, subject.Verb),
                            cancellationToken).ConfigureAwait(false);
                    }
                    catch
                    {
                        NatsMessagingMetrics.JetStreamListenerPending.Add(-1);
                        throw;
                    }
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    // The delivery was not admitted and remains unacknowledged.
                    break;
                }
                catch (Exception exception)
                {
                    NatsMessagingMetrics.DispatchFailures.Add(1);
                    _logger.LogErrorEvent(
                        ServiceId,
                        exception,
                        "JetStream event listener {EventListenerId} failed before handler admission.",
                        _eventListenerId);
                    await TryNegativeAcknowledgeAsync(message).ConfigureAwait(false);
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (ObjectDisposedException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            NatsMessagingMetrics.DispatchFailures.Add(1);
            _logger.LogErrorEvent(
                ServiceId,
                exception,
                "JetStream event listener {EventListenerId} consumer loop stopped unexpectedly.",
                _eventListenerId);
        }
    }

    async Task DispatchLoopAsync(ChannelReader<PendingDelivery> reader)
    {
        await foreach (var delivery in reader.ReadAllAsync().ConfigureAwait(false))
        {
            try
            {
                var message = delivery.Message;
                var coreMessage = new NatsMsg<byte[]>(
                    message.Subject,
                    message.ReplyTo,
                    default,
                    default,
                    message.Data,
                    message.Connection,
                    default);
                await _eventHandler(delivery.Verb, coreMessage).ConfigureAwait(false);
                await message.AckAsync(cancellationToken: CancellationToken.None).ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                NatsMessagingMetrics.DispatchFailures.Add(1);
                _logger.LogErrorEvent(
                    ServiceId,
                    exception,
                    "JetStream event listener {EventListenerId} handler failed; delivery will be retried.",
                    _eventListenerId);
                await TryNegativeAcknowledgeAsync(delivery.Message).ConfigureAwait(false);
            }
            finally
            {
                NatsMessagingMetrics.JetStreamListenerPending.Add(-1);
            }
        }
    }

    async ValueTask<INatsJSStream> ResolveStreamAsync(
        INatsJSContext jetStream,
        string filterSubject,
        CancellationToken cancellationToken)
    {
        await foreach (var stream in jetStream.ListStreamsAsync(
            filterSubject,
            cancellationToken).ConfigureAwait(false))
        {
            return stream;
        }

        INatsJSStream? configuredStream = null;
        await foreach (var stream in jetStream.ListStreamsAsync(
            cancellationToken: cancellationToken).ConfigureAwait(false))
        {
            if (string.Equals(stream.Info.Config.Name, _options.StreamName, StringComparison.Ordinal))
            {
                configuredStream = stream;
                break;
            }
        }

        if (configuredStream is null)
        {
            return await jetStream.CreateStreamAsync(
                new StreamConfig(_options.StreamName, [filterSubject]),
                cancellationToken).ConfigureAwait(false);
        }

        var config = configuredStream.Info.Config;
        var subjects = config.Subjects ?? [];
        if (!subjects.Contains(filterSubject, StringComparer.Ordinal))
        {
            config.Subjects = [.. subjects, filterSubject];
            configuredStream = await jetStream.UpdateStreamAsync(config, cancellationToken).ConfigureAwait(false);
        }

        return configuredStream;
    }

    string ResolveFilterSubject(ActorMailboxId mailboxId, int mailboxCount)
    {
        var mailboxSubject = $"{mailboxId}.>";
        if (string.IsNullOrWhiteSpace(_options.FilterSubject))
            return mailboxSubject;
        if (mailboxCount != 1)
            throw new InvalidOperationException(
                $"{nameof(_options.FilterSubject)} can only be used with one mailbox.");
        if (!_options.FilterSubject.StartsWith($"{mailboxId}.", StringComparison.Ordinal)
            && !string.Equals(_options.FilterSubject, mailboxId.ToString(), StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"{nameof(_options.FilterSubject)} must be inside mailbox '{mailboxId}'.");
        }
        return _options.FilterSubject;
    }

    async ValueTask TryNegativeAcknowledgeAsync(INatsJSMsg<byte[]> message)
    {
        try
        {
            await message.NakAsync(
                opts: new AckOpts { NakDelay = _options.NegativeAcknowledgeDelay },
                cancellationToken: CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            _logger.LogWarning(
                exception,
                "JetStream event listener {EventListenerId} could not send a negative acknowledgement; the ACK timeout will preserve redelivery.",
                _eventListenerId);
        }
    }

    async ValueTask StopFailedStartAsync()
    {
        _consumerCancellation?.Cancel();
        await AwaitExpectedCancellationAsync(_mailboxes.Select(runtime => runtime.ConsumerTask)).ConfigureAwait(false);
        foreach (var runtime in _mailboxes)
            foreach (var channel in runtime.Channels)
                channel.Writer.TryComplete();
        await Task.WhenAll(_mailboxes.SelectMany(runtime => runtime.DispatcherTasks)).ConfigureAwait(false);
        _mailboxes.Clear();
        _consumerCancellation?.Dispose();
        _consumerCancellation = null;
        _client = null;
        if (_ownsConnectionManager && _connectionManager is not null)
        {
            await _connectionManager.DisposeAsync().ConfigureAwait(false);
            _connectionManager = null;
        }
        _state = EventListenerState.Unknown;
    }

    static async Task AwaitExpectedCancellationAsync(IEnumerable<Task> tasks)
    {
        try
        {
            await Task.WhenAll(tasks).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
        }
    }

    static void ValidateStartArguments(
        string eventListenerId,
        Dictionary<ActorMailboxId, List<string>> eventMap,
        Func<string, NatsMsg<byte[]>, ValueTask> eventHandler)
    {
        ArgumentNullException.ThrowIfNull(eventListenerId);
        ArgumentNullException.ThrowIfNull(eventMap);
        ArgumentNullException.ThrowIfNull(eventHandler);
        if (string.IsNullOrWhiteSpace(eventListenerId))
            throw new ArgumentException("Event listener ID cannot be empty or whitespace.", nameof(eventListenerId));
        if (eventMap.Count == 0)
            throw new ArgumentException("Event map cannot be empty.", nameof(eventMap));
        if (eventMap.Any(entry => string.IsNullOrWhiteSpace(entry.Key.Name)
                                  || entry.Value is null
                                  || entry.Value.Count == 0
                                  || entry.Value.Any(string.IsNullOrWhiteSpace)))
        {
            throw new ArgumentException(
                "Each event-map mailbox requires an identity and at least one non-empty verb.",
                nameof(eventMap));
        }
    }

    NatsJetStreamEventListenerOptions GetValidatedOptions()
    {
        var concreteOptions = _options as NatsJetStreamEventListenerOptions ?? new NatsJetStreamEventListenerOptions
        {
            Url = _options.Url,
            StreamName = _options.StreamName,
            DurableConsumerNamePrefix = _options.DurableConsumerNamePrefix,
            FilterSubject = _options.FilterSubject,
            DeliverPolicy = _options.DeliverPolicy,
            AckWait = _options.AckWait,
            MaxDeliver = _options.MaxDeliver,
            DispatcherCount = _options.DispatcherCount,
            DispatcherCapacity = _options.DispatcherCapacity,
            MaxAckPending = _options.MaxAckPending,
            MaxMessages = _options.MaxMessages,
            ThresholdMessages = _options.ThresholdMessages,
            NegativeAcknowledgeDelay = _options.NegativeAcknowledgeDelay
        };
        concreteOptions.Validate();
        return concreteOptions;
    }

    int GetOutstandingLimit() => _options.MaxAckPending > 0
        ? _options.MaxAckPending
        : checked(_options.DispatcherCount * _options.DispatcherCapacity);

    int GetMaxMessages() => _options.MaxMessages > 0 ? _options.MaxMessages : GetOutstandingLimit();

    int GetThresholdMessages() => _options.ThresholdMessages > 0
        ? _options.ThresholdMessages
        : Math.Min(_options.DispatcherCapacity, GetMaxMessages());

    internal static string CreateDurableConsumerName(
        string prefix,
        string eventListenerId,
        ActorMailboxId mailboxId)
    {
        var identity = $"{eventListenerId}-{mailboxId}";
        var safeIdentity = new string(identity.Select(character =>
            char.IsLetterOrDigit(character) || character is '-' or '_'
                ? character
                : '-').ToArray());
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(identity)))[..12].ToLowerInvariant();
        var maximumIdentityLength = 120 - prefix.Length - hash.Length - 2;
        if (maximumIdentityLength <= 0)
            throw new InvalidOperationException("The durable consumer prefix is too long.");
        if (safeIdentity.Length > maximumIdentityLength)
            safeIdentity = safeIdentity[..maximumIdentityLength];
        return $"{prefix}-{safeIdentity}-{hash}";
    }

    readonly record struct PendingDelivery(INatsJSMsg<byte[]> Message, string Verb);

    sealed record MailboxRuntime(
        Channel<PendingDelivery>[] Channels,
        Task[] DispatcherTasks,
        Task ConsumerTask);
}
