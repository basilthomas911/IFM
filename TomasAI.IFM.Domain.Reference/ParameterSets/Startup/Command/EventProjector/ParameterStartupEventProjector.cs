using TomasAI.IFM.Domain.Reference.Shared.ProjectionOutcomes;
using TomasAI.IFM.Domain.Reference.ParameterSets.Persistence;
using TomasAI.IFM.Domain.Reference.ParameterSets.Startup.Command.Actor;
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
namespace TomasAI.IFM.Domain.Reference.ParameterSets.Startup.Command.EventProjector;

public sealed class ParameterStartupEventProjector : ConventionalEventProjector<ParameterStartupCommandActor>
{
    readonly IParameterStartupCommandContext context;
    readonly EventProjectionDescriptor[] descriptors;
    public ParameterStartupEventProjector(ICommandActorContext<ParameterStartupCommandActor> value,
        EventProjectorReliabilityOptions? reliabilityOptions = null)
     : base(((IParameterStartupCommandContext)(value ?? throw new ArgumentNullException(nameof(value)))).DurableReplayQueue, ((IParameterStartupCommandContext)value).DbEventSource, ((IParameterStartupCommandContext)value).Logger, reliabilityOptions)
    {
        ArgumentNullException.ThrowIfNull(value);
        context = (IParameterStartupCommandContext)value;
        descriptors = [Describe<ParameterStartupCreatedEvent>(), Describe<ParameterStartupChangedEvent>()];
    }
    public override IReadOnlyCollection<EventProjectionDescriptor> ProjectionDescriptors => descriptors;
    EventProjectionDescriptor Describe<T>() where T : class, IEvent<ParameterStartupEntityId> => new EventProjectionDescriptor(typeof(T), EventProjectionIdempotencyStrategy.NaturalKeyMutation,
        async (fact, execution) =>
        {
            await RebuildAsync(fact, execution.CancellationToken).ConfigureAwait(false);
            return new EventProjectionApplyResult(EventProjectionApplyOutcome.Applied);
        },
        fact => Complete((ParameterStartupChangedEvent)fact), (fact, error) => Fail((ParameterStartupChangedEvent)fact, error),
        publishProcessingEvent: false, useDurableReplay: true)
        .WithReadModelRebuild((fact, token) => RebuildAsync(fact, token));
    static ICompleteEvent Complete(ParameterStartupChangedEvent fact) =>
        fact is ParameterStartupCreatedEvent { ImportedFromRegistry: true } ? ReferenceProjectionOutcomeFactory.Complete<ParameterStartupEntityId, ParameterStartupRegistryImportedCompleteEvent>(fact, "ParameterStartupEvent")
        : fact.Released ? ReferenceProjectionOutcomeFactory.Complete<ParameterStartupEntityId, ParameterStartupReleasedCompleteEvent>(fact, "ParameterStartupEvent")
        : (fact.Report is not null || !string.IsNullOrEmpty(fact.ReportJson)) && fact is not ParameterStartupCreatedEvent ? ReferenceProjectionOutcomeFactory.Complete<ParameterStartupEntityId, ParameterStartupReportRecordedCompleteEvent>(fact, "ParameterStartupEvent")
        : ReferenceProjectionOutcomeFactory.Complete<ParameterStartupEntityId, ParameterStartupCreatedCompleteEvent>(fact, "ParameterStartupEvent");

    static IErrorEvent Fail(ParameterStartupChangedEvent fact, Exception error) =>
        fact is ParameterStartupCreatedEvent { ImportedFromRegistry: true } ? ReferenceProjectionOutcomeFactory.Fail<ParameterStartupEntityId, ParameterStartupRegistryImportedFailEvent>(fact, "ParameterStartupEvent", error)
        : fact.Released ? ReferenceProjectionOutcomeFactory.Fail<ParameterStartupEntityId, ParameterStartupReleasedFailEvent>(fact, "ParameterStartupEvent", error)
        : (fact.Report is not null || !string.IsNullOrEmpty(fact.ReportJson)) && fact is not ParameterStartupCreatedEvent ? ReferenceProjectionOutcomeFactory.Fail<ParameterStartupEntityId, ParameterStartupReportRecordedFailEvent>(fact, "ParameterStartupEvent", error)
        : ReferenceProjectionOutcomeFactory.Fail<ParameterStartupEntityId, ParameterStartupCreatedFailEvent>(fact, "ParameterStartupEvent", error);

    bool IsPostgres => string.Equals(context.ConfigurationDb.ProviderName, "System.Data.Postgres", StringComparison.Ordinal);

    Task RebuildAsync(IEvent fact, CancellationToken token)
    {
        if (!IsPostgres)
        {
            var startup = (ParameterStartupChangedEvent)fact;
            AcceptedParameterIdentity.RequireStartup(startup);
            return context.ConfigurationDb.ProjectParameterStartupAsync(startup, token);
        }
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
