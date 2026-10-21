using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.MarketData.Pricing;
using TomasAI.IFM.Application.MarketData.Databento.Resiliency;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Framework.MarketData.Contracts;
using TomasAI.IFM.Application.Blackboard;
using TomasAI.IFM.Domain.MarketData.Feed.FuturesBarData.Command.Model;
using TomasAI.IFM.Domain.Trade.Shared.Contracts;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Shared.StatusConsole.ServiceApi;
using ApplicationMarketDataApi = TomasAI.IFM.Application.MarketData.Contracts.IMarketDataApi;

namespace TomasAI.IFM.Domain.MarketData.Feed.FuturesOptionTickData.Event.Actor;

/// <summary>Provides the typed runtime context used by <see cref="FuturesOptionTickDataEventActor"/>.</summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S6672", Justification = "Typed actor contexts intentionally retain ILogger of their owning actor, matching the actor context contract and diagnostic category.")]
public sealed class FuturesOptionTickDataEventContext : EventActorContext, IFuturesOptionTickDataEventContext
{
    /// <summary>Initializes the typed event context.</summary>
    public FuturesOptionTickDataEventContext(
        IActorSupervisor supervisor,
        ILogger<FuturesOptionTickDataEventActor> logger,
        ApplicationMarketDataApi marketDataApi,
        IStatusConsoleWriter statusConsoleWriter,
        QualifiedCompositionDiscovery? discovery = null,
        DatasetWorkerAdmissionRegistry? admissions = null,
        IDbContextFactory? databases = null,
        TreasuryPublicationPolicy? publication = null,
        TreasuryRateConversionPolicy? conversion = null, IndividualOptionRiskReader? riskReader = null)
        : base(supervisor, new ActorMailboxId(ActorType.Event, FuturesOptionTickDataEventActor.Actor))
    {
        QualifiedFeeds = discovery is not null && admissions is not null && databases is not null
            && publication is not null && conversion is not null
            ? new(discovery, admissions, databases, publication, conversion, riskReader, logger) : null;
        Supervisor = IsArgumentNull.Set(supervisor);
        Logger = IsArgumentNull.Set(logger);
        MarketDataApi = IsArgumentNull.Set(marketDataApi);
        StatusConsoleWriter = IsArgumentNull.Set(statusConsoleWriter);
    }
    /// <inheritdoc/>
    public QualifiedIndividualOptionFeeds? QualifiedFeeds { get; }
    /// <inheritdoc/>
    public IActorSupervisor Supervisor { get; }
    /// <inheritdoc/>
    public ILogger<FuturesOptionTickDataEventActor> Logger { get; }
    /// <inheritdoc/>
    public ApplicationMarketDataApi MarketDataApi { get; }
    /// <inheritdoc/>
    public IStatusConsoleWriter StatusConsoleWriter { get; }
}

