using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Reference.Shared.ViewModels;
using TomasAI.IFM.Domain.Reference.Shared;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TomasAI.IFM.Domain.Reference.Shared.ViewModels;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;

namespace TomasAI.IFM.Application.Storage.ReferenceDb
{
    public interface IReferenceDbReadContext 
    {
        Task<int> GetNextSeedIdAsync(string seedType);
        Task<int> GetNextSeedIdAsync(string seedType, CancellationToken cancellationToken);
        Task<int> GetCurrentSeedIdAsync(string seedType);
        Task<int> GetCurrentSeedIdAsync(string seedType, CancellationToken cancellationToken);
        Task<LookupTypeReadModel?> GetLookupTypeAsync(LookupTypeId lookupTypeId);
        Task<LookupTypeReadModel?> GetLookupTypeAsync(LookupTypeId lookupTypeId, CancellationToken cancellationToken);
        Task<ICollection<LookupTypeReadModel>> GetLookupTypeAsync(string lookupTypeName);
        Task<ICollection<LookupTypeReadModel>> GetLookupTypeAsync(string lookupTypeName, CancellationToken cancellationToken);
        Task<ICollection<LookupTypeReadModel>> GetLookupTypesAsync();
        Task<ICollection<LookupTypeReadModel>> GetLookupTypesAsync(CancellationToken cancellationToken);
        Task<ICollection<string>> GetLookupTypeNamesAsync();
        Task<ICollection<string>> GetLookupTypeNamesAsync(CancellationToken cancellationToken);
        Task<ICollection<LookupTypeShortCodeReadModel>> GetLookupTypeShortCodesAsync(string lookupTypeName);
        Task<ICollection<LookupTypeShortCodeReadModel>> GetLookupTypeShortCodesAsync(string lookupTypeName, CancellationToken cancellationToken);
        Task<bool> LookupTypeShortCodeExistsAsync(string lookupTypeName, string shortCode);
        Task<bool> LookupTypeShortCodeExistsAsync(string lookupTypeName, string shortCode, CancellationToken cancellationToken);
        Task<ICollection<ScheduledJobReadModel>> GetScheduledJobsAsync();
        Task<ICollection<ScheduledJobReadModel>> GetScheduledJobsAsync(CancellationToken cancellationToken);
        Task<int> GetScheduledJobIdAsync(string jobName);
        Task<int> GetScheduledJobIdAsync(string jobName, CancellationToken cancellationToken);
        Task<ICollection<MDIForwardLossRatioReadModel>> GetMDIForwardLossRatiosAsync(IntrinsicTimeTrendType trendDirection, TradeType tradeType);
        Task<ICollection<MDIForwardLossRatioReadModel>> GetMDIForwardLossRatiosAsync(IntrinsicTimeTrendType trendDirection, TradeType tradeType, CancellationToken cancellationToken);
    }
}
