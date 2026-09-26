using FluentAssertions;
using TomasAI.IFM.Application.Storage.EventSourceDb.CommandAudit;
using TomasAI.IFM.Application.Storage.PortfolioFinancial;
using TomasAI.IFM.Domain.Portfolio.Shared.Financial;
using TomasAI.IFM.Shared.EventModelActor;
using static TomasAI.IFM.Domain.Portfolio.IntegrationTests.Persistence.GeneralLedgerPostingIntegrationTests;

namespace TomasAI.IFM.Domain.Portfolio.IntegrationTests.Persistence;

[Collection("PortfolioFinancialDatabase"), Trait("Category","PortfolioFinancial")]
public sealed class BrokerAccountingIntentIntegrationTests(PortfolioEventStoreFixture fixture)
    : IClassFixture<PortfolioEventStoreFixture>
{
    private static async Task<PostFundTransactionsCommand> Command()
    {
        var book=await CreateBook();
        var single=Request(book,LedgerTransactionKind.DepositConfirmed,123.4500m,0);
        var items=new[] {single.Body};
        var command=new PostFundTransactionsCommand
        {
            CommandId=single.CommandId,OperationId=single.OperationId,PortfolioId=single.PortfolioId,EntityId=single.EntityId,
            Subject=new(ActorType.Command,PostFundTransactionsCommand.Actor,PostFundTransactionsCommand.Verb,single.EntityId.Format()),
            CorrelationId=single.CorrelationId,CausationId=single.CausationId,RequestedAtUtc=single.RequestedAtUtc,
            ExpiresAtUtc=single.ExpiresAtUtc,Access=single.Access,
            Body=new() { BookId=book.BookId,Items=items,ManifestHash=FinancialCanonicalHash.Compute(items) }
        };
        return command with { InputSha256=FinancialCanonicalHash.Request(command) };
    }

    [Fact]
    public async Task Concurrent_claims_preserve_exact_first_transport_before_any_actor_dispatch()
    {
        var command=await Command(); var alternate=command with { ExpectedFinancialRevision=99 };
        alternate=alternate with { InputSha256=FinancialCanonicalHash.Request(alternate) };
        var hash=new string('A',64);
        var winners=await Task.WhenAll(
            new BrokerAccountingIntentStore(Transactions()).ClaimAsync(command,hash),
            new BrokerAccountingIntentStore(Transactions()).ClaimAsync(alternate,hash));
        var codec=new CommandAuditMessagePackCodec();
        codec.Serialize(winners[0]).Bytes.Should().Equal(codec.Serialize(winners[1]).Bytes);
        var recovered=await new BrokerAccountingIntentStore(Transactions()).ReadAsync(command.PortfolioId,command.OperationId,hash);
        codec.Serialize(recovered!).Bytes.Should().Equal(codec.Serialize(winners[0]).Bytes);
        var conflict=await FluentActions.Awaiting(()=>new BrokerAccountingIntentStore(Transactions())
            .ClaimAsync(command,new string('B',64))).Should().ThrowAsync<FinancialOperationException>();
        conflict.Which.Code.Should().Be(FinancialReasons.RequestMismatch);
    }

    [Fact]
    public async Task Enlisted_intent_rolls_back_when_allocation_transaction_fails()
    {
        var command=await Command(); var hash=new string('A',64);
        await FluentActions.Awaiting(()=>Transactions().ExecuteAsync<bool>(async(db,ct)=>
        {
            await BrokerAccountingIntentStore.ClaimEnlistedAsync(db,command,hash,ct);
            throw new InvalidOperationException("Synthetic failure after intent insertion");
        })).Should().ThrowAsync<InvalidOperationException>();
        (await new BrokerAccountingIntentStore(Transactions()).ReadAsync(command.PortfolioId,command.OperationId,hash))
            .Should().BeNull();
        (await new BrokerAccountingIntentStore(Transactions()).ClaimAsync(command,hash)).OperationId.Should().Be(command.OperationId);
    }

    [Fact]
    public async Task Existing_audited_command_without_intent_requires_reconciliation()
    {
        var command=await Command();
        await fixture.EventSourceDb.TryInsertCommandLogAsync(command,DateTime.UtcNow,"{}");
        var failure=await FluentActions.Awaiting(()=>new BrokerAccountingIntentStore(Transactions())
            .ClaimAsync(command,new string('A',64))).Should().ThrowAsync<FinancialOperationException>();
        failure.Which.Code.Should().Be(FinancialReasons.RequestMismatch);
        failure.Which.Message.Should().Contain("reconciliation");
        (await new BrokerAccountingIntentStore(Transactions()).ReadAsync(command.PortfolioId,command.OperationId,new string('A',64)))
            .Should().BeNull();
    }

    [Fact]
    public async Task Corrupted_owned_fixture_transport_hash_is_rejected()
    {
        var command=await Command(); var hash=new string('A',64);
        await new BrokerAccountingIntentStore(Transactions()).ClaimAsync(command,hash);
        // Alter only this test's freshly allocated synthetic intent.
        await Transactions().ExecuteAsync(async (db,ct) =>
        {
            await db.ExecuteAsync("UPDATE portfolio_financial.broker_accounting_intent SET command_hash=$1 WHERE portfolio_id=$2 AND operation_id=$3;",
                [new string('0',64),command.PortfolioId,command.OperationId],ct);
            return true;
        });
        var failure=await FluentActions.Awaiting(()=>new BrokerAccountingIntentStore(Transactions())
            .ReadAsync(command.PortfolioId,command.OperationId,hash)).Should().ThrowAsync<FinancialOperationException>();
        failure.Which.Code.Should().Be(FinancialReasons.RequestMismatch);
    }
}
