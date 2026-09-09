using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.Storage.FundDb;
using TomasAI.IFM.Application.Storage.PortfolioFinancial;
using TomasAI.IFM.Domain.Fund.Shared;
using TomasAI.IFM.Domain.Fund.Shared.ViewModels;
using TomasAI.IFM.Domain.Portfolio.Command.Model;
using TomasAI.IFM.Domain.Portfolio.Shared.Financial;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Framework.SequenceId;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Shared.Storage;
using static TomasAI.IFM.Domain.Portfolio.IntegrationTests.Persistence.GeneralLedgerPostingIntegrationTests;

namespace TomasAI.IFM.Domain.Portfolio.IntegrationTests.Persistence;

[Collection("PortfolioFinancialDatabase"),Trait("Category","PortfolioFinancial"),Trait("Gate","PF-FIN-06")]
public sealed class LegacyFinancialWriterFenceIntegrationTests(PortfolioEventStoreFixture fixture):IClassFixture<PortfolioEventStoreFixture>
{
    [Fact]
    public async Task Fresh_scope_fence_rejects_old_event_append_and_all_transaction_write_entry_points()
    {
        _=fixture;await new PortfolioFinancialSchema(Transactions()).InitializeAsync();
        var id=Random.Shared.Next(100000,900000000);var qualification=Guid.NewGuid();var fence=new LegacyFinancialWriterFence(Transactions());
        await fence.FreezeFreshScopeAsync(id+1,id,qualification);await fence.FreezeFreshScopeAsync(id+1,id,qualification);
        await FluentActions.Awaiting(()=>new LegacyFinancialWriterFence(Transactions()).BeginWriteAsync([id])).Should().ThrowAsync<FinancialOperationException>();
        foreach(var actor in new[] { "FundTransactionCommand","FundCommand" })
        {
            var command=Guid.NewGuid();
            await FluentActions.Awaiting(()=>Transactions().ExecuteAsync((db,ct)=>db.AppendAsync($"Command.{actor}.{id}.1",command,
                new PortfolioCreated(Guid.NewGuid(),command,1,DateTime.UtcNow,"isolated fence fixture",new()),0,ct)))
                .Should().ThrowAsync<Npgsql.PostgresException>();
        }
        var settings=new DbConnectionSettings().Add(FundDbContext.FundDbConnection,"Contact Points=localhost;Port=9042;Default Keyspace=fund_test_db","System.Data.ScyllaDb");
        var factory=new DbContextFactory(new DbContextResolver(_=>throw new InvalidOperationException("A fenced write must not reach Scylla.")));
        var source=new FundDbContext(settings,factory,Substitute.For<ISequenceIdGenerator>(),Substitute.For<ILogger<DbProvider>>(),fence);
        var row=new FundTransactionReadModel(0,DateTime.UtcNow,FundTransactionType.OpeningTrade,id,1,1,TradeType.LongIronCondor,new(2026,9,8),TradeStatus.Open,"Fenced test",1,1);
        await FluentActions.Awaiting(()=>source.InsertFundTransactionAsync(row)).Should().ThrowAsync<FinancialOperationException>();
        await FluentActions.Awaiting(()=>source.DeleteFundTransactionAsync(id,row.ValueDate,1,1,row.TradeType,row.TransactionType,row.TransactionDate))
            .Should().ThrowAsync<FinancialOperationException>();
        await FluentActions.Awaiting(()=>source.BackfillFundTransactionProjectionsAsync(id,new(2026,9,1),new(2026,9,30)))
            .Should().ThrowAsync<FinancialOperationException>();
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task Pending_or_completed_legacy_write_history_cannot_be_qualified_as_a_fresh_scope(bool completed)
    {
        await new PortfolioFinancialSchema(Transactions()).InitializeAsync();var id=Random.Shared.Next(100000,900000000);
        var fence=new LegacyFinancialWriterFence(Transactions());var ticket=await fence.BeginWriteAsync([id]);
        if(completed) await fence.CompleteWriteAsync(ticket);
        await FluentActions.Awaiting(()=>new LegacyFinancialWriterFence(Transactions()).FreezeFreshScopeAsync(id+1,id,Guid.NewGuid()))
            .Should().ThrowAsync<FinancialOperationException>();
        var saved=await Transactions().ExecuteAsync((db,ct)=>db.ScalarAsync("SELECT state FROM portfolio_financial.legacy_write_intent WHERE ticket_id=$1;",[ticket],ct));
        saved.Should().Be(completed?"Completed":"Pending");
    }

    [Fact]
    public async Task Mixed_fund_write_is_atomic_and_cannot_leave_a_ticket_for_an_unfenced_fund()
    {
        await new PortfolioFinancialSchema(Transactions()).InitializeAsync();var id=Random.Shared.Next(100000,900000000);
        var fence=new LegacyFinancialWriterFence(Transactions());await fence.FreezeFreshScopeAsync(id+2,id+1,Guid.NewGuid());
        await FluentActions.Awaiting(()=>fence.BeginWriteAsync([id,id+1])).Should().ThrowAsync<FinancialOperationException>();
        (await Transactions().ExecuteAsync((db,ct)=>db.ScalarAsync("SELECT count(*) FROM portfolio_financial.legacy_write_intent WHERE fund_id=$1;",[id],ct))).Should().Be(0L);
        await fence.FreezeFreshScopeAsync(id+2,id,Guid.NewGuid());
    }

    [Fact]
    public async Task Retention_fences_new_writes_and_preserves_pending_intents_until_confirmed_completion()
    {
        await new PortfolioFinancialSchema(Transactions()).InitializeAsync();var id=Random.Shared.Next(100000,900000000);
        var firstHost=new LegacyFinancialWriterFence(Transactions());var secondHost=new LegacyFinancialWriterFence(Transactions());
        var ticket=await firstHost.BeginWriteAsync([id]);var retention=Guid.NewGuid();
        (await secondHost.FreezeRetainedScopeAsync(id+1,id,retention)).Should().Be(1);
        await FluentActions.Awaiting(()=>firstHost.BeginWriteAsync([id])).Should().ThrowAsync<FinancialOperationException>();
        await firstHost.CompleteWriteAsync(ticket);
        (await new LegacyFinancialWriterFence(Transactions()).FreezeRetainedScopeAsync(id+1,id,retention)).Should().Be(0);
        await FluentActions.Awaiting(()=>secondHost.FreezeRetainedScopeAsync(id+1,id,Guid.NewGuid())).Should().ThrowAsync<FinancialOperationException>();
        (await Transactions().ExecuteAsync((db,ct)=>db.ScalarAsync("SELECT count(*) FROM portfolio_financial.legacy_write_intent WHERE ticket_id=$1 AND state='Completed';",[ticket],ct))).Should().Be(1L);
        // A retained source must never be presented as independently verified empty.
        (await Transactions().ExecuteAsync((db,ct)=>db.ScalarAsync("SELECT empty_verified FROM portfolio_financial.legacy_writer_scope WHERE fund_id=$1;",[id],ct))).Should().Be(false);
    }

    [Fact]
    public async Task Independent_connections_cannot_both_start_a_legacy_write_and_freeze_the_same_fresh_scope()
    {
        await new PortfolioFinancialSchema(Transactions()).InitializeAsync();var id=Random.Shared.Next(100000,900000000);
        async Task<bool> Write()
        { try { await new LegacyFinancialWriterFence(Transactions()).BeginWriteAsync([id]);return true; } catch(FinancialOperationException) { return false; } }
        async Task<bool> Freeze()
        { try { await new LegacyFinancialWriterFence(Transactions()).FreezeFreshScopeAsync(id+1,id,Guid.NewGuid());return true; } catch(FinancialOperationException) { return false; } }
        var results=await Task.WhenAll(Write(),Freeze());results.Count(x=>x).Should().Be(1);
    }
}
