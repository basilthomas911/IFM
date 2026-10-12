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
