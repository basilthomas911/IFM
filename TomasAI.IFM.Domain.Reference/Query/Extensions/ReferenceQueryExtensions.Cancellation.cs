#pragma warning disable CA1034 // C# 14 extension blocks generate nested metadata types; no public nested type is declared here.
#pragma warning disable CA1708 // Extension-block metadata names are compiler generated; public methods have distinct signatures.
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.Reference.Shared;
using TomasAI.IFM.Domain.Reference.Shared.Queries;
using TomasAI.IFM.Domain.Reference.Shared.ViewModels;
using TomasAI.IFM.Domain.Reference.Query.Actor;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Reference.Query.Extensions;

[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "C# extension members use their declared receiver; static conversion would remove the established actor context extension contract.")]
public static partial class ReferenceQueryExtensions
{
    extension(IReferenceQueryContext context)
    {
        public Task<ServiceResult<LookupTypeCollection>> GetMarketDataDefinitionTypesAsync(CancellationToken cancellationToken)
            => context.GetLookupTypesAsync("MarketDataDefinitionType", cancellationToken);

        public Task<ServiceResult<LookupTypeCollection>> GetReferenceDataDefinitionTypesAsync(CancellationToken cancellationToken)
            => context.GetLookupTypesAsync("ReferenceDataDefinitionType", cancellationToken);

        public Task<ServiceResult<LookupTypeCollection>> GetSystemAdminFunctionTypesAsync(CancellationToken cancellationToken)
            => context.GetLookupTypesAsync("SystemAdminFunctionType", cancellationToken);

        public Task<ServiceResult<LookupTypeCollection>> GetLookupTypesAsync(CancellationToken cancellationToken)
            => ExecuteAsync(
                GetLookupTypesQuery.ErrorId, async () => new LookupTypeCollection(
                    [.. await (context.DbFactory.ReferenceDb.GetLookupTypesAsync(cancellationToken)).ConfigureAwait(false)]), true, cancellationToken);

        public Task<ServiceResult<LookupTypeCollection>> GetLookupTypesAsync(
            string lookupTypeName,
            CancellationToken cancellationToken)
            => ExecuteAsync(
                GetLookupTypeQuery.ErrorId, async () => new LookupTypeCollection(
                    [.. await (context.DbFactory.ReferenceDb.GetLookupTypeAsync(lookupTypeName, cancellationToken)).ConfigureAwait(false)]), true, cancellationToken);

        public Task<ServiceResult<string[]>> GetLookupTypeNamesAsync(CancellationToken cancellationToken)
            => ExecuteAsync<string[]>(
                GetLookupTypeNamesQuery.ErrorId, async () => [.. await (context.DbFactory.ReferenceDb.GetLookupTypeNamesAsync(cancellationToken)).ConfigureAwait(false)], true, cancellationToken);

        public Task<ServiceResult<LookupTypeShortCodeReadModel[]>> GetLookupTypeShortCodesAsync(
            string lookupTypeName,
            CancellationToken cancellationToken)
            => ExecuteAsync<LookupTypeShortCodeReadModel[]>(
                GetLookupTypeShortCodesQuery.ErrorId, async () => [.. await (context.DbFactory.ReferenceDb
                .GetLookupTypeShortCodesAsync(lookupTypeName, cancellationToken)).ConfigureAwait(false)], true, cancellationToken);

        public Task<ServiceResult<ScalarReadModel<int>>> GetNextSeedIdAsync(
            string seedType,
            CancellationToken cancellationToken)
            => ExecuteAsync(
                GetNextSeedIdQuery.ErrorId, async () => new ScalarReadModel<int>(await (context.DbFactory.ReferenceDb
                    .GetNextSeedIdAsync(seedType, cancellationToken)).ConfigureAwait(false)), false, cancellationToken);

        public Task<ServiceResult<ScalarReadModel<int>>> GetCurrentSeedIdAsync(
            string seedType,
            CancellationToken cancellationToken)
            => ExecuteAsync(
                GetCurrentSeedIdQuery.ErrorId, async () => new ScalarReadModel<int>(await (context.DbFactory.ReferenceDb
                    .GetCurrentSeedIdAsync(seedType, cancellationToken)).ConfigureAwait(false)), true, cancellationToken);

        public Task<ServiceResult<DefaultFuturesContractDefinitionsReadModel>> GetDefaultFuturesContractDefinitionsAsync(
            CancellationToken cancellationToken)
            => ExecuteAsync(
                GetDefaultFuturesContractDefinitionsQuery.ErrorId, () => GetDefaultFuturesContractDefinitions
                    .GetDefaultFuturesContractDefinitionsAsync(context.DbFactory.ReferenceDb, cancellationToken).AsTask(), true, cancellationToken);

        public Task<ServiceResult<FuturesOptionStrikePriceReadModel>> GetFuturesOptionStrikePriceDefinitionsAsync(
            CancellationToken cancellationToken)
            => ExecuteAsync(
                GetFuturesOptionStrikePriceDefinitionsQuery.ErrorId, () => GetFuturesOptionStrikePriceDefinitions
                    .GetFuturesOptionStrikePriceDefinitionsAsync(context.DbFactory.ReferenceDb, cancellationToken).AsTask(), true, cancellationToken);

        public Task<ServiceResult<ScalarReadModel<bool>>> LookupTypeShortCodeExistsAsync(
            string lookupTypeName,
            string shortCode,
            CancellationToken cancellationToken)
            => ExecuteAsync(
                GetLookupTypeShortCodeExistsQuery.ErrorId, async () => new ScalarReadModel<bool>(await (context.DbFactory.ReferenceDb
                    .LookupTypeShortCodeExistsAsync(lookupTypeName, shortCode, cancellationToken)).ConfigureAwait(false)), true, cancellationToken);


        public Task<ServiceResult<MDIForwardLossRatioReadModel[]>> GetMDIForwardLossRatiosAsync(
            IntrinsicTimeTrendType trendDirection,
            TradeType tradeType,
            CancellationToken cancellationToken)
            => ExecuteAsync<MDIForwardLossRatioReadModel[]>(
                GetMDIForwardLossRatiosQuery.ErrorId, async () => [.. await (context.DbFactory.ReferenceDb
                .GetMDIForwardLossRatiosAsync(trendDirection, tradeType, cancellationToken)).ConfigureAwait(false)], true, cancellationToken);

        public Task<ServiceResult<TradeStrategyFamilyReadModel[]>> GetTradeStrategyFamiliesAsync(CancellationToken cancellationToken)
            => ExecuteAsync<TradeStrategyFamilyReadModel[]>(
                GetTradeStrategyFamiliesQuery.ErrorId, async () => [.. await (context.DbFactory.ReferenceDb.GetTradeStrategyFamiliesAsync(cancellationToken)).ConfigureAwait(false)], true, cancellationToken);

    }
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1031", Justification = "This message boundary converts arbitrary provider failures into the typed failed reply; failures must not escape the actor protocol.")] 
    static async Task<ServiceResult<T>> ExecuteAsync<T>(
        int errorId,
        Func<Task<T>> operation,
        bool observeCancellationAfterCompletion,
        CancellationToken cancellationToken)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var result = await operation().ConfigureAwait(false);
            if (observeCancellationAfterCompletion)
                cancellationToken.ThrowIfCancellationRequested();
            return new ServiceOk<T>(result);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            return new ServiceFailed<T>(errorId, ex.Message);
        }
    }
}

#pragma warning restore CA1708

#pragma warning restore CA1034
