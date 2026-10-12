using MessagePack;
using TomasAI.IFM.Application.TradeBroker.Contracts;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.BrokerAccount.Contracts;

/// <summary>Reads the durable qualification and operational state for one broker account.</summary>
public interface IBrokerAccountQueryApi
{
    /// <summary>Gets the current account state, or a successful result with no value when not initialized.</summary>
    ValueTask<ServiceResult<BrokerAccountDefinition>> GetAsync(
        BrokerAccountId accountId,
        CancellationToken cancellationToken = default);
}
