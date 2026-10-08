using Microsoft.Extensions.Logging;
using TomasAI.IFM.Domain.SystemAdmin.Shared.ScheduledTask.Contracts;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
namespace TomasAI.IFM.Domain.SystemAdmin.ScheduledTask.Query.Actor;
/// <summary>Supplies persisted read models and a clock to the query actor.</summary>
public sealed class ScheduledTaskQueryContext : QueryActorContext, IQueryActorContext<ScheduledTaskQueryActor>
{
    /// <summary>Initializes the query actor's readonly services.</summary>
    public ScheduledTaskQueryContext(IActorSupervisor supervisor, IScheduledTaskReadStore readStore, TomasAI.IFM.Application.Storage.TradeDb.ITradeDbContext tradeReads, ILogger<ScheduledTaskQueryActor> logger, TimeProvider? clock = null, IScheduledTaskOutputReader? outputReader = null)
        : base(supervisor, new(ActorType.Query, ScheduledTaskQueryActor.Actor))
    { OutputReader = outputReader; ReadStore = readStore; Logger = logger; Clock = clock ?? TimeProvider.System; TradeReads = tradeReads; }
    /// <summary>Gets the configured retained-output reader.</summary>
    public IScheduledTaskOutputReader? OutputReader { get; }
    /// <summary>Gets the persisted ScyllaDB read-model store.</summary>
    public IScheduledTaskReadStore ReadStore { get; }
    /// <summary>Gets persisted financial position read models for dated market finalization.</summary>
    public TomasAI.IFM.Application.Storage.TradeDb.ITradeDbReadContext TradeReads { get; }
    /// <summary>Gets structured query logging.</summary>
    public ILogger<ScheduledTaskQueryActor> Logger { get; }
    /// <summary>Gets the explicit preview clock.</summary>
    public TimeProvider Clock { get; }
}
