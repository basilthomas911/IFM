using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Storage.ConfigurationDb;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Domain.Reference.Configuration.Strategy.Query.Actor;

/// <summary>Defines readonly services owned by the strategy-configuration Query actor.</summary>
public interface IRegimeDiscoveryConfigurationQueryContext
    : IQueryActorContext<RegimeDiscoveryConfigurationQueryActor>
{
    /// <summary>Gets ConfigurationDb.</summary>
    IConfigurationDbContext ConfigurationDb { get; }
    /// <summary>Gets the logger.</summary>
    ILogger<RegimeDiscoveryConfigurationQueryActor> Logger { get; }
}

/// <summary>Provides the closed-generic strategy-configuration Query context.</summary>
public sealed class RegimeDiscoveryConfigurationQueryContext
    : QueryActorContext,
      IQueryActorContext<RegimeDiscoveryConfigurationQueryActor>,
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
