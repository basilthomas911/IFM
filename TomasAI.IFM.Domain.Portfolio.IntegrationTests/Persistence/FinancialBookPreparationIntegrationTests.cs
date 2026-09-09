using FluentAssertions;
using NSubstitute;
using TomasAI.IFM.Application.Storage.PortfolioFinancial;
using TomasAI.IFM.Domain.Portfolio.Command.State;
using TomasAI.IFM.Domain.Portfolio.GeneralLedger.Command;
using TomasAI.IFM.Domain.Portfolio.GeneralLedger.Model;
using TomasAI.IFM.Domain.Portfolio.GeneralLedger.Query;
using TomasAI.IFM.Domain.Portfolio.Persistence;
using TomasAI.IFM.Domain.Portfolio.Shared.Contracts;
using TomasAI.IFM.Domain.Portfolio.Shared.Financial;
using TomasAI.IFM.Framework.SequenceId;
using TomasAI.IFM.Shared.Validation;
using static TomasAI.IFM.Domain.Portfolio.IntegrationTests.Persistence.GeneralLedgerPostingIntegrationTests;

namespace TomasAI.IFM.Domain.Portfolio.IntegrationTests.Persistence;

[Collection("PortfolioFinancialDatabase"),Trait("Category","PortfolioFinancial"),Trait("Gate","PF-FIN-06")]
public sealed class FinancialBookPreparationIntegrationTests(PortfolioEventStoreFixture fixture):IClassFixture<PortfolioEventStoreFixture>
{
    [Fact]
    public async Task Fresh_development_book_qualifies_after_explicit_capital_reconciliation_and_real_legacy_absence_checks()
    {
        var (preparation,scope,sources)=await Setup();
        var draft=(await preparation.PrepareAsync(scope,new($"DEV-ACCOUNT-{scope.PortfolioId}",new(2026,1,1),new(2026,12,31)),default)).Value!.Draft!;
        var book=draft.Book!;var store=new LedgerConfigurationStore(Transactions(),new(true));
        var create=LedgerConfigurationIntegrationTests.Command(book,0,draft with { Reason="Development setup" });
        await store.ConfigureAsync(create,create.Complete,FinancialCanonicalHash.Compute);
        var rule=draft.Rules.Single(x=>x.Kind==LedgerTransactionKind.OpeningBalance);
        var template=Request(book,LedgerTransactionKind.DepositConfirmed,100,1);
        var opening=template with { Body=template.Body with { TransactionKind=LedgerTransactionKind.OpeningBalance,
            PostingRule=new() { RuleId=rule.RuleId,Version=rule.Version,ContentHash=rule.ContentHash },Source=template.Body.Source with { System="DevelopmentOpeningCapital" } } };
        opening=opening with { InputSha256=FinancialCanonicalHash.Request(opening) };
        await new GeneralLedgerStore(Transactions(),new(true)).PostAsync(opening,
            [new(Random.Shared.NextInt64(100000,long.MaxValue),Random.Shared.NextInt64(100000,long.MaxValue),opening.Body)],
            (body,selected,prior,original,remaining)=>LedgerPostingModel.Calculate(body,selected,prior,original,remaining,true),opening.Complete);
        var reconcile=LedgerConfigurationIntegrationTests.Command(book,2,new() { Action=LedgerConfigurationAction.Reconcile,BookId=book.BookId,SourceCut="development-capital:100",Reason="Independent journal reconciliation" });
        await store.ConfigureAsync(reconcile,reconcile.Complete,FinancialCanonicalHash.Compute);
        var command=LedgerConfigurationIntegrationTests.Command(book,3,new() { Action=LedgerConfigurationAction.QualifyDevelopmentBook,BookId=book.BookId,Book=book,
            ReconciliationId=reconcile.OperationId,SourceCut=reconcile.Body.SourceCut,Reason="Qualify fresh development scope" });
        await FluentActions.Awaiting(()=>store.ConfigureAsync(command,command.Complete,FinancialCanonicalHash.Compute)).Should().ThrowAsync<FinancialOperationException>();

        var settings=new TomasAI.IFM.Shared.Storage.DbConnectionSettings().Add(TomasAI.IFM.Application.Storage.FundDb.FundDbContext.FundDbConnection,
            "Contact Points=localhost;Port=9042;Default Keyspace=fund_test_db","System.Data.ScyllaDb");
        var logger=Substitute.For<Microsoft.Extensions.Logging.ILogger<TomasAI.IFM.Framework.Storage.DbProvider>>();
        await new TomasAI.IFM.Application.Storage.FundDb.Schema.FundSchemaDb(settings,logger).CreateAllAsync();
        var repositories=new Dictionary<Type,object>();
        var factory=new TomasAI.IFM.Application.Storage.DbContextFactory(new TomasAI.IFM.Application.Storage.DbContextResolver(type=>repositories[type]));
        var fence=new LegacyFinancialWriterFence(Transactions());
        var legacy=new TomasAI.IFM.Application.Storage.FundDb.FundDbContext(settings,factory,Substitute.For<ISequenceIdGenerator>(),logger,fence);
        repositories.Add(typeof(TomasAI.IFM.Framework.Storage.IObjectRepository<TomasAI.IFM.Application.Storage.FundDb.FundDbContext>),legacy);
        var services=new LedgerConfigurationCommandServices(store,new PortfolioFinancialDbContext(Transactions()),sources,
            Substitute.For<TomasAI.IFM.Application.EventProjector.Contracts.IEventProjector<TomasAI.IFM.Domain.Portfolio.GeneralLedger.Command.Actor.LedgerConfigurationCommandActor>>(),
            Substitute.For<Microsoft.Extensions.Logging.ILogger<TomasAI.IFM.Domain.Portfolio.GeneralLedger.Command.Actor.LedgerConfigurationCommandActor>>(),fence,legacy,new(true));
        await command.PrepareDevelopmentQualificationAsync(services,default);
        await FluentActions.Awaiting(()=>new LedgerConfigurationStore(Transactions()).ConfigureAsync(command,command.Complete,FinancialCanonicalHash.Compute))
            .Should().ThrowAsync<FinancialOperationException>();
        var result=await store.ConfigureAsync(command,command.Complete,FinancialCanonicalHash.Compute);
        result.Receipt.OperatingState.Should().Be("NeedsRefresh");
        var saved=(await new PortfolioFinancialDbContext(Transactions()).ReadBookAsync(book.PortfolioId))!;
        saved.MigrationQualified.Should().BeTrue();saved.Funds.Should().OnlyContain(x=>!x.CanSpend);
        var migration=await Transactions().ExecuteAsync((db,ct)=>db.ScalarAsync("SELECT cutover_state FROM portfolio_financial.ledger_migration WHERE migration_id=$1;",[command.OperationId],ct));
        migration.Should().Be("Qualified");
        var mutation=await FluentActions.Awaiting(()=>Transactions().ExecuteAsync((db,ct)=>db.ExecuteAsync(
            "UPDATE portfolio_financial.ledger_migration SET manifest_hash='changed' WHERE migration_id=$1;",[command.OperationId],ct)))
            .Should().ThrowAsync<Npgsql.PostgresException>();
        mutation.Which.SqlState.Should().Be("23514");
        (await new LedgerConfigurationStore(Transactions()).ConfigureAsync(command,command.Complete,FinancialCanonicalHash.Compute)).Id.Should().Be(result.Id);
        await FluentActions.Awaiting(()=>fence.BeginWriteAsync([book.Funds[0].FundId])).Should().ThrowAsync<FinancialOperationException>();
    }

