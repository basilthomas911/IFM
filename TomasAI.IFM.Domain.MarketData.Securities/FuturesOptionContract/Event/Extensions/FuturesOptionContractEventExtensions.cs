using Microsoft.Extensions.Logging;
using TomasAI.IFM.Domain.MarketData.Securities.FuturesOptionContract.Event.Actor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Domain.MarketData.Securities.FuturesOptionContract.Event.Extensions;

/// <summary>Exposes futures-option-contract event services as readonly extension properties.</summary>
public static class FuturesOptionContractEventExtensions
{
    extension(IEventActorContext<FuturesOptionContractEventActor> context)
    {
        /// <summary>Gets the domain event context.</summary>
        public IFuturesOptionContractEventContext FuturesOptionContractContext => IsArgumentNull.Set(context as IFuturesOptionContractEventContext)!;
        /// <summary>Gets the actor supervisor.</summary>
        public IActorSupervisor Supervisor => context.FuturesOptionContractContext.Supervisor;
        /// <summary>Gets the actor logger.</summary>
        public ILogger<FuturesOptionContractEventActor> Logger => context.FuturesOptionContractContext.Logger;
    }
}
