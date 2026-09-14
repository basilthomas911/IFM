using Microsoft.Extensions.Logging;
using TomasAI.IFM.Domain.Trade.Futures.Option.Position.VerticalSpread.Plan.Function.State;
using TomasAI.IFM.Domain.Trade.Futures.Option.Position.VerticalSpread.Plan.Model;
using TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Plan;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Domain.Trade.Futures.Option.Position.VerticalSpread.Plan.Function.Actor;

public interface IVerticalSpreadTradePlanFunctionContext : IFunctionActorContext<VerticalSpreadTradePlanFunctionActor>
{
    VerticalSpreadTradePlanAlgorithm Algorithm { get; }
    TimeProvider TimeProvider { get; }
    ILogger<VerticalSpreadTradePlanFunctionActor> Logger { get; }
    IEventSourceFunctionStateRepository<VerticalSpreadTradePlanFunctionState,
        UpdateVerticalSpreadTradePlanCommand> StateRepository { get; }
}

public sealed class VerticalSpreadTradePlanFunctionContext : FunctionActorContext,
    IFunctionActorContext<VerticalSpreadTradePlanFunctionActor>, IVerticalSpreadTradePlanFunctionContext
{
    readonly Lazy<IEventSourceFunctionStateRepository<VerticalSpreadTradePlanFunctionState,
        UpdateVerticalSpreadTradePlanCommand>> _repository;

    public VerticalSpreadTradePlanFunctionContext(IActorSupervisor supervisor,
        ILogger<VerticalSpreadTradePlanFunctionActor> logger)
        : base(supervisor, new(ActorType.Function, VerticalSpreadTradePlanFunctionActor.ActorName))
    {
        Logger = logger;
        _repository = new(() => Container.Resolve<IEventSourceFunctionStateRepository<
            VerticalSpreadTradePlanFunctionState, UpdateVerticalSpreadTradePlanCommand>>());
    }

    public VerticalSpreadTradePlanAlgorithm Algorithm { get; } = new();
    public TimeProvider TimeProvider { get; } = TimeProvider.System;
    public ILogger<VerticalSpreadTradePlanFunctionActor> Logger { get; }
    public IEventSourceFunctionStateRepository<VerticalSpreadTradePlanFunctionState,
        UpdateVerticalSpreadTradePlanCommand> StateRepository => _repository.Value;
}
