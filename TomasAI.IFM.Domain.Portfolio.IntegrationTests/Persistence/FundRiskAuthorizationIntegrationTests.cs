using FluentAssertions;
using TomasAI.IFM.Application.Storage.PortfolioFinancial;
using TomasAI.IFM.Domain.Portfolio.Command.Model;
using TomasAI.IFM.Domain.Portfolio.Persistence;
using TomasAI.IFM.Domain.Portfolio.Shared.Financial;
using static TomasAI.IFM.Domain.Portfolio.IntegrationTests.Persistence.GeneralLedgerPostingIntegrationTests;

namespace TomasAI.IFM.Domain.Portfolio.IntegrationTests.Persistence;

[Collection("PortfolioFinancialDatabase"), Trait("Category","PortfolioFinancial"), Trait("Gate","PF-FIN-05")]
public sealed class FundRiskAuthorizationIntegrationTests(PortfolioEventStoreFixture fixture):IClassFixture<PortfolioEventStoreFixture>
{
    [Fact]
    public async Task Exact_grant_commits_with_fund_event_and_is_queryable_without_scylla()
    {
        var (book, grant) = await Grant();
        var events = new PortfolioEventStore(fixture.EventSourceDb, new PortfolioAuthorityFence(Transactions()));
        var changed = Event(grant);
        await events.AppendFundAsync(new(book.PortfolioId,grant.FundId),changed,1);
        var actual = await new FinancialQueryStore(Transactions()).ReadAsync(Scope(grant),new GetFundRiskAuthorizationRequest(changed.CommandId));
        actual.Value!.Authorization.Should().Be(grant);
        actual.Value.EventId.Should().Be(changed.Id);
        actual.FinancialRevision.Should().Be(grant.FinancialRevision);
        (await new PortfolioFinancialDbContext(Transactions()).ReadBookAsync(book.PortfolioId))!.Funds[0].FundStreamVersion.Should().Be(2);
    }

    [Theory]
    [InlineData("units")]
    [InlineData("hash")]
    [InlineData("order")]
    [InlineData("event")]
    [InlineData("operation")]
    [InlineData("epoch")]
    [InlineData("expired")]
    public async Task Changed_grant_cannot_append_fund_approval(string field)
    {
        var (book, grant) = await Grant();
        grant = field switch
        {
            "units" => grant with { StrategyUnits=grant.StrategyUnits+1 },
            "hash" => grant with { SizedOrderHash=new('F',64) },
            "order" => grant with { OrderId=grant.OrderId+1 },
            "event" => grant with { ReservationCompletedEventId=Guid.NewGuid() },
            "operation" => grant with { ReservationOperationId=Guid.NewGuid() },
            "epoch" => grant with { AuthorityEpoch=grant.AuthorityEpoch+1 },
            _ => grant with { ValidUntilUtc=DateTime.UtcNow.AddSeconds(-1) }
        };
        var events = new PortfolioEventStore(fixture.EventSourceDb,new PortfolioAuthorityFence(Transactions()));
        var changed=Event(grant);
        await FluentActions.Awaiting(()=>events.AppendFundAsync(new(book.PortfolioId,grant.FundId),changed,1))
            .Should().ThrowAsync<FinancialOperationException>();
        var count=await Transactions().ExecuteAsync((db,ct)=>db.ScalarAsync("SELECT count(*) FROM event_log WHERE commandid=$1;",[changed.CommandId],ct));
        Convert.ToInt64(count).Should().Be(0);
    }

    [Fact]
    public async Task Query_cannot_expose_other_funds_authorization()
    {
        var (book, grant) = await Grant(); var changed=Event(grant);
        await new PortfolioEventStore(fixture.EventSourceDb,new PortfolioAuthorityFence(Transactions()))
            .AppendFundAsync(new(book.PortfolioId,grant.FundId),changed,1);
        var scope=Scope(grant) with { FundId=grant.FundId+1 };
        await FluentActions.Awaiting(()=>new FinancialQueryStore(Transactions()).ReadAsync(scope,new GetFundRiskAuthorizationRequest(changed.CommandId)))
            .Should().ThrowAsync<FinancialOperationException>();
    }

    [Fact]
    public async Task Financial_approval_cannot_use_unfenced_legacy_event_store()
    {
        var (book, grant) = await Grant();
        await FluentActions.Awaiting(()=>new PortfolioEventStore(fixture.EventSourceDb)
            .AppendFundAsync(new(book.PortfolioId,grant.FundId),Event(grant),1)).Should().ThrowAsync<InvalidOperationException>();
    }

    static async Task<(FinancialBookConfiguration,FundRiskAuthorizationReference)> Grant()
    {
        var book=await CreateBook(b=>b with { Environment="Emulator",AuthorityEpoch=1,
            Funds=[b.Funds[0] with { Reference=b.Funds[0].Reference with { AuthorityEpoch=1 },
                Limits=[new(CapacityScopeKind.Portfolio,b.PortfolioId.ToString(),CapacityMeasure.LossCharge,CapacityUnit.Usd,1000)] }] });
        await Post(Request(book,LedgerTransactionKind.DepositConfirmed,1000,0));
        var request=await CapacityReservationIntegrationTests.ReserveRequest(book,700);
        var completed=await CapacityReservationIntegrationTests.Reserve(request);
        return (book,FundRiskAuthorizationReference.From(request.Body.RiskInvocationId,request.Body.WorkflowId,completed.Receipt));
    }
    static FinancialReadScope Scope(FundRiskAuthorizationReference grant)=>new()
    { PortfolioId=grant.PortfolioId,FundId=grant.FundId,Access=new("integration",["LedgerRead"],[grant.PortfolioId]) };
    static FundCompositionStateChanged Event(FundRiskAuthorizationReference grant)=>new(Guid.NewGuid(),Guid.NewGuid(),2,DateTime.UtcNow,"integration",
        new() { PortfolioId=grant.PortfolioId,FundId=grant.FundId,OrderId=grant.OrderId,Status="RiskApproved",RiskAuthorization=grant });
}
