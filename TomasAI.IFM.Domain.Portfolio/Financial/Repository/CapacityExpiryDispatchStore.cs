
using TomasAI.IFM.Application.Storage.EventSourceDb;
using TomasAI.IFM.Application.Storage.PortfolioDb;
using TomasAI.IFM.Domain.Portfolio.Shared.Financial;
using static TomasAI.IFM.Domain.Portfolio.Financial.Repository.FinancialRepositorySupport;

namespace TomasAI.IFM.Domain.Portfolio.Financial.Repository;

/// <summary>Durable dispatch journal only. Expiry changes money/usage exclusively through the lifecycle Command actor.</summary>
public sealed class CapacityExpiryDispatchStore(IPostgresEventTransaction transactions)
{
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1068", Justification = "Preserve the established storage context contract, including public named arguments and default values used by callers.")]
    public Task<IReadOnlyList<CapacityExpiryDispatch>> PrepareAsync(Func<CapacityExpiryCandidate, DateTime, ChangeCapacityReservationCommand> create,
        CancellationToken token = default, int? portfolioId = null) => transactions.ExecuteAsync(async (db, ct) =>
    {
        var now = DateTime.UtcNow;
        var candidates = await (db.QueryAsync(PortfolioDbSql.Financial.CapacityExpiryDispatchStore.Select01, [now, portfolioId], r => new CapacityExpiryCandidate(r.GetInt32(0), r.GetInt32(1), r.GetGuid(2), r.GetInt64(3), r.GetInt32(4), r.GetString(5), r.GetInt64(6), r.GetDateTime(7)), ct)).ConfigureAwait(false);
        foreach (var candidate in candidates)
        {
            var command = create(candidate, now);
            Require(command.Body.ReservationId == candidate.ReservationId && command.Body.ChangeKind == CapacityChangeKind.ExpireUnconsumed &&
                string.Equals(command.InputSha256,FinancialCanonicalHash.Request(command),StringComparison.Ordinal), FinancialReasons.InvalidContract, "Invalid expiry dispatch request.");
            await (db.ExecuteAsync(PortfolioDbSql.Financial.CapacityExpiryDispatchStore.Insert01, [command.OperationId, candidate.ReservationId, candidate.PortfolioId, candidate.FundId, Json(command), command.InputSha256, now], ct)).ConfigureAwait(false);
        }
        var pending = await (db.QueryAsync(PortfolioDbSql.Financial.CapacityExpiryDispatchStore.Select02, [portfolioId], r => new CapacityExpiryDispatch(r.GetInt32(0), Decode<ChangeCapacityReservationCommand>(r.GetString(1))), ct)).ConfigureAwait(false);
        return pending;
    }, token);

    /// <summary>Closes a dispatcher attempt under the same fence as posting, only after receipt or expired no-commit proof.</summary>
    public Task<bool> ReconcileAsync(ChangeCapacityReservationCommand request, CancellationToken token = default) => transactions.ExecuteAsync(async (db, ct) =>
    {
        await (LockAuthorityAsync(db, request.PortfolioId, null, ct)).ConfigureAwait(false);
        var receipt = await (ReadOperationAsync<CapacityLifecycleCompletedEvent>(db, request.PortfolioId, request.OperationId, request.InputSha256, ct)).ConfigureAwait(false);
        string? status = null;
        if (receipt is not null)
        {
            Require(receipt.Receipt.ReservationId == request.Body.ReservationId && receipt.Receipt.Status == ReservationStatus.Expired,
                FinancialReasons.RequestMismatch, "Expiry receipt differs from the dispatched operation.");
            status = "Committed";
        }
        else if (DateTime.UtcNow >= request.ExpiresAtUtc) status = "ExpiredWithoutCommit";
        if (status is null) return false;
        await (db.ExecuteAsync(PortfolioDbSql.Financial.CapacityExpiryDispatchStore.Update01, [request.OperationId, request.InputSha256, status, DateTime.UtcNow], ct)).ConfigureAwait(false);
        return true;
    }, token);
}