    [Fact]
    public async Task Authority_preparation_uses_committed_sources_and_rejects_a_later_financial_revision()
    {
        var (preparation,scope,sources)=await Setup();
        var draft=(await preparation.PrepareAsync(scope,new($"DEV-ACCOUNT-{scope.PortfolioId}",new(2026,1,1),new(2026,12,31)),default)).Value!.Draft!;
        var store=new LedgerConfigurationStore(Transactions());
        var create=LedgerConfigurationIntegrationTests.Command(draft.Book!,0,draft with { Reason="Test preparation" });
        await store.ConfigureAsync(create,create.Complete,FinancialCanonicalHash.Compute);
        var query=new FinancialAuthorityPreparation(new(Transactions()),sources,Substitute.For<TomasAI.IFM.Application.Storage.ConfigurationDb.IConfigurationDbContext>());
        var review=await query.PrepareAsync(scope,new(true),default);
        review.FinancialRevision.Should().Be(1);review.Value!.Draft.Book!.Funds.Should().OnlyContain(x=>!x.CanSpend);
        review.Value.Notes.Should().ContainSingle(x=>x.Contains("new spending disabled"));
        var refresh=LedgerConfigurationIntegrationTests.Command(draft.Book!,1,review.Value.Draft with { Reason="Refresh current sources" });
        await refresh.ValidateAuthoritySourcesAsync(sources,default);
        var reconcile=LedgerConfigurationIntegrationTests.Command(draft.Book!,1,new() { Action=LedgerConfigurationAction.Reconcile,BookId=draft.BookId,SourceCut="empty:1",Reason="Intervening revision" });
        await store.ConfigureAsync(reconcile,reconcile.Complete,FinancialCanonicalHash.Compute);
        (await FluentActions.Awaiting(()=>store.ConfigureAsync(refresh,refresh.Complete,FinancialCanonicalHash.Compute)).Should().ThrowAsync<FinancialOperationException>())
            .Which.Code.Should().Be(FinancialReasons.RevisionConflict);
        var fresh=await query.PrepareAsync(scope,new(),default);
        refresh=LedgerConfigurationIntegrationTests.Command(draft.Book!,fresh.FinancialRevision,fresh.Value!.Draft with { Reason="Fresh sources" });
        await refresh.ValidateAuthoritySourcesAsync(sources,default);
        (await store.ConfigureAsync(refresh,refresh.Complete,FinancialCanonicalHash.Compute)).Receipt.OperatingState.Should().Be("Importing");
        (await new PortfolioFinancialDbContext(Transactions()).ReadBookAsync(scope.PortfolioId))!.Funds.Should().OnlyContain(x=>!x.CanSpend);
        await FluentActions.Awaiting(()=>query.PrepareAsync(scope with { Access=new("reader",["LedgerRead"],[scope.PortfolioId]) },new(),default))
            .Should().ThrowAsync<FinancialOperationException>();
    }

