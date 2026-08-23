using Microsoft.Extensions.Logging;
using TomasAI.IFM.Domain.Fund.Transaction.Event.Actor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Domain.Fund.Transaction.Event.Extensions;

/// <summary>
/// Provides Fund transaction services as readonly extension properties on a typed event context.
/// </summary>
public static class FundTransactionEventExtensions
{
    extension(IEventActorContext<FundTransactionEventActor> context)
    {
        /// <summary>Gets the actor supervisor.</summary>
        public IActorSupervisor Supervisor => GetContext(context).Supervisor;

        /// <summary>Gets the event actor logger.</summary>
        public ILogger<FundTransactionEventActor> Logger => GetContext(context).Logger;
    }

    static IFundTransactionEventContext GetContext(
        IEventActorContext<FundTransactionEventActor> context)
        => IsArgumentNull.Set(context as IFundTransactionEventContext, nameof(context))!;
}
