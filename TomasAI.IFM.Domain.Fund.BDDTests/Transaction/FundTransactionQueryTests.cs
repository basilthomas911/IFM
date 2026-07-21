using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.Storage.ScyllaDb.FundDb;
using TomasAI.IFM.Domain.Fund.Shared;
using TomasAI.IFM.Domain.Fund.Shared.Queries;
using TomasAI.IFM.Domain.Fund.Shared.ViewModels;
using TomasAI.IFM.Domain.Fund.Transaction.Query;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Domain.Fund.Transaction.Query.Actor;

namespace TomasAI.IFM.Domain.Fund.BDDTests.Transaction;

/// <summary>
/// BDD-style tests for <see cref="FundTransactionQueryActor"/> query handlers, exercising the query dispatch
/// pipeline (ReceiveAsync) end to end against a stubbed <see cref="IDbContextFactory"/>/<see cref="IFundDbContext"/>,
/// covering both happy paths and edge cases for every registered fund transaction query.
/// </summary>
public class FundTransactionQueryTests
{
    // Test helper to expose the protected ReceiveAsync method for BDD-style testing.
    class TestableFundTransactionQueryActor(IDbContextFactory dbFactory, ILogger<FundTransactionQueryActor> logger)
        : FundTransactionQueryActor(dbFactory, logger)
    {
        public async ValueTask InvokeReceiveAsync(IQueryActorContext context, IQuery query)
            => await ReceiveAsync(context, query);
    }

    static TestableFundTransactionQueryActor CreateActor(IDbContextFactory dbFactory)
        => new(dbFactory, Substitute.For<ILogger<FundTransactionQueryActor>>());

    static (IDbContextFactory DbFactory, IFundDbContext FundDb) CreateDbFactory()
    {
        var fundDb = Substitute.For<IFundDbContext>();
        var dbFactory = Substitute.For<IDbContextFactory>();
        dbFactory.FundDb.Returns(fundDb);
        return (dbFactory, fundDb);
    }

    static IQueryActorContext CreateContext()
    {
        var context = Substitute.For<IQueryActorContext>();
        context.SetMessageInfo(Arg.Any<ActorThreadId>(), Arg.Any<string>(), Arg.Any<ActorMessageInfo>()).Returns(true);
        return context;
    }

    static TQuery WithSubject<TQuery>(TQuery query, string verb) where TQuery : IQuery
    {
        var subjectProp = typeof(TQuery).GetProperty(nameof(IQuery.Subject));
        var entityId = (query.EntityId!).Format();
        var subject = new ActorSubject(ActorType.Query, FundTransactionQueryActor.ActorName, verb, entityId);
        subjectProp!.SetValue(query, subject);
        return query;
    }

    #region GetFundTransactionsQuery - happy paths

    [Fact]
    public async Task GetFundTransactionsQuery_GivenExistingTransactionsInRange_WhenExecuted_ThenRepliesWithTransactions()
    {
        // Arrange - Given fund transactions exist within the requested date range
        var (dbFactory, fundDb) = CreateDbFactory();
        var startDate = SampleData.FundTransaction.ValueDate;
        var endDate = startDate.AddDays(1);
        ICollection<FundTransactionReadModel> transactions = [SampleData.FundTransaction];
        fundDb.GetFundTransactionsAsync(SampleData.FundTransaction.FundId, startDate, endDate).Returns(Task.FromResult(transactions));
        var actor = CreateActor(dbFactory);
        var context = CreateContext();
        var query = WithSubject(new GetFundTransactionsQuery(SampleData.FundTransaction.FundId, startDate, endDate), GetFundTransactionsQuery.Verb);

        // Act - When the query is executed
        await actor.InvokeReceiveAsync(context, query);

        // Assert - Then reply with the matching fund transactions
        await context.Received(1).ReplyAsync(
            Arg.Is<ActorThreadId>(id => id == query.Subject.ThreadId),
            Arg.Is<string>(v => v == GetFundTransactionsQuery.Verb),
            Arg.Is<ServiceResult<ICollection<FundTransactionReadModel>>>(r =>
                r.Success && r.Value!.Count == 1 && r.Value.First().TransactionId == SampleData.FundTransaction.TransactionId));
    }