    [Fact]
    public async Task Prepared_book_uses_committed_membership_and_generated_keys_without_posting_or_enabling_capital()
    {
        var (preparation,scope,store)=await Setup();
        var choices=await preparation.PrepareAsync(scope,new(),default);
        choices.Value!.ExecutionAccounts.Should().Equal($"DEV-ACCOUNT-{scope.PortfolioId}");choices.Value.Draft.Should().BeNull();
        var result=await preparation.PrepareAsync(scope,new($"DEV-ACCOUNT-{scope.PortfolioId}",new(2026,1,1),new(2026,12,31)),default);
        var draft=result.Value!.Draft!;
        draft.Accounts.Should().HaveCount(6);draft.Accounts.Select(x=>x.AccountId).Should().OnlyHaveUniqueItems();
        draft.Book!.MigrationQualified.Should().BeFalse();draft.Book.Funds.Should().OnlyContain(x=>!x.CanSpend);
        draft.Rules.Should().NotContain(x=>x.Kind==LedgerTransactionKind.TradeSettlement);
        var command=LedgerConfigurationIntegrationTests.Command(draft.Book,0,draft with { Reason="Review and create development configuration" });
        new List<ValidationError>().ValidateLedgerConfiguration(command).Should().BeEmpty();
        await command.ValidateAuthoritySourcesAsync(store,default);
        await new LedgerConfigurationStore(Transactions()).ConfigureAsync(command,command.Complete,FinancialCanonicalHash.Compute);
        var read=await new FinancialQueryStore(Transactions()).ReadAsync(scope,new GetFinancialLedgerConfigurationRequest());
        read.Value!.OperatingState.Should().Be("Importing");read.Value.Rules.Should().HaveCount(10);
        var journals=await Transactions().ExecuteAsync((db,ct)=>db.ScalarAsync("SELECT count(*) FROM portfolio_financial.ledger_journal WHERE book_id=$1;",[draft.BookId],ct));
        journals.Should().Be(0L);
        await FluentActions.Awaiting(()=>preparation.PrepareAsync(scope,new($"DEV-ACCOUNT-{scope.PortfolioId}",new(2026,1,1),new(2026,12,31)),default))
            .Should().ThrowAsync<FinancialOperationException>();
    }

