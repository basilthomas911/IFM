using Microsoft.Extensions.Logging;
using NATS.Client.Core;
using TomasAI.IFM.Framework.Messaging.NatsJetStream;
using TomasAI.IFM.Framework.Messaging.NatsJetStream.Contracts;
using TomasAI.IFM.Framework.Messaging.NatsJetStream.Serializers;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.UI.EventConsumer;

/// <summary>
/// Represents a consumer for command response UI events received through NATS.
/// </summary>
/// <remarks>This class is responsible for subscribing to and processing command response events specific to a
/// site, identified by a unique site ID. It extends the functionality of <see cref="NatsActorEventListener"/> and
/// implements <see cref="ICommandResponseUIEventConsumer"/>.</remarks>
/// <param name="options"></param>
/// <param name="logger"></param>
public class CommandResponseUIEventConsumer(INatsEventListenerOptions options, ILogger logger)
    : NatsActorEventListener(options, logger), ICommandResponseUIEventConsumer
{
    const string EventConsumer = nameof(CommandResponseUIEventConsumer);
    static readonly NatsMessagePackDataSerializer Serializer = new();
    readonly ILogger _logger = logger;

    public async ValueTask StartAsync(ICollection<IEvent> commandResponseEvents, Action<IEvent> eventAction)
    {
        ArgumentNullException.ThrowIfNull(commandResponseEvents);
        ArgumentNullException.ThrowIfNull(eventAction);
        if (commandResponseEvents.Count == 0)
            throw new ArgumentException("At least one command-response event is required.", nameof(commandResponseEvents));

        var descriptors = commandResponseEvents
            .Select(CreateDescriptor)
            .ToArray();
        var duplicate = descriptors
            .GroupBy(descriptor => (descriptor.Actor, descriptor.Verb))
            .FirstOrDefault(group => group.Select(item => item.EventType).Distinct().Count() > 1);
        if (duplicate is not null)
            throw new ArgumentException(
                $"Command-response route '{duplicate.Key.Actor}.{duplicate.Key.Verb}' maps to multiple event types.",
                nameof(commandResponseEvents));

        var descriptorMap = descriptors
            .GroupBy(descriptor => (descriptor.Actor, descriptor.Verb))
            .ToDictionary(group => group.Key, group => group.First());
        var eventMap = descriptors
            .GroupBy(descriptor => descriptor.Actor)
            .ToDictionary(
                group => new ActorMailboxId(ActorType.Event, group.Key),
                group => group.Select(item => item.Verb).Distinct(StringComparer.OrdinalIgnoreCase).ToList());

        await base.StartAsync(EventConsumer, eventMap, EventHandlerAsync).ConfigureAwait(false);

        ValueTask EventHandlerAsync(string eventVerb, NatsMsg<byte[]> eventMessage)
        {
            try
            {
                var actor = eventMessage.Subject.ToSubject().Name;
                if (!descriptorMap.TryGetValue((actor, eventVerb), out var descriptor)
                    || eventMessage.Data is null)
                    return ValueTask.CompletedTask;

                var commandResponse = descriptor.Deserialize(eventMessage.Data);
                if (commandResponse is not null)
                    eventAction(commandResponse);
            }
            catch (Exception exception)
            {
                _logger.LogErrorEvent(
                    EventConsumer,
                    exception,
                    "Command-response event handling failed for verb {EventVerb}.",
                    eventVerb);
            }
            return ValueTask.CompletedTask;
        }
    }

    public new ValueTask StopAsync() => base.StopAsync();

    static CommandResponseDescriptor CreateDescriptor(IEvent prototype)
    {
        var eventType = prototype.GetType();
        var actor = ReadRouteConstant(eventType, "Actor");
        var verb = ReadRouteConstant(eventType, "Verb");
        var deserializeMethod = typeof(CommandResponseUIEventConsumer)
            .GetMethod(nameof(Deserialize), System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!
            .MakeGenericMethod(eventType);
        var deserialize = (Func<byte[], IEvent?>)deserializeMethod.CreateDelegate(typeof(Func<byte[], IEvent?>));
        return new CommandResponseDescriptor(actor, verb, eventType, deserialize);
    }

    static string ReadRouteConstant(Type eventType, string fieldName)
    {
        var value = eventType.GetField(
                fieldName,
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.FlattenHierarchy)
            ?.GetRawConstantValue() as string;
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException(
                $"Command-response event '{eventType.FullName}' does not declare public const string {fieldName}.");
        return value;
    }

    static IEvent? Deserialize<TEvent>(byte[] data)
        where TEvent : class, IEvent
        => Serializer.Deserialize<TEvent>(data);

    sealed record CommandResponseDescriptor(
        string Actor,
        string Verb,
        Type EventType,
        Func<byte[], IEvent?> Deserialize);

}
