namespace TomasAI.IFM.Shared.EventSourcing.ViewModels;

public record struct EventNameIdReadModel(
    int EventNameId,
    string EventName,
    string EventTypeName)
{
    public bool IsValid => EventNameId > 0 && !string.IsNullOrEmpty(EventName) && !string.IsNullOrEmpty(EventTypeName);
}
