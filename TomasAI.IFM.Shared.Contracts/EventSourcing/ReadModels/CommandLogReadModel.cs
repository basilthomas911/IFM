namespace TomasAI.IFM.Shared.EventSourcing.ViewModels
{
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1819", Justification = "Preserve the existing serialized/configuration array contract and consumer bindings; changing collection types is a separate API migration.")]
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
