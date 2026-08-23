using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Blackboard;
using TomasAI.IFM.Application.EventProjector.Contracts;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Domain.Fund.Command.Actor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Domain.Fund.Command.Extensions;

/// <summary>
/// Provides Fund-specific members for a typed <see cref="FundCommandActor"/> command context.
/// </summary>
public static class FundCommandExtensions
{
    extension(ICommandActorContext<FundCommandActor> context)
    {
        /// <summary>
        /// Gets the Fund database-context factory exposed by the underlying <see cref="IFundCommandContext"/>.
        /// </summary>
        public IDbContextFactory DbFactory
            => IsArgumentNull.Set(
                (context as IFundCommandContext)?.DbFactory,
                nameof(context))!;

        /// <summary>
        /// Gets the blackboard service exposed by the underlying <see cref="IFundCommandContext"/>.
        /// </summary>
        public IBlackboardService BlackboardService
            => IsArgumentNull.Set(
                (context as IFundCommandContext)?.BlackboardService,
                nameof(context))!;

        /// <summary>
        /// Gets the logger exposed by the underlying <see cref="IFundCommandContext"/>.
        /// </summary>
        /// <exception cref="ArgumentNullException">
        /// The context is <see langword="null"/>, does not implement <see cref="IFundCommandContext"/>, or exposes a
        /// <see langword="null"/> logger.
        /// </exception>
        public ILogger<FundCommandActor> Logger
            => IsArgumentNull.Set(
                (context as IFundCommandContext)?.Logger,
                nameof(context))!;

        /// <summary>
        /// Gets the event-source database context exposed by the underlying <see cref="IFundCommandContext"/>.
        /// </summary>
        public IEventSourceActorDbContext DbEventSource
            => IsArgumentNull.Set(
                (context as IFundCommandContext)?.DbEventSource,
                nameof(context))!;

        /// <summary>
        /// Gets the durable replay queue exposed by the underlying <see cref="IFundCommandContext"/>.
        /// </summary>
        public IDurableReplayQueue DurableReplayQueue
            => IsArgumentNull.Set(
                (context as IFundCommandContext)?.DurableReplayQueue,
                nameof(context))!;

        /// <summary>
        /// Gets the actor-state factory exposed by the underlying <see cref="IFundCommandContext"/>.
        /// </summary>
        public IEventSourceActorStateFactory StateFactory
            => IsArgumentNull.Set(
                (context as IFundCommandContext)?.StateFactory,
                nameof(context))!;

        /// <summary>
        /// Gets the actor service exposed by the underlying <see cref="IFundCommandContext"/>.
        /// </summary>
        public IActorService ActorService
            => IsArgumentNull.Set(
                (context as IFundCommandContext)?.ActorService,
                nameof(context))!;

        /// <summary>
        /// Gets the Fund event projector exposed by the underlying <see cref="IFundCommandContext"/>.
        /// </summary>
        public IEventProjector<FundCommandActor> EventProjector
            => IsArgumentNull.Set(
                (context as IFundCommandContext)?.EventProjector,
                nameof(context))!;
    }
}
