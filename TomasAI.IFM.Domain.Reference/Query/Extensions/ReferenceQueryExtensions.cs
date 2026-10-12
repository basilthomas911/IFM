#pragma warning disable CA1034 // C# 14 extension blocks generate nested metadata types; no public nested type is declared here.
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.Reference.Shared;
using TomasAI.IFM.Domain.Reference.Shared.Queries;
using TomasAI.IFM.Domain.Reference.Shared.ViewModels;
using TomasAI.IFM.Domain.Reference.Query.Actor;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Domain.Reference.Query.Extensions;

/// <summary>
/// Provides direct, in-process Reference queries without actor messaging.
/// </summary>
/// <remarks>
/// Lookup, seed, futures-default, and economic-calendar data is read through Reference storage and the
/// optional external calendar reader. Every public method owns its typed success/failure mapping. The
/// implementation does not capture actor context and may be registered as a singleton.
/// </remarks>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "C# extension members use their declared receiver; static conversion would remove the established actor context extension contract.")]
public static partial class ReferenceQueryExtensions
{
    extension(IReferenceQueryContext context)
    {
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1031", Justification = "This message boundary converts arbitrary provider failures into the typed failed reply; failures must not escape the actor protocol.")] 
        /// <summary>
        /// Gets market data definition types.
        /// </summary>
        /// <returns>A task containing the typed success result or the operation-specific failure result.</returns>
        public async Task<ServiceResult<LookupTypeCollection>> GetMarketDataDefinitionTypesAsync()
        {
            try
            {
                LookupTypeCollection result = await (GetLookupTypesCoreAsync(context, "MarketDataDefinitionType")).ConfigureAwait(false);
                return new ServiceOk<LookupTypeCollection>(result);
            }
            catch (Exception ex)
            {
                return new ServiceFailed<LookupTypeCollection>(GetLookupTypeQuery.ErrorId, ex.Message);
            }
        }
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1031", Justification = "This message boundary converts arbitrary provider failures into the typed failed reply; failures must not escape the actor protocol.")] 
        /// <summary>
        /// Gets reference data definition types.
        /// </summary>
        /// <returns>A task containing the typed success result or the operation-specific failure result.</returns>
        public async Task<ServiceResult<LookupTypeCollection>> GetReferenceDataDefinitionTypesAsync()
        {
            try
            {
                LookupTypeCollection result = await (GetLookupTypesCoreAsync(context, "ReferenceDataDefinitionType")).ConfigureAwait(false);
                return new ServiceOk<LookupTypeCollection>(result);
            }
            catch (Exception ex)
            {
                return new ServiceFailed<LookupTypeCollection>(GetLookupTypeQuery.ErrorId, ex.Message);
            }
        }
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1031", Justification = "This message boundary converts arbitrary provider failures into the typed failed reply; failures must not escape the actor protocol.")] 
        /// <summary>
        /// Gets system admin function types.
        /// </summary>
        /// <returns>A task containing the typed success result or the operation-specific failure result.</returns>
        public async Task<ServiceResult<LookupTypeCollection>> GetSystemAdminFunctionTypesAsync()
        {
            try
            {
                LookupTypeCollection result = await (GetLookupTypesCoreAsync(context, "SystemAdminFunctionType")).ConfigureAwait(false);
                return new ServiceOk<LookupTypeCollection>(result);
            }
            catch (Exception ex)
            {
                return new ServiceFailed<LookupTypeCollection>(GetLookupTypeQuery.ErrorId, ex.Message);
            }
        }
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1031", Justification = "This message boundary converts arbitrary provider failures into the typed failed reply; failures must not escape the actor protocol.")] 
        /// <summary>
        /// Gets lookup types.
        /// </summary>
        /// <returns>A task containing the typed success result or the operation-specific failure result.</returns>
        public async Task<ServiceResult<LookupTypeCollection>> GetLookupTypesAsync()
        {
            try
            {
                var result = new LookupTypeCollection([.. await (context.DbFactory.ReferenceDb.GetLookupTypesAsync()).ConfigureAwait(false)]);
                return new ServiceOk<LookupTypeCollection>(result);
            }
            catch (Exception ex)
            {
                return new ServiceFailed<LookupTypeCollection>(GetLookupTypesQuery.ErrorId, ex.Message);
            }
        }
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1031", Justification = "This message boundary converts arbitrary provider failures into the typed failed reply; failures must not escape the actor protocol.")] 
        /// <summary>
        /// Gets lookup types.
        /// </summary>
        /// <param name="lookupTypeName">The lookup-type name.</param>
        /// <returns>A task containing the typed success result or the operation-specific failure result.</returns>
        public async Task<ServiceResult<LookupTypeCollection>> GetLookupTypesAsync(string lookupTypeName)
        {
            try
            {
                LookupTypeCollection result = await (GetLookupTypesCoreAsync(context, lookupTypeName)).ConfigureAwait(false);
                return new ServiceOk<LookupTypeCollection>(result);
            }
            catch (Exception ex)
            {
                return new ServiceFailed<LookupTypeCollection>(GetLookupTypeQuery.ErrorId, ex.Message);
            }
        }
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1031", Justification = "This message boundary converts arbitrary provider failures into the typed failed reply; failures must not escape the actor protocol.")] 
        /// <summary>
        /// Gets lookup type names.
        /// </summary>
        /// <returns>A task containing the typed success result or the operation-specific failure result.</returns>
        public async Task<ServiceResult<string[]>> GetLookupTypeNamesAsync()
        {
            try
            {
                string[] result = [.. await (context.DbFactory.ReferenceDb.GetLookupTypeNamesAsync()).ConfigureAwait(false)];
                return new ServiceOk<string[]>(result);
            }
            catch (Exception ex)
            {
                return new ServiceFailed<string[]>(GetLookupTypeNamesQuery.ErrorId, ex.Message);
            }
        }
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1031", Justification = "This message boundary converts arbitrary provider failures into the typed failed reply; failures must not escape the actor protocol.")] 
        /// <summary>
        /// Gets lookup type short codes.
        /// </summary>
        /// <param name="lookupTypeName">The lookup-type name.</param>
        /// <returns>A task containing the typed success result or the operation-specific failure result.</returns>
        public async Task<ServiceResult<LookupTypeShortCodeReadModel[]>> GetLookupTypeShortCodesAsync(
            string lookupTypeName)
        {
            try
            {
                LookupTypeShortCodeReadModel[] result =
                    [.. await (context.DbFactory.ReferenceDb.GetLookupTypeShortCodesAsync(lookupTypeName)).ConfigureAwait(false)];
                return new ServiceOk<LookupTypeShortCodeReadModel[]>(result);
            }
            catch (Exception ex)
            {
                return new ServiceFailed<LookupTypeShortCodeReadModel[]>(
                    GetLookupTypeShortCodesQuery.ErrorId,
                    ex.Message);
            }
        }
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1031", Justification = "This message boundary converts arbitrary provider failures into the typed failed reply; failures must not escape the actor protocol.")] 
        /// <summary>
        /// Gets next seed ID.
        /// </summary>
        /// <param name="seedType">The seed category.</param>
        /// <returns>A task containing the typed success result or the operation-specific failure result.</returns>
        public async Task<ServiceResult<ScalarReadModel<int>>> GetNextSeedIdAsync(string seedType)
        {
            try
            {
                var result = new ScalarReadModel<int>(await (context.DbFactory.ReferenceDb.GetNextSeedIdAsync(seedType)).ConfigureAwait(false));
                return new ServiceOk<ScalarReadModel<int>>(result);
            }
            catch (Exception ex)
            {
                return new ServiceFailed<ScalarReadModel<int>>(GetNextSeedIdQuery.ErrorId, ex.Message);
            }
        }
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1031", Justification = "This message boundary converts arbitrary provider failures into the typed failed reply; failures must not escape the actor protocol.")] 
        /// <summary>
        /// Gets the highest seed ID currently reserved by PostgreSQL.
        /// </summary>
        /// <param name="seedType">The seed category.</param>
        /// <returns>A task containing the typed success result or the operation-specific failure result.</returns>
        public async Task<ServiceResult<ScalarReadModel<int>>> GetCurrentSeedIdAsync(string seedType)
        {
            try
            {
                var result = new ScalarReadModel<int>(await (context.DbFactory.ReferenceDb.GetCurrentSeedIdAsync(seedType)).ConfigureAwait(false));
                return new ServiceOk<ScalarReadModel<int>>(result);
            }
            catch (Exception ex)
            {
                return new ServiceFailed<ScalarReadModel<int>>(GetCurrentSeedIdQuery.ErrorId, ex.Message);
            }
        }
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1031", Justification = "This message boundary converts arbitrary provider failures into the typed failed reply; failures must not escape the actor protocol.")] 
        /// <summary>
        /// Gets default futures contract definitions.
        /// </summary>
        /// <returns>A task containing the typed success result or the operation-specific failure result.</returns>
        public async Task<ServiceResult<DefaultFuturesContractDefinitionsReadModel>>
            GetDefaultFuturesContractDefinitionsAsync()
        {
            try
            {
                var result = await (GetDefaultFuturesContractDefinitions
                    .GetDefaultFuturesContractDefinitionsAsync(context.DbFactory.ReferenceDb)).ConfigureAwait(false);
                return new ServiceOk<DefaultFuturesContractDefinitionsReadModel>(result);
            }
            catch (Exception ex)
            {
                return new ServiceFailed<DefaultFuturesContractDefinitionsReadModel>(
                    GetDefaultFuturesContractDefinitionsQuery.ErrorId,
                    ex.Message);
            }
        }
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1031", Justification = "This message boundary converts arbitrary provider failures into the typed failed reply; failures must not escape the actor protocol.")] 
        /// <summary>
        /// Gets futures option strike price definitions.
        /// </summary>
        /// <returns>A task containing the typed success result or the operation-specific failure result.</returns>
        public async Task<ServiceResult<FuturesOptionStrikePriceReadModel>>
            GetFuturesOptionStrikePriceDefinitionsAsync()
        {
            try
            {
                var result = await (GetFuturesOptionStrikePriceDefinitions
                    .GetFuturesOptionStrikePriceDefinitionsAsync(context.DbFactory.ReferenceDb)).ConfigureAwait(false);
                return new ServiceOk<FuturesOptionStrikePriceReadModel>(result);
            }
            catch (Exception ex)
            {
                return new ServiceFailed<FuturesOptionStrikePriceReadModel>(
                    GetFuturesOptionStrikePriceDefinitionsQuery.ErrorId,
                    ex.Message);
            }
        }
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1031", Justification = "This message boundary converts arbitrary provider failures into the typed failed reply; failures must not escape the actor protocol.")] 
        /// <summary>
        /// Determines whether lookup type short code exists.
        /// </summary>
        /// <param name="lookupTypeName">The lookup-type name.</param>
        /// <param name="shortCode">The lookup short code.</param>
        /// <returns>A task containing the typed success result or the operation-specific failure result.</returns>
        public async Task<ServiceResult<ScalarReadModel<bool>>> LookupTypeShortCodeExistsAsync(
            string lookupTypeName, string shortCode)
        {
            try
            {
                var result = new ScalarReadModel<bool>(await (context.DbFactory.ReferenceDb
                    .LookupTypeShortCodeExistsAsync(lookupTypeName, shortCode)).ConfigureAwait(false));
                return new ServiceOk<ScalarReadModel<bool>>(result);
            }
            catch (Exception ex)
            {
                return new ServiceFailed<ScalarReadModel<bool>>(
                    GetLookupTypeShortCodeExistsQuery.ErrorId,
                    ex.Message);
            }
        }
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1031", Justification = "This message boundary converts arbitrary provider failures into the typed failed reply; failures must not escape the actor protocol.")] 

