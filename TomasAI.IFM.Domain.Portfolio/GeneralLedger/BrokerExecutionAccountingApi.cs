using System.Security.Cryptography;
using System.Text;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.Storage.EventSourceDb;
using TomasAI.IFM.Domain.Portfolio.Financial.Repository;
using TomasAI.IFM.Domain.Portfolio.GeneralLedger.Model;
using TomasAI.IFM.Domain.Portfolio.Shared.Financial;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Portfolio.GeneralLedger;

/// <summary>Builds the authoritative Fund batch and sends it through the GeneralLedger command actor.</summary>
public sealed class BrokerExecutionAccountingApi(IFinancialQueryStore financialQueries,
    IDbContextFactory databases, IActorService actors, IPostgresEventTransaction transactions) : IPortfolioTradeAccountingApi
{
    /// <inheritdoc />
    public async ValueTask<ServiceResult<Guid>> PostConfirmedExecutionAsync(
        OrderExecutionDefinition execution,
        Guid sourceEventId,
        DateTime confirmedAtUtc,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(execution);
        if (execution.Order.StrategyWorkflowId is not null)
            return await PostStrategyOpeningAsync(execution, sourceEventId, confirmedAtUtc, cancellationToken).ConfigureAwait(false);
        ValidateExecution(execution, sourceEventId, confirmedAtUtc);

        var operationId = DeterministicId("portfolio-accounting", execution.Id.Format());
        // Delivery metadata may differ on retry; execution evidence must not.
        var evidenceHash = FinancialCanonicalHash.Compute(execution);
        var intents = new BrokerAccountingIntentStore(transactions);
        var original = await intents.ReadAsync(execution.TradeOrderId.PortfolioId, operationId,
            evidenceHash, cancellationToken).ConfigureAwait(false);
        if (original is not null)
            return await actors.SendAsync<PostFundTransactionsCommand, LedgerPortfolioId>(
                original, original.EntityId, cancellationToken).ConfigureAwait(false);
        var access = new FinancialAccess("BrokerExecutionAccounting", ["PortfolioAdministrator"]);
        var scope = new FinancialReadScope
        {
            PortfolioId = execution.TradeOrderId.PortfolioId,
            FundId = execution.TradeOrderId.FundId,
            Access = access
        };
        var accountingDate = DateOnly.FromDateTime(execution.Fills.Min(static fill => fill.FilledAtUtc));
        var selected = await financialQueries.ReadAsync(scope,
            new GetFinancialPostingConfigurationRequest(accountingDate), cancellationToken).ConfigureAwait(false);
        if (selected.Status != FinancialReadStatus.Found || selected.Value is null || !selected.Value.PeriodOpen)
            throw new InvalidOperationException("PORTFOLIO_ACCOUNTING.CONFIGURATION_UNAVAILABLE");

        if (execution.PositionType != execution.Order.PositionType || execution.TradeOrderId != execution.Order.Id ||
            !string.Equals(FinancialCanonicalHash.Compute(execution.Components),FinancialCanonicalHash.Compute(execution.Order.Components),StringComparison.Ordinal))
            throw new ArgumentException("Execution identity and approved order disagree.",nameof(execution));
        PostFundTransactionsCommand BuildCommand(IReadOnlyDictionary<Guid, decimal>? openingBasis)
        {
            var movementReference = $"BrokerExecution:{execution.Id.Format()}";
            var batch = BrokerExecutionAccountingModel.Create(new(
                selected.Value, execution.Order, execution.Fills, movementReference,
                confirmedAtUtc, openingBasis));
            var command = new PostFundTransactionsCommand
            {
                CommandId = DeterministicId("post-portfolio-accounting", execution.Id.Format()),
                Subject = new(ActorType.Command, PostFundTransactionsCommand.Actor,
                    PostFundTransactionsCommand.Verb, new LedgerPortfolioId(execution.TradeOrderId.PortfolioId).Format()),
                EntityId = new(execution.TradeOrderId.PortfolioId),
                OperationId = operationId,
                PortfolioId = execution.TradeOrderId.PortfolioId,
                CorrelationId = execution.ExecutionAttemptId,
                CausationId = sourceEventId,
                RequestedAtUtc = confirmedAtUtc,
                ExpiresAtUtc = confirmedAtUtc.AddDays(1),
                ExpectedFinancialRevision = selected.FinancialRevision,
                Body = batch,
                Access = access
            };
            return global::TomasAI.IFM.Shared.EventModelActor.MessageOrigins.Copy(command, command with { InputSha256 = FinancialCanonicalHash.Request(command) });
        }
        var command = execution.PositionType == TradeOrderPositionType.Closing
            ? await new ClosingBasisIntentClaim(transactions, databases).ClaimAsync(
                execution, operationId, evidenceHash, selected.Value, BuildCommand, cancellationToken).ConfigureAwait(false)
            : await intents.ClaimAsync(BuildCommand(null), evidenceHash, cancellationToken).ConfigureAwait(false);
        return await actors.SendAsync<PostFundTransactionsCommand, LedgerPortfolioId>(
            command, command.EntityId, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Posts cumulative automatic-opening evidence once, retaining original receipts and converting accepted capacity.</summary>
    public async ValueTask<ServiceResult<Guid>> PostExpiredOpeningAsync(TradeOrderDefinition order, Guid executionAttemptId,
        Guid expiryCommandId, DateTime confirmedAtUtc, CancellationToken cancellationToken = default)
    {
        if (order.Status != TradeOrderStatus.Expired || order.BoundExecutionAttemptId is not null ||
            order.StrategyWorkflowId is null || order.BrokerEnvironment != BrokerEnvironment.Emulator ||
            order.PositionType != TradeOrderPositionType.Opening || executionAttemptId == Guid.Empty ||
            expiryCommandId == Guid.Empty || confirmedAtUtc.Kind != DateTimeKind.Utc)
            throw new ArgumentException("An unbound, committed expired automatic opening is required.");
        var hash = FinancialCanonicalHash.Compute(order);
        var operation = DeterministicId("strategy-expiry-accounting", new OrderExecutionId(order.Id, executionAttemptId).Format());
        var intents = new BrokerAccountingIntentStore(transactions);
        var prior = await intents.ReadAsync(order.Id.PortfolioId, operation, hash, cancellationToken).ConfigureAwait(false);
        if (prior is not null)
            return await actors.RequestAsync<PostFundTransactionsCommand, LedgerPortfolioId>(prior, cancellationToken).ConfigureAwait(false);
        var scope = new FinancialReadScope { PortfolioId = order.Id.PortfolioId, FundId = order.Id.FundId,
            Access = new("StrategyExpiryAccounting", ["PortfolioAdministrator"], [order.Id.PortfolioId]) };
        var selected = await financialQueries.ReadAsync(scope, new GetFinancialPostingConfigurationRequest(DateOnly.FromDateTime(confirmedAtUtc)), cancellationToken).ConfigureAwait(false);
        if (selected.Status != FinancialReadStatus.Found || selected.Value is not { } configuration)
            throw new InvalidOperationException("PORTFOLIO_ACCOUNTING.CONFIGURATION_UNAVAILABLE");
        var units = order.Components.Sum(component => component.Legs.Max(leg => Math.Abs(leg.SignedQuantity)));
        var request = new PostFundTransactionsCommand
        {
            CommandId = DeterministicId("post-strategy-expiry-accounting", operation.ToString("N")), OperationId = operation,
            EntityId = new(order.Id.PortfolioId), PortfolioId = order.Id.PortfolioId,
            Subject = new(ActorType.Command, PostFundTransactionsCommand.Actor, PostFundTransactionsCommand.Verb, new LedgerPortfolioId(order.Id.PortfolioId).Format()),
            CorrelationId = executionAttemptId, CausationId = expiryCommandId, RequestedAtUtc = confirmedAtUtc,
            ExpiresAtUtc = confirmedAtUtc.AddDays(1), ExpectedFinancialRevision = selected.FinancialRevision, Access = scope.Access,
            Body = new() { BookId = configuration.BookId, ManifestHash = FinancialCanonicalHash.Compute(Array.Empty<LedgerPostingRequest>()),
                ExecutionCapacity = new() { OrderId = order.Id.OrderId, FundId = order.Id.FundId, ExecutionId = executionAttemptId,
                    SourceCommandId = expiryCommandId, ExecutionHash = hash, ApprovedUnits = units, FilledUnits = 0,
                    CancelledUnits = units, UnsubmittedExpiry = true } }
        };
        request = global::TomasAI.IFM.Shared.EventModelActor.MessageOrigins.Copy(request,
            request with { InputSha256 = FinancialCanonicalHash.Request(request) });
        var claimed = await intents.ClaimAsync(request, hash, cancellationToken).ConfigureAwait(false);
        return await actors.RequestAsync<PostFundTransactionsCommand, LedgerPortfolioId>(claimed, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Posts cumulative automatic-opening evidence once, retaining original receipts and converting accepted capacity.</summary>
    async ValueTask<ServiceResult<Guid>> PostStrategyOpeningAsync(OrderExecutionDefinition execution, Guid sourceCommandId,
        DateTime confirmedAtUtc, CancellationToken token)
    {
        if (sourceCommandId == Guid.Empty || !execution.Id.IsValid || confirmedAtUtc.Kind != DateTimeKind.Utc ||
            execution.Order.PositionType != TradeOrderPositionType.Opening || execution.Order.BrokerEnvironment != BrokerEnvironment.Emulator ||
            execution.OrderQuantity <= 0 || execution.CumulativeFilledQuantity < 0 ||
            execution.CumulativeFilledQuantity > execution.OrderQuantity ||
            execution.Status is not (OrderExecutionStatus.PartiallyFilled or OrderExecutionStatus.Filled or OrderExecutionStatus.Cancelled or OrderExecutionStatus.Rejected))
            throw new ArgumentException("A committed emulator opening execution and exact source command are required.");
        var hash = FinancialCanonicalHash.Compute(execution);
        var operation = DeterministicId("strategy-accounting", execution.Id.Format() + "|" + hash);
        var intents = new BrokerAccountingIntentStore(transactions);
        var original = await intents.ReadAsync(execution.TradeOrderId.PortfolioId, operation, hash, token).ConfigureAwait(false);
        if (original is not null)
            return await actors.RequestAsync<PostFundTransactionsCommand, LedgerPortfolioId>(original, token).ConfigureAwait(false);
        var scope = new FinancialReadScope { PortfolioId = execution.TradeOrderId.PortfolioId, FundId = execution.TradeOrderId.FundId,
            Access = new("StrategyExecutionAccounting", ["PortfolioAdministrator"], [execution.TradeOrderId.PortfolioId]) };
        var accountingDate = DateOnly.FromDateTime(execution.Fills.Length == 0 ? confirmedAtUtc : execution.Fills.Min(fill => fill.FilledAtUtc));
        var selected = await financialQueries.ReadAsync(scope, new GetFinancialPostingConfigurationRequest(accountingDate), token).ConfigureAwait(false);
        if (selected.Status != FinancialReadStatus.Found || selected.Value is not { PeriodOpen: true } configuration)
            throw new InvalidOperationException("PORTFOLIO_ACCOUNTING.CONFIGURATION_UNAVAILABLE");
        var all = execution.Fills.Length == 0 ? new LedgerPostingBatchRequest { BookId = configuration.BookId } :
            BrokerExecutionAccountingModel.Create(new(configuration, execution.Order, execution.Fills,
                "BrokerExecution:" + execution.Id.Format(), confirmedAtUtc));
        var posted = await intents.PostedSourcesAsync(configuration.BookId, all.Items, token).ConfigureAwait(false);
        foreach (var item in all.Items)
            if (posted.TryGetValue((item.Source.SourceEventId.ToString("N"), item.TransactionKind.ToString()), out var priorHash) && priorHash != item.Source.SourceContentHash)
                throw new InvalidOperationException("PORTFOLIO_ACCOUNTING.FILL_CONFLICT");
        var delta = all.Items.Where(item => !posted.ContainsKey((item.Source.SourceEventId.ToString("N"), item.TransactionKind.ToString()))).ToArray();
        var request = new PostFundTransactionsCommand
        {
            CommandId = DeterministicId("post-strategy-accounting", operation.ToString("N")), OperationId = operation,
            EntityId = new(execution.TradeOrderId.PortfolioId), PortfolioId = execution.TradeOrderId.PortfolioId,
            Subject = new(ActorType.Command, PostFundTransactionsCommand.Actor, PostFundTransactionsCommand.Verb,
                new LedgerPortfolioId(execution.TradeOrderId.PortfolioId).Format()),
            CorrelationId = execution.ExecutionAttemptId, CausationId = sourceCommandId, RequestedAtUtc = confirmedAtUtc,
            ExpiresAtUtc = confirmedAtUtc.AddDays(1), ExpectedFinancialRevision = selected.FinancialRevision, Access = scope.Access,
            Body = all with { Items = delta, ManifestHash = FinancialCanonicalHash.Compute(delta), ExecutionCapacity = new()
            {
                OrderId = execution.TradeOrderId.OrderId, FundId = execution.TradeOrderId.FundId,
                ExecutionId = execution.ExecutionAttemptId, SourceCommandId = sourceCommandId, ExecutionHash = hash,
                ApprovedUnits = execution.OrderQuantity, FilledUnits = execution.CumulativeFilledQuantity,
                CancelledUnits = execution.Status is OrderExecutionStatus.Cancelled or OrderExecutionStatus.Rejected
                    ? execution.OrderQuantity - execution.CumulativeFilledQuantity : 0
            } }
        };
        request = global::TomasAI.IFM.Shared.EventModelActor.MessageOrigins.Copy(request,
            request with { InputSha256 = FinancialCanonicalHash.Request(request) });
        var claimed = await intents.ClaimAsync(request, hash, token).ConfigureAwait(false);
        return await actors.RequestAsync<PostFundTransactionsCommand, LedgerPortfolioId>(claimed, token).ConfigureAwait(false);
    }

    /// <summary>Requires a completed identified broker execution with confirmed fills and a UTC source observation before preparing financial posting facts.</summary>
    static void ValidateExecution(OrderExecutionDefinition execution, Guid sourceEventId, DateTime confirmedAtUtc)
    {
        var cumulativeFilledQuantity = (execution.CumulativeFilledQuantity > 0) switch
{
    true => execution.CumulativeFilledQuantity,
    false => execution.Fills.Length > 0 ? 1 : 0
};
        if (execution.Status is not (OrderExecutionStatus.Filled or OrderExecutionStatus.Cancelled) ||
            execution.CompletedAtUtc is null || cumulativeFilledQuantity <= 0 || execution.Fills.Length == 0 ||
            !execution.Id.IsValid)
            throw new ArgumentException("A completed execution with fills, source event and UTC confirmation time is required.", nameof(execution));
        if (sourceEventId == Guid.Empty)
            throw new ArgumentException("A completed execution with fills, source event and UTC confirmation time is required.", nameof(sourceEventId));
        if (confirmedAtUtc.Kind != DateTimeKind.Utc)
            throw new ArgumentException("A completed execution with fills, source event and UTC confirmation time is required.", nameof(confirmedAtUtc));

    }

    private static Guid DeterministicId(string purpose, string identity) =>
        new(SHA256.HashData(Encoding.UTF8.GetBytes($"{purpose}|{identity}"))[..16]);
}
