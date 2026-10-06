using TomasAI.IFM.Application.Storage.PortfolioFinancial;
using TomasAI.IFM.Domain.Portfolio.GeneralLedger.Model;
using TomasAI.IFM.Domain.Portfolio.Shared.Financial;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Portfolio.GeneralLedger.Command;

public static class PostFundTransaction
{
    /// <summary>Handles Execute Async at the command boundary.</summary>
    /// <param name="command">The concrete business command and originating identity.</param>
    /// <param name="services">The enlisted persistence, receipt replay and projection services.</param>
    /// <param name="cancellationToken">Cancels pre-commit work; an uncertain commit must be reconciled.</param>
    /// <returns>Command acceptance or its classified failure; durable completion remains tied to commit.</returns>
    /// <exception cref="OperationCanceledException">Cancellation interrupts pre-commit work; committed or uncertain operations require reconciliation.</exception>
    public static async ValueTask<ServiceResult<GuidResult>> ExecuteAsync(this PostFundTransactionCommand command, GeneralLedgerCommandServices services, CancellationToken cancellationToken)
    {
        // Receipt replay precedes sequence allocation and expiry checks; validation still enforces caller permission.
        var replay = await services.Database.ReadOperationAsync<LedgerPostingCompletedEvent>(command.PortfolioId, command.OperationId, command.InputSha256, cancellationToken);
        if (replay is not null) return await replay.NotifyAsync(services.Projector, services.Logger);
        var allowAdjustment = FinancialRequestValidation.DemandPosting(command, command.Body, DateTime.UtcNow);
        var item = new PreparedLedgerPosting(await services.Ids.TransactionAsync(cancellationToken), await services.Ids.JournalAsync(cancellationToken), command.Body);
        var completed = await services.Store.PostAsync(command, [item], (body, rule, prior, original, remaining) =>
            command.Compute(body, rule, prior, original, remaining, allowAdjustment),
            ledgerCommit => command.Complete(ledgerCommit), cancellationToken);
        return await completed.NotifyAsync(services.Projector, services.Logger);
    }
    /// <summary>Handles Complete at the command boundary.</summary>
    /// <param name="command">The concrete business command and originating identity.</param>
    /// <param name="ledgerCommit">The verified committed ledger revision and posting identities.</param>
    /// <returns>The operation result.</returns>
    public static LedgerPostingCompletedEvent Complete(this PostFundTransactionCommand command, LedgerCommitInfo ledgerCommit) => new()
    {
        Id = ledgerCommit.EventId,
        Subject = new(ActorType.Event, PostFundTransactionCommand.Actor, nameof(LedgerPostingCompletedEvent), command.EntityId.Format()),
        EntityId = command.EntityId,
        CommandId = command.CommandId,
        OperationId = command.OperationId,
        PortfolioId = command.PortfolioId,
        CorrelationId = command.CorrelationId,
        CausationId = command.CausationId,
        CommittedAtUtc = ledgerCommit.CommittedAtUtc,
        ReceivedOn = ledgerCommit.CommittedAtUtc,
        InputHash = command.InputSha256,
        AggregateId = command.EntityId.Format(),
        Receipt = new()
        {
            OperationId = command.OperationId,
            PortfolioId = command.PortfolioId,
            BookId = command.Body.BookId,
            FundId = command.Body.FundId,
            TransactionId = ledgerCommit.Items[0].TransactionId,
            JournalId = ledgerCommit.Items[0].JournalId,
            JournalHash = ledgerCommit.Items[0].JournalHash,
            InputHash = command.InputSha256,
            FinancialRevision = ledgerCommit.Revision,
            CommittedAtUtc = ledgerCommit.CommittedAtUtc,
            CompletedEventId = ledgerCommit.EventId,
            Source = command.Body.Source,
            ObligationId = ledgerCommit.Items[0].ObligationId
        }
    };
    /// <summary>Calculates a ledger posting from the business state read under the financial transaction fence.</summary>
    /// <param name="command">The concrete business command and originating identity.</param>
    /// <param name="ledgerPosting">The ledger posting business input.</param>
    /// <param name="postingRule">The posting rule business input.</param>
    /// <param name="previousUnrealized">The previous unrealized business input.</param>
    /// <param name="reversalLines">The reversal lines business input.</param>
    /// <param name="remainingReversibleAmount">The remaining reversible amount business input.</param>
    /// <param name="allowRawAdjustment">The allow raw adjustment business input.</param>
    /// <returns>The computed business values or created event; no transport or storage effects.</returns>
    /// <exception cref="InvalidOperationException">The current business state cannot accept the requested transition.</exception>
    internal static LedgerPostingPlan Compute(this PostFundTransactionCommand command, LedgerPostingRequest ledgerPosting,
        LedgerPostingRule postingRule, decimal previousUnrealized, IReadOnlyList<PlannedLedgerLine>? reversalLines,
        decimal remainingReversibleAmount, bool allowRawAdjustment)
        => LedgerPostingModel.Calculate(ledgerPosting, postingRule, previousUnrealized, reversalLines,
            remainingReversibleAmount, allowRawAdjustment);
}
