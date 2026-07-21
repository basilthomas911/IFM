using FluentAssertions;
using Microsoft.Extensions.Logging;
using NATS.Client.Core;
using NSubstitute;
using TomasAI.IFM.Application.Storage.Postgres.EventSourceDb;
using TomasAI.IFM.Domain.Reference.LookupType.Command;
using TomasAI.IFM.Domain.Reference.LookupType.Command.Exceptions;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Reference;
using TomasAI.IFM.Shared.Reference.Commands;
using TomasAI.IFM.Shared.Reference.Events;
using TomasAI.IFM.Shared.Reference.ViewModels;
using TomasAI.IFM.Shared.Trade;
using TomasAI.IFM.Domain.Reference.LookupType.Command.Actor;

namespace TomasAI.IFM.Domain.Reference.UnitTests.LookupType;

/// <summary>
/// Contains unit tests for the LookupTypeCommandActor class, verifying command parsing, message handling, and repository
/// resolution behaviors.
/// </summary>
/// <remarks>These tests ensure that LookupTypeCommandActor correctly deserializes various lookup type-related commands from
/// messages, sets message information in the actor context, and resolves dependencies from the container during
/// startup. The tests use substitutes and test helpers to simulate runtime conditions and validate the actor's exposed
/// behaviors.</remarks>
public class LookupTypeCommandActorTests : IClassFixture<ReferenceTestFixture>
{
    readonly ReferenceTestFixture _fixture;

    public LookupTypeCommandActorTests(ReferenceTestFixture fixture)
    {
        _fixture = fixture;
    }

    // Test helper to expose protected ParseMessage and ReceiveAsync for unit testing.
    public class TestableLookupTypeCommandActor(IEventSourceActorDbContext dbEventSource, ILogger<LookupTypeCommandActor> logger) 
        : LookupTypeCommandActor(dbEventSource, logger)
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

}
