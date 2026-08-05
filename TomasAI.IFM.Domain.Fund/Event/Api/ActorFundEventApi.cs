using TomasAI.IFM.Domain.Fund.Shared;
using TomasAI.IFM.Domain.Fund.Shared.Events;
using TomasAI.IFM.Domain.Fund.Shared.ServiceApi;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Domain.Fund.Event.Api;

/// <summary>
/// Sends Fund-domain completion and failure events from a running event actor.
/// </summary>
/// <remarks>
/// The instance is bound to one <see cref="IEventActorContext"/> and preserves the source event's
/// correlation and <see cref="FundId"/> identity when producing complete or fail events.
/// Create instances through <see cref="ActorFundEventApiFactory"/>; do not share them between actors.
/// </remarks>
public sealed class ActorFundEventApi(IEventActorContext context) : IActorFundEventApi
{
    readonly IEventActorContext _context = IsArgumentNull.Set(context);

    /// <summary>
    /// Sends the fund max profit generated complete event.
    /// </summary>
    /// <param name="e">The source domain event.</param>
    /// <returns>A value task that completes when the event has been sent.</returns>
    public async ValueTask SendFundMaxProfitGeneratedCompleteAsync(FundMaxProfitGeneratedEvent e)
    {
        var completeEvent = e.ToCompleteEvent<FundMaxProfitGeneratedCompleteEvent, FundId>()
            as FundMaxProfitGeneratedCompleteEvent;
        await _context.SendAsync<FundMaxProfitGeneratedCompleteEvent, FundId>(completeEvent!).ConfigureAwait(false);
    }

    /// <summary>
    /// Sends the fund max profit generated fail event.
    /// </summary>
    /// <param name="e">The source domain event.</param>
    /// <param name="ex">The exception that caused the failure.</param>
    /// <returns>A value task that completes when the event has been sent.</returns>
    public async ValueTask SendFundMaxProfitGeneratedFailAsync(FundMaxProfitGeneratedEvent e, Exception ex)
    {
        var failEvent = e.ToFailEvent<FundMaxProfitGeneratedFailEvent, FundId>(ex)
            as FundMaxProfitGeneratedFailEvent;
        await _context.SendAsync<FundMaxProfitGeneratedFailEvent, FundId>(failEvent!).ConfigureAwait(false);
    }
}

/// <summary>
/// Creates Fund event APIs bound to the supplied event-actor context.
/// </summary>
public sealed class ActorFundEventApiFactory : IActorFundEventApiFactory
{
    /// <summary>
    /// Creates a Fund event API for a running actor.
    /// </summary>
    /// <param name="context">The actor context used to send Fund events.</param>
    /// <returns>A context-bound Fund event API.</returns>
    public IActorFundEventApi Create(IEventActorContext context)
        => new ActorFundEventApi(context);
}
