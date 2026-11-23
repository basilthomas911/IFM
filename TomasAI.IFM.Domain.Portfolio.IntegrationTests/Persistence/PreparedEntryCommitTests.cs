using FluentAssertions;
using TomasAI.IFM.Domain.Portfolio.OrderComposition.Model;
using TomasAI.IFM.Domain.Portfolio.OrderComposition.Repository;
using TomasAI.IFM.Domain.Portfolio.Shared.Financial;
using TomasAI.IFM.Domain.Portfolio.Shared.OrderComposition;
using TomasAI.IFM.Shared.Storage;
using TomasAI.IFM.Shared.EventModelActor;

namespace TomasAI.IFM.Domain.Portfolio.IntegrationTests.Persistence;

public sealed partial class PortfolioOrderCompositionIntegrationTests
{
    [Fact]
    public async Task Prepared_approval_has_no_effects_until_commit_and_concurrent_duplicates_share_one_receipt()
    {
        await InitializePortfolioSchema();
        var book = await CreateOrderBook();
        var store = new PortfolioOrderCompositionStore(Transactions());
        var approval = await PrepareApproval(store, book);
        var before = await EntryCounts(approval.Request);
        before.Should().Be((0L, 0L, 0L));
        approval.Funds.Should().ContainSingle(value => value.Accepted);
        approval.Funds[0].CapacityEffect!.OrderId.Should().Be(0);
        var replies = await Task.WhenAll(Enumerable.Range(0, 3).Select(_ => store.CommitApprovedAsync(approval,
            new TestIdentityAllocator(approval.Request.OperationId))));
        replies.Select(value => value.Id).Distinct().Should().ContainSingle();
        replies[0].Receipt.FinancialRevision.Should().Be(approval.Request.ExpectedFinancialRevision + 1);
        (await EntryCounts(approval.Request)).Should().Be((1L, 1L, 1L));
        var recovered = await new PortfolioOrderCompositionStore(Transactions()).ReconcileApprovedAsync(approval);
        recovered.Outcome.Should().Be(PortfolioEntryCommitOutcome.Committed);
        recovered.Completed!.Id.Should().Be(replies[0].Id);
    }

    [Fact]
    public async Task A_changed_financial_revision_rejects_the_old_approval_without_recalculating_or_creating_orders()
    {
        await InitializePortfolioSchema();
        var book = await CreateOrderBook();
        var store = new PortfolioOrderCompositionStore(Transactions());
        var first = await PrepareApproval(store, book);
        var stale = await PrepareApproval(store, book);
        await store.CommitApprovedAsync(first, new TestIdentityAllocator(first.Request.OperationId));
        var error = await FluentActions.Awaiting(() => store.CommitApprovedAsync(stale, new TestIdentityAllocator(stale.Request.OperationId)))
            .Should().ThrowAsync<FinancialOperationException>();
        error.Which.Code.Should().Be(FinancialReasons.RevisionConflict);
        (await EntryCounts(stale.Request)).Should().Be((0L, 0L, 0L));
    }

    [Fact]
    public async Task No_trade_approval_cannot_enter_the_financial_commit_boundary()
    {
        await InitializePortfolioSchema();
        var book = await CreateOrderBook(value => value with { Funds = value.Funds.Select(fund => fund with { CanSpend = false }).ToArray() });
        var store = new PortfolioOrderCompositionStore(Transactions());
        var approval = await PrepareApproval(store, book);
        approval.Funds.Should().OnlyContain(value => !value.Accepted && value.CapacityEffect == null);
        await FluentActions.Awaiting(() => store.CommitApprovedAsync(approval, new TestIdentityAllocator(approval.Request.OperationId)))
            .Should().ThrowAsync<ArgumentException>();
        (await EntryCounts(approval.Request)).Should().Be((0L, 0L, 0L));
    }

    [Fact]
    public async Task Expired_uncommitted_approval_reconciles_to_rejected_with_zero_effects()
    {
        await InitializePortfolioSchema();
        var book = await CreateOrderBook();
        var store = new PortfolioOrderCompositionStore(Transactions());
        var approval = (await PrepareApproval(store, book)) with { ValidUntilUtc = DateTime.UtcNow.AddSeconds(-1) };
        approval = approval.Seal();
        await FluentActions.Awaiting(() => store.CommitApprovedAsync(approval, new TestIdentityAllocator(approval.Request.OperationId)))
            .Should().ThrowAsync<TimeoutException>();
        (await store.ReconcileApprovedAsync(approval)).Outcome.Should().Be(PortfolioEntryCommitOutcome.Rejected);
        (await EntryCounts(approval.Request)).Should().Be((0L, 0L, 0L));
    }

    [Fact]
    public async Task Lost_commit_acknowledgement_is_reconciled_without_another_decision_or_financial_commit()
    {
        await InitializePortfolioSchema();
        var book = await CreateOrderBook();
        var store = new PortfolioOrderCompositionStore(Transactions());
        var approval = await PrepareApproval(store, book);
        await using var proxy = new LostCommitReplyProxy(refuseRecovery: true);
        var uncertain = new PortfolioOrderCompositionStore(FinancialCommitUncertaintyTests.ProxiedTransactions(proxy.Port));
        await FluentActions.Awaiting(() => uncertain.CommitApprovedAsync(approval, new TestIdentityAllocator(approval.Request.OperationId)))
            .Should().ThrowAsync<FunctionCommitOutcomeUnknownException>();
        proxy.Dropped.Should().BeTrue();
        var recovered = await store.ReconcileApprovedAsync(approval);
        recovered.Outcome.Should().Be(PortfolioEntryCommitOutcome.Committed);
        (await EntryCounts(approval.Request)).Should().Be((1L, 1L, 1L));
    }

    static async Task<PortfolioEntryApproval> PrepareApproval(PortfolioOrderCompositionStore store, FinancialBookConfiguration book)
    {
        var state = await store.ReadDecisionStateAsync(book.PortfolioId);
        var request = Request(book) with { ExpectedFinancialRevision = state.FinancialRevision };
        request = request with { InputSha256 = FinancialCanonicalHash.Request(request) };
        var assessments = new PortfolioEntryDecisionCalculator().Calculate(request, state, default);
        return new PortfolioEntryApproval { Request = request, Funds = assessments, ProducedAtUtc = DateTime.UtcNow,
            ValidUntilUtc = state.ValidUntilUtc, DecisionSessionId = Guid.NewGuid() }.Seal();
    }

    static Task<(long Orders, long Events, long Receipts)> EntryCounts(EvaluatePortfolioOrderCompositionCommand request) =>
        Transactions().ExecuteOnceAsync(async (db, token) => (
            Convert.ToInt64(await db.ScalarAsync("SELECT count(*) FROM portfolio.accepted_trade_order WHERE operation_id=$1;", [request.OperationId], token)),
            Convert.ToInt64(await db.ScalarAsync("SELECT count(*) FROM event_log WHERE commandid=$1;", [request.CommandId], token)),
            Convert.ToInt64(await db.ScalarAsync("SELECT count(*) FROM portfolio_financial.financial_operation_receipt WHERE portfolio_id=$1 AND operation_id=$2;", [request.PortfolioId, request.OperationId], token))));
}
