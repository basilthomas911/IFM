using Microsoft.Extensions.Logging.Abstractions;
using NATS.Net;
using NSubstitute;
using TomasAI.IFM.Application.EventProjector.Contracts;
using TomasAI.IFM.Application.Storage.PortfolioFinancial;
using TomasAI.IFM.Domain.Portfolio.CapacityReservation.Function.Actor;
using TomasAI.IFM.Domain.Portfolio.CapacityReservation.Function.State;
using TomasAI.IFM.Domain.Portfolio.GeneralLedger.Command;
using TomasAI.IFM.Domain.Portfolio.GeneralLedger.Command.Actor;
using TomasAI.IFM.Domain.Portfolio.IntegrationTests.Persistence;
using TomasAI.IFM.Framework.Messaging.NatsJetStream;
using TomasAI.IFM.Framework.Messaging.NatsJetStream.Serializers;
using TomasAI.IFM.Framework.SequenceId;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using static TomasAI.IFM.Domain.Portfolio.IntegrationTests.Persistence.GeneralLedgerPostingIntegrationTests;

namespace TomasAI.IFM.Domain.Portfolio.IntegrationTests.Messaging;

/// <summary>Test assembly entry point: isolated one-request actor process, never included in application startup.</summary>
internal static class FinancialProcessHost
{
    public static async Task<int> Main(string[] args)
    {
        if(args.Length!=3 || args[0]!="--financial-test-worker" || args[1] is not ("Reserve" or "Post")) return 2;
        var broker=Environment.GetEnvironmentVariable("IFM_FINANCIAL_TEST_NATS_URL")??throw new InvalidOperationException("An isolated broker is required.");
        using var deadline=new CancellationTokenSource(TimeSpan.FromSeconds(45));
        await using var connection=new NatsClient(broker);await connection.ConnectAsync();
        await using var subscription=await connection.Connection.SubscribeCoreAsync<byte[]>(args[2],serializer:new NatsByteArrayMessageSerializer());
        GeneralLedgerCommandActor? ledger=null;
        if(args[1]=="Post")
        {
            // The parent prepares schema before starting competing hosts.
            var fixture=new PortfolioEventStoreFixture(false);
            var context=Substitute.For<ICommandActorContext<GeneralLedgerCommandActor>>();
            context.ActorId.Returns(new ActorMailboxId(ActorType.Command,GeneralLedgerCommandActor.ActorName));
            var container=Substitute.For<IContainerInstance>();context.Container.Returns(container);container.Resolve<ICommandAuditLogger>().Returns(fixture.EventSourceDb);
            var supervisor=Substitute.For<IActorSupervisor>();supervisor.CreateMailbox(context.ActorId).Returns(Substitute.For<IActorMailbox>());
            supervisor.GetProducer(context.ActorId).Returns(Substitute.For<IActorProducer>());
            var sequences=Substitute.For<ISequenceIdGenerator>();sequences.GetSequenceIdAsync(Arg.Any<SequenceName>(),Arg.Any<CancellationToken>())
                .Returns(_=>ValueTask.FromResult(Random.Shared.NextInt64(100000,long.MaxValue)));
            ledger=new GeneralLedgerCommandActor(context,new(new GeneralLedgerStore(Transactions()),new PortfolioFinancialDbContext(Transactions()),
                new(sequences),Substitute.For<IEventProjector<GeneralLedgerCommandActor>>(),NullLogger<GeneralLedgerCommandActor>.Instance),NullLogger<GeneralLedgerCommandActor>.Instance);
            await ledger.StartAsync(supervisor,deadline.Token);
        }
        await connection.Connection.PingAsync(deadline.Token);
        Console.WriteLine("FINANCIAL_WORKER_READY");
        try
        {
            using var message=new NatsActorMessage(await subscription.Msgs.ReadAsync(deadline.Token));
            if(ledger is not null) await ledger.HandleMessageAsync(message,message.Subject.ThreadId,deadline.Token);
            else await new CapacityReservationFunctionActor(new CapacityFunctionActorIntegrationTests.Context(
                new CapacityReservationFunctionStateRepository(new PortfolioFinancialDbContext(Transactions()),new CapacityReservationStore(Transactions())))).HandleMessageAsync(message);
            await connection.Connection.PingAsync(deadline.Token);
            return 0;
        }
        finally { if(ledger is not null) await ledger.StopAsync(); }
    }
}