        /// <summary>
        /// Gets MDI forward loss ratios.
        /// </summary>
        /// <param name="trendDirection">The intrinsic-time trend direction.</param>
        /// <param name="tradeType">The trade strategy type.</param>
        /// <returns>A task containing the typed success result or the operation-specific failure result.</returns>
        public async Task<ServiceResult<MDIForwardLossRatioReadModel[]>> GetMDIForwardLossRatiosAsync(
            IntrinsicTimeTrendType trendDirection, TradeType tradeType)
        {
            try
            {
                MDIForwardLossRatioReadModel[] result =
                    [.. await (context.DbFactory.ReferenceDb.GetMDIForwardLossRatiosAsync(trendDirection, tradeType)).ConfigureAwait(false)];
                return new ServiceOk<MDIForwardLossRatioReadModel[]>(result);
            }
            catch (Exception ex)
            {
                return new ServiceFailed<MDIForwardLossRatioReadModel[]>(
                    GetMDIForwardLossRatiosQuery.ErrorId,
                    ex.Message);
            }
        }

    }

    static async Task<LookupTypeCollection> GetLookupTypesCoreAsync(
        IReferenceQueryContext context,
        string lookupTypeName)
        => new([.. await (context.DbFactory.ReferenceDb.GetLookupTypeAsync(lookupTypeName)).ConfigureAwait(false)]);
}

/// <summary>Provides readonly Reference services on the typed query context.</summary>
public static partial class ReferenceQueryExtensions
{
    extension(IQueryActorContext<ReferenceQueryActor> context)
    {
        /// <summary>Gets the database-context factory.</summary>
        public IDbContextFactory DbFactory => Typed(context).DbFactory;
        /// <summary>Gets the query actor logger.</summary>
        public ILogger<ReferenceQueryActor> Logger => Typed(context).Logger;
    }
    static IReferenceQueryContext Typed(IQueryActorContext<ReferenceQueryActor> context)
        => IsArgumentNull.Named(context as IReferenceQueryContext, nameof(context))!;
}

#pragma warning restore CA1034
