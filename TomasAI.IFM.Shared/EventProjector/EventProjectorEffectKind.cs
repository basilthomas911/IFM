namespace TomasAI.IFM.Shared.EventProjector;

/// <summary>
/// Identifies a durable side effect produced while projecting one source event.
/// </summary>
public enum EventProjectorEffectKind : byte
{
    None = 0,
    ProcessingPublication = 1,
    TargetProjection = 2,
    CompletedPublication = 3,
    FailedPublication = 4
}
