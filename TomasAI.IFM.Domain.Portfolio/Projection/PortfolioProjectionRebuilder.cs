using System.Security.Cryptography;
using System.Text;
using TomasAI.IFM.Application.Storage.PortfolioDb;
using TomasAI.IFM.Domain.Portfolio.Command.Model;
using TomasAI.IFM.Domain.Portfolio.Persistence;
using TomasAI.IFM.Domain.Portfolio.Shared.Identities;

namespace TomasAI.IFM.Domain.Portfolio.Projection;

public sealed record PortfolioProjectionRebuildRequest(
    IReadOnlyList<PortfolioId> Portfolios,
    IReadOnlyList<PortfolioFundId> Funds,
    IReadOnlyList<PortfolioFinancialPolicyId>? Policies = null);

public sealed record PortfolioProjectionRebuildResult(
    int EventCount,
    long LastSourceEventId,
    string SourceCatalogSha256);

public interface IPortfolioProjectionRebuilder
{
    Task<PortfolioProjectionRebuildResult> RebuildAsync(PortfolioProjectionRebuildRequest request, CancellationToken cancellationToken = default);
}

/// <summary>
/// Controlled replay of authoritative PostgreSQL Portfolio streams into an empty or existing idempotent Scylla projection.
/// The caller owns destructive table reset; this service only replays explicitly named aggregate streams.
/// </summary>
public sealed class PortfolioProjectionRebuilder(IPortfolioEventStore events, IPortfolioDbWriteContext projections)
    : IPortfolioProjectionRebuilder
{
    readonly IPortfolioEventStore _events = events ?? throw new ArgumentNullException(nameof(events));
    readonly IPortfolioDbWriteContext _projections = projections ?? throw new ArgumentNullException(nameof(projections));

    public async Task<PortfolioProjectionRebuildResult> RebuildAsync(
        PortfolioProjectionRebuildRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.Portfolios.Count == 0) throw new ArgumentException("At least one Portfolio stream is required.", nameof(request));
        if (request.Portfolios.Select(x => x.Id).Distinct().Count() != request.Portfolios.Count ||
            request.Funds.Select(x => x.Format()).Distinct(StringComparer.Ordinal).Count() != request.Funds.Count ||
            (request.Policies ?? []).Select(x => x.Format()).Distinct(StringComparer.Ordinal).Count() != (request.Policies?.Count ?? 0))
            throw new ArgumentException("Rebuild stream identities must be unique.", nameof(request));

        var history = new List<object>();
        foreach (var id in request.Portfolios)
        {
            cancellationToken.ThrowIfCancellationRequested();
            history.AddRange(await _events.LoadPortfolioHistoryAsync(id, cancellationToken).ConfigureAwait(false));
        }
        foreach (var id in request.Funds)
        {
            cancellationToken.ThrowIfCancellationRequested();
            history.AddRange(await _events.LoadFundHistoryAsync(id, cancellationToken).ConfigureAwait(false));
        }
        foreach (var id in request.Policies ?? [])
        {
            cancellationToken.ThrowIfCancellationRequested();
            history.AddRange(await _events.LoadPolicyHistoryAsync(id, cancellationToken).ConfigureAwait(false));
        }
        var ordered = history.OrderBy(SourceEventId).ThenBy(x => x.GetType().FullName, StringComparer.Ordinal).ToArray();
        if (ordered.Count(x => SourceEventId(x) <= 0) != 0)
            throw new InvalidOperationException("Only committed authoritative events can be rebuilt.");
        var handler = new PortfolioProjectionHandler(_events, _projections);
        foreach (var item in ordered)
        {
            cancellationToken.ThrowIfCancellationRequested();
            switch (item)
            {
                case PortfolioFinancialPolicyDomainEvent policy: await handler.ApplyAsync(policy, cancellationToken).ConfigureAwait(false); break;
                case PortfolioDomainEvent portfolio: await handler.ApplyAsync(portfolio, cancellationToken).ConfigureAwait(false); break;
                case PortfolioFundDomainEvent fund: await handler.ApplyAsync(fund, cancellationToken).ConfigureAwait(false); break;
                default: throw new InvalidOperationException($"Unsupported rebuild event {item.GetType().FullName}.");
            }
        }
        var manifest = string.Join('\n', ordered.Select(x => $"{SourceEventId(x)}:{x.GetType().FullName}"));
        return new(ordered.Length, ordered.Length == 0 ? 0 : SourceEventId(ordered[^1]), Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(manifest))).ToLowerInvariant());
    }

    static long SourceEventId(object value) => value switch
    {
        PortfolioFinancialPolicyDomainEvent x => x.EventId,
        PortfolioDomainEvent x => x.EventId,
        PortfolioFundDomainEvent x => x.EventId,
        _ => 0,
    };
}
