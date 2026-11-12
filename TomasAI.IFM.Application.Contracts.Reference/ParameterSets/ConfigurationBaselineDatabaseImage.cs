namespace TomasAI.IFM.Domain.Reference.Shared.ParameterSets;

/// <summary>A consistent configuration table image at an enlisted event-log cut.</summary>
public sealed record ConfigurationBaselineDatabaseImage(
    long SourceCut, IReadOnlyList<ConfigurationBaselineTable> Tables, long SequenceLast, bool SequenceCalled);
