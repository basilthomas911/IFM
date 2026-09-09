using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NATS.Client.Core;
using TomasAI.IFM.Application.Storage.PortfolioFinancial;
using TomasAI.IFM.Domain.Portfolio.CapacityReservation.Function.Actor;
using TomasAI.IFM.Domain.Portfolio.CapacityReservation.Function.State;
using TomasAI.IFM.Domain.Portfolio.GeneralLedger.Model;
using TomasAI.IFM.Domain.Portfolio.Shared.Financial;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Portfolio.IntegrationTests.Persistence;

[Trait("Category","PortfolioFinancial"),Trait("Gate","PF-FIN-04")]
[Collection("PortfolioFinancialDatabase")]
public sealed class CapacityFunctionActorIntegrationTests(PortfolioEventStoreFixture fixture) : IClassFixture<PortfolioEventStoreFixture>
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Real_function_commit_response_loss_recovers_or_returns_unknown_then_replays_exact_capacity(bool refuseRecovery)
    {
        var book=await CapacityReservationIntegrationTests.FundedBook();
        var request=await CapacityReservationIntegrationTests.ReserveRequest(book,700);
        var database=new PortfolioFinancialDbContext(GeneralLedgerPostingIntegrationTests.Transactions());
        await using var proxy=new LostCommitReplyProxy(refuseRecovery);
        var repository=new CapacityReservationFunctionStateRepository(database,
            new CapacityReservationStore(FinancialCommitUncertaintyTests.ProxiedTransactions(proxy.Port)));
        var message=new Message(request);
        await new CapacityReservationFunctionActor(new Context(repository)).HandleMessageAsync(message);
        proxy.Dropped.Should().BeTrue();
        if(refuseRecovery)
        {
            message.Reply!.Value!.Failed!.CommitDisposition.Should().Be(FinancialCommitDisposition.OutcomeUnknown);
            message.Reply.Value.Failed.ErrorCode.Should().Be(FinancialReasons.CommitUnknown);
        }
        else message.Reply!.Value!.Completed.Should().NotBeNull();
        var stored=await database.ReadOperationAsync<CapacityReservationCompletedEvent>(book.PortfolioId,request.OperationId);
        stored.Should().NotBeNull(); stored!.Receipt.FinancialRevision.Should().Be(2);
        var directRepository=new CapacityReservationFunctionStateRepository(database,new CapacityReservationStore(GeneralLedgerPostingIntegrationTests.Transactions()));
        var replay=new Message(request);
        await new CapacityReservationFunctionActor(new Context(directRepository)).HandleMessageAsync(replay);
        replay.Reply!.Value!.Completed!.Id.Should().Be(stored.Id);
        var balances=await new FinancialQueryStore(GeneralLedgerPostingIntegrationTests.Transactions()).ReadAsync(
            new FinancialReadScope { PortfolioId=book.PortfolioId,FundId=book.Funds[0].FundId,Access=new("integration",["PortfolioAdministrator"]) },new GetAccountBalancesRequest());
        balances.Value!.AvailableCash.Should().Be(300); balances.FinancialRevision.Should().Be(2);
    }

    [Fact]
    public async Task Real_actor_maps_commit_and_replay_original_postgres_receipt_and_reject_conflicting_retry()
    {
        var book=await CapacityReservationIntegrationTests.FundedBook();
        var request=await CapacityReservationIntegrationTests.ReserveRequest(book,700);
        var database=new PortfolioFinancialDbContext(GeneralLedgerPostingIntegrationTests.Transactions());
        var repository=new CapacityReservationFunctionStateRepository(database,new CapacityReservationStore(GeneralLedgerPostingIntegrationTests.Transactions()));
        var actor=new CapacityReservationFunctionActor(new Context(repository));
        var first=new Message(request); await actor.HandleMessageAsync(first);
        first.Reply!.Success.Should().BeTrue(first.Reply.Value?.Failed?.Message);
        var completed=first.Reply.Value!.Completed!;
        completed.EventId.Should().BePositive(); completed.Receipt.FinancialRevision.Should().Be(2);
        completed.Receipt.CompletedEventId.Should().Be(completed.Id);
        var replay=new Message(request with { CommandId=Guid.NewGuid() });
        await new CapacityReservationFunctionActor(new Context(repository)).HandleMessageAsync(replay);
        replay.Reply!.Value!.Completed!.Id.Should().Be(completed.Id);
        replay.Reply.Value.Completed.CommandId.Should().Be(request.CommandId);
        var changed=request with { Body=request.Body with { SizedOrderHash=new string('1',64) } };
        changed=changed with { InputSha256=FinancialCanonicalHash.Request(changed) };
        var conflict=new Message(changed); await actor.HandleMessageAsync(conflict);
        conflict.Reply!.Value!.Failed!.ErrorCode.Should().Be(FinancialReasons.RequestMismatch);
        (await database.ReadOperationAsync<CapacityReservationCompletedEvent>(book.PortfolioId,request.OperationId))!.Id.Should().Be(completed.Id);
    }

    internal sealed class Context(IEventSourceFunctionStateRepository<CapacityReservationFunctionState,ReservePortfolioTradeRiskCommand> repository) : ICapacityReservationFunctionContext
    {
        public ActorMailboxId ActorId=>new(ActorType.Function,CapacityReservationFunctionActor.ActorName);
        public IContainerInstance Container=>throw new NotSupportedException();
        public IEventSourceFunctionStateRepository<CapacityReservationFunctionState,ReservePortfolioTradeRiskCommand> StateRepository=>repository;
        public TimeProvider TimeProvider=>TimeProvider.System;
        public ILogger<CapacityReservationFunctionActor> Logger=>NullLogger<CapacityReservationFunctionActor>.Instance;
    }
    sealed class Message(ReservePortfolioTradeRiskCommand request) : IActorMessage
    {
        public ServiceResult<FunctionResult<CapacityReservationCompletedEvent,CapacityReservationFailedEvent>>? Reply { get; private set; }
        public ActorSubject Subject=>request.Subject;
        public ActorSubject ReplySubject { get; set; }
        public T? AsCommand<T>() where T:class,ICommand=>request as T;
        public T? AsEvent<T>() where T:class,IEvent=>default;
        public T? AsQuery<T,TResult>() where T:class,IQuery<TResult> where TResult:class=>default;
        public ValueTask ReplyAsync<T>(T result) where T:class
        { Reply=result as ServiceResult<FunctionResult<CapacityReservationCompletedEvent,CapacityReservationFailedEvent>>; return ValueTask.CompletedTask; }
        public void ReleasePayload() { }
        public NatsMsg<byte[]> GetMessage()=>default;
        public void Dispose() { }
    }
}
