namespace TomasAI.IFM.Domain.Portfolio.Shared.Financial;

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
        ArgumentNullException.ThrowIfNull(book);
        if (!IsDevelopmentEnvironment || !string.Equals(book.Environment,"Emulator",StringComparison.Ordinal))
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
