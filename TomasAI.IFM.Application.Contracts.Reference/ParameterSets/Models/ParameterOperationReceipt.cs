using MessagePack;
namespace TomasAI.IFM.Domain.Reference.Shared.ParameterSets;

[MessagePackObject]
public sealed record ParameterOperationReceipt(
    [property: Key(0)] Guid OperationId,
    [property: Key(1)] string RequestSha256,
    [property: Key(2)] long Revision,
    [property: Key(3)] ParameterVersionRef Reference);
