using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.UI.EventConsumer;

namespace TomasAI.IFM.UI.Net.Models;

/// <summary>
/// Owns the lookup-maintenance terminal-event listener used by the Reference editor.
/// </summary>
public sealed class LookupTypeEventModel(ILookupTypeUIEventConsumer eventConsumer)
    : BaseModel<LookupTypeEventModel>
{
    readonly ILookupTypeUIEventConsumer _eventConsumer =
        eventConsumer ?? throw new ArgumentNullException(nameof(eventConsumer));

    public ValueTask StartAsync(Func<IEvent, ValueTask> eventAction)
        => ExecuteValueTaskAsync(() => _eventConsumer.StartAsync(eventAction));

    public ValueTask StopAsync()
        => ExecuteValueTaskAsync(_eventConsumer.StopAsync);
}
