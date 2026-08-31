using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using TomasAI.IFM.Application.EventProjector.Contracts;
using TomasAI.IFM.Domain.Portfolio.Command.Actor;
using TomasAI.IFM.Domain.Portfolio.Command.Model;
using TomasAI.IFM.Domain.Portfolio.Command.State;
using TomasAI.IFM.Domain.Portfolio.Persistence;
using TomasAI.IFM.Domain.Portfolio.Operations;
using TomasAI.IFM.Domain.Portfolio.Shared.Commands;
using TomasAI.IFM.Domain.Portfolio.Shared.Contracts;
using TomasAI.IFM.Domain.Portfolio.Shared.Identities;
using TomasAI.IFM.Domain.Portfolio.Shared.Validation;
using TomasAI.IFM.Domain.Portfolio.Shared.ViewModels;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Portfolio.IntegrationTests.Messaging;

public sealed class PortfolioCommandActorTests
{
    [Fact]
    [Trait("Gate", "PF-03")]
    [Trait("Gate", "PF-10")]
    [Trait("Category", "Portfolio")]
    public async Task Portfolio_command_actor_returns_committed_idempotent_create_and_rejects_changed_payload()
    {
        var id = new PortfolioId(102);
        var context = Substitute.For<ICommandActorContext<PortfolioCommandActor>>();
        context.ActorId.Returns(new ActorMailboxId(ActorType.Command, PortfolioCommandActor.ActorName));
        var events = Substitute.For<IPortfolioEventStore>();
        events.LoadPortfolioAsync(id, Arg.Any<CancellationToken>()).Returns(new PortfolioAggregate());
        var projector = Substitute.For<IEventProjector<PortfolioCommandActor>>();
        var actor = new PortfolioCommandActor(context, events, projector, Guard(), Substitute.For<ILogger<PortfolioCommandActor>>());
        var now = DateTime.UtcNow;
        var commandId = Guid.NewGuid();
        var model = new PortfolioReadModel
        {
            PortfolioId = id.Id, Name = "Idempotent", PortfolioVersion = 1,
            OperatingState = PortfolioOperatingState.Draft, EffectiveFromUtc = now,
            CreatedOnUtc = now, CreatedBy = "admin",
        };
        events.FindCommittedPortfolioCommandAsync(id, commandId, Arg.Any<CancellationToken>())
            .Returns(new PortfolioCreated(Guid.NewGuid(), commandId, 1, now, "admin", model) { IdempotencyKey = commandId });
        events.FindPortfolioCreateByIdempotencyKeyAsync(id, commandId, Arg.Any<CancellationToken>())
            .Returns(new PortfolioCreated(Guid.NewGuid(), commandId, 1, now, "admin", model) { IdempotencyKey = commandId });
        var command = new PortfolioCommand<CreatePortfolioPayload, PortfolioId>
        {
            CommandId = commandId, EntityId = id, ErrorCode = 34002,
            Subject = new ActorSubject(ActorType.Command, PortfolioCommandActor.ActorName, "CreatePortfolio", id.Format()),
            Payload = new(model, commandId),
            Access = PortfolioAccessContext.Administrator("integration-admin"),
        };
        var typed = (ICommandActor<PortfolioCommandActor>)actor;
        var state = await typed.OnLoadStateAsync(context, command.Subject.ThreadId, command);

        var replay = await typed.ReceiveAsync(context, state, command);
        var conflict = await typed.ReceiveAsync(context, state, command with
        {
            CommandId = Guid.NewGuid(),
            Payload = new(model with { Name = "Changed" }, commandId),
        });

        replay.Success.Should().BeTrue();
        replay.Value!.Guid.Should().Be(commandId);
        conflict.Success.Should().BeFalse();
        conflict.ErrorCode.Should().Be(PortfolioErrorCodes.IdempotencyConflict);
        await events.DidNotReceive().AppendPortfolioAsync(Arg.Any<PortfolioId>(), Arg.Any<PortfolioDomainEvent>(), Arg.Any<long>(), Arg.Any<PortfolioEventMetadata?>(), Arg.Any<CancellationToken>());
        await projector.DidNotReceive().DomainEventsProjectionAsync(Arg.Any<DomainEventCollection>());
    }

