using FluentAssertions;
using Microsoft.Extensions.Logging;
using NATS.Client.Core;
using NSubstitute;
using TomasAI.IFM.Domain.MarketData.Securities.FuturesOptionContract.Command;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Exceptions;
using TomasAI.IFM.Domain.MarketData.Shared;
using TomasAI.IFM.Domain.MarketData.Shared.Commands;
using TomasAI.IFM.Domain.MarketData.Shared.Events;
using TomasAI.IFM.Domain.MarketData.Shared.Exceptions;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Domain.Reference.Shared.ServiceApi;
using TomasAI.IFM.Domain.MarketData.Securities.FuturesOptionContract.Command.Actor;
using TomasAI.IFM.Application.Storage;

namespace TomasAI.IFM.Domain.MarketData.Securities.UnitTests.FuturesOptionContract;

public class FuturesOptionContractCommandActorTests : IClassFixture<SecuritiesFixture>
{
    readonly SecuritiesFixture _fixture;

    public FuturesOptionContractCommandActorTests(SecuritiesFixture fixture)
    {
        _fixture = fixture;
    }

    // Test helper to expose protected ParseMessage and ReceiveAsync for unit testing.
    public class TestableFuturesOptionContractCommandActor(IEventSourceActorDbContext dbEventSource, ILogger<FuturesOptionContractCommandActor> logger)
        : FuturesOptionContractCommandActor(dbEventSource, logger)
    {
        public ICommand InvokeParseMessage(ICommandActorContext context, NatsMsg<byte[]> message)
            => ParseMessage(context, message);

        public async ValueTask<ServiceResult<GuidResult>> InvokeReceiveAsync(ICommandActorContext context, IActorState state, ICommand cmd)
            => await ReceiveAsync(context, state, cmd);

        public async ValueTask InvokeOnValidateAsync(ICommandActorContext context, ActorThreadId threadId, ICommand cmd)
            => await OnValidateAsync(context, threadId, cmd);

        public async ValueTask<ServiceResult<GuidResult>> InvokeOnExceptionAsync(ICommandActorContext context, ActorThreadId threadId, ICommand cmd, Exception ex)
            => await OnExceptionAsync(context, threadId, cmd, ex);
    }

    [Fact]
    public void ParseMessage_IncompleteAudit_DoesNotBlockActorThread()
    {
        var pendingAudit = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        dbEventSource.InsertCommandLogAsync(
                Arg.Any<ICommand>(),
                Arg.Any<DateTime>(),
                Arg.Any<string>())
            .Returns(pendingAudit.Task);
        var actor = _fixture.CreateActor(
            dbEventSource,
            Substitute.For<ILogger<FuturesOptionContractCommandActor>>());
        var command = new AddFuturesOptionContractCommand(SampleData.FuturesOptionContract1)
        {
            CommandId = Guid.NewGuid(),
            Subject = new ActorSubject(
                ActorType.Command,
                FuturesOptionContractCommandActor.ActorName,
                AddFuturesOptionContractCommand.Verb,
                SampleData.FuturesOptionContract1.ContractId)
        };
        var message = new NatsMsg<byte[]>
        {
            Subject = command.Subject.ToString(),
            Data = ActorExtensions.DataSerializer!.Serialize(command)
        };

        var parsed = actor.InvokeParseMessage(
            Substitute.For<ICommandActorContext>(),
            message);

        parsed.CommandId.Should().Be(command.CommandId);
        pendingAudit.Task.IsCompleted.Should().BeFalse();
        pendingAudit.SetResult();
    }

}
