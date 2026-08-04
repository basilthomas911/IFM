namespace TomasAI.IFM.Shared.EventSourcing;

/// <summary>
/// 
/// </summary>
public enum ErrorType
{
    Undefined,
    Command,
    CommandValidation,
    Denormalizer,
    EventService,
    Storage,
    System,
    Concurrency,
}
