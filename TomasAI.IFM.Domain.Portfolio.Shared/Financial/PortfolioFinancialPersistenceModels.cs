namespace TomasAI.IFM.Domain.Portfolio.Shared.Financial;

/// <summary>Describes a durable capacity-expiry command awaiting dispatch.</summary>
/// <param name="FundId">The affected fund identifier.</param>
/// <param name="Request">The capacity-change command.</param>
public sealed record CapacityExpiryDispatch(int FundId, ChangeCapacityReservationCommand Request);

/// <summary>Contains financial-authority preparation data read at one revision.</summary>
/// <param name="Book">The financial book configuration.</param>
/// <param name="Revision">The financial authority revision.</param>
/// <param name="Epoch">The authority epoch.</param>
/// <param name="State">The persisted authority state.</param>
/// <param name="AvailableCash">Available cash by fund identifier.</param>
public sealed record FinancialAuthorityPreparationSnapshot(
    FinancialBookConfiguration Book,
    long Revision,
    long Epoch,
    string State,
    IReadOnlyDictionary<int, decimal> AvailableCash);

/// <summary>
/// Defines trusted host policy for synthetic opening capital.
/// </summary>
/// <param name="IsDevelopmentEnvironment">Whether the trusted host is a development environment.</param>
public sealed record FinancialDevelopmentPolicy(bool IsDevelopmentEnvironment = false)
{
    /// <summary>Applies the trusted Development emulator qualification exemption to a temporary trading view.</summary>
    /// <param name="book">The financial configuration read from PostgreSQL.</param>
    /// <returns>A trading view; persisted qualification and operator evidence are unchanged.</returns>
    public FinancialBookConfiguration TradingBook(FinancialBookConfiguration book)
    {
        if (!IsDevelopmentEnvironment || book.Environment != "Emulator")
            return book with { DevelopmentQualificationsExempt = false };
        var expires = DateTime.SpecifyKind(DateTime.MaxValue, DateTimeKind.Utc);
        return book with
        {
            DevelopmentQualificationsExempt = true,
            MigrationQualified = true,
            Funds = book.Funds.Select(fund => fund with
            {
                CanSpend = true,
                Reference = fund.Reference with { ValidUntilUtc = expires },
                Deployments = fund.Deployments.Select(deployment => deployment with
                {
                    Reference = deployment.Reference with { ValidUntilUtc = expires }
                }).ToArray()
            }).ToArray()
        };
    }
}

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
