using MessagePack;
using System.Security.Cryptography;
using TomasAI.IFM.Domain.Reference.Shared.ParameterSets;
namespace TomasAI.IFM.Domain.Reference.ParameterSets.Model;

internal static class ParameterBinaryFingerprint
{
    internal static string Hash<T>(T value)=>Convert.ToHexStringLower(SHA256.HashData(MessagePackSerializer.Serialize(value,
        MessagePackSerializerOptions.Standard.WithCompression(MessagePackCompression.None))));
}
[MessagePackObject(AllowPrivate=true)]
internal sealed record ParameterMutationIdentity(
    [property:Key(0)] string Command,[property:Key(1)] Guid SetId,[property:Key(2)] long ExpectedRevision,
    [property:Key(3)] int Version,[property:Key(4)] string Component,[property:Key(5)] string Name,
    [property:Key(6)] string Description,[property:Key(7)] int SchemaVersion,
    [property:Key(8)] ParameterSetValue? Parameters,[property:Key(9)] ParameterLegacyReference? LegacySource);
