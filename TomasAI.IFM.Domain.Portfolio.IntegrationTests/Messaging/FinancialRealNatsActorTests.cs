using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Logging;
using NATS.Net;
using NATS.Client.Core;
using NSubstitute;
using TomasAI.IFM.Application.Api.Nats.Client;
using TomasAI.IFM.Application.EventProjector.Contracts;
using TomasAI.IFM.Application.Storage.PortfolioFinancial;
using TomasAI.IFM.Domain.Portfolio.CapacityReservation.Function.Actor;
using TomasAI.IFM.Domain.Portfolio.CapacityReservation.Function.State;
using TomasAI.IFM.Domain.Portfolio.GeneralLedger.Command;
using TomasAI.IFM.Domain.Portfolio.GeneralLedger.Command.Actor;
using TomasAI.IFM.Domain.Portfolio.GeneralLedger.Model;
using TomasAI.IFM.Domain.Portfolio.IntegrationTests.Persistence;
using TomasAI.IFM.Domain.Portfolio.Shared.Financial;
using TomasAI.IFM.Framework.Messaging.NatsJetStream;
using TomasAI.IFM.Framework.Messaging.NatsJetStream.Serializers;
using TomasAI.IFM.Framework.SequenceId;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using static TomasAI.IFM.Domain.Portfolio.IntegrationTests.Persistence.GeneralLedgerPostingIntegrationTests;

namespace TomasAI.IFM.Domain.Portfolio.IntegrationTests.Messaging;

[Collection("PortfolioFinancialDatabase"),Trait("Category","PortfolioFinancialNats"),Trait("Gate","PF-FIN-02")]
public sealed class FinancialRealNatsActorTests(PortfolioEventStoreFixture fixture):IClassFixture<PortfolioEventStoreFixture>
{
    sealed class CaptureLogger<T>:ILogger<T>
    {
        public List<string> Errors { get; }=[];
        public IDisposable? BeginScope<TState>(TState state) where TState:notnull=>null;
        public bool IsEnabled(LogLevel level)=>true;
        public void Log<TState>(LogLevel level,EventId eventId,TState state,Exception? error,Func<TState,Exception?,string> formatter)
        { if(error is not null) Errors.Add(error.ToString()); }
    }
    // Deliberately no fallback to the application's broker: its wildcard subscriptions could route tests into production storage.
    static string Url=>Environment.GetEnvironmentVariable("IFM_FINANCIAL_TEST_NATS_URL")
        ??throw new InvalidOperationException("Set IFM_FINANCIAL_TEST_NATS_URL to an isolated test broker.");

    [Fact]
    public async Task Typed_capacity_api_runs_actual_function_and_replays_postgres_completion_over_real_nats()
    {
        var book=await CapacityReservationIntegrationTests.FundedBook();
        var request=await CapacityReservationIntegrationTests.ReserveRequest(book,700);
        using var deadline=new CancellationTokenSource(TimeSpan.FromSeconds(30));
        await using var server=new NatsClient(Url); await server.ConnectAsync();
        await using var subscription=await server.Connection.SubscribeCoreAsync<byte[]>(request.Subject.ToString(),serializer:new NatsByteArrayMessageSerializer());
        await server.Connection.PingAsync(deadline.Token);
        var producer=new NatsActorProducer(new NatsProducerOptions { Url=Url },NullLogger.Instance);
        await producer.StartAsync(new(ActorType.Function,$"FinancialTest{Guid.NewGuid():N}"),deadline.Token);
        try
        {
            var api=new PortfolioFinancialApi(producer);
            var repository=new CapacityReservationFunctionStateRepository(new PortfolioFinancialDbContext(Transactions()),new CapacityReservationStore(Transactions()));
            Guid? first=null;
            for(var index=0;index<2;index++)
            {
                var pending=api.ReserveAsync(request,deadline.Token).AsTask();
                var message=await subscription.Msgs.ReadAsync(deadline.Token);
                using var actorMessage=new NatsActorMessage(message);
                await new CapacityReservationFunctionActor(new CapacityFunctionActorIntegrationTests.Context(repository)).HandleMessageAsync(actorMessage);
                var result=await pending.WaitAsync(deadline.Token);
                result.Success.Should().BeTrue(); result.Value!.Completed.Should().NotBeNull(result.Value.Failed?.Message);
                var completed=result.Value.Completed!;
                completed.CommandId.Should().Be(request.CommandId); completed.CorrelationId.Should().Be(request.CorrelationId);
                completed.Receipt.FinancialRevision.Should().Be(2);
                if(first is null) first=completed.Id; else completed.Id.Should().Be(first.Value);
            }
        }
        finally { await producer.StopAsync(); }
    }

