using FluentAssertions;
using TomasAI.IFM.Application.Storage.PortfolioFinancial;
using TomasAI.IFM.Domain.Portfolio.CapacityReservation.Model;
using TomasAI.IFM.Domain.Portfolio.Shared.Financial;
using static TomasAI.IFM.Domain.Portfolio.IntegrationTests.Persistence.CapacityReservationIntegrationTests;
using static TomasAI.IFM.Domain.Portfolio.IntegrationTests.Persistence.GeneralLedgerPostingIntegrationTests;

namespace TomasAI.IFM.Domain.Portfolio.IntegrationTests.Persistence;

[Collection("PortfolioFinancialDatabase"),Trait("Category","PortfolioFinancial"),Trait("Gate","PF-FIN-04")]
public sealed class CapacityExpiryIntegrationTests(PortfolioEventStoreFixture fixture):IClassFixture<PortfolioEventStoreFixture>
{
    [Fact]
    public async Task Independent_dispatchers_and_restart_preserve_one_expiry_operation_after_lost_reply()
    {
        _=fixture;var book=await FundedBook();var reserve=await ReserveRequest(book,700,validity:TimeSpan.FromSeconds(1));await Reserve(reserve);
        await Past(reserve.Body.ValidUntilUtc);
        var stores=new[] { new CapacityExpiryDispatchStore(Transactions()),new CapacityExpiryDispatchStore(Transactions()) };
        var pending=await Task.WhenAll(stores.Select(x=>x.PrepareAsync(CapacityExpiryModel.Create,portfolioId:book.PortfolioId)));
        var command=pending[0].Single().Request;pending[1].Single().Request.Should().BeEquivalentTo(command);
        (await Usage(book)).Should().Be((700m,0m,0m)); // Preparing is never a financial release.
        var receipt=await Change(command); // Deliberately lose this command reply; the next process reconciles PostgreSQL.
        receipt.Receipt.Status.Should().Be(ReservationStatus.Expired);
        var restarted=new CapacityExpiryDispatchStore(Transactions());
        (await restarted.PrepareAsync(CapacityExpiryModel.Create,portfolioId:book.PortfolioId)).Single().Request.OperationId.Should().Be(command.OperationId);
        (await restarted.ReconcileAsync(command)).Should().BeTrue();
        (await restarted.PrepareAsync(CapacityExpiryModel.Create,portfolioId:book.PortfolioId)).Should().BeEmpty();
        (await Usage(book)).Should().Be((0m,0m,0m));(await Change(command)).Id.Should().Be(receipt.Id);
    }
    [Fact]
    public async Task Expired_consumed_capacity_is_never_selected_for_unconsumed_expiry()
    {
        var book=await FundedBook();var reserve=await ReserveRequest(book,700,validity:TimeSpan.FromSeconds(2));await Reserve(reserve);
        var consume=ConsumeRequest(reserve,2);await Accept(reserve,consume);await Consume(consume);
        await Past(reserve.Body.ValidUntilUtc);
        (await new CapacityExpiryDispatchStore(Transactions()).PrepareAsync(CapacityExpiryModel.Create,portfolioId:book.PortfolioId)).Should().BeEmpty();
        (await Usage(book)).Should().Be((0m,700m,0m));
    }
    [Fact]
    public async Task Stale_attempt_gets_a_new_identity_only_after_fenced_expired_no_commit_proof()
    {
        var book=await FundedBook();var reserve=await ReserveRequest(book,700,validity:TimeSpan.FromSeconds(1));await Reserve(reserve);await Past(reserve.Body.ValidUntilUtc);
        var store=new CapacityExpiryDispatchStore(Transactions());
        ChangeCapacityReservationCommand ShortAttempt(CapacityExpiryCandidate candidate,DateTime now)
        {
            var command=CapacityExpiryModel.Create(candidate,now) with { ExpiresAtUtc=now.AddSeconds(1) };
            return command with { InputSha256=FinancialCanonicalHash.Request(command) };
        }
        var command=(await store.PrepareAsync(ShortAttempt,portfolioId:book.PortfolioId)).Single().Request;
        await Post(Request(book,LedgerTransactionKind.DepositConfirmed,1,2));
        await FluentActions.Awaiting(()=>Change(command)).Should().ThrowAsync<FinancialOperationException>();
        (await store.ReconcileAsync(command)).Should().BeFalse();
        (await store.PrepareAsync(CapacityExpiryModel.Create,portfolioId:book.PortfolioId)).Single().Request.OperationId.Should().Be(command.OperationId);
        await Past(command.ExpiresAtUtc);(await store.ReconcileAsync(command)).Should().BeTrue();
        var retry=(await store.PrepareAsync(CapacityExpiryModel.Create,portfolioId:book.PortfolioId)).Single().Request;
        retry.OperationId.Should().NotBe(command.OperationId);retry.ExpectedFinancialRevision.Should().Be(3);
        retry.Body.Source.SourceEventId.Should().Be(command.Body.Source.SourceEventId);
        (await Change(retry)).Receipt.Status.Should().Be(ReservationStatus.Expired);
        (await new PortfolioFinancialDbContext(Transactions()).ReadOperationAsync<CapacityLifecycleCompletedEvent>(book.PortfolioId,command.OperationId)).Should().BeNull();
        (await Usage(book)).Should().Be((0m,0m,0m));
    }
    static async Task Past(DateTime deadline)
    {
        var remaining=deadline-DateTime.UtcNow;
        if(remaining>TimeSpan.Zero) await Task.Delay(remaining+TimeSpan.FromMilliseconds(25));
    }
}
