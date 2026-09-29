using Microsoft.Extensions.Logging;
using TomasAI.IFM.Domain.Supervisor.Shared.Queries;
using TomasAI.IFM.Domain.Supervisor.Shared.ReadModels;
using TomasAI.IFM.Domain.Supervisor.Shared.ServiceApi;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Supervisor.Query.Actor;

/// <summary>Serves immutable Supervisor operational-health queries through the standard NATS actor runtime.</summary>
public sealed class SupervisorQueryActor(IQueryActorContext<SupervisorQueryActor> context)
    : BaseQueryActor<SupervisorQueryActor>(context, Require(context).Logger)
{
    public const string ActorName = GetSupervisorHealthSummaryQuery.Actor;

    static readonly IReadOnlyDictionary<string, Func<IActorMessage, IQuery>> ParseMap =
        new Dictionary<string, Func<IActorMessage, IQuery>>(StringComparer.Ordinal)
        {
            [GetSupervisorHealthSummaryQuery.QueryVerb] = static message =>
                message.AsQuery<GetSupervisorHealthSummaryQuery, SupervisorHealthSummaryReadModel>()!
        };

    protected override IQuery ParseMessage(
        IQueryActorContext<SupervisorQueryActor> context,
        IActorMessage message) => ParseMappedQuery(context, message, ParseMap);

    protected override ValueTask ReceiveAsync(
        IQueryActorContext<SupervisorQueryActor> context,
        IQuery query) => ReceiveAsync(context, query, CancellationToken.None);

    protected override async ValueTask ReceiveAsync(
        IQueryActorContext<SupervisorQueryActor> context,
        IQuery query,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var typed = (GetSupervisorHealthSummaryQuery)query;
        var snapshot = Require(context).Supervisor.ActorMetrics.Current;
        var result = new SupervisorHealthSummaryReadModel(
            snapshot.Revision, snapshot.ObservedUtc, snapshot.ExpectedActors,
            snapshot.CollectedActors, snapshot.FailedActors, snapshot.HealthyActors,
            snapshot.DegradedActors, snapshot.CriticalActors, snapshot.UnknownActors,
            snapshot.Quality);
        await context.ReplyAsync(
            typed.Subject.ThreadId,
            typed.Verb,
            new ServiceOk<SupervisorHealthSummaryReadModel>(result)).ConfigureAwait(false);
    }

    protected override ValueTask OnExceptionAsync(
        IQueryActorContext<SupervisorQueryActor> context,
        ActorThreadId threadId,
        IQuery query,
        string verb,
        Exception exception) => context.ReplyAsync(
            threadId,
            verb,
            new ServiceFailed<SupervisorHealthSummaryReadModel>(query.ErrorCode, exception.Message));

    static ISupervisorQueryActorContext Require(IQueryActorContext<SupervisorQueryActor> context) =>
        context as ISupervisorQueryActorContext
        ?? throw new ArgumentException("A privileged Supervisor query context is required.", nameof(context));
}

/// <summary>Defines the privileged capabilities available to the Supervisor query actor.</summary>
public interface ISupervisorQueryActorContext : IQueryActorContext<SupervisorQueryActor>
{
    ISupervisorActorContext Supervisor { get; }
    ILogger<SupervisorQueryActor> Logger { get; }
}

/// <summary>Provides the privileged, capability-restricted Supervisor query context.</summary>
public sealed class SupervisorQueryActorContext(
    IActorSupervisor actorSupervisor,
    TomasAI.IFM.Domain.Supervisor.Context.SupervisorActorContext supervisor,
    ILogger<SupervisorQueryActor> logger)
    : QueryActorContext(actorSupervisor, new(ActorType.Query, SupervisorQueryActor.ActorName)),
        IQueryActorContext<SupervisorQueryActor>, ISupervisorQueryActorContext
{
    public ISupervisorActorContext Supervisor { get; } = supervisor;
    public ILogger<SupervisorQueryActor> Logger { get; } = logger;
}
