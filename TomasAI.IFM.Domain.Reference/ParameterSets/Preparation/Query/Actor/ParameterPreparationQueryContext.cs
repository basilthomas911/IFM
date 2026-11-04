using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Storage.ConfigurationDb;
using TomasAI.IFM.Domain.Reference.Shared.ParameterSets;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Domain.Reference.ParameterSets.Preparation.Query.Actor;

/// <summary>Resolves read services for preparing cross-owner command evidence.</summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S6672", Justification = "The logger category identifies the owning query actor, following the actor context convention.")]
public sealed class ParameterPreparationQueryContext : QueryActorContext, IParameterPreparationQueryContext
{
    readonly Lazy<IParameterAccessPolicy> access;
    readonly Lazy<IConfigurationDbContext> database;

    public ParameterPreparationQueryContext(IActorSupervisor supervisor, ILogger<ParameterPreparationQueryActor> logger)
        : base(supervisor, new ActorMailboxId(ActorType.Query, ParameterPreparationQueryActor.ActorName))
    {
        ArgumentNullException.ThrowIfNull(logger);
        Logger = logger;
        access = new(() => Container.Resolve<IParameterAccessPolicy>());
        database = new(() => Container.Resolve<IConfigurationDbContext>());
    }

    public IParameterAccessPolicy AccessPolicy => access.Value;
    public IConfigurationDbContext ConfigurationDb => database.Value;
    public ILogger<ParameterPreparationQueryActor> Logger { get; }
}
