using TomasAI.IFM.Domain.Fund.Shared;
using TomasAI.IFM.Domain.Fund.Shared.Events;
using TomasAI.IFM.Domain.Fund.Shared.ServiceApi;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Domain.Fund.Event.Api;

public sealed class ActorFundEventApi(IEventActorContext context) : IActorFundEventApi
{
    readonly IEventActorContext _context = IsArgumentNull.Set(context);

    public async ValueTask SendFundMaxProfitGeneratedCompleteAsync(FundMaxProfitGeneratedEvent e)
    {
        var completeEvent = e.ToCompleteEvent<FundMaxProfitGeneratedCompleteEvent, FundId>()
            as FundMaxProfitGeneratedCompleteEvent;
        await _context.SendAsync<FundMaxProfitGeneratedCompleteEvent, FundId>(completeEvent!);
    }

    public async ValueTask SendFundMaxProfitGeneratedFailAsync(FundMaxProfitGeneratedEvent e, Exception ex)
    {
        var failEvent = e.ToFailEvent<FundMaxProfitGeneratedFailEvent, FundId>(ex)
            as FundMaxProfitGeneratedFailEvent;
        await _context.SendAsync<FundMaxProfitGeneratedFailEvent, FundId>(failEvent!);
    }
}

public sealed class ActorFundEventApiFactory : IActorFundEventApiFactory
{
    public IActorFundEventApi Create(IEventActorContext context)
        => new ActorFundEventApi(context);
}