    [Fact]
    [Trait("Gate", "PF-09")]
    [Trait("Gate", "PF-10")]
    [Trait("Category", "Portfolio")]
    public async Task Portfolio_command_actor_appends_then_enqueues_the_committed_event()
    {
        var id = new PortfolioId(101);
        var context = Substitute.For<ICommandActorContext<PortfolioCommandActor>>();
        context.ActorId.Returns(new ActorMailboxId(ActorType.Command, PortfolioCommandActor.ActorName));
        var events = Substitute.For<IPortfolioEventStore>();
        events.LoadPortfolioAsync(id, Arg.Any<CancellationToken>()).Returns(new PortfolioAggregate());
        var projector = Substitute.For<IEventProjector<PortfolioCommandActor>>();
        var actor = new PortfolioCommandActor(context, events, projector, Guard(), Substitute.For<ILogger<PortfolioCommandActor>>());
        var now = DateTime.UtcNow;
        var command = new PortfolioCommand<CreatePortfolioPayload, PortfolioId>
        {
            CommandId = Guid.NewGuid(), EntityId = id, ErrorCode = 34002,
            Subject = new ActorSubject(ActorType.Command, PortfolioCommandActor.ActorName, "CreatePortfolio", id.Format()),
            Payload = new(new PortfolioReadModel
            {
                PortfolioId = 101, Name = "Core", PortfolioVersion = 1,
                OperatingState = PortfolioOperatingState.Draft, EffectiveFromUtc = now,
                CreatedOnUtc = now, CreatedBy = "admin",
            }, Guid.NewGuid()),
            Access = PortfolioAccessContext.Administrator("integration-admin"),
        };
        var typed = (ICommandActor<PortfolioCommandActor>)actor;

        var state = await typed.OnLoadStateAsync(context, command.Subject.ThreadId, command);
        var result = await typed.ReceiveAsync(context, state, command);

        result.Success.Should().BeTrue();
        await events.Received(1).AppendPortfolioAsync(id, Arg.Is<PortfolioDomainEvent>(x => x is PortfolioCreated), 0,
            Arg.Is<PortfolioEventMetadata>(x => x.CorrelationId == command.CommandId && x.CausationId == command.CommandId), Arg.Any<CancellationToken>());
        await projector.Received(1).DomainEventsProjectionAsync(Arg.Is<DomainEventCollection>(x => x.Count == 1 && x.Single() is PortfolioCreated));
    }

