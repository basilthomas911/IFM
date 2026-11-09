using TomasAI.IFM.Domain.Portfolio.Shared.Common;
using TomasAI.IFM.Domain.Trade.Shared;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Domain.Portfolio.Shared.Identities;
using TomasAI.IFM.Domain.Portfolio.Shared.Financial;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Identity;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.OrderComposition;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.RiskManagement;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.RiskManager.Query.Actor;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Query.Model;

namespace TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.RiskManager.Query.Model;
/// <summary>Provides shared authorization and exact-read calculations for risk queries.</summary>
internal static class RiskQueryModel
{
    internal static async Task AuthorizeAsync(IRiskQueryContext s, CompositionQueryAccess access, int portfolio, int fund, CancellationToken t)
    {
        if (access is null || string.IsNullOrWhiteSpace(access.Principal) || access.Roles.Length == 0)
            throw new UnauthorizedAccessException("Portfolio read authority required.");
        using var scope = PortfolioAccessScope.Push(new() { Principal = access.Principal, Roles = access.Roles });
        var value = await (s.PortfolioQueries.GetFundAsync(portfolio, fund, cancellationToken: t)).ConfigureAwait(false);
        if (!value.Success || value.Value?.PortfolioId != portfolio || value.Value.FundId != fund)
            throw new UnauthorizedAccessException("Portfolio/Fund access denied.");
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1031", Justification = "A read-only observation reports unavailable broker or financial services without changing persisted trading state; cancellation is rethrown.")]
    internal static async Task<RiskObservation> ExactAsync(IRiskQueryContext s, CompositionQueryAccess access, StrategyWorkflowId workflow, Guid invocation, CancellationToken t)
    {
        if (workflow.Value == Guid.Empty || invocation == Guid.Empty)
            throw new ArgumentException("Exact Risk identities required.",nameof(workflow));
        var store = s.DbFactory.TradeDb.DbReader;
        var snapshot = await store.GetRiskInvocationAsync(workflow.Value, invocation, t).ConfigureAwait(false);
        if (snapshot is null || RiskHistoryIdentity.Row(snapshot) is not { } row || row.WorkflowId != workflow.Value || row.InvocationId != invocation)
            throw new KeyNotFoundException("Exact Risk invocation awaiting history projection.");
        await AuthorizeAsync(s, access, row.PortfolioId, row.FundId, t).ConfigureAwait(false);
        var calculation = snapshot.State.RiskManagement.Result?.RiskResult;
        var accepted = calculation is not null;
        if (calculation is null)
        {
            var completion = await store.GetRiskCompletionAsync(workflow.Value, invocation, t).ConfigureAwait(false);
            if (completion is not null)
            {
                if (completion.EntityId != snapshot.EntityId
                    || completion.InputWorkflowRevision != snapshot.State.RiskManagement.InputWorkflowRevision
                    || completion.Result.PortfolioId != row.PortfolioId || completion.Result.FundId != row.FundId
                    || completion.Result.OrderId != row.OrderId)
                    throw new InvalidDataException("Risk calculation does not match its projected workflow invocation.");
                calculation = completion.Result;
            }
        }

        var current = await ProjectedWorkflowView.ReadCurrentAsync(store, snapshot.EntityId.Format(), t).ConfigureAwait(false);
        var isCurrent = current is not null && current.WorkflowId == workflow && current.RiskExecution?.CommandId == invocation;
        var behind = isCurrent && current!.WorkflowRevision > snapshot.WorkflowRevision;
        if (isCurrent && current!.WorkflowRevision != snapshot.WorkflowRevision)
            return new(snapshot, calculation, "Unavailable", "Unavailable", s.TimeProvider.GetUtcNow().UtcDateTime, null, behind, accepted);
        TomasAI.IFM.Domain.Portfolio.Shared.ViewModels.FundOrderProjectionReadModel order;
        try
        {
            order = await s.Funds.ReadOrderAsync(new PortfolioFundId(row.PortfolioId, row.FundId), checked((int)row.OrderId), t).ConfigureAwait(false);
        }
        catch (OperationCanceledException)when (t.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception e)
        {
            if (s.Logger.IsEnabled(global::Microsoft.Extensions.Logging.LogLevel.Warning))
                global::TomasAI.IFM.Domain.Trade.TradeBoundaryDiagnosticLog.Write53(s.Logger, e, nameof(RiskQueryModel), nameof(ExactAsync), invocation);
            return new(snapshot, calculation, "Unavailable", "Unavailable", s.TimeProvider.GetUtcNow().UtcDateTime, null, behind, accepted);
        }

        try
        {
            var sync = (snapshot.TerminalRisk switch
            {
{ } terminal=> ((order.TerminalRisk == terminal) switch
                {
                    true => "Synchronized",
                    _ => order.RiskAuthorization is null ? "Pending" : "Reconciliation required"
                }),
                _ => order.Status
            });
            if (string.Equals(sync, "Pending", StringComparison.Ordinal) && snapshot.State.FinancialHandoff is { } pending)
            {
                var scope = new FinancialReadScope
                {
                    PortfolioId = row.PortfolioId,
                    FundId = row.FundId,
                    Access = new(access.Principal, ["LedgerRead"], [row.PortfolioId])
                };
                var receipt = await (s.Financial.ReadAsync(scope, new GetPostingReceiptRequest(pending.ReservationRequest.OperationId), t)).ConfigureAwait(false);
                if (receipt.Status == FinancialReadStatus.Unavailable)
                    sync = "Pending: financial state unavailable";
                else if (receipt.Value?.Reservation is { } grant)
                {
                    var hold = await (s.Financial.ReadAsync(scope, new GetCapacityReservationRequest(grant.Receipt.ReservationId), t)).ConfigureAwait(false);
                    sync = ((hold.Status == FinancialReadStatus.Unavailable) switch
                    {
                        true => "Pending: financial state unavailable",
                        _ => hold.Value?.Current.Status is ReservationStatus.Released or ReservationStatus.Expired ? "Pending" : "Pending: capacity reconciliation required"
                    });
                }
            }

            var authority = "Not authorized";
            var now = s.TimeProvider.GetUtcNow().UtcDateTime;
            if (snapshot.State.FinancialHandoff is { Phase: RiskFinancialHandoffPhase.Authorized, Authorization: { } authorization })
            {
                authority = "Superseded";
                if (isCurrent)
                {
                    var scope = new FinancialReadScope
                    {
                        PortfolioId = row.PortfolioId,
                        FundId = row.FundId,
                        Access = new(access.Principal, ["LedgerRead"], [row.PortfolioId])
                    };
                    var reservation = await (s.Financial.ReadAsync(scope, new GetCapacityReservationRequest(authorization.ReservationId), t)).ConfigureAwait(false);
                    var admission = await (s.Financial.ReadAsync(scope, new GetFinancialAdmissionSnapshotRequest(snapshot.State.RiskExecution!.Authority.DeploymentKey, snapshot.State.RiskExecution.SizingAuthority.UnderlyingId), t)).ConfigureAwait(false);
                    now = s.TimeProvider.GetUtcNow().UtcDateTime;
                    authority = ((reservation.Status == FinancialReadStatus.Found && admission.Status == FinancialReadStatus.Found && reservation.FinancialRevision != admission.FinancialRevision) switch
                    {
                        true => "Changed during read; refresh",
                        _ => ((admission.Status == FinancialReadStatus.Unavailable) switch
                        {
                            true => "Unavailable",
                            _ => ((admission.Value is null || !admission.Value.CanPrepareAdmission || admission.Value.Authority != snapshot.State.RiskExecution.Authority) switch
                            {
                                true => "Authority revoked or changed",
                                _ => ((reservation.Status == FinancialReadStatus.Unavailable) switch
                                {
                                    true => "Unavailable",
                                    _ => (reservation.Value switch
                                    {
{ } value=> ((now >= value.Current.ValidUntilUtc) switch
                                        {
                                            true => "Expired",
                                            _ => ((value.Current.Status != ReservationStatus.Reserved) switch
                                            {
                                                true => value.Current.Status.ToString(),
                                                _ => order.RiskAuthorization != authorization ? "Fund authorization differs" : "Authorized intent"
                                            })}),_=> "Reservation not found"})})})})});
                }
            }

            return new(snapshot, calculation, authority, sync, now, order, behind, accepted);
        }
        catch (OperationCanceledException)when (t.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception e)
        {
            if (s.Logger.IsEnabled(global::Microsoft.Extensions.Logging.LogLevel.Warning))
                global::TomasAI.IFM.Domain.Trade.TradeBoundaryDiagnosticLog.Write54(s.Logger, e, nameof(RiskQueryModel), nameof(ExactAsync), invocation);
            return new(snapshot, calculation, "Unavailable", "Pending: financial state unavailable", s.TimeProvider.GetUtcNow().UtcDateTime, order, behind, accepted);
        }
    }
}
