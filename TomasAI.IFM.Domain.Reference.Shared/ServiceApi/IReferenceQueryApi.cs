using TomasAI.IFM.Domain.Trade.Shared;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TomasAI.IFM.Domain.Reference.Shared;
using TomasAI.IFM.Domain.Reference.Shared.ViewModels;
using TomasAI.IFM.Domain.Reference.Shared.ViewModels;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.Trade.Shared;

namespace TomasAI.IFM.Domain.Reference.Shared.ServiceApi
{
    public interface IReferenceQueryApi
    {
        Task<ServiceResult<Lookups.LookupDefinitionReadModel[]>> GetLookupDefinitionsAsync(string groupName, CancellationToken cancellationToken = default)
            => throw new NotSupportedException("ConfigurationDb lookup queries are unavailable.");
        Task<ServiceResult<string>> QueryStrategyCatalogAsync(StrategyCatalog.CatalogQueryRequest request, CancellationToken cancellationToken = default)
            => throw new NotSupportedException("ConfigurationDb strategy catalog queries are unavailable.");
        async Task<ServiceResult<StrategyCatalog.StrategyDeploymentChoice[]>> GetStrategyDeploymentChoicesAsync(CancellationToken cancellationToken = default)
        {
            var rows = new List<StrategyCatalog.StrategyDeploymentChoice>(); string? cursor = null;
            while (true)
            {
                var reply = await QueryStrategyCatalogAsync(new(StrategyCatalog.CatalogQueryOperation.DeploymentChoices, StrategyCatalog.StrategyCatalogKind.Deployment, Limit: 64, AfterCode: cursor), cancellationToken);
                if (!reply.Success || reply.Value is null) return new ServiceFailed<StrategyCatalog.StrategyDeploymentChoice[]>(1063, reply.ErrorMessage ?? "Deployment catalog unavailable.");
                var page = StrategyCatalog.StrategyCatalogJson.Read<StrategyCatalog.StrategyDeploymentPage>(reply.Value);
                rows.AddRange(page.Items); if (page.NextCode is null) break;
                if (rows.Count >= 4096 || page.Items.Length == 0 || page.NextCode == cursor) throw new InvalidOperationException("Deployment catalog paging exceeded its limit.");
                cursor = page.NextCode;
            }
            return new ServiceOk<StrategyCatalog.StrategyDeploymentChoice[]>(rows.ToArray());
        }
        Task<ServiceResult<TomasAI.IFM.Domain.MarketData.Shared.ViewModels.TradeStrategySymbolReadModel[]>> GetTradeStrategySymbolsAsync(TradeStrategyFamilyType family, CancellationToken cancellationToken = default)
            => throw new NotSupportedException("Trade strategy symbol queries are not implemented by this adapter.");
        Task<ServiceResult<LookupTypeCollection>> GetMarketDataDefinitionTypesAsync();
        Task<ServiceResult<LookupTypeCollection>> GetReferenceDataDefinitionTypesAsync();
        Task<ServiceResult<LookupTypeCollection>> GetSystemAdminFunctionTypesAsync();
        Task<ServiceResult<LookupTypeCollection>> GetLookupTypesAsync();
        Task<ServiceResult<LookupTypeCollection>> GetLookupTypesAsync(string lookupTypeName);
        Task<ServiceResult<string[]>> GetLookupTypeNamesAsync();
        Task<ServiceResult<LookupTypeShortCodeReadModel[]>> GetLookupTypeShortCodesAsync(string lookupTypeName);
        Task<ServiceResult<ScalarReadModel<int>>> GetNextSeedIdAsync(string seedType);
        Task<ServiceResult<ScalarReadModel<int>>> GetCurrentSeedIdAsync(string seedType);
        Task<ServiceResult<DefaultFuturesContractDefinitionsReadModel>> GetDefaultFuturesContractDefinitionsAsync();
        Task<ServiceResult<FuturesOptionStrikePriceReadModel>> GetFuturesOptionStrikePriceDefinitionsAsync();
        Task<ServiceResult<ScalarReadModel<bool>>> LookupTypeShortCodeExistsAsync(string lookupTypeName, string shortCode);
        Task<ServiceResult<MDIForwardLossRatioReadModel[]>> GetMDIForwardLossRatiosAsync(IntrinsicTimeTrendType trendDirection, TradeType tradeType);
        Task<ServiceResult<TradeStrategyFamilyReadModel[]>> GetTradeStrategyFamiliesAsync(CancellationToken cancellationToken = default)
            => throw new NotSupportedException("TradeStrategyFamily queries are not implemented by this legacy adapter.");
    }
}
