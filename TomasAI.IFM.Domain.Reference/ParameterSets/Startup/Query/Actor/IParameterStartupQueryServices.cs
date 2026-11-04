using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Storage.ConfigurationDb;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.RegimeDiscovery;
using TomasAI.IFM.Domain.Reference.Shared.ParameterSets;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Domain.Reference.ParameterSets.Assignment.Query.Actor;
namespace TomasAI.IFM.Domain.Reference.ParameterSets.Startup.Query.Actor;

/// <summary>Read services for startup queries and legacy route compatibility.</summary>
public interface IParameterStartupQueryServices : IParameterAssignmentQueryServices
{
    IRegimeDiscoveryMarketSignalSnapshotProvider SignalSnapshots { get; }
}
