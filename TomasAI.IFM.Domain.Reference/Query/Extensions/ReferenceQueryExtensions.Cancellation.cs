using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.Reference.Shared;
using TomasAI.IFM.Domain.Reference.Shared.Queries;
using TomasAI.IFM.Domain.Reference.Shared.ViewModels;
using TomasAI.IFM.Domain.Reference.Query.Actor;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Reference.Query.Extensions;

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
            GetLookupTypesQuery.ErrorId,
            cancellationToken,
            async () => new LookupTypeCollection(
                [.. await context.DbFactory.ReferenceDb.GetLookupTypesAsync(cancellationToken)]));

    public Task<ServiceResult<LookupTypeCollection>> GetLookupTypesAsync(
        string lookupTypeName,
        CancellationToken cancellationToken)
        => ExecuteAsync(
            GetLookupTypeQuery.ErrorId,
            cancellationToken,
            async () => new LookupTypeCollection(
                [.. await context.DbFactory.ReferenceDb.GetLookupTypeAsync(lookupTypeName, cancellationToken)]));

    public Task<ServiceResult<string[]>> GetLookupTypeNamesAsync(CancellationToken cancellationToken)
        => ExecuteAsync<string[]>(
            GetLookupTypeNamesQuery.ErrorId,
            cancellationToken,
            async () => [.. await context.DbFactory.ReferenceDb.GetLookupTypeNamesAsync(cancellationToken)]);

    public Task<ServiceResult<LookupTypeShortCodeReadModel[]>> GetLookupTypeShortCodesAsync(
        string lookupTypeName,
        CancellationToken cancellationToken)
        => ExecuteAsync<LookupTypeShortCodeReadModel[]>(
            GetLookupTypeShortCodesQuery.ErrorId,
            cancellationToken,
            async () => [.. await context.DbFactory.ReferenceDb
                .GetLookupTypeShortCodesAsync(lookupTypeName, cancellationToken)]);

    public Task<ServiceResult<ScalarReadModel<int>>> GetNextSeedIdAsync(
        string seedType,
        CancellationToken cancellationToken)
        => ExecuteAsync(
            GetNextSeedIdQuery.ErrorId,
            cancellationToken,
            async () => new ScalarReadModel<int>(await context.DbFactory.ReferenceDb
                .GetNextSeedIdAsync(seedType, cancellationToken)),
            observeCancellationAfterCompletion: false);

    public Task<ServiceResult<ScalarReadModel<int>>> GetCurrentSeedIdAsync(
        string seedType,
        CancellationToken cancellationToken)
        => ExecuteAsync(
            GetCurrentSeedIdQuery.ErrorId,
            cancellationToken,
            async () => new ScalarReadModel<int>(await context.DbFactory.ReferenceDb
                .GetCurrentSeedIdAsync(seedType, cancellationToken)));

    public Task<ServiceResult<DefaultFuturesContractDefinitionsReadModel>> GetDefaultFuturesContractDefinitionsAsync(
        CancellationToken cancellationToken)
        => ExecuteAsync(
            GetDefaultFuturesContractDefinitionsQuery.ErrorId,
            cancellationToken,
            () => GetDefaultFuturesContractDefinitions
                .GetDefaultFuturesContractDefinitionsAsync(context.DbFactory.ReferenceDb, cancellationToken).AsTask());

    public Task<ServiceResult<FuturesOptionStrikePriceReadModel>> GetFuturesOptionStrikePriceDefinitionsAsync(
        CancellationToken cancellationToken)
        => ExecuteAsync(
            GetFuturesOptionStrikePriceDefinitionsQuery.ErrorId,
            cancellationToken,
            () => GetFuturesOptionStrikePriceDefinitions
                .GetFuturesOptionStrikePriceDefinitionsAsync(context.DbFactory.ReferenceDb, cancellationToken).AsTask());

    public Task<ServiceResult<ScalarReadModel<bool>>> LookupTypeShortCodeExistsAsync(
        string lookupTypeName,
        string shortCode,
        CancellationToken cancellationToken)
        => ExecuteAsync(
            GetLookupTypeShortCodeExistsQuery.ErrorId,
            cancellationToken,
            async () => new ScalarReadModel<bool>(await context.DbFactory.ReferenceDb
                .LookupTypeShortCodeExistsAsync(lookupTypeName, shortCode, cancellationToken)));


    public Task<ServiceResult<MDIForwardLossRatioReadModel[]>> GetMDIForwardLossRatiosAsync(
        IntrinsicTimeTrendType trendDirection,
        TradeType tradeType,
        CancellationToken cancellationToken)
        => ExecuteAsync<MDIForwardLossRatioReadModel[]>(
            GetMDIForwardLossRatiosQuery.ErrorId,
            cancellationToken,
            async () => [.. await context.DbFactory.ReferenceDb
                .GetMDIForwardLossRatiosAsync(trendDirection, tradeType, cancellationToken)]);

    public Task<ServiceResult<TradeStrategyFamilyReadModel[]>> GetTradeStrategyFamiliesAsync(CancellationToken cancellationToken)
        => ExecuteAsync<TradeStrategyFamilyReadModel[]>(
            GetTradeStrategyFamiliesQuery.ErrorId,
            cancellationToken,
            async () => [.. await context.DbFactory.ReferenceDb.GetTradeStrategyFamiliesAsync(cancellationToken)]);

    }

    static async Task<ServiceResult<T>> ExecuteAsync<T>(
        int errorId,
        CancellationToken cancellationToken,
        Func<Task<T>> operation,
        bool observeCancellationAfterCompletion = true)
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
