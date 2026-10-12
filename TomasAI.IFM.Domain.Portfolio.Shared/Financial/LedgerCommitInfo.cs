namespace TomasAI.IFM.Domain.Portfolio.Shared.Financial;

/// <summary>Describes one committed general-ledger operation.</summary>
/// <param name="Revision">The committed financial revision.</param>
/// <param name="CommittedAtUtc">The UTC commit timestamp.</param>
/// <param name="EventId">The committed event identifier.</param>
/// <param name="Items">The posted ledger transactions.</param>
public sealed record LedgerCommitInfo(
    long Revision,
    DateTime CommittedAtUtc,
    Guid EventId,
    IReadOnlyList<LedgerPostedTransaction> Items);
