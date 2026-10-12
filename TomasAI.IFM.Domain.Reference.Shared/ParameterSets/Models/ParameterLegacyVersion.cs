using MessagePack;
namespace TomasAI.IFM.Domain.Reference.Shared.ParameterSets;
[MessagePackObject]
public sealed record ParameterLegacyVersion([property: Key(0)] ParameterLegacyReference Reference, [property: Key(1)] int SchemaVersion, [property: Key(2)] string PayloadJson, [property: Key(3)] string Description, [property: Key(4)] int Status);
