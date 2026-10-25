using System.Collections.Frozen;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Workflow;
using TomasAI.IFM.Application.CommandPipeline;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Domain.Trade.Model.Position.Workflow.Query.Actor;
public sealed class PositionExitWorkflowQueryContext(IActorSupervisor supervisor, IDbContextFactory dbFactory, ILogger<PositionExitWorkflowQueryActor> logger) : QueryActorContext(supervisor, new(ActorType.Query, PositionExitWorkflowQueryActor.ActorName)), IPositionExitWorkflowQueryContext
{
    public IDbContextFactory DbFactory { get; } = dbFactory;
    public ILogger<PositionExitWorkflowQueryActor> Logger { get; } = logger;
}
