using TomasAI.IFM.Domain.Fund.Shared.Events;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Domain.Fund.Shared.ServiceApi;

/// <summary>
/// Defines NATS-backed Fund events intended for use by domain event actors.
/// </summary>
public interface IActorFundEventApi
{
    ValueTask SendFundMaxProfitGeneratedCompleteAsync(FundMaxProfitGeneratedEvent e);
    ValueTask SendFundMaxProfitGeneratedFailAsync(FundMaxProfitGeneratedEvent e, Exception ex);
}

public interface IActorFundEventApiFactory
{
    IActorFundEventApi Create(IEventActorContext context);
}
