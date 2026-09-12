namespace TomasAI.IFM.Shared.EventSourcing.ViewModels
{
    public record CommandLogReadModel(
        Guid CommandId, 
        string StreamId,
        BoundedContextName AggregateName,
        string CommandName,
        DateTime CommandTimestamp,
        string CommandData,
        byte[]? CommandPayload = null,
        short? CommandPayloadFormat = null,
        int? CommandPayloadVersion = null,
        byte[]? CommandPayloadSha256 = null)
    {
    }
}
