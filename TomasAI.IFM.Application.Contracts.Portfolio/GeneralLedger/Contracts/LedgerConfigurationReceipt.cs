using MessagePack;

namespace TomasAI.IFM.Domain.Portfolio.Shared.Financial;

[MessagePackObject]
public sealed record LedgerConfigurationReceipt(
    [property: Key(0)] Guid OperationId, [property: Key(1)] int BookId,
    [property: Key(2)] LedgerConfigurationAction Action, [property: Key(3)] long FinancialRevision,
    [property: Key(4)] DateTime CommittedAtUtc, [property: Key(5)] Guid? ReconciliationId,
    [property: Key(6)] string OperatingState);
