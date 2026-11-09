namespace TomasAI.IFM.Shared.EventSourcing.ViewModels;

public class EventStreamReadModel
{
    /// <summary>Persisted creation provenance, absent for legacy rows.</summary>
    public TomasAI.IFM.Shared.EventModelActor.MessageOrigin? Origin { get; set; }

    public long EventVersion { get; set; }
    public long StreamVersion { get; set; }
    public string EventTypeName { get; set; } = string.Empty;
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1819", Justification = "Preserve the existing serialized/configuration array contract and consumer bindings; changing collection types is a separate API migration.")]
    public byte[] EventData { get; set; } = [];
}
