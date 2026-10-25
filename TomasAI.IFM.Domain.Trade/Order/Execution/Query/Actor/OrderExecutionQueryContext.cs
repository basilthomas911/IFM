using System.Collections.Frozen;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Domain.Trade.Order.Execution.Query;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Order.Execution;
using TomasAI.IFM.Application.CommandPipeline;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Domain.Trade.Order.Execution.Query.Actor;
public sealed class OrderExecutionQueryContext(IActorSupervisor s, IDbContextFactory d, ILogger<OrderExecutionQueryActor> l) : QueryActorContext(s, new(ActorType.Query, OrderExecutionQueryActor.ActorName)), IOrderExecutionQueryContext
{
    public IDbContextFactory DbFactory { get; } = d;
    public ILogger<OrderExecutionQueryActor> Logger { get; } = l;
}
