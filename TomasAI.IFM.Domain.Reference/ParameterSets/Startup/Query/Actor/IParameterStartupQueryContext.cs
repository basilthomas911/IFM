using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Storage.ConfigurationDb;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.RegimeDiscovery;
using TomasAI.IFM.Domain.Reference.Shared.ParameterSets;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
namespace TomasAI.IFM.Domain.Reference.ParameterSets.Startup.Query.Actor;

/// <summary>Services owned by the startup query actor.</summary>
public interface IParameterStartupQueryContext : IQueryActorContext<ParameterStartupQueryActor>, IParameterStartupQueryServices
{
    ILogger<ParameterStartupQueryActor> Logger { get; }
}
