using MessagePack;
namespace TomasAI.IFM.Domain.Reference.Shared.ParameterSets;

/// <summary>Exact accepted column layout and canonical JSON row image for one allowlisted table.</summary>
[MessagePackObject]
public sealed record ConfigurationBaselineTable(
    [property: Key(0)] string Name,
    [property: Key(1)] string ColumnsJson,
    [property: Key(2)] string RowsJson,
    [property: Key(3)] string Sha256);
