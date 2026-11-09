using TomasAI.IFM.Domain.Reference.Shared.ProjectionOutcomes;
using TomasAI.IFM.Domain.Reference.ParameterSets.Persistence;
using TomasAI.IFM.Domain.Reference.ParameterSets.Assignment.Command.Actor;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.Storage.EventSourceDb;
using TomasAI.IFM.Domain.Reference.ParameterSets.Command.State;
using TomasAI.IFM.Application.EventProjector;
using TomasAI.IFM.Application.EventProjector.Contracts;
using TomasAI.IFM.Domain.Reference.ParameterSets.Command.Actor;
using TomasAI.IFM.Domain.Reference.Shared.ParameterSets;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventProjector;
using TomasAI.IFM.Shared.EventSourcing;
namespace TomasAI.IFM.Domain.Reference.ParameterSets.Assignment.Command.EventProjector;

public sealed class ParameterAssignmentEventProjector : ConventionalEventProjector<ParameterAssignmentCommandActor>
{
    readonly IParameterAssignmentCommandContext context;
    readonly EventProjectionDescriptor[] descriptors;
    public ParameterAssignmentEventProjector(ICommandActorContext<ParameterAssignmentCommandActor> value,
        EventProjectorReliabilityOptions? reliabilityOptions = null)
     : base(((IParameterAssignmentCommandContext)(value ?? throw new ArgumentNullException(nameof(value)))).DurableReplayQueue, ((IParameterAssignmentCommandContext)value).DbEventSource, ((IParameterAssignmentCommandContext)value).Logger, reliabilityOptions)
    {
        ArgumentNullException.ThrowIfNull(value);
        context = (IParameterAssignmentCommandContext)value;
        descriptors = [Describe<ParameterAssignmentCreatedEvent>(), Describe<ParameterAssignmentChangedEvent>()];
    }
    public override IReadOnlyCollection<EventProjectionDescriptor> ProjectionDescriptors => descriptors;
    EventProjectionDescriptor Describe<T>() where T : class, IEvent<ParameterAssignmentEntityId> => new EventProjectionDescriptor(typeof(T), EventProjectionIdempotencyStrategy.NaturalKeyMutation,
        async (fact, execution) =>
        {
            await RebuildAsync(fact, execution.CancellationToken).ConfigureAwait(false);
            return new EventProjectionApplyResult(EventProjectionApplyOutcome.Applied);
        },
        fact => Complete((ParameterAssignmentChangedEvent)fact), (fact, error) => Fail((ParameterAssignmentChangedEvent)fact, error),
        publishProcessingEvent: false, useDurableReplay: true)
        .WithReadModelRebuild((fact, token) => RebuildAsync(fact, token));
    static ICompleteEvent Complete(ParameterAssignmentChangedEvent fact) =>
        fact.ReadAssignment().Enabled ? ReferenceProjectionOutcomeFactory.Complete<ParameterAssignmentEntityId, ParameterAssignmentAssignedCompleteEvent>(fact, "ParameterAssignmentEvent")
        : ReferenceProjectionOutcomeFactory.Complete<ParameterAssignmentEntityId, ParameterAssignmentDisabledCompleteEvent>(fact, "ParameterAssignmentEvent");

    static IErrorEvent Fail(ParameterAssignmentChangedEvent fact, Exception error) =>
        fact.ReadAssignment().Enabled ? ReferenceProjectionOutcomeFactory.Fail<ParameterAssignmentEntityId, ParameterAssignmentAssignedFailEvent>(fact, "ParameterAssignmentEvent", error)
        : ReferenceProjectionOutcomeFactory.Fail<ParameterAssignmentEntityId, ParameterAssignmentDisabledFailEvent>(fact, "ParameterAssignmentEvent", error);

    bool IsPostgres => string.Equals(context.ConfigurationDb.ProviderName, "System.Data.Postgres", StringComparison.Ordinal);

    Task RebuildAsync(IEvent fact, CancellationToken token)
    {
        if (!IsPostgres) {
return context.ConfigurationDb.ProjectParameterAssignmentAsync((ParameterAssignmentChangedEvent)fact, token);}
        var databases = context.Container.Resolve<IDbContextFactory>();
        return context.Container.Resolve<IPostgresEventTransaction>().ExecuteOnceAsync(async (transaction, cancellationToken) =>
        {
            await ParameterAcceptedProjection.ApplyAsync(databases, transaction, fact, cancellationToken).ConfigureAwait(false);
            if (fact.EventId > 0)
                await context.ConfigurationDb.CompleteParameterProjectionAsync(transaction, fact.EventId, cancellationToken).ConfigureAwait(false);
            return true;
        }, token);
    }
}