    [Fact]
    public async Task GetFundTransactionsQuery_GivenMultipleTransactions_WhenExecuted_ThenRepliesWithAllTransactions()
    {
        // Arrange - Given multiple fund transactions exist for the fund
        var (dbFactory, fundDb) = CreateDbFactory();
        var startDate = SampleData.FundTransaction.ValueDate;
        var endDate = startDate.AddDays(5);
        ICollection<FundTransactionReadModel> transactions =
        [
            SampleData.FundTransaction,
            SampleData.FundTransaction with { TransactionId = 2, TransactionType = FundTransactionType.TradeCommission }
        ];
        fundDb.GetFundTransactionsAsync(SampleData.FundTransaction.FundId, startDate, endDate).Returns(Task.FromResult(transactions));
        var actor = CreateActor(dbFactory);
        var context = CreateContext();
        var query = WithSubject(new GetFundTransactionsQuery(SampleData.FundTransaction.FundId, startDate, endDate), GetFundTransactionsQuery.Verb);

        // Act - When the query is executed
        await actor.InvokeReceiveAsync(context, query);

        // Assert - Then reply with both fund transactions
        await context.Received(1).ReplyAsync(
            Arg.Is<ActorThreadId>(id => id == query.Subject.ThreadId),
            Arg.Is<string>(v => v == GetFundTransactionsQuery.Verb),
            Arg.Is<ServiceResult<ICollection<FundTransactionReadModel>>>(r => r.Success && r.Value!.Count == 2));
    }

    #endregion

    #region GetFundTransactionsQuery - edge cases

    [Fact]
    public async Task GetFundTransactionsQuery_GivenNoTransactionsExist_WhenExecuted_ThenRepliesWithEmptyCollection()
    {
        // Arrange - Given no fund transactions exist for the requested fund/date range (edge case)
        var (dbFactory, fundDb) = CreateDbFactory();
        fundDb.GetFundTransactionsAsync(Arg.Any<int>(), Arg.Any<DateOnly>(), Arg.Any<DateOnly>())
            .Returns(Task.FromResult<ICollection<FundTransactionReadModel>>([]));
        var actor = CreateActor(dbFactory);
        var context = CreateContext();
        var query = WithSubject(new GetFundTransactionsQuery(9999, DateOnly.MinValue, DateOnly.MaxValue), GetFundTransactionsQuery.Verb);

        // Act - When the query is executed
        await actor.InvokeReceiveAsync(context, query);

        // Assert - Then reply with an empty collection without throwing
        await context.Received(1).ReplyAsync(
            Arg.Is<ActorThreadId>(id => id == query.Subject.ThreadId),
            Arg.Is<string>(v => v == GetFundTransactionsQuery.Verb),
            Arg.Is<ServiceResult<ICollection<FundTransactionReadModel>>>(r => r.Success && r.Value!.Count == 0));
    }

    [Fact]
    public async Task GetFundTransactionsQuery_GivenSameStartAndEndDate_WhenExecuted_ThenRepliesWithMatchingTransactions()
    {
        // Arrange - Given the start and end date are identical (single-day edge case)
        var (dbFactory, fundDb) = CreateDbFactory();
        var singleDate = SampleData.FundTransaction.ValueDate;
        ICollection<FundTransactionReadModel> transactions = [SampleData.FundTransaction];
        fundDb.GetFundTransactionsAsync(SampleData.FundTransaction.FundId, singleDate, singleDate).Returns(Task.FromResult(transactions));
        var actor = CreateActor(dbFactory);
        var context = CreateContext();
        var query = WithSubject(new GetFundTransactionsQuery(SampleData.FundTransaction.FundId, singleDate, singleDate), GetFundTransactionsQuery.Verb);

        // Act - When the query is executed
        await actor.InvokeReceiveAsync(context, query);

        // Assert - Then reply with the transaction found on that single day
        await context.Received(1).ReplyAsync(
            Arg.Is<ActorThreadId>(id => id == query.Subject.ThreadId),
            Arg.Is<string>(v => v == GetFundTransactionsQuery.Verb),
            Arg.Is<ServiceResult<ICollection<FundTransactionReadModel>>>(r => r.Success && r.Value!.Count == 1));
    }

    [Fact]
    public async Task GetFundTransactionsQuery_GivenNonExistingFundId_WhenExecuted_ThenRepliesWithEmptyCollection()
    {
        // Arrange - Given a fund id that does not exist (edge case)
        var (dbFactory, fundDb) = CreateDbFactory();
        fundDb.GetFundTransactionsAsync(Arg.Any<int>(), Arg.Any<DateOnly>(), Arg.Any<DateOnly>())
            .Returns(Task.FromResult<ICollection<FundTransactionReadModel>>([]));
        var actor = CreateActor(dbFactory);
        var context = CreateContext();
        var query = WithSubject(
            new GetFundTransactionsQuery(9999, SampleData.FundTransaction.ValueDate, SampleData.FundTransaction.ValueDate.AddDays(1)),
            GetFundTransactionsQuery.Verb);

        // Act - When the query is executed
        await actor.InvokeReceiveAsync(context, query);

        // Assert - Then reply with an empty collection without throwing
        await context.Received(1).ReplyAsync(
            Arg.Is<ActorThreadId>(id => id == query.Subject.ThreadId),
            Arg.Is<string>(v => v == GetFundTransactionsQuery.Verb),
            Arg.Is<ServiceResult<ICollection<FundTransactionReadModel>>>(r => r.Success && r.Value!.Count == 0));
    }

    #endregion
}
