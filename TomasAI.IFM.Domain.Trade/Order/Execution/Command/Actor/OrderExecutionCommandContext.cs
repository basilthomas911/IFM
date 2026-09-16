using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Blackboard;
using TomasAI.IFM.Application.EventProjector.Contracts;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Domain.Portfolio.GeneralLedger;
using TomasAI.IFM.Domain.Trade.Order.Execution.Command.State;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Order.Execution.Command.Actor;

/// <summary>Exposes the services used by OrderExecution command handlers and projections.</summary>
public interface IOrderExecutionCommandContext : ICommandActorContext<OrderExecutionCommandActor>
{
    IDbContextFactory DbFactory { get; }
    IEventSourceActorDbContext DbEventSource { get; }
    IDurableReplayQueue DurableReplayQueue { get; }
    IBlackboardService BlackboardService { get; }
    IActorService ActorService { get; }
    IPortfolioTradeAccountingApi PortfolioAccounting { get; }
    IEventProjector<OrderExecutionCommandActor> EventProjector { get; }
    IEventSourceActorStateRepository<OrderExecutionCommandState> StateRepository { get; }
    ILogger<OrderExecutionCommandActor> Logger { get; }
}

/// <summary>Resolves OrderExecution actor services lazily from the actor container.</summary>
public sealed class OrderExecutionCommandContext : CommandActorContext,
    ICommandActorContext<OrderExecutionCommandActor>, IOrderExecutionCommandContext
{
    private readonly Lazy<IEventSourceActorDbContext> _eventDb;
    private readonly Lazy<IDurableReplayQueue> _replay;
    private readonly Lazy<IActorService> _actors;
    private readonly Lazy<IPortfolioTradeAccountingApi> _accounting;
    private readonly Lazy<IEventProjector<OrderExecutionCommandActor>> _projector;
    private readonly Lazy<IEventSourceActorStateRepository<OrderExecutionCommandState>> _repository;

    /// <summary>Creates the typed actor context and its lazy runtime service bindings.</summary>
    public OrderExecutionCommandContext(IActorSupervisor supervisor, IDbContextFactory dbFactory,
        IBlackboardService blackboard, ILogger<OrderExecutionCommandActor> logger)
        : base(supervisor, new(ActorType.Command, OrderExecutionCommandActor.ActorName))
    {
        DbFactory = dbFactory;
        BlackboardService = blackboard;
        Logger = logger;
        _eventDb = Resolve<IEventSourceActorDbContext>();
        _replay = Resolve<IDurableReplayQueue>();
        _actors = Resolve<IActorService>();
        _accounting = Resolve<IPortfolioTradeAccountingApi>();
        _projector = Resolve<IEventProjector<OrderExecutionCommandActor>>();
        _repository = Resolve<IEventSourceActorStateRepository<OrderExecutionCommandState>>();
    }

    public IDbContextFactory DbFactory { get; }
    public IBlackboardService BlackboardService { get; }
    public ILogger<OrderExecutionCommandActor> Logger { get; }
    public IEventSourceActorDbContext DbEventSource => _eventDb.Value;
    public IDurableReplayQueue DurableReplayQueue => _replay.Value;
    public IActorService ActorService => _actors.Value;
    public IPortfolioTradeAccountingApi PortfolioAccounting => _accounting.Value;
    public IEventProjector<OrderExecutionCommandActor> EventProjector => _projector.Value;
    public IEventSourceActorStateRepository<OrderExecutionCommandState> StateRepository => _repository.Value;

    private Lazy<T> Resolve<T>() where T : class => new(() => Container.Resolve<T>());
}
