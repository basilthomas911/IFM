using MessagePack;
namespace TomasAI.IFM.Domain.Reference.Shared.ParameterSets;
[MessagePackObject]
public sealed record ParameterLegacyReference([property:Key(0)] string Kind,[property:Key(1)] Guid SetId,[property:Key(2)] int Version,[property:Key(3)] string PayloadSha256,[property:Key(4)] string Codec);
[MessagePackObject]
public sealed record ParameterLegacyVersion([property:Key(0)] ParameterLegacyReference Reference,[property:Key(1)] int SchemaVersion,[property:Key(2)] string PayloadJson,[property:Key(3)] string Description,[property:Key(4)] int Status);
