using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TomasAI.IFM.Shared.Reference.Queries;
using TomasAI.IFM.Shared.Reference.ViewModels;
using TomasAI.IFM.Shared.Reference.ServiceApi;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Queries;

namespace TomasAI.IFM.Application.Query.SignalRClient.Queries
{
    public class ReferenceQueryApi : IReferenceQueryApi
    {
        private IQueryService _querySvc;

        public ReferenceQueryApi(IQueryService querySvc)
        {
            _querySvc = querySvc;
        }

        /// <summary>
        /// return defualt futures contract definitions
        /// </summary>
        /// <returns></returns>
        public async Task<ServiceResult<DefaultFuturesContractDefinitionsReadModel>> GetDefaultFuturesContractDefinitionsAsync()
            => await _querySvc.ExecuteQueryAsync<DefaultFuturesContractDefinitionsReadModel>(new GetDefaultFuturesContractDefinitionsQuery { });

        /// <summary>
        /// return lookup types by lookup type name
        /// </summary>
        /// <param name="lookupTypeName"></param>
        /// <returns></returns>
        public async Task<ServiceResult<LookupTypeReadModel[]>> GetLookupTypesAsync()
            => await _querySvc.ExecuteQueryAsync<LookupTypeReadModel[]>(new GetLookupTypesQuery{});

        /// <summary>
        /// return lookup types by lookup type name
        /// </summary>
        /// <param name="lookupTypeName"></param>
        /// <returns></returns>
        public async Task<ServiceResult<LookupTypeReadModel[]>> GetLookupTypesAsync(string lookupTypeName)
            => await _querySvc.ExecuteQueryAsync<LookupTypeReadModel[]>(new GetLookupTypeQuery { LookupTypeName = lookupTypeName });

        /// <summary>
        /// return list of market type definitions
        /// </summary>
        /// <returns></returns>
        public async Task<ServiceResult<LookupTypeReadModel[]>> GetMarketDataDefinitionTypesAsync()
            => await _querySvc.ExecuteQueryAsync<LookupTypeReadModel[]>(new GetLookupTypeQuery { LookupTypeName = "MarketDataDefinitionType" });

        /// <summary>
        /// return list of system admin function types
        /// </summary>
        /// <returns></returns>
        public async Task<ServiceResult<LookupTypeReadModel[]>> GetSystemAdminFunctionTypesAsync()
            => await _querySvc.ExecuteQueryAsync<LookupTypeReadModel[]>(new GetLookupTypeQuery { LookupTypeName = "SystemAdminFunctionType" });

        public async Task<ServiceResult<ScalarReadModel<int>>> GetNextSeedIdAsync(string seedType)
            => await _querySvc.ExecuteQueryAsync<ScalarReadModel<int>>(new GetNextSeedIdQuery { SeedType = seedType });

        public async Task<ServiceResult<ScalarReadModel<int>>> GetCurrentSeedIdAsync(string seedType)
            => await _querySvc.ExecuteQueryAsync<ScalarReadModel<int>>(new GetCurrentSeedIdQuery { SeedType = seedType });

        public async Task<ServiceResult<FuturesOptionStrikePriceReadModel>> GetFuturesOptionStrikePriceDefinitionsAsync()
            => await _querySvc.ExecuteQueryAsync<FuturesOptionStrikePriceReadModel>(new GetFuturesOptionStrikePriceDefinitionsQuery { });

        /// <summary>
        /// check if short code exists for selceted lookup type
        /// </summary>
        /// <param name="lookupTypeName"></param>
        /// <param name="shortCode"></param>
        /// <returns></returns>
        public async Task<ServiceResult<ScalarReadModel<bool>>> LookupTypeShortCodeExistsAsync(string lookupTypeName, string shortCode)
            => await _querySvc.ExecuteQueryAsync<ScalarReadModel<bool>>(new GetLookupTypeShortCodeExistsQuery { LookupTypeName = lookupTypeName, ShortCode = shortCode });
    }
}