    [Fact]
    public async Task Audited_but_uncommitted_command_resumes_and_replay_checks_receipt_even_when_notification_is_down()
    {
        var book=await CreateBook(); var request=Request(book,LedgerTransactionKind.DepositConfirmed,100,0);
        (await fixture.EventSourceDb.TryReserveAsync(request)).Accepted.Should().BeTrue();
        using var deadline=new CancellationTokenSource(TimeSpan.FromSeconds(30));
        await using var server=new NatsClient(Url); await server.ConnectAsync();
        await using var subscription=await server.Connection.SubscribeCoreAsync<byte[]>(request.Subject.ToString(),serializer:new NatsByteArrayMessageSerializer());
        await server.Connection.PingAsync(deadline.Token);
        var producer=new NatsActorProducer(new NatsProducerOptions { Url=Url },NullLogger.Instance);
        await producer.StartAsync(new(ActorType.Command,$"FinancialTest{Guid.NewGuid():N}"),deadline.Token);
        var context=Substitute.For<ICommandActorContext<GeneralLedgerCommandActor>>();
        context.ActorId.Returns(new ActorMailboxId(ActorType.Command,GeneralLedgerCommandActor.ActorName));
        var container=Substitute.For<IContainerInstance>(); context.Container.Returns(container);
        container.Resolve<ICommandAuditLogger>().Returns(fixture.EventSourceDb);
        var supervisor=Substitute.For<IActorSupervisor>();
        supervisor.CreateMailbox(context.ActorId).Returns(Substitute.For<IActorMailbox>());
        supervisor.GetProducer(context.ActorId).Returns(Substitute.For<IActorProducer>());
        var sequences=Substitute.For<ISequenceIdGenerator>();
        sequences.GetSequenceIdAsync(Arg.Any<SequenceName>(),Arg.Any<CancellationToken>()).Returns(_=>ValueTask.FromResult(Random.Shared.NextInt64(100000,long.MaxValue)));
        var projector=Substitute.For<IEventProjector<GeneralLedgerCommandActor>>();
        projector.DomainEventsProjectionAsync(Arg.Any<DomainEventCollection>()).Returns(_=>throw new IOException("Injected post-commit publication outage"));
        var actorLogger=new CaptureLogger<GeneralLedgerCommandActor>();
        var actor=new GeneralLedgerCommandActor(context,new(new GeneralLedgerStore(Transactions()),new PortfolioFinancialDbContext(Transactions()),
            new(sequences),projector,actorLogger),actorLogger);
        await actor.StartAsync(supervisor,deadline.Token);
        try
        {
            var api=new PortfolioFinancialApi(producer);
            var first=await Send(request); first.Success.Should().BeTrue(first.ErrorMessage+string.Join(Environment.NewLine,actorLogger.Errors)); first.Value!.Guid.Should().Be(request.OperationId);
            var receipt=await new PortfolioFinancialDbContext(Transactions()).ReadOperationAsync<LedgerPostingCompletedEvent>(book.PortfolioId,request.OperationId);
            receipt.Should().NotBeNull();
            var replay=await Send(request); replay.Success.Should().BeTrue(); replay.Value!.Guid.Should().Be(request.OperationId);
            var changed=request with { Body=request.Body with { Amount=200 } }; changed=changed with { InputSha256=FinancialCanonicalHash.Request(changed) };
            var conflict=await Send(changed); conflict.Success.Should().BeFalse(); conflict.ErrorCode.Should().Be(FinancialReasons.RequestMismatch);
            var stored=await new PortfolioFinancialDbContext(Transactions()).ReadOperationAsync<LedgerPostingCompletedEvent>(book.PortfolioId,request.OperationId);
            stored!.Id.Should().Be(receipt!.Id);
            var balances=await new FinancialQueryStore(Transactions()).ReadAsync(new() { PortfolioId=book.PortfolioId,Access=request.Access },new GetAccountBalancesRequest());
            balances.Value!.AvailableCash.Should().Be(100); balances.FinancialRevision.Should().Be(1);

            async Task<ServiceResult<GuidResult>> Send(PostFundTransactionCommand command)
            {
                var pending=api.PostAsync(command,deadline.Token).AsTask();
                using var message=new NatsActorMessage(await subscription.Msgs.ReadAsync(deadline.Token));
                await actor.HandleMessageAsync(message,message.Subject.ThreadId,deadline.Token);
                return await pending.WaitAsync(deadline.Token);
            }
        }
        finally { await actor.StopAsync(); await producer.StopAsync(); }
    }
}


