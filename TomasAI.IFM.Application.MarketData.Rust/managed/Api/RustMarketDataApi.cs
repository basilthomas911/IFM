using TomasAI.IFM.Application.MarketData.Contracts;
using TomasAI.IFM.Application.MarketData.Databento;
using TomasAI.IFM.Application.MarketData.Databento.Resiliency;
using TomasAI.IFM.Application.MarketData.Databento.Workers;
using TomasAI.IFM.Domain.MarketData.Shared.ServiceApi;

namespace TomasAI.IFM.Application.MarketData.Rust.Api;

/// <summary>Typed command/query facade for the Rust market-data owner. The
/// dataset-owner constructor requires every application and Host capability;
/// operations cannot fall through to unsupported interface defaults. The older
/// epoch constructor remains for prototype qualification compatibility only.</summary>
/// <remarks>This facade is not a production selector registration. Its concrete
/// native epoch and Host service composition must pass the full acceptance matrix
/// before the new backend can be enabled. The existing backend is unchanged.</remarks>
public sealed partial class RustMarketDataApi : IMarketDataApi, IAsyncDisposable
{
    /// <summary>Dataset detection and recovery are native-owned.</summary>
    public MarketDataRecoveryOwnership DatasetRecoveryOwnership => datasetOwner ? MarketDataRecoveryOwnership.TokioDatasetOwner : MarketDataRecoveryOwnership.Process;
    readonly DatabentoMarketDataApi? policy;
    readonly IRustMarketDataEpochFactory? epochs;
    readonly SemaphoreSlim apiLifecycle=new(1);
    readonly object disposalGate=new();Task? disposal;
    readonly IRustMarketDataApplicationServices application;
    readonly bool datasetOwner;
    readonly IRustMarketDataHostServices host;
    readonly IRustMarketDataLifecycle? lifecycle;

    /// <summary>Composes application policy with native-only epoch ownership and
    /// the complete Host operation contract. No current-provider factory or
    /// unavailable option-cache default is selected by this constructor.</summary>
    public RustMarketDataApi(IRustMarketDataEpochFactory epochs, IRustMarketDataHostServices host,
        DatabentoMarketDataApiOptions options, IDatabentoCurrentFuturesContractResolver? currentContractResolver = null,
        IFuturesContractRolloverStore? rolloverStore = null, IDatabentoContractRegistrationRegistry? contractRegistry = null,
        DatasetWorkerCurrentValues? currentValues = null, ITradeStrategySymbolCatalog? symbolCatalog = null, IRustMarketDataLifecycle? lifecycle = null)
    {
        ArgumentNullException.ThrowIfNull(epochs); ArgumentNullException.ThrowIfNull(host); ArgumentNullException.ThrowIfNull(options);
        this.host = host;this.lifecycle=lifecycle;this.epochs=epochs;
        policy = new(epochs, options, epochs.Clock, currentContractResolver, rolloverStore, contractRegistry,
            currentValues, symbolCatalog, host.OptionChainCache ?? throw new ArgumentException("Native Host must supply its option cache.", nameof(host)));
        application = new PrototypeApplicationServices(policy);
    }
    /// <summary>Composes the complete native dataset owner with mandatory typed
    /// command/query and Host adapters. This path constructs no managed provider
    /// epoch, discovery coordinator, renewal loop, or DatabentoMarketDataApi.</summary>
    public RustMarketDataApi(IRustMarketDataApplicationServices application,IRustMarketDataHostServices host)
    {
        ArgumentNullException.ThrowIfNull(application);ArgumentNullException.ThrowIfNull(host);
        this.application=application;this.host=host;datasetOwner=true;
    }
    /// <inheritdoc/>
    public IOptionChainCache OptionChainCache => host.OptionChainCache;
    /// <inheritdoc/>
    public bool CoreFuturesRoutesAreRuntimeOwned => true;
    /// <summary>Disposes the application-owned native epoch through its explicit
    /// asynchronous lifecycle. Host service ownership remains with composition.</summary>
    public ValueTask DisposeAsync(){lock(disposalGate)return new(disposal??=DisposeCoreAsync());}
    async Task DisposeCoreAsync()
    {
        if(datasetOwner){await application.DisposeAsync().ConfigureAwait(false);return;}
        Exception? failed=null;
        if(epochs is NativeMarketDataEpochFactory native&&native.TryGetCurrent(out var current))
            try{await StopAsync(current!.ValueDate).ConfigureAwait(false);}catch(Exception error){failed=error;}
        try{await policy!.DisposeAsync().ConfigureAwait(false);}catch(Exception error)when(failed is not null){throw new AggregateException(failed,error);}
        if(failed is not null)System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failed).Throw();
    }
}
