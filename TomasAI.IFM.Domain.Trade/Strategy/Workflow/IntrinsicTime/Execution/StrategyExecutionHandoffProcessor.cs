using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Domain.Portfolio.Shared.Financial;
using TomasAI.IFM.Domain.Trade.Model;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Order;
using TomasAI.IFM.Domain.Trade.Shared.Portfolio;
using TomasAI.IFM.Domain.Portfolio.Shared.Commands;
using TomasAI.IFM.Domain.Portfolio.Shared.Contracts;
using TomasAI.IFM.Domain.Portfolio.Shared.Common;
using TomasAI.IFM.Domain.Portfolio.Shared.ServiceApi;
using TomasAI.IFM.Domain.Portfolio.Shared.Identities;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Execution;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Execution.Projection;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Domain.Portfolio.GeneralLedger;

namespace TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Execution;

/// <summary>Prepares one external effect from a projected intent and records its evidence under the original identity.</summary>
public sealed class StrategyExecutionHandoffProcessor(IActorService actors, IDbContextFactory databases,
    IPortfolioFinancialApi financial, StrategyExecutionHandoffReadStore store, TimeProvider clock, IPortfolioQueryApi portfolio,
    IPortfolioTradeAccountingApi accounting)
{
    public async Task StepAsync(StrategyExecutionHandoffDefinition offered, CancellationToken token = default)
    {
        var current = await store.ReadAsync(offered.Id, token).ConfigureAwait(false);
        if (current is null || current.Revision != offered.Revision || current.Phase == StrategyExecutionHandoffPhase.Expired)
            return;
        var next = current;
        try
        {
            next = await PrepareAsync(current, token).ConfigureAwait(false);
        }
        catch (Exception error) when (error is not OperationCanceledException || !token.IsCancellationRequested)
        {
            next = current with { ReconciliationReason = error.Message };
        }
        if (ReferenceEquals(next, current) || next.Phase == current.Phase && next.ReconciliationReason == current.ReconciliationReason &&
            FinancialCanonicalHash.Compute(next.Execution) == FinancialCanonicalHash.Compute(current.Execution))
            return;
        var command = new AdvanceStrategyExecutionHandoffCommand
        {
            CommandId = TradeHandoffIdentity.Create("advance-strategy-execution", current.Id.Format(),
                current.Revision.ToString(System.Globalization.CultureInfo.InvariantCulture), FinancialCanonicalHash.Compute(next)),
            EntityId = current.Id, ExpectedRevision = current.Revision, Evidence = next,
            Subject = new(ActorType.Command, BeginStrategyExecutionHandoffCommand.Actor, AdvanceStrategyExecutionHandoffCommand.Verb, current.Id.Format())
        };
        Require(await actors.RequestAsync<AdvanceStrategyExecutionHandoffCommand, OrderExecutionId>(command, token).ConfigureAwait(false));
    }

    async Task<StrategyExecutionHandoffDefinition> PrepareAsync(StrategyExecutionHandoffDefinition current, CancellationToken token)
    {
        var id = current.Order.Id;
        var at = clock.GetUtcNow().UtcDateTime;
        var next = current with { ReconciliationReason = string.Empty };
        if (current.Phase < StrategyExecutionHandoffPhase.ExecutionLinked && at >= current.Order.ValidUntilUtc)
            return await ExpireAsync(current, token).ConfigureAwait(false);
        switch (current.Phase)
        {
            case StrategyExecutionHandoffPhase.Accepted:
                if (current.ConsumptionRequest is not null)
                    throw new InvalidOperationException("EXECUTION.LEGACY_CONSUMPTION_NOT_SUPPORTED");
                return next with { Phase = StrategyExecutionHandoffPhase.FinancialAdmitted };
            case StrategyExecutionHandoffPhase.FinancialAdmitted:
                RequireNotExpired(current, at);
                var registration = new RegisterStrategyFundOrderCommand
                {
                    CommandId = Operation(current, "register-fund-order"), EntityId = new(id.PortfolioId, id.FundId),
                    Order = current.Order.ToPortfolioInstruction(), WorkflowId = current.Authorization.WorkflowId.Value,
                    SourceCommandId = current.Authorization.CommandId, SourceEventVersion = current.Authorization.EventId,
                    CorrelationId = current.Authorization.CorrelationId, RequestedOnUtc = current.Authorization.UpdatedAtUtc,
                    Access = PortfolioAccessContext.Workflow("StrategyExecutionHandoff"),
                    UnderlyingContractId = current.Authorization.UnderlyingContractId,
                    Subject = new(ActorType.Command, RegisterStrategyFundOrderCommand.Actor, RegisterStrategyFundOrderCommand.Verb, new PortfolioFundId(id.PortfolioId, id.FundId).Format())
                };
                Require(await actors.RequestAsync<RegisterStrategyFundOrderCommand, PortfolioFundId>(registration, token).ConfigureAwait(false));
                await SendAsync(new CreateTradeOrderCommand { CommandId = Operation(current, "create"), EntityId = id,
                    Subject = Subject(CreateTradeOrderCommand.Verb, id), Order = current.Order }, token).ConfigureAwait(false);
                return next with { Phase = StrategyExecutionHandoffPhase.OrderCreated };
            case StrategyExecutionHandoffPhase.OrderCreated:
                await SendAsync(new ApproveTradeOrderCommand { CommandId = Operation(current, "approve"), EntityId = id,
                    Subject = Subject(ApproveTradeOrderCommand.Verb, id) }, token).ConfigureAwait(false);
                return next with { Phase = StrategyExecutionHandoffPhase.OrderApproved };
            case StrategyExecutionHandoffPhase.OrderApproved:
                await SendAsync(new ReadyTradeOrderCommand { CommandId = Operation(current, "ready"), EntityId = id,
                    Subject = Subject(ReadyTradeOrderCommand.Verb, id) }, token).ConfigureAwait(false);
                return next with { Phase = StrategyExecutionHandoffPhase.OrderReady };
            case StrategyExecutionHandoffPhase.OrderReady:
                RequireNotExpired(current, at);
                await SendAsync(new BindTradeOrderExecutionCommand { CommandId = Operation(current, "bind"), EntityId = id,
                    Subject = Subject(BindTradeOrderExecutionCommand.Verb, id), ExecutionAttemptId = current.Id.ExecutionAttemptId,
                    ExecutionChannel = ExecutionChannel.Broker, EffectiveAtUtc = current.ExecutionRequestedAtUtc ?? current.Authorization.UpdatedAtUtc }, token).ConfigureAwait(false);
                return next with { Phase = StrategyExecutionHandoffPhase.ExecutionLinked };
            case StrategyExecutionHandoffPhase.ExecutionLinked:
            case StrategyExecutionHandoffPhase.Opened:
            case StrategyExecutionHandoffPhase.Rejected:
            case StrategyExecutionHandoffPhase.Cancelled:
                var execution = await databases.TradeDb.GetOrderExecutionAsync(id, current.Id.ExecutionAttemptId, token).ConfigureAwait(false);
                if (execution is null) return current;
                if (current.Phase == StrategyExecutionHandoffPhase.Opened &&
                    FinancialCanonicalHash.Compute(execution) == FinancialCanonicalHash.Compute(current.Execution)) return current;
                next = next with { Execution = execution };
                if (execution.Status is not (OrderExecutionStatus.Filled or OrderExecutionStatus.Cancelled or OrderExecutionStatus.Rejected)) return next;
                var receiptScope = new FinancialReadScope { PortfolioId = id.PortfolioId, FundId = id.FundId,
                    Access = new("StrategyExecutionHandoff", ["LedgerRead"], [id.PortfolioId]) };
                var receiptOperation = TradeHandoffIdentity.Create("strategy-accounting", execution.Id.Format() + "|" + FinancialCanonicalHash.Compute(execution));
                var accounting = await financial.GetPostingReceiptAsync(receiptScope, new(receiptOperation), token).ConfigureAwait(false);
                var batch = accounting.Value?.Value?.Batch;
                if (!accounting.Success || batch?.ProjectionFacts?.ExecutionCapacity?.Outcome.ExecutionHash != FinancialCanonicalHash.Compute(execution))
                    return next with { ReconciliationReason = "EXECUTION.AWAITING_FINANCIAL_RECEIPT" };
                var fund = await portfolio.GetOrderAsync(id.OrderId, token).ConfigureAwait(false);
                if (!fund.Success || fund.Value is not { } fundOrder || fundOrder.PortfolioId != id.PortfolioId || fundOrder.FundId != id.FundId)
                    return next with { ReconciliationReason = "EXECUTION.AWAITING_FUND_LIFECYCLE" };
                next = next with { FinancialReceipt = batch, FundEvidence = fundOrder };
                if (execution.Fills.Length == 0)
                    return fundOrder.Status is "Rejected" or "Cancelled" ? next with
                    {
                        Phase = execution.Status == OrderExecutionStatus.Rejected ? StrategyExecutionHandoffPhase.Rejected : StrategyExecutionHandoffPhase.Cancelled
                    } : next with { ReconciliationReason = "EXECUTION.AWAITING_FUND_RELEASE" };
                var order = await databases.TradeDb.GetTradeOrderAsync(id, token).ConfigureAwait(false);
                if (order?.Status != TradeOrderStatus.Completed) return next;
                foreach (var component in current.Order.Components)
                    if (await databases.TradeDb.GetEstablishedTradeAsync(new(id.PortfolioId, id.FundId, id.OrderId, component.ReservedTradeId), token).ConfigureAwait(false) is null)
                        return next;
                return fundOrder.Status is "Opened" or "Closing" or "Closed" ? next with { Phase = StrategyExecutionHandoffPhase.Opened }
                    : next with { ReconciliationReason = "EXECUTION.FILLED_AWAITING_FUND_OPENING" };
            default:
                return current;
        }
    }

    async Task<StrategyExecutionHandoffDefinition> ExpireAsync(StrategyExecutionHandoffDefinition current, CancellationToken token)
    {
        var id = current.Order.Id;
        var fundId = new PortfolioFundId(id.PortfolioId, id.FundId);
        var registration = new RegisterStrategyFundOrderCommand
        {
            CommandId = Operation(current, "register-fund-order"), EntityId = fundId,
            Order = current.Order.ToPortfolioInstruction(), WorkflowId = current.Authorization.WorkflowId.Value,
            SourceCommandId = current.Authorization.CommandId, SourceEventVersion = current.Authorization.EventId,
            CorrelationId = current.Authorization.CorrelationId, RequestedOnUtc = current.Authorization.UpdatedAtUtc,
            UnderlyingContractId = current.Authorization.UnderlyingContractId,
            Access = PortfolioAccessContext.Workflow("StrategyExecutionHandoff"),
            Subject = new(ActorType.Command, RegisterStrategyFundOrderCommand.Actor, RegisterStrategyFundOrderCommand.Verb, fundId.Format())
        };
        Require(await actors.RequestAsync<RegisterStrategyFundOrderCommand, PortfolioFundId>(registration, token).ConfigureAwait(false));
        await SendAsync(new CreateTradeOrderCommand { CommandId = Operation(current, "create"), EntityId = id,
            Subject = Subject(CreateTradeOrderCommand.Verb, id), Order = current.Order }, token).ConfigureAwait(false);
        var order = await databases.TradeDb.GetTradeOrderAsync(id, token).ConfigureAwait(false)
            ?? throw new InvalidOperationException("EXECUTION.EXPIRY.AWAITING_ORDER");
        if (order.BoundExecutionAttemptId is { } bound)
        {
            if (bound != current.Id.ExecutionAttemptId) throw new InvalidOperationException("EXECUTION.EXPIRY.OTHER_EXECUTION");
            return current with { Phase = StrategyExecutionHandoffPhase.ExecutionLinked, ReconciliationReason = string.Empty };
        }
        var expiryCommand = Operation(current, "expire");
        await SendAsync(new ExpireTradeOrderCommand { CommandId = expiryCommand, EntityId = id,
            Subject = Subject(ExpireTradeOrderCommand.Verb, id), EffectiveAtUtc = current.Order.ValidUntilUtc }, token).ConfigureAwait(false);
        order = await databases.TradeDb.GetTradeOrderAsync(id, token).ConfigureAwait(false);
        if (order is not { Status: TradeOrderStatus.Expired, BoundExecutionAttemptId: null })
            throw new InvalidOperationException("EXECUTION.EXPIRY.UNCONFIRMED");
        Require(await accounting.PostExpiredOpeningAsync(order, current.Id.ExecutionAttemptId, expiryCommand,
            clock.GetUtcNow().UtcDateTime, token).ConfigureAwait(false));
        var scope = new FinancialReadScope { PortfolioId = id.PortfolioId, FundId = id.FundId,
            Access = new("StrategyExpiryAccounting", ["LedgerRead"], [id.PortfolioId]) };
        var operation = TradeHandoffIdentity.Create("strategy-expiry-accounting", current.Id.Format());
        var receipt = await financial.GetPostingReceiptAsync(scope, new(operation), token).ConfigureAwait(false);
        if (!receipt.Success || receipt.Value?.Value?.Batch is not { } batch ||
            batch.ProjectionFacts?.ExecutionCapacity?.Outcome is not { UnsubmittedExpiry: true, FilledUnits: 0 } facts ||
            facts.ExecutionId != current.Id.ExecutionAttemptId || facts.ExecutionHash != FinancialCanonicalHash.Compute(order))
            throw new InvalidOperationException("EXECUTION.EXPIRY.AWAITING_FINANCIAL_RELEASE");
        var fund = await portfolio.GetOrderAsync(id.OrderId, token).ConfigureAwait(false);
        if (!fund.Success || fund.Value is not { } fundOrder) throw new InvalidOperationException("EXECUTION.EXPIRY.AWAITING_FUND");
        if (fundOrder.Status != "Cancelled")
        {
            var cancel = new ReleaseExpiredStrategyFundOrderCommand
            {
                // A refreshed order version is a new prepared CAS request; the financial
                // release operation remains stable across those attempts.
                CommandId = Operation(current, "expire-fund-order/" + fundOrder.AggregateVersion.ToString(System.Globalization.CultureInfo.InvariantCulture)), EntityId = fundId,
                OrderId = id.OrderId, ExpectedVersion = fundOrder.AggregateVersion,
                SourceCommandId = expiryCommand, FinancialOperationId = operation, CorrelationId = current.Authorization.CorrelationId,
                RequestedOnUtc = current.Order.ValidUntilUtc, Access = PortfolioAccessContext.Workflow("StrategyExecutionHandoff"),
                Subject = new(ActorType.Command, ReleaseExpiredStrategyFundOrderCommand.Actor, ReleaseExpiredStrategyFundOrderCommand.Verb, fundId.Format())
            };
            Require(await actors.RequestAsync<ReleaseExpiredStrategyFundOrderCommand, PortfolioFundId>(cancel, token).ConfigureAwait(false));
            fund = await portfolio.GetOrderAsync(id.OrderId, token).ConfigureAwait(false);
        }
        return fund.Value is { Status: "Cancelled" } released
            ? current with { Phase = StrategyExecutionHandoffPhase.Expired, FinancialReceipt = batch, FundEvidence = released, ReconciliationReason = string.Empty }
            : current with { ReconciliationReason = "EXECUTION.EXPIRY.AWAITING_FUND_RELEASE" };
    }

    static Guid Operation(StrategyExecutionHandoffDefinition current, string purpose) => TradeHandoffIdentity.Create("strategy-opening", current.Id.Format(), purpose);
    static ActorSubject Subject(string verb, TradeOrderId id) => new(ActorType.Command, TradeOrderActorNames.Command, verb, id.Format());
    async Task SendAsync<TCommand>(TCommand command, CancellationToken token) where TCommand : class, ICommand<TradeOrderId>
        => Require(await actors.RequestAsync<TCommand, TradeOrderId>(command, token).ConfigureAwait(false));
    static void Require<T>(ServiceResult<T> result)
    { if (!result.Success) throw new InvalidOperationException(result.ErrorMessage ?? "Execution handoff command was rejected."); }
    static void RequireNotExpired(StrategyExecutionHandoffDefinition current, DateTime now)
    { if (now >= current.Order.ValidUntilUtc) throw new InvalidOperationException("EXECUTION.EXPIRED_RECONCILIATION_REQUIRED"); }
}
