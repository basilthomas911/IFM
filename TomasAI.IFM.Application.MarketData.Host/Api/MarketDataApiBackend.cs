using TomasAI.IFM.Application.MarketData.Contracts;
using TomasAI.IFM.Application.MarketData.Databento;
using TomasAI.IFM.Application.MarketData.Databento.Resiliency;
using TomasAI.IFM.Application.MarketData.Databento.Workers;
using TomasAI.IFM.Application.MarketData.Historical;
using TomasAI.IFM.Application.MarketData.Pricing;
using TomasAI.IFM.Application.MarketData.Remote;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;

namespace TomasAI.IFM.Application.MarketData.Host;

/// <summary>Host implementation of the public contract; child ownership never crosses into API callers.</summary>
public sealed partial class MarketDataApiBackend(MarketDataHostConnection connection, DatabentoMarketDataApi reads,
    MarketDataWorkerHost host, IServiceProvider services) : MarketDataApiClient(connection, reads)
{
    readonly SemaphoreSlim lifecycle = new(1, 1);
    DateOnly? activeDate;

    protected override async Task<T> CallAsync<T>(string operation, Type[] types, object?[] args, CancellationToken token)
    {
        object? result;
        switch (operation)
        {
            case nameof(RepairOptionObservationsAsync): result = await host.RepairOptionObservationsAsync((OptionObservationRepairRequest)args[0]!, token).ConfigureAwait(false); break;
            case nameof(CloseTradeFeedRecoveryAsync):
                await TradeRecovery.CloseAsync((DateOnly)args[0]!, token).ConfigureAwait(false); result = true; break;
            case nameof(ReadTradeFeedRecoveryAsync):
                result = await TradeRecovery.ReadAsync((DateOnly)args[0]!, token).ConfigureAwait(false); break;
            case nameof(LoadTradeFeedRecoveryAsync):
                result = await TradeRecovery.LoadAsync((string)args[0]!, (DateOnly)args[1]!, false, token).ConfigureAwait(false); break;
            case nameof(EnableTradeFeedRecoveryAsync):
                result = await TradeRecovery.LoadAsync((string)args[0]!, (DateOnly)args[1]!, true, token).ConfigureAwait(false); break;
            case nameof(DisableTradeFeedRecoveryAsync):
                await TradeRecovery.DisableAsync((string)args[0]!, (DateOnly)args[1]!, token).ConfigureAwait(false); result = true; break;
            case nameof(InjectDevelopmentOptionWorkerFaultAsync):
                result = await host.InjectDevelopmentFaultAsync((MarketDataDevelopmentFaultRequest)args[0]!, services.GetRequiredService<IHostEnvironment>(), token).ConfigureAwait(false); break;
            case nameof(WarmTreasuryPricingAsync):
                result = await services.GetRequiredService<TreasuryPricingProvider>().GetAsync(DateTimeOffset.UtcNow, 0,
                    services.GetRequiredService<TreasuryPublicationPolicy>(), services.GetRequiredService<TomasAI.IFM.Framework.MarketData.Contracts.TreasuryRateConversionPolicy>(), token).ConfigureAwait(false); break;
            case nameof(ReadTreasuryCurveAsync):
                result = await services.GetRequiredService<TomasAI.IFM.Framework.MarketData.Contracts.ITreasuryCurve>().GetLatestAsync((DateOnly)args[0]!, token).ConfigureAwait(false); break;
            case nameof(ReadTreasuryCurvesAsync):
                result = (await services.GetRequiredService<TomasAI.IFM.Framework.MarketData.Contracts.ITreasuryCurve>().GetRangeAsync((DateOnly)args[0]!, (DateOnly)args[1]!, token).ConfigureAwait(false)).ToArray(); break;
            case nameof(ReadEconomicCalendarAsync):
                result = (await services.GetRequiredService<TomasAI.IFM.Framework.MarketData.Contracts.IEconomicCalendar>().GetAsync((DateOnly)args[0]!, (DateOnly)args[1]!, ((string[]?)args[2])?.ToHashSet(StringComparer.OrdinalIgnoreCase), token).ConfigureAwait(false)).ToArray(); break;
            case nameof(RefreshInstrumentDefinitionsAsync):
                result = await services.GetRequiredService<TradeStrategySymbols.InstrumentDefinitionRefresh>().RefreshAsync(token).ConfigureAwait(false); break;
            case nameof(RefreshOptionCalendarAsync):
                result = await services.GetRequiredService<OptionChains.OptionContractExpiryCalendarRefreshService>().RefreshAsync((string)args[0]!, token).ConfigureAwait(false); break;
            case nameof(EvaluateOptionChainAsync):
                result = await OptionChains.QualifiedEvaluatedOptionChain.ExecuteAsync((TomasAI.IFM.Domain.MarketData.Shared.Queries.GetEvaluatedOptionChainQuery)args[0]!, services.GetRequiredService<OptionChains.HostOptionChainContext>(), token).ConfigureAwait(false); break;
            case nameof(ResetMarketDataAsync):
                foreach (var target in host.GetBackendStatus().Datasets.Where(target => target.Running && target.ValueDate == (DateOnly)args[0]!))
                {
                    var reset = await host.RecoverAsync(new(Guid.NewGuid(), target.Role, target.Dataset, target.WorkerInstanceId,
                        target.GenerationId, (DateOnly)args[0]!, "Requested market-data reset: " + args[1]), token).ConfigureAwait(false);
                    if (!reset.Recovered) throw new InvalidOperationException(reset.Error);
                }
                result = true; break;
            case nameof(ReconcileCommittedSubscriptionsAsync):
                result = await services.GetRequiredService<Subscriptions.DurableCompositionRuntime>().ReconcileOnceAsync(token).ConfigureAwait(false); break;
            case nameof(ReleaseCompositionAsync):
                result = await services.GetRequiredService<ICompositionMarketDataApi>().ReleaseAsync((string)args[0]!, (WorkerOptionChainRelease)args[1]!, token).ConfigureAwait(false); break;
            case nameof(AcquireMonitoredOptionAsync):
                services.GetRequiredService<QualifiedIndividualOptionFeeds>().ValidateOwner((TomasAI.IFM.Framework.MarketData.Contracts.Ticker.TickerStreamOwner)args[0]!);
                await EnsureOptionsAsync((DateOnly)args[2]!, token).ConfigureAwait(false);
                await services.GetRequiredService<QualifiedIndividualOptionFeeds>().AcquireAsync(
                    (TomasAI.IFM.Framework.MarketData.Contracts.Ticker.TickerStreamOwner)args[0]!,
                    (FuturesOptionContractReadModel)args[1]!, (DateOnly)args[2]!).WaitAsync(token).ConfigureAwait(false);
                result = true; break;
            case nameof(ReleaseMonitoredOptionAsync):
                await services.GetRequiredService<QualifiedIndividualOptionFeeds>().ReleaseAsync(
                    (TomasAI.IFM.Framework.MarketData.Contracts.Ticker.TickerStreamOwner)args[0]!, (string)args[1]!).WaitAsync(token).ConfigureAwait(false);
                result = true; break;
            case nameof(ReadFiveMinuteSeedBarsAsync):
                result = await services.GetRequiredService<Seeding.FourHourDatabentoSeedReplay>().ReadAsync((string)args[0]!, token).ConfigureAwait(false); break;
            case nameof(GetBackendReadSnapshotAsync):
                var snapshot = host.CaptureReadSnapshot();
                var contractsForMirror = PreparedContracts();
                var seeds = new List<MarketDataBootstrapPrice>();
                var currentValues = services.GetRequiredService<Databento.Workers.DatasetWorkerCurrentValues>();
                foreach (var contractForMirror in contractsForMirror)
                    if (currentValues.TryGetBootstrapPrice(contractForMirror.ContractId, out var priceForMirror, out var sourceForMirror))
                        seeds.Add(new(priceForMirror, sourceForMirror));
                result = snapshot with { Contracts = contractsForMirror, BootstrapPrices = seeds.ToArray() }; break;
            case nameof(PrepareSessionAsync):
                await services.GetRequiredService<IDatabentoContractAuthority>().ReconcileAsync((DateOnly)args[0]!, nameof(MarketDataApiBackend), token).ConfigureAwait(false);
                result = PreparedContracts(); break;
            case nameof(StartAsync): await StartBackendAsync((DateOnly)args[0]!, token).ConfigureAwait(false); result = true; break;
            case nameof(StopAsync): await StopBackendAsync((DateOnly)args[0]!, token).ConfigureAwait(false); result = true; break;
            case nameof(GetBackendStatusAsync): result = host.GetBackendStatus(); break;
            case nameof(RecoverMarketDataAsync): result = await host.RecoverAsync((MarketDataRecoveryRequest)args[0]!, token).ConfigureAwait(false); break;
            case nameof(AcquireOptionScopeAsync):
                result = await services.GetRequiredService<QualifiedCompositionDiscovery>().AcquireAsync((CompositionDiscoveryRequest)args[0]!, token, renewAutomatically: true).ConfigureAwait(false); break;
            case nameof(ReleaseOptionScopeAsync):
                result = await services.GetRequiredService<QualifiedCompositionDiscovery>().ReleaseAsync((WorkerOptionChainRequest)args[0]!, token).ConfigureAwait(false); break;
            case nameof(CaptureCompositionAsync):
                result = await services.GetRequiredService<ICompositionMarketDataApi>().CaptureAsync((string)args[0]!, (CompositionSnapshotRequest)args[1]!, token).ConfigureAwait(false); break;
            case nameof(CaptureOptionRiskAsync):
                result = await services.GetRequiredService<IndividualOptionRiskReader>().CaptureAsync((string)args[0]!, (DateOnly)args[1]!, token).ConfigureAwait(false); break;
            case nameof(PrepareCompositionAsync):
                result = await services.GetRequiredService<CompositionMarketPreparation>().PrepareAsync((CompositionPreparationKey)args[0]!,
                    (CompositionMarketDataPlan)args[1]!, (string)args[2]!, (DateTimeOffset)args[3]!, token).ConfigureAwait(false); break;
            case nameof(PrepareCachedCompositionAsync):
                result = await services.GetRequiredService<CompositionMarketPreparation>().PrepareCachedAsync((CompositionPreparationKey)args[0]!,
                    reads.OptionChainCache, (OptionChainSnapshotRequest)args[1]!, token).ConfigureAwait(false); break;
            case nameof(EstimateHistoricalAsync):
                result = await services.GetRequiredService<Contracts.Historical.IMarketDataHistoricalApi>().EstimateAsync(
                    (Contracts.Historical.MarketDataHistoricalRequest)args[0]!, token).ConfigureAwait(false); break;
            case nameof(LoadHistoricalAsync):
                result = await services.GetRequiredService<HistoricalDataLoader>().ExecuteAsync(
                    (Contracts.Historical.MarketDataHistoricalRequest)args[0]!, token).ConfigureAwait(false); break;
            case nameof(GetFuturesPriceAsync): result = await reads.GetFuturesPriceAsync((string)args[0]!).ConfigureAwait(false); break;
            case nameof(UpdateFuturesTermStructureContractsAsync):
            case nameof(UpdateOnTheRunFuturesContractAsync):
            case nameof(GetTradeStrategySymbolsAsync):
                return MarketDataHostConnection.Decode<T>(await new MarketDataApiDispatcher(reads).InvokeAsync(Call(operation, types, args), token).ConfigureAwait(false));
            case nameof(GetFuturesContractsAsync):
                var contracts = new List<FuturesContractV3ReadModel>();
                foreach (var id in (string[])args[0]!)
                {
                    var item = await CallAsync<FuturesContractV3ReadModel?>(nameof(GetFuturesContractAsync), [typeof(string)], [id], token).ConfigureAwait(false);
                    if (item is not null) contracts.Add(item);
                }
                result = contracts.ToArray(); break;
            default:
                var role = operation.Contains("Option", StringComparison.Ordinal) ? "Options" : "Futures";
                var contract = args.OfType<string>().FirstOrDefault() ?? "ES";
                var root = new string(contract.TakeWhile(char.IsLetter).ToArray());
                var runtimeOptions = services.GetRequiredService<DatabentoMarketDataRuntimeOptions>();
                var dataset = runtimeOptions.FuturesContractDatasets.GetValueOrDefault(root)
                    ?? (string.Equals(root, "VX", StringComparison.OrdinalIgnoreCase) ? "XCBF.PITCH" : runtimeOptions.FeedOptions.Dataset);
                var owner = host.GetBackendStatus().Datasets.SingleOrDefault(state => state.Role == role && state.Dataset == dataset && state.Running)
                    ?? throw new InvalidOperationException($"The {role} dataset {dataset} is not active.");
                var worker = host.RequireWorker(role, owner.WorkerInstanceId);
                return MarketDataHostConnection.Decode<T>(await worker.InvokeMarketDataApiAsync(Call(operation, types, args), token).ConfigureAwait(false));
        }
        return (T)result!;
    }

    static MarketDataApiCall Call(string operation, Type[] types, object?[] args)
        => new(Signature(typeof(IMarketDataApi).GetMethod(operation, types) ?? throw new MissingMethodException(operation)),
            args.Select(value => value is null || value is CancellationToken ? [] : MessagePack.MessagePackSerializer.Serialize(value.GetType(), value, MarketDataHostConnection.Serialization)).ToArray());

    FuturesContractV3ReadModel[] PreparedContracts()
    {
        var output = new Dictionary<string, FuturesContractV3ReadModel>(StringComparer.Ordinal);
        foreach (var registration in services.GetRequiredService<IDatabentoContractRegistrationRegistry>().Snapshot())
        {
            if (reads.TryGetFuturesTermStructureContracts(registration.RootSymbol!, out var pair))
            { output[pair.Front.ContractId] = pair.Front; output[pair.Back.ContractId] = pair.Back; }
            else if (reads.TryGetOnTheRunFuturesContract(registration.RootSymbol!, out var contract)) output[contract.ContractId] = contract;
        }
        return output.Values.ToArray();
    }

    async Task StartBackendAsync(DateOnly date, CancellationToken token)
    {
        await lifecycle.WaitAsync(token).ConfigureAwait(false);
        try
        {
            if (activeDate == date) return;
            if (activeDate is not null) throw new InvalidOperationException("Close the current market-data value date before starting another.");
            await services.GetRequiredService<IDatabentoContractAuthority>().ReconcileAsync(date, nameof(MarketDataApiBackend), token).ConfigureAwait(false);
            var registry = services.GetRequiredService<IDatabentoContractRegistrationRegistry>();
            foreach (var group in registry.Snapshot().GroupBy(contract => contract.Dataset!, StringComparer.Ordinal)
                .OrderBy(group => group.Key == "XCBF.PITCH" ? 0 : 1))
            {
                var manifest = services.GetRequiredService<DatasetDesiredSubscriptionRegistry>().Set(group.Key, date, group);
                await host.StartWorkerAsync("Futures", new(manifest, Guid.NewGuid(), Guid.NewGuid()), token).ConfigureAwait(false);
                if (group.Key == "GLBX.MDP3")
                    await host.StartWorkerAsync("Options", new(manifest, Guid.NewGuid(), Guid.NewGuid()), token).ConfigureAwait(false);
            }
            activeDate = date;
        }
        catch
        {
            await Task.WhenAll(host.StopWorkerAsync("Options"), host.StopWorkerAsync("Futures")).ConfigureAwait(false);
            services.GetRequiredService<MarketDataHostState>().Futures.Stop();
            throw;
        }
        finally { lifecycle.Release(); }
    }

    async Task StopBackendAsync(DateOnly date, CancellationToken token)
    {
        await lifecycle.WaitAsync(token).ConfigureAwait(false);
        try
        {
            if (activeDate is { } current && current != date) throw new InvalidOperationException("Market-data stop value date differs.");
            await Task.WhenAll(host.StopWorkerAsync("Options"), host.StopWorkerAsync("Futures")).ConfigureAwait(false);
            services.GetRequiredService<MarketDataHostState>().Futures.Stop();
            await services.GetRequiredService<MarketDataHostState>().RetireOptions!(token).WaitAsync(token).ConfigureAwait(false);
            activeDate = null;
        }
        finally { lifecycle.Release(); }
    }

    async Task EnsureOptionsAsync(DateOnly date, CancellationToken token)
    {
        await TomasAI.IFM.Application.MarketData.Recovery.OptionWorkerReadiness.EnsureAsync(ObserveAsync, RestartStoppedAsync, token).ConfigureAwait(false);

        async Task<MarketDataDatasetStatus?> ObserveAsync(CancellationToken cancellation)
        {
            await lifecycle.WaitAsync(cancellation).ConfigureAwait(false);
            try
            {
                if (activeDate != date) throw new InvalidOperationException("The market-data session is not active for this value date.");
                return host.GetBackendStatus().Datasets.SingleOrDefault(state => state.Role == "Options" && state.Dataset == "GLBX.MDP3");
            }
            finally { lifecycle.Release(); }
        }

        async Task RestartStoppedAsync(CancellationToken cancellation)
        {
            await lifecycle.WaitAsync(cancellation).ConfigureAwait(false);
            try
            {
                if (activeDate != date) throw new InvalidOperationException("The market-data session is not active for this value date.");
                // Another caller may have joined/restored the worker since inspection.
                var current = host.GetBackendStatus().Datasets.SingleOrDefault(state => state.Role == "Options" && state.Dataset == "GLBX.MDP3");
                if (current?.Lifecycle != "StoppedError") return;
                var manifest = host.CaptureReadSnapshot().Manifests.Single(manifest => manifest.Dataset == "GLBX.MDP3");
                await host.StartWorkerAsync("Options", new(manifest, Guid.NewGuid(), Guid.NewGuid()), cancellation).ConfigureAwait(false);
            }
            finally { lifecycle.Release(); }
        }
    }
}
