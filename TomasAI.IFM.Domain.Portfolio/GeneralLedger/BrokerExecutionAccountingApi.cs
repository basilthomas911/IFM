using System.Security.Cryptography;
using System.Text;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.Storage.EventSourceDb;
using TomasAI.IFM.Application.Storage.PortfolioFinancial;
using TomasAI.IFM.Domain.Portfolio.GeneralLedger.Model;
using TomasAI.IFM.Domain.Portfolio.Shared.Financial;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Portfolio.GeneralLedger;

/// <summary>Typed Portfolio boundary for confirmed broker executions.</summary>
public interface IPortfolioTradeAccountingApi
{
    /// <summary>Posts confirmed settlement, commission, and closing realized P&amp;L idempotently.</summary>
    ValueTask<ServiceResult<Guid>> PostConfirmedExecutionAsync(OrderExecutionDefinition execution,
        Guid sourceEventId, DateTime confirmedAtUtc, CancellationToken cancellationToken = default);
}

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
        var cumulativeFilledQuantity = execution.CumulativeFilledQuantity > 0
            ? execution.CumulativeFilledQuantity
            : execution.Fills.Length > 0 ? 1 : 0;
        if (execution.Status is not (OrderExecutionStatus.Filled or OrderExecutionStatus.Cancelled) ||
            execution.CompletedAtUtc is null || cumulativeFilledQuantity <= 0 || execution.Fills.Length == 0 ||
            !execution.Id.IsValid || sourceEventId == Guid.Empty || confirmedAtUtc.Kind != DateTimeKind.Utc)
            throw new ArgumentException("A completed execution with fills, source event and UTC confirmation time is required.");

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
            FinancialCanonicalHash.Compute(execution.Components) != FinancialCanonicalHash.Compute(execution.Order.Components))
            throw new ArgumentException("Execution identity and approved order disagree.");
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
            return command with { InputSha256 = FinancialCanonicalHash.Request(command) };
        }
        var command = execution.PositionType == TradeOrderPositionType.Closing
            ? await new ClosingBasisIntentClaim(transactions, databases).ClaimAsync(
                execution, operationId, evidenceHash, selected.Value, BuildCommand, cancellationToken).ConfigureAwait(false)
            : await intents.ClaimAsync(BuildCommand(null), evidenceHash, cancellationToken).ConfigureAwait(false);
        return await actors.SendAsync<PostFundTransactionsCommand, LedgerPortfolioId>(
            command, command.EntityId, cancellationToken).ConfigureAwait(false);
    }

    private static Guid DeterministicId(string purpose, string identity) =>
        new(SHA256.HashData(Encoding.UTF8.GetBytes($"{purpose}|{identity}"))[..16]);
}
