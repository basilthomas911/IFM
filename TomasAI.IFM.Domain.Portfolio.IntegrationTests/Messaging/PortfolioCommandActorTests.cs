using TomasAI.IFM.Domain.Portfolio.Shared.Common;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using TomasAI.IFM.Application.EventProjector.Contracts;
using TomasAI.IFM.Domain.Portfolio.Command.Actor;
using TomasAI.IFM.Domain.Portfolio.Shared.Events;
using TomasAI.IFM.Domain.Portfolio.Shared.Fund.Events;
using TomasAI.IFM.Domain.Portfolio.Shared.FinancialPolicy.Events;
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
        events.FindCommittedPortfolioCommandAsync(id, Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IPortfolioDomainEvent?>(null));
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
            .Returns(new PortfolioCreatedEvent(Guid.NewGuid(), commandId, 1, now, "admin", model) { IdempotencyKey = commandId });
        events.FindPortfolioCreateByIdempotencyKeyAsync(id, commandId, Arg.Any<CancellationToken>())
            .Returns(new PortfolioCreatedEvent(Guid.NewGuid(), commandId, 1, now, "admin", model) { IdempotencyKey = commandId });
        var command = new CreatePortfolioCommand
        {
            CommandId = commandId, EntityId = id, ErrorCode = 34002,
            Subject = new ActorSubject(ActorType.Command, PortfolioCommandActor.ActorName, "CreatePortfolio", id.Format()),
            Portfolio = model, IdempotencyKey = commandId,
            Access = PortfolioAccessContext.Administrator("integration-admin"),
        };
        var typed = (ICommandActor<PortfolioCommandActor>)actor;
        var state = await typed.OnLoadStateAsync(context, command.Subject.ThreadId, command);

        var replay = await typed.ReceiveAsync(context, state, command);
        var conflict = await typed.ReceiveAsync(context, state, command with
        {
            CommandId = Guid.NewGuid(),
            Portfolio = model with { Name = "Changed" }, IdempotencyKey = commandId,
        });

        replay.Success.Should().BeTrue();
        replay.Value!.Guid.Should().Be(commandId);
        conflict.Success.Should().BeFalse();
        conflict.ErrorCode.Should().Be(PortfolioErrorCodes.IdempotencyConflict);
        await events.DidNotReceive().AppendPortfolioAsync(Arg.Any<PortfolioId>(), Arg.Any<IPortfolioDomainEvent>(), Arg.Any<long>(), Arg.Any<PortfolioEventMetadata?>(), Arg.Any<CancellationToken>());
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
        events.FindCommittedPortfolioCommandAsync(id, Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IPortfolioDomainEvent?>(null));
        var projector = Substitute.For<IEventProjector<PortfolioCommandActor>>();
        var actor = new PortfolioCommandActor(context, events, projector, Guard(), Substitute.For<ILogger<PortfolioCommandActor>>());
        var now = DateTime.UtcNow;
        var command = new CreatePortfolioCommand
        {
            CommandId = Guid.NewGuid(), EntityId = id, ErrorCode = 34002,
            Subject = new ActorSubject(ActorType.Command, PortfolioCommandActor.ActorName, "CreatePortfolio", id.Format()),
            Portfolio = new PortfolioReadModel
            {
                PortfolioId = 101, Name = "Core", PortfolioVersion = 1,
                OperatingState = PortfolioOperatingState.Draft, EffectiveFromUtc = now,
                CreatedOnUtc = now, CreatedBy = "admin",
            }, IdempotencyKey = Guid.NewGuid(),
            Access = PortfolioAccessContext.Administrator("integration-admin"),
        };
        var typed = (ICommandActor<PortfolioCommandActor>)actor;

        var state = await typed.OnLoadStateAsync(context, command.Subject.ThreadId, command);
        var result = await typed.ReceiveAsync(context, state, command);

        result.Success.Should().BeTrue();
        var append = Assert.Single(events.ReceivedCalls(), call =>
            call.GetMethodInfo().Name == nameof(IPortfolioEventStore.AppendPortfolioAsync));
        var arguments = append.GetArguments();
        Assert.Equal(id, arguments[0]);
        Assert.IsType<PortfolioCreatedEvent>(arguments[1]);
        Assert.Equal(0L, arguments[2]);
        var metadata = Assert.IsType<PortfolioEventMetadata>(arguments[3]);
        Assert.Equal(command.CommandId, metadata.CorrelationId);
        Assert.Equal(command.CommandId, metadata.CausationId);
        await projector.Received(1).DomainEventsProjectionAsync(Arg.Is<DomainEventCollection>(x => x.Count == 1 && x.Single() is PortfolioCreatedEvent));
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
        events.FindCommittedPortfolioCommandAsync(id, Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IPortfolioDomainEvent?>(null));
        var projector = Substitute.For<IEventProjector<PortfolioCommandActor>>();
        var actor = new PortfolioCommandActor(context, events, projector, Guard(), Substitute.For<ILogger<PortfolioCommandActor>>());
        var command = new DeleteDraftPortfolioCommand
        {
            CommandId = Guid.NewGuid(), EntityId = id, ErrorCode = PortfolioErrorCodes.DraftDeletionNotAllowed,
            Subject = new ActorSubject(ActorType.Command, PortfolioCommandActor.ActorName, "DeleteDraftPortfolio", id.Format()),
            ExpectedVersion = 1, Reason = "duplicate",
            Access = PortfolioAccessContext.Administrator("integration-admin"),
        };
        var typed = (ICommandActor<PortfolioCommandActor>)actor;

        var state = await typed.OnLoadStateAsync(context, command.Subject.ThreadId, command);
        var result = await typed.ReceiveAsync(context, state, command);

        result.Success.Should().BeTrue();
        await events.Received(1).AppendPortfolioAsync(id, Arg.Is<IPortfolioDomainEvent>(x => x is DraftPortfolioDeletedEvent), 1,
            Arg.Any<PortfolioEventMetadata?>(), Arg.Any<CancellationToken>());
        await projector.Received(1).DomainEventsProjectionAsync(Arg.Is<DomainEventCollection>(x => x.Single() is DraftPortfolioDeletedEvent));
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
        var command = new CreatePortfolioCommand
        {
            CommandId = Guid.NewGuid(), EntityId = id, ErrorCode = PortfolioErrorCodes.ValidationFailed,
            Subject = new ActorSubject(ActorType.Command, PortfolioCommandActor.ActorName, "CreatePortfolio", id.Format()),
            Portfolio = new PortfolioReadModel
            {
                PortfolioId = id.Id, Name = "Denied", PortfolioVersion = 1,
                OperatingState = PortfolioOperatingState.Draft, EffectiveFromUtc = now,
                CreatedOnUtc = now, CreatedBy = "ignored",
            }, IdempotencyKey = Guid.NewGuid(),
            CorrelationId = Guid.NewGuid(), RequestedOnUtc = now,
            Access = PortfolioAccessContext.Reader("read-only-user"),
        };
        var typed = (ICommandActor<PortfolioCommandActor>)actor;
        var state = await typed.OnLoadStateAsync(context, command.Subject.ThreadId, command);

        var act = async () => await typed.ReceiveAsync(context, state, command);

        await act.Should().ThrowAsync<PortfolioAuthorizationException>();
        await events.DidNotReceive().AppendPortfolioAsync(Arg.Any<PortfolioId>(), Arg.Any<IPortfolioDomainEvent>(),
            Arg.Any<long>(), Arg.Any<PortfolioEventMetadata?>(), Arg.Any<CancellationToken>());
    }

    static IPortfolioOperationalGuard Guard() => new PortfolioOperationalGuard(new PortfolioOperationalOptions());
}
