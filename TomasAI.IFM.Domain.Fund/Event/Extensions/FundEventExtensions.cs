using Microsoft.Extensions.Logging;
using TomasAI.IFM.Domain.Fund.Event.Actor;
using TomasAI.IFM.Domain.Fund.Shared;
using TomasAI.IFM.Domain.Fund.Shared.Events;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Domain.Fund.Event.Extensions;

/// <summary>
/// Provides Fund-specific members for a typed <see cref="FundEventActor"/> event context.
/// </summary>
public static class FundEventExtensions
{
    extension(IEventActorContext<FundEventActor> context)
    {
        /// <summary>
        /// Gets the actor supervisor exposed by the underlying <see cref="IFundEventContext"/>.
        /// </summary>
        public IActorSupervisor Supervisor
            => IsArgumentNull.Set(
                (context as IFundEventContext)?.Supervisor,
                nameof(context))!;

        /// <summary>
        /// Gets the logger exposed by the underlying <see cref="IFundEventContext"/>.
        /// </summary>
        public ILogger<FundEventActor> Logger
            => IsArgumentNull.Set(
                (context as IFundEventContext)?.Logger,
                nameof(context))!;
    }

    extension(IFundEventContext context)
    {
        /// <summary>
        /// Converts a Fund maximum-profit event to its completion event and sends it through the actor context.
        /// </summary>
        /// <param name="sourceEvent">The source Fund maximum-profit event.</param>
        /// <returns>A value task that completes when the event has been sent.</returns>
        public async ValueTask SendFundMaxProfitGeneratedCompleteAsync(
            FundMaxProfitGeneratedEvent sourceEvent)
        {
            IsArgumentNull.Check(context);
            IsArgumentNull.Check(sourceEvent);
            var completeEvent = IsArgumentNull.Set(
                sourceEvent.ToCompleteEvent<FundMaxProfitGeneratedCompleteEvent, FundId>()
                    as FundMaxProfitGeneratedCompleteEvent)!;
            await context.SendAsync<FundMaxProfitGeneratedCompleteEvent, FundId>(completeEvent)
                .ConfigureAwait(false);
        }

        /// <summary>
        /// Converts a Fund maximum-profit event to its failure event and sends it through the actor context.
        /// </summary>
        /// <param name="sourceEvent">The source Fund maximum-profit event.</param>
        /// <param name="exception">The exception that caused processing to fail.</param>
        /// <returns>A value task that completes when the event has been sent.</returns>
        public async ValueTask SendFundMaxProfitGeneratedFailAsync(
            FundMaxProfitGeneratedEvent sourceEvent,
            Exception exception)
        {
            IsArgumentNull.Check(context);
            IsArgumentNull.Check(sourceEvent);
            IsArgumentNull.Check(exception);
            var failEvent = IsArgumentNull.Set(
                sourceEvent.ToFailEvent<FundMaxProfitGeneratedFailEvent, FundId>(exception)
                    as FundMaxProfitGeneratedFailEvent)!;
            await context.SendAsync<FundMaxProfitGeneratedFailEvent, FundId>(failEvent)
                .ConfigureAwait(false);
        }
    }
}
