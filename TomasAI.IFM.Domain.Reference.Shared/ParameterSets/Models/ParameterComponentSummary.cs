using MessagePack;
namespace TomasAI.IFM.Domain.Reference.Shared.ParameterSets;
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1819", Justification = "The published serialization contract uses arrays; preserve its wire shape.")]
[MessagePackObject]
public sealed record ParameterComponentSummary(
    [property: Key(0)] string AreaCode, [property: Key(1)] string AreaName,
    [property: Key(2)] string ComponentCode, [property: Key(3)] string Name,
    [property: Key(4)] int[] SchemaVersions, [property: Key(5)] bool CanEdit);
