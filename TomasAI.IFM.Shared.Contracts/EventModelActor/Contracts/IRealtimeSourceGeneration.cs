namespace TomasAI.IFM.Shared.EventModelActor.Contracts;

/// <summary>Transport-only source identity for supervised realtime publications.</summary>
public interface IRealtimeSourceGeneration
{
    string SourceDataset { get; }
    Guid SourceGenerationId { get; }
}
