using Microsoft.Extensions.Logging;
using TomasAI.IFM.Domain.MarketData.Securities.FuturesContract.Event.Actor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Domain.MarketData.Securities.FuturesContract.Event.Extensions;

/// <summary>Exposes futures-contract event services as readonly extension properties.</summary>
public static class FuturesContractEventExtensions
{
    extension(IEventActorContext<FuturesContractEventActor> context)
    {
        /// <summary>Gets the domain event context.</summary>
        public IFuturesContractEventContext FuturesContractContext => IsArgumentNull.Set(context as IFuturesContractEventContext)!;
        /// <summary>Gets the actor supervisor.</summary>
        public IActorSupervisor Supervisor => context.FuturesContractContext.Supervisor;
        /// <summary>Gets the actor logger.</summary>
        public ILogger<FuturesContractEventActor> Logger => context.FuturesContractContext.Logger;
    }
}
