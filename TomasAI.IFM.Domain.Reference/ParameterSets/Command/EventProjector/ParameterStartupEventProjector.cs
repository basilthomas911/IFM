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
namespace TomasAI.IFM.Domain.Reference.ParameterSets.Command.EventProjector;

public sealed class ParameterStartupEventProjector : ConventionalEventProjector<ParameterStartupCommandActor>
{
    readonly IParameterStartupCommandContext context;
    readonly EventProjectionDescriptor[] descriptors;
    public ParameterStartupEventProjector(ICommandActorContext<ParameterStartupCommandActor> value)
     : base(((IParameterStartupCommandContext)(value ?? throw new ArgumentNullException(nameof(value)))).DurableReplayQueue, ((IParameterStartupCommandContext)value).DbEventSource,
       ((IParameterStartupCommandContext)value).BlackboardService, ((IParameterStartupCommandContext)value).Logger)
    {
        ArgumentNullException.ThrowIfNull(value);
        context = (IParameterStartupCommandContext)value;
        descriptors = [Describe<ParameterStartupChangedEvent>()];
    }
    public override IReadOnlyCollection<EventProjectionDescriptor> ProjectionDescriptors => descriptors;
    EventProjectionDescriptor Describe<T>() where T : class, IEvent<ParameterStartupEntityId> => new EventProjectionDescriptor(typeof(T), EventProjectionIdempotencyStrategy.NaturalKeyMutation,
     async (fact, token) => { if (!IsPostgres) {
await context.ConfigurationDb.ProjectParameterStartupAsync((ParameterStartupChangedEvent)fact, token.CancellationToken).ConfigureAwait(false);}
return new EventProjectionApplyResult(EventProjectionApplyOutcome.Applied); }, _ => null, (_, _) => null, false, false, false, false)
        .WithReadModelRebuild((fact, token) => RebuildAsync(fact, token));
    bool IsPostgres => string.Equals(context.ConfigurationDb.ProviderName, "System.Data.Postgres", StringComparison.Ordinal);

    Task RebuildAsync(IEvent fact, CancellationToken token)
    {
        if (!IsPostgres) {
return context.ConfigurationDb.ProjectParameterStartupAsync((ParameterStartupChangedEvent)fact, token);}
        var databases = context.Container.Resolve<IDbContextFactory>();
        return context.Container.Resolve<IPostgresEventTransaction>().ExecuteOnceAsync(async (transaction, cancellationToken) =>
        {
            await ParameterAcceptedProjection.ApplyAsync(databases, transaction, fact, cancellationToken).ConfigureAwait(false);
            return true;
        }, token);
    }
}
