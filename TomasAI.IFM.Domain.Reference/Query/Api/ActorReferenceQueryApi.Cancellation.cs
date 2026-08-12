using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.Reference.Shared;
using TomasAI.IFM.Domain.Reference.Shared.Queries;
using TomasAI.IFM.Domain.Reference.Shared.ViewModels;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Reference.Query.Api;

public sealed partial class ActorReferenceQueryApi
{
    public Task<ServiceResult<LookupTypeCollection>> GetMarketDataDefinitionTypesAsync(CancellationToken cancellationToken)
        => GetLookupTypesAsync("MarketDataDefinitionType", cancellationToken);

    public Task<ServiceResult<LookupTypeCollection>> GetReferenceDataDefinitionTypesAsync(CancellationToken cancellationToken)
        => GetLookupTypesAsync("ReferenceDataDefinitionType", cancellationToken);

    public Task<ServiceResult<LookupTypeCollection>> GetSystemAdminFunctionTypesAsync(CancellationToken cancellationToken)
        => GetLookupTypesAsync("SystemAdminFunctionType", cancellationToken);

    public Task<ServiceResult<LookupTypeCollection>> GetLookupTypesAsync(CancellationToken cancellationToken)
        => ExecuteAsync(
            GetLookupTypesQuery.ErrorId,
            cancellationToken,
            async () => new LookupTypeCollection(
                [.. await _dbFactory.ReferenceDb.GetLookupTypesAsync(cancellationToken)]));

    public Task<ServiceResult<LookupTypeCollection>> GetLookupTypesAsync(
        string lookupTypeName,
        CancellationToken cancellationToken)
        => ExecuteAsync(
            GetLookupTypeQuery.ErrorId,
            cancellationToken,
            async () => new LookupTypeCollection(
                [.. await _dbFactory.ReferenceDb.GetLookupTypeAsync(lookupTypeName, cancellationToken)]));

    public Task<ServiceResult<string[]>> GetLookupTypeNamesAsync(CancellationToken cancellationToken)
        => ExecuteAsync<string[]>(
            GetLookupTypeNamesQuery.ErrorId,
            cancellationToken,
            async () => [.. await _dbFactory.ReferenceDb.GetLookupTypeNamesAsync(cancellationToken)]);

    public Task<ServiceResult<LookupTypeShortCodeReadModel[]>> GetLookupTypeShortCodesAsync(
        string lookupTypeName,
        CancellationToken cancellationToken)
        => ExecuteAsync<LookupTypeShortCodeReadModel[]>(
            GetLookupTypeShortCodesQuery.ErrorId,
            cancellationToken,
            async () => [.. await _dbFactory.ReferenceDb
                .GetLookupTypeShortCodesAsync(lookupTypeName, cancellationToken)]);

    public Task<ServiceResult<ScalarReadModel<int>>> GetNextSeedIdAsync(
        string seedType,
        CancellationToken cancellationToken)
        => ExecuteAsync(
            GetNextSeedIdQuery.ErrorId,
            cancellationToken,
            async () => new ScalarReadModel<int>(await _dbFactory.ReferenceDb
                .GetNextSeedIdAsync(seedType, cancellationToken)),
            observeCancellationAfterCompletion: false);

    public Task<ServiceResult<ScalarReadModel<int>>> GetCurrentSeedIdAsync(
        string seedType,
        CancellationToken cancellationToken)
        => ExecuteAsync(
            GetCurrentSeedIdQuery.ErrorId,
            cancellationToken,
            async () => new ScalarReadModel<int>(await _dbFactory.ReferenceDb
                .GetCurrentSeedIdAsync(seedType, cancellationToken)));

    public Task<ServiceResult<DefaultFuturesContractDefinitionsReadModel>> GetDefaultFuturesContractDefinitionsAsync(
        CancellationToken cancellationToken)
        => ExecuteAsync(
            GetDefaultFuturesContractDefinitionsQuery.ErrorId,
            cancellationToken,
            () => GetDefaultFuturesContractDefinitions
                .GetDefaultFuturesContractDefinitionsAsync(_dbFactory.ReferenceDb, cancellationToken).AsTask());

    public Task<ServiceResult<FuturesOptionStrikePriceReadModel>> GetFuturesOptionStrikePriceDefinitionsAsync(
        CancellationToken cancellationToken)
        => ExecuteAsync(
            GetFuturesOptionStrikePriceDefinitionsQuery.ErrorId,
            cancellationToken,
            () => GetFuturesOptionStrikePriceDefinitions
                .GetFuturesOptionStrikePriceDefinitionsAsync(_dbFactory.ReferenceDb, cancellationToken).AsTask());

    public Task<ServiceResult<ScalarReadModel<bool>>> LookupTypeShortCodeExistsAsync(
        string lookupTypeName,
        string shortCode,
        CancellationToken cancellationToken)
        => ExecuteAsync(
            GetLookupTypeShortCodeExistsQuery.ErrorId,
            cancellationToken,
            async () => new ScalarReadModel<bool>(await _dbFactory.ReferenceDb
                .LookupTypeShortCodeExistsAsync(lookupTypeName, shortCode, cancellationToken)));


    public Task<ServiceResult<MDIForwardLossRatioReadModel[]>> GetMDIForwardLossRatiosAsync(
        IntrinsicTimeTrendType trendDirection,
        TradeType tradeType,
        CancellationToken cancellationToken)
        => ExecuteAsync<MDIForwardLossRatioReadModel[]>(
            GetMDIForwardLossRatiosQuery.ErrorId,
            cancellationToken,
            async () => [.. await _dbFactory.ReferenceDb
                .GetMDIForwardLossRatiosAsync(trendDirection, tradeType, cancellationToken)]);

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
