using FluentAssertions;
using TomasAI.IFM.Domain.Portfolio.Financial.Repository;
using TomasAI.IFM.Domain.Portfolio.GeneralLedger.Command;
using TomasAI.IFM.Domain.Portfolio.GeneralLedger.Model;
using TomasAI.IFM.Domain.Portfolio.Shared.Financial;
using TomasAI.IFM.Domain.Trade.Shared;
using static TomasAI.IFM.Domain.Portfolio.IntegrationTests.Persistence.GeneralLedgerPostingIntegrationTests;

namespace TomasAI.IFM.Domain.Portfolio.IntegrationTests.Persistence;

[Collection("PortfolioFinancialDatabase")]
public sealed class MultiplierReconciliationIntegrationTests(PortfolioEventStoreFixture fixture)
    : IClassFixture<PortfolioEventStoreFixture>
{
    [Fact]
    public async Task Correction_preserves_original_journal_and_retries_without_duplicate_settlement()
    {
        _ = fixture;
        var book = await CreateBook();
        var rule = new LedgerPostingRule(Guid.NewGuid(), 1, "", LedgerTransactionKind.TradeSettlement,
            new(102,1), new(101,1), true);
        rule = rule with { ContentHash = FinancialCanonicalHash.Compute(rule) };
        var configuration = LedgerConfigurationIntegrationTests.Command(book, 0, new() {
            Action = LedgerConfigurationAction.AddPostingRuleVersion, BookId = book.BookId,
            Rules = [rule], PeriodStart = new(2020,1,1), Reason = "Historical multiplier reconciliation test"
        });
        await new LedgerConfigurationStore(Transactions()).ConfigureAsync(configuration,
            info => configuration.Complete(info), FinancialCanonicalHash.Compute);
        var originalRequest = Request(book, LedgerTransactionKind.TradeSettlement, -12.45m, 1);
        originalRequest = originalRequest with { Body = originalRequest.Body with {
            PostingRule = new() { RuleId = rule.RuleId, Version = rule.Version, ContentHash = rule.ContentHash }
        }};
        originalRequest = originalRequest with { InputSha256 = FinancialCanonicalHash.Request(originalRequest) };
        var posted = await Post(originalRequest);
        var at = DateTime.UtcNow; var legId = Guid.NewGuid(); var component = Guid.NewGuid(); var attempt = Guid.NewGuid();
        var leg = new TradeLegDefinition { TradeLegId = legId, ContractId = "ES-option", SignedQuantity = -1, CashMultiplier = 1 };
        var fill = new ExecutionFillEvidence { ExecutionFillId = originalRequest.Body.Source.SourceEventId,
            ExecutionAttemptId = attempt, ComponentId = component, TradeLegId = legId, ContractId = leg.ContractId,
            SignedQuantity = -1, Price = 12.45m, FilledAtUtc = at, ExternalExecutionId = "original-opening-fill" };
        var original = new EstablishedTradeDefinition { Id = new(book.PortfolioId,book.Funds[0].FundId,1901,1501),
            Legs = [leg], OriginalFills = [fill], SourceComponentId = component, ExecutionAttemptId = attempt, EvidenceRevision = 1 };
        var correctionId = Guid.NewGuid();
        var batch = OpeningMultiplierReconciliation.Create(original, [leg with { CashMultiplier = 50 }],
            new(book.BookId,book.Funds[0].FundId,book.Funds[0].Reference,[rule],true,DateOnly.FromDateTime(at)),
            new Dictionary<Guid,PostedOpeningSettlement> { [fill.ExecutionFillId] = new(posted.Receipt.JournalId!.Value, -12.45m) }, correctionId, at);
        var command = new PostFundTransactionsCommand { CommandId = correctionId, OperationId = correctionId,
            PortfolioId = book.PortfolioId, EntityId = new(book.PortfolioId),
            Subject = new(TomasAI.IFM.Shared.EventModelActor.ActorType.Command,PostFundTransactionsCommand.Actor,
                PostFundTransactionsCommand.Verb,book.PortfolioId.ToString()), RequestedAtUtc = at, ExpiresAtUtc = at.AddMinutes(2),
            ExpectedFinancialRevision = 2, Access = new("integration",["PortfolioAdministrator"]), Body = batch };
        command = command with { InputSha256 = FinancialCanonicalHash.Request(command) };
        var prepared = batch.Items.Select(item => new PreparedLedgerPosting(Random.Shared.NextInt64(100000,long.MaxValue),
            Random.Shared.NextInt64(100000,long.MaxValue), item)).ToArray();
        var store = new GeneralLedgerStore(Transactions());
        Task<LedgerPostingBatchCompletedEvent> Apply() => store.PostAsync(command, prepared,
            (body, selected, previous, reversal, remaining) => LedgerPostingModel.Calculate(body,selected,previous,reversal,remaining),
            info => command.Complete(info));
        var first = await Apply();
        (await Apply()).Id.Should().Be(first.Id);
        var amounts = await Transactions().ExecuteOnceAsync((db, token) => db.QueryAsync(
            "SELECT amount,related_journal_id FROM portfolio_financial.ledger_transaction WHERE operation_id=$1;",
            [correctionId], row => (row.GetDecimal(0),row.GetInt64(1)),token));
        amounts.Should().ContainSingle().Which.Should().Be((-610.05m,posted.Receipt.JournalId!.Value));
        var unchanged = await Transactions().ExecuteOnceAsync((db,token) => db.ScalarAsync(
            "SELECT amount FROM portfolio_financial.ledger_transaction WHERE transaction_id=$1;",[posted.Receipt.TransactionId],token));
        unchanged.Should().Be(-12.45m);
    }
}
