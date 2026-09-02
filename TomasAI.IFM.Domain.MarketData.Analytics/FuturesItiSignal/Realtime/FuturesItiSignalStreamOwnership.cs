using TomasAI.IFM.Application.MarketData.Contracts;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Framework.MarketData.Contracts.Ticker;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesItiSignal.Realtime;

/// <summary>
/// Owns the current ES and VX live-stream registrations required by the realtime
/// ITI workflow. The actor mailbox serializes calls to this lifecycle object.
/// </summary>
public sealed class FuturesItiSignalStreamOwnership
{
    const string WorkflowType = "FuturesItiSignal";
    const string WorkflowId = "CurrentContracts";

    static readonly TickerStreamOwner EsOwner = new(WorkflowType, WorkflowId, "ES");
    static readonly TickerStreamOwner VxOwner = new(WorkflowType, WorkflowId, "VX");

    string? _esContractId;
    string? _vxContractId;

    /// <summary>
    /// Resolves the startup-validated current contracts and idempotently acquires
    /// the actor's stable ES and VX registrations. Changed rollover contracts are
    /// acquired before the old registrations are released.
    /// </summary>
    public async ValueTask<FuturesItiSignalStreamContracts> EnsureAsync(
        IMarketDataApi marketDataApi)
    {
        ArgumentNullException.ThrowIfNull(marketDataApi);

        var esContract = ResolveCurrentContract(marketDataApi, "ES");
        var vxContract = ResolveCurrentContract(marketDataApi, "VX");
        var esChanged = !StringComparer.Ordinal.Equals(_esContractId, esContract.ContractId);
        var vxChanged = !StringComparer.Ordinal.Equals(_vxContractId, vxContract.ContractId);

        if (!esChanged && !vxChanged
            && marketDataApi.IsTickDataStreamActive(esContract.ContractId)
            && marketDataApi.IsTickDataStreamActive(vxContract.ContractId))
        {
            return new(esContract, vxContract);
        }

        var acquiredEs = false;
        var acquiredVx = false;
        try
        {
            if (esChanged || !marketDataApi.IsTickDataStreamActive(esContract.ContractId))
            {
                _ = await marketDataApi.StartStreamingFuturesTickDataAsync(
                    esContract.ContractId,
                    EsOwner).ConfigureAwait(false);
                acquiredEs = true;
            }

            if (vxChanged || !marketDataApi.IsTickDataStreamActive(vxContract.ContractId))
            {
                _ = await marketDataApi.StartStreamingFuturesTickDataAsync(
                    vxContract.ContractId,
                    VxOwner).ConfigureAwait(false);
                acquiredVx = true;
            }
        }
        catch
        {
            if (acquiredVx)
                await TryReleaseAsync(marketDataApi, vxContract.ContractId, VxOwner).ConfigureAwait(false);
            if (acquiredEs)
                await TryReleaseAsync(marketDataApi, esContract.ContractId, EsOwner).ConfigureAwait(false);
            throw;
        }

        var previousEs = _esContractId;
        var previousVx = _vxContractId;
        _esContractId = esContract.ContractId;
        _vxContractId = vxContract.ContractId;

        if (previousEs is not null && !StringComparer.Ordinal.Equals(previousEs, _esContractId))
            await TryReleaseAsync(marketDataApi, previousEs, EsOwner).ConfigureAwait(false);
        if (previousVx is not null && !StringComparer.Ordinal.Equals(previousVx, _vxContractId))
            await TryReleaseAsync(marketDataApi, previousVx, VxOwner).ConfigureAwait(false);

        return new(esContract, vxContract);
    }

    /// <summary>
    /// Releases registrations acquired by this actor. A stopped market-data epoch
    /// has already discarded all registrations and is therefore treated as clean.
    /// </summary>
    public async ValueTask ReleaseAsync(IMarketDataApi marketDataApi)
    {
        ArgumentNullException.ThrowIfNull(marketDataApi);

        var esContractId = _esContractId;
        var vxContractId = _vxContractId;
        _esContractId = null;
        _vxContractId = null;

        if (vxContractId is not null)
            await TryReleaseAsync(marketDataApi, vxContractId, VxOwner).ConfigureAwait(false);
        if (esContractId is not null)
            await TryReleaseAsync(marketDataApi, esContractId, EsOwner).ConfigureAwait(false);
    }

    static FuturesContractV3ReadModel ResolveCurrentContract(
        IMarketDataApi marketDataApi,
        string symbol)
    {
        if (marketDataApi.TryGetOnTheRunFuturesContract(symbol, out var contract))
            return contract;

        throw new FuturesContractRolloverConfigurationException(
            $"The current {symbol} futures contract is not available in the startup rollover registry.");
    }

    static async ValueTask TryReleaseAsync(
        IMarketDataApi marketDataApi,
        string contractId,
        TickerStreamOwner owner)
    {
        try
        {
            _ = await marketDataApi.StopStreamingFuturesTickDataAsync(contractId, owner)
                .ConfigureAwait(false);
        }
        catch (MarketDataApiNotRunningException)
        {
            // Stopping an epoch atomically retires all of its stream registrations.
        }
        catch (MarketDataContractNotFoundException)
        {
            // A replacement epoch no longer contains the retired rollover contract.
        }
    }
}

/// <summary>Current futures contracts owned by the realtime ITI workflow.</summary>
public readonly record struct FuturesItiSignalStreamContracts(
    FuturesContractV3ReadModel Es,
    FuturesContractV3ReadModel Vx);