    [Fact]
    [Trait("Gate", "PF-03")]
    [Trait("Gate", "PF-10")]
    [Trait("Category", "Portfolio")]
    public async Task Typed_actor_route_commits_Draft_deletion_tombstone_with_expected_revision()
    {
        var id = new PortfolioId(901);
        var now = DateTime.UtcNow;
        var aggregate = new PortfolioAggregate();
        aggregate.Create(Guid.NewGuid(), new PortfolioReadModel
        {
            PortfolioId = id.Id, Name = "Delete", PortfolioVersion = 1,
            OperatingState = PortfolioOperatingState.Draft, EffectiveFromUtc = now, CreatedOnUtc = now, CreatedBy = "admin",
        }, now, "admin");
        var context = Substitute.For<ICommandActorContext<PortfolioCommandActor>>();
        context.ActorId.Returns(new ActorMailboxId(ActorType.Command, PortfolioCommandActor.ActorName));
        var events = Substitute.For<IPortfolioEventStore>();
        events.LoadPortfolioAsync(id, Arg.Any<CancellationToken>()).Returns(aggregate);
        var projector = Substitute.For<IEventProjector<PortfolioCommandActor>>();
        var actor = new PortfolioCommandActor(context, events, projector, Guard(), Substitute.For<ILogger<PortfolioCommandActor>>());
        var command = new PortfolioCommand<DeleteDraftPortfolioPayload, PortfolioId>
        {
            CommandId = Guid.NewGuid(), EntityId = id, ErrorCode = PortfolioErrorCodes.DraftDeletionNotAllowed,
            Subject = new ActorSubject(ActorType.Command, PortfolioCommandActor.ActorName, "DeleteDraftPortfolio", id.Format()),
            Payload = new(1, "duplicate"),
            Access = PortfolioAccessContext.Administrator("integration-admin"),
        };
        var typed = (ICommandActor<PortfolioCommandActor>)actor;

        var state = await typed.OnLoadStateAsync(context, command.Subject.ThreadId, command);
        var result = await typed.ReceiveAsync(context, state, command);

        result.Success.Should().BeTrue();
        await events.Received(1).AppendPortfolioAsync(id, Arg.Is<PortfolioDomainEvent>(x => x is DraftPortfolioDeleted), 1,
            Arg.Any<PortfolioEventMetadata?>(), Arg.Any<CancellationToken>());
        await projector.Received(1).DomainEventsProjectionAsync(Arg.Is<DomainEventCollection>(x => x.Single() is DraftPortfolioDeleted));
    }

    [Fact]
    [Trait("Gate", "PF-30")]
    [Trait("Category", "Integration")]
    public async Task Reader_cannot_mutate_through_the_actor_and_no_event_is_appended()
    {
        var id = new PortfolioId(990);
        var context = Substitute.For<ICommandActorContext<PortfolioCommandActor>>();
        context.ActorId.Returns(new ActorMailboxId(ActorType.Command, PortfolioCommandActor.ActorName));
        var events = Substitute.For<IPortfolioEventStore>();
        events.LoadPortfolioAsync(id, Arg.Any<CancellationToken>()).Returns(new PortfolioAggregate());
        var projector = Substitute.For<IEventProjector<PortfolioCommandActor>>();
        var actor = new PortfolioCommandActor(context, events, projector, Guard(), Substitute.For<ILogger<PortfolioCommandActor>>());
        var now = DateTime.UtcNow;
        var command = new PortfolioCommand<CreatePortfolioPayload, PortfolioId>
        {
            CommandId = Guid.NewGuid(), EntityId = id, ErrorCode = PortfolioErrorCodes.ValidationFailed,
            Subject = new ActorSubject(ActorType.Command, PortfolioCommandActor.ActorName, "CreatePortfolio", id.Format()),
            Payload = new(new PortfolioReadModel
            {
                PortfolioId = id.Id, Name = "Denied", PortfolioVersion = 1,
                OperatingState = PortfolioOperatingState.Draft, EffectiveFromUtc = now,
                CreatedOnUtc = now, CreatedBy = "ignored",
            }, Guid.NewGuid()),
            CorrelationId = Guid.NewGuid(), RequestedOnUtc = now,
            Access = PortfolioAccessContext.Reader("read-only-user"),
        };
        var typed = (ICommandActor<PortfolioCommandActor>)actor;
        var state = await typed.OnLoadStateAsync(context, command.Subject.ThreadId, command);

        var act = async () => await typed.ReceiveAsync(context, state, command);

        await act.Should().ThrowAsync<PortfolioAuthorizationException>();
        await events.DidNotReceive().AppendPortfolioAsync(Arg.Any<PortfolioId>(), Arg.Any<PortfolioDomainEvent>(),
            Arg.Any<long>(), Arg.Any<PortfolioEventMetadata?>(), Arg.Any<CancellationToken>());
    }

    static IPortfolioOperationalGuard Guard() => new PortfolioOperationalGuard(new PortfolioOperationalOptions());
}
