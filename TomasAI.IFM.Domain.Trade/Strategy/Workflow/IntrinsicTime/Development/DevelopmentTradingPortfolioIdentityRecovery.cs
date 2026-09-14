using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Storage.PortfolioDb;
using TomasAI.IFM.Domain.Portfolio.Projection;
using TomasAI.IFM.Domain.Portfolio.Shared.Identities;
using TomasAI.IFM.Domain.Portfolio.Shared.ViewModels;

namespace TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Development;

/// <summary>
/// Resolves the Portfolio that owns the configured development execution account and restores only its
/// missing PostgreSQL read projections from the authoritative event streams.
/// </summary>
public class DevelopmentTradingPortfolioIdentityRecovery(
    IPortfolioDbReadContext database,
    IPortfolioProjectionRebuilder projections,
    ILogger<DevelopmentTradingPortfolioIdentityRecovery> logger)
{
    /// <summary>
    /// Returns the active financial-book owner, rebuilding its Portfolio, Fund and policy projections when
    /// the financial authority exists but its Portfolio projection is missing.
    /// </summary>
    public virtual async Task<PortfolioReadModel?> ResolveAsync(
        string environment,
        string executionAccountReference,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(environment);
        ArgumentException.ThrowIfNullOrWhiteSpace(executionAccountReference);

        var book = await database.ReadActiveBookByExecutionAccountAsync(
            environment, executionAccountReference, cancellationToken).ConfigureAwait(false);
        if (book is null) return null;
        if (book.PortfolioId <= 0 || book.BookId <= 0 || book.Environment != environment
            || book.ExecutionAccountReference != executionAccountReference)
            throw new InvalidOperationException("The active development financial book has conflicting ownership metadata.");

        var funds = book.Funds
            .Select(x => new PortfolioFundId(book.PortfolioId, x.FundId))
            .Distinct()
            .ToArray();
        var policies = book.Funds
            .SelectMany(x => x.Deployments.Select(deployment => deployment.Reference.PolicyId)
                .Append(x.Reference.PolicyId))
            .Where(x => x > 0)
            .Distinct()
            .Select(x => new PortfolioFinancialPolicyId(book.PortfolioId, x))
            .ToArray();

        var portfolio = await database.GetPortfolioAsync(book.PortfolioId, cancellationToken).ConfigureAwait(false);
        var complete = portfolio is not null;
        foreach (var fund in funds)
            complete &= await database.GetFundAsync(fund.FundId, cancellationToken).ConfigureAwait(false) is { PortfolioId: var owner }
                && owner == book.PortfolioId;
        foreach (var policy in policies)
            complete &= await database.GetPolicyAsync(policy.PolicyId, cancellationToken: cancellationToken).ConfigureAwait(false)
                is { PortfolioId: var owner } && owner == book.PortfolioId;
        if (complete) return portfolio;

        var rebuilt = await projections.RebuildAsync(
            new([new PortfolioId(book.PortfolioId)], funds, policies), cancellationToken).ConfigureAwait(false);
        portfolio = await database.GetPortfolioAsync(book.PortfolioId, cancellationToken).ConfigureAwait(false);
        if (portfolio is null)
            throw new InvalidOperationException(
                $"Development Portfolio {book.PortfolioId} owns financial book {book.BookId}, but its projection could not be restored.");

        logger.LogWarning(
            "Restored missing projections for development Portfolio {PortfolioId} from {EventCount} authoritative events through source event {SourceEventId}",
            book.PortfolioId, rebuilt.EventCount, rebuilt.LastSourceEventId);
        return portfolio;
    }
}