    [Theory]
    [InlineData(false,"DEV-ACCOUNT",false)]
    [InlineData(true,"UNASSIGNED",false)]
    [InlineData(true,"DEV-ACCOUNT",true)]
    public async Task Preparation_rejects_non_development_unassigned_account_and_unauthorized_scope(bool development,string account,bool forbidden)
    {
        var (preparation,scope,_)=await Setup(development);
        if(forbidden) scope=scope with { Access=new("reader",["LedgerRead"],[scope.PortfolioId]) };
        await FluentActions.Awaiting(()=>preparation.PrepareAsync(scope,new(account=="DEV-ACCOUNT"?$"DEV-ACCOUNT-{scope.PortfolioId}":account,new(2026,1,1),new(2026,12,31)),default))
            .Should().ThrowAsync<FinancialOperationException>();
        (await new PortfolioFinancialDbContext(Transactions()).ReadBookAsync(scope.PortfolioId)).Should().BeNull();
    }

    [Fact]
    public async Task Prepared_book_rejects_membership_changes_before_commit()
    {
        var (preparation,scope,store)=await Setup();
        var draft=(await preparation.PrepareAsync(scope,new($"DEV-ACCOUNT-{scope.PortfolioId}",new(2026,1,1),new(2026,12,31)),default)).Value!.Draft!;
        var current=await store.LoadPortfolioAsync(new(scope.PortfolioId));
        var change=current.AddVersion(Guid.NewGuid(),current.Revision,current.Current! with { PortfolioVersion=2,Name="Updated" },DateTime.UtcNow,"test");
        await store.AppendPortfolioAsync(new(scope.PortfolioId),change,current.Revision-1);
        var command=LedgerConfigurationIntegrationTests.Command(draft.Book!,0,draft with { Reason="Stale draft must not commit" });
        await FluentActions.Awaiting(()=>command.ValidateAuthoritySourcesAsync(store,default)).Should().ThrowAsync<FinancialOperationException>();
        await FluentActions.Awaiting(()=>new LedgerConfigurationStore(Transactions()).ConfigureAsync(command,command.Complete,FinancialCanonicalHash.Compute))
            .Should().ThrowAsync<FinancialOperationException>();
        (await new PortfolioFinancialDbContext(Transactions()).ReadBookAsync(scope.PortfolioId)).Should().BeNull();
    }

    async Task<(FinancialBookPreparation Preparation,FinancialReadScope Scope,PortfolioEventStore Store)> Setup(bool development=true)
    {
        await new PortfolioFinancialSchema(Transactions()).InitializeAsync();
        var id=Random.Shared.Next(100000,900000000);var now=DateTime.UtcNow;var store=new PortfolioEventStore(fixture.EventSourceDb);
        var portfolio=new PortfolioAggregate();
        var created=portfolio.Create(Guid.NewGuid(),new() { PortfolioId=id,PortfolioVersion=1,Name="Book setup test",OperatingState=PortfolioOperatingState.Draft,
            BrokerAccountRefs=[$"DEV-ACCOUNT-{id}"],EffectiveFromUtc=now,CreatedOnUtc=now,CreatedBy="test" },now,"test");
        await store.AppendPortfolioAsync(new(id),created,0);
        var added=portfolio.AddFund(Guid.NewGuid(),1,new(id,id+1),now,"test");await store.AppendPortfolioAsync(new(id),added,1);
        var fund=new PortfolioFundAggregate();
        var mandate=fund.Create(Guid.NewGuid(),new() { PortfolioId=id,FundId=id+1,FundCode=$"F{id+1}",Name="Development Fund",FundMandateVersion=1,
            TradingYear=2026,OperatingState=FundOperatingState.Draft,DecisionHorizon="Daily",Objective="Development",
            UnderlyingUniverse=["ES"],EligibleAssetTypes=["Futures"],PermittedDirections=["Long"],PermittedConditions=["Trending"],PermittedTradeFamilies=["Futures"],
            EffectiveFromUtc=now,CreatedOnUtc=now,CreatedBy="test" },now,"test");
        await store.AppendFundAsync(new(id,id+1),mandate,0);
        var sequence=Substitute.For<ISequenceIdGenerator>();long next=Random.Shared.Next(100000,900000000);
        sequence.GetSequenceIdAsync(Arg.Any<SequenceName>(),Arg.Any<CancellationToken>()).Returns(_=>new ValueTask<long>(Interlocked.Increment(ref next)));
        var scope=new FinancialReadScope { PortfolioId=id,Access=new("test",["PortfolioAdministrator"]) };
        return(new(store,new PortfolioFinancialDbContext(Transactions()),new(sequence),new(development)),scope,store);
    }
}
