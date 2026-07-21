using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.UI.EventConsumer;

namespace TomasAI.IFM.UI.Net.Models;

/// <summary>
/// Represents an event model that manages command response event consumption and tracks the associated site identifier.
/// </summary>
/// <remarks>Use this class to coordinate the lifecycle of command response event consumers and to maintain state
/// related to event processing. The model provides methods to start and stop event consumption, and exposes properties
/// for tracking the current site and whether it is awaiting a command response.</remarks>
public class EventModel : BaseModel<EventModel>, IEventModel
{
    readonly ICommandResponseUIEventConsumer _commandResponseEventConsumer;
    Guid _siteId;

    public EventModel(ICommandResponseUIEventConsumer commandResponseEventConsumer)
    {
        _commandResponseEventConsumer = commandResponseEventConsumer;
    }

    public Guid SiteId => _siteId;

    public bool WaitingForCommandResponse { get; set; }

    public void SetSiteId(Guid siteId) => _siteId = siteId;

    public async Task StartCommandResponseEventConsumerAsync(EventTopic eventTopic, ICollection<IEvent> commandResponseEvents, Action<IEvent> eventAction) 
    {
        var eventSource = $"{eventTopic}";
        foreach (var e in commandResponseEvents)
            e.SetEventSource(eventSource);
        await _commandResponseEventConsumer.StartAsync(commandResponseEvents, eventAction);
        WaitingForCommandResponse = true;
    }

    public async Task StopCommandResponseEventConsumerAsync()
    {
        await _commandResponseEventConsumer.StopAsync();
        WaitingForCommandResponse = false;
    }

}
