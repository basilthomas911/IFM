using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Storage.ConfigurationDb;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Domain.Reference.Configuration.Strategy.Query.Actor;

/// <summary>Provides the closed-generic strategy-configuration Query context.</summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S6672", Justification = "Typed actor contexts intentionally retain ILogger of their owning actor, matching the actor context contract and diagnostic category.")]
public sealed class RegimeDiscoveryConfigurationQueryContext
    : QueryActorContext,
      IRegimeDiscoveryConfigurationQueryContext
{
    readonly Lazy<IConfigurationDbContext> configurationDb;

    /// <summary>Initializes the Query context.</summary>
    public RegimeDiscoveryConfigurationQueryContext(
        IActorSupervisor supervisor,
        ILogger<RegimeDiscoveryConfigurationQueryActor> logger)
        : base(supervisor, new ActorMailboxId(ActorType.Query, RegimeDiscoveryConfigurationQueryActor.ActorName))
    {
        Logger = IsArgumentNull.Set(logger);
        configurationDb = new(() => IsArgumentNull.Set(Container.Resolve<IConfigurationDbContext>())!);
    }

    /// <inheritdoc />
    public IConfigurationDbContext ConfigurationDb => configurationDb.Value;
    /// <inheritdoc />
    public ILogger<RegimeDiscoveryConfigurationQueryActor> Logger { get; }
}
