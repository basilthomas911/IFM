using TomasAI.IFM.Shared.MarketData.Definitions;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Collections.Immutable;
using TomasAI.IFM.Application.MarketData.Pricing;
using TomasAI.IFM.Application.MarketData.Databento.Resiliency;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Domain.MarketData.Query.Actor;
using TomasAI.IFM.Domain.MarketData.Shared;
using TomasAI.IFM.Domain.MarketData.Shared.Queries;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Shared.MarketData.Contracts.Pricing;
using TomasAI.IFM.Framework.MarketData.DataBento;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.MarketData.Query;
static class QualifiedEvaluatedOptionChain
{
    static readonly ConcurrentDictionary<string, SemaphoreSlim> ReferencePublicationGates = new(StringComparer.Ordinal);
    static readonly ConcurrentDictionary<(string ContractId, string MappingVersion), byte> PublishedReferences = new();
    public static async Task<ServiceResult<EvaluatedOptionChainReadModel>> ExecuteAsync(GetEvaluatedOptionChainQuery query, IMarketDataQueryContext context, CancellationToken token)
    {
        if (context.MarketDataApi is null) return new ServiceFailed<EvaluatedOptionChainReadModel>(503, "Market-data Host client is unavailable.");
        var result = await context.MarketDataApi.EvaluateOptionChainAsync(query, token).ConfigureAwait(false);
        // Emulator matching consumes the backend-generated display quotes without repricing them.
        if (result.Success && result.Value is { } chain && query.AllowFrozenEmulatorPreview)
            FrozenEmulatorQuoteStore.Publish(chain.Contracts, chain.AsOfUtc);
        return result;
    }

    internal static async Task PublishSelectedReferencesAsync(string key, TomasAI.IFM.Application.Storage.SecuritiesDb.ISecuritiesDbContext securities, Microsoft.Extensions.Logging.ILogger logger, FuturesOptionContractReadModel[] contracts, CancellationToken token)
    {
        var gate = ReferencePublicationGates.GetOrAdd(key, static _ => new(1, 1));
        await (gate.WaitAsync(token)).ConfigureAwait(false);
        try
        {
            var selected = contracts.DistinctBy(static contract => (contract.ContractId, contract.MappingVersion)).ToArray();
            await (Parallel.ForEachAsync(selected, new ParallelOptions { CancellationToken = token, MaxDegreeOfParallelism = Math.Min(8, selected.Length) }, async (definition, cancellationToken) =>
            {
                if (definition.ReviewState != ReferenceReviewState.Reviewed || string.IsNullOrWhiteSpace(definition.MappingVersion))
                {
                    throw new InvalidDataException($"Selected option definition '{definition.ContractId}' is not reviewed.");
                }

                var referenceKey = (definition.ContractId, definition.MappingVersion);
                if (PublishedReferences.ContainsKey(referenceKey))
                {
                    return;
                }

                if (await securities.GetReferenceVersionAsync(definition.ContractId, definition.MappingVersion, cancellationToken).ConfigureAwait(false)is not null)
                {
                    PublishedReferences.TryAdd(referenceKey, 0);
                    return;
                }

                var pending = await securities.StageReferenceVersionAsync(definition, cancellationToken).ConfigureAwait(false);
                await securities.CommitReferenceVersionAsync(pending, cancellationToken).ConfigureAwait(false);
                PublishedReferences.TryAdd(referenceKey, 0);
            })).ConfigureAwait(false);
            global::TomasAI.IFM.Domain.MarketData.ActorLifecycleDiagnosticLog.Write9(logger, null, key, selected.Length);
        }
        finally
        {
            gate.Release();
        }
    }

    internal static OptionDefinitionCandidate Candidate(FuturesOptionContractReadModel value)
    {
        if (value.PublisherId is null || value.InstrumentId is null || value.ExpirationUtc is null || value.MappingVersion is null || value.DefinitionDigest is null || value.RawSymbol is null || value.Dataset is null || value.UnderlyingContractId is null)
        {
            throw new InvalidDataException("Option definition is not reviewed for live pricing.");
        }

        if (value.OptionRight is not (ReferenceOptionRight.Call or ReferenceOptionRight.Put))
        {
            throw new InvalidDataException("Option definition has no qualified call/put right.");
        }

        var definition = new OptionContractDefinition
        {
            Dataset = value.Dataset,
            RawSymbol = value.RawSymbol,
            Ticker = value.Symbol,
            Underlying = value.UnderlyingContractId,
            Instrument = new(value.PublisherId.Value, value.InstrumentId.Value),
            Right = value.OptionRight == ReferenceOptionRight.Call ? OptionRightSelection.Call : OptionRightSelection.Put,
            StrikePrice = value.GetExactStrikePrice(),
            MaturityDate = DateOnly.FromDateTime(value.ExpirationUtc.Value.UtcDateTime),
            ExpirationTimestampNanoseconds = ToNanoseconds(value.ExpirationUtc.Value),
            ContractMultiplier = value.MultiplierValue is null ? null : checked((int)value.MultiplierValue.Value)
        };
        return new(value.ContractId, value.MappingVersion, value.DefinitionDigest, definition);
    }

    static ulong ToNanoseconds(DateTimeOffset value) => checked((ulong)(value.UtcTicks - DateTimeOffset.UnixEpoch.UtcTicks) * 100UL);
}
