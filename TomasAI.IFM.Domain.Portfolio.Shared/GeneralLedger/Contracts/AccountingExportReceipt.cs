using MessagePack;

namespace TomasAI.IFM.Domain.Portfolio.Shared.Financial;
[MessagePackObject]
public sealed record AccountingExportReceipt([property: Key(0)] AccountingExportPayload Payload, [property: Key(1)] string PayloadHash,
    [property: Key(2)] string RequestHash, [property: Key(3)] string DeliveryStatus, [property: Key(4)] int Attempts,
    [property: Key(5)] string? ExternalReceipt);
