using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.Reference.Shared;
using TomasAI.IFM.Domain.Reference.Shared.ViewModels;
using TomasAI.IFM.Domain.Trade.Shared;

namespace TomasAI.IFM.Application.Storage.ReferenceDb;

/// <summary>Defines Reference database queries.</summary>
public interface IReferenceDbReadContext
{
    /// <summary>Gets and advances the named seed allocator.</summary>
    /// <param name="seedType">The seed type.</param>
    /// <returns>The next seed identifier.</returns>
    Task<int> GetNextSeedIdAsync(string seedType);

    /// <summary>Gets and advances the named seed allocator.</summary>
    /// <param name="seedType">The seed type.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The next seed identifier.</returns>
    Task<int> GetNextSeedIdAsync(string seedType, CancellationToken cancellationToken);

    /// <summary>Gets the current value of the named seed allocator.</summary>
    /// <param name="seedType">The seed type.</param>
    /// <returns>The current seed identifier.</returns>
    Task<int> GetCurrentSeedIdAsync(string seedType);

    /// <summary>Gets the current value of the named seed allocator.</summary>
    /// <param name="seedType">The seed type.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The current seed identifier.</returns>
    Task<int> GetCurrentSeedIdAsync(string seedType, CancellationToken cancellationToken);

    /// <summary>Gets one lookup value by identity.</summary>
    /// <param name="lookupTypeId">The lookup identity.</param>
    /// <returns>The matching lookup value, or <see langword="null"/>.</returns>
    Task<LookupTypeReadModel?> GetLookupTypeAsync(LookupTypeId lookupTypeId);

    /// <summary>Gets one lookup value by identity.</summary>
    /// <param name="lookupTypeId">The lookup identity.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The matching lookup value, or <see langword="null"/>.</returns>
    Task<LookupTypeReadModel?> GetLookupTypeAsync(LookupTypeId lookupTypeId, CancellationToken cancellationToken);

    /// <summary>Gets all values for a lookup type.</summary>
    /// <param name="lookupTypeName">The lookup type name.</param>
    /// <returns>The matching lookup values.</returns>
    Task<ICollection<LookupTypeReadModel>> GetLookupTypeAsync(string lookupTypeName);

    /// <summary>Gets all values for a lookup type.</summary>
    /// <param name="lookupTypeName">The lookup type name.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The matching lookup values.</returns>
    Task<ICollection<LookupTypeReadModel>> GetLookupTypeAsync(string lookupTypeName, CancellationToken cancellationToken);

    /// <summary>Gets every lookup value.</summary>
    /// <returns>All lookup values.</returns>
    Task<ICollection<LookupTypeReadModel>> GetLookupTypesAsync();

    /// <summary>Gets every lookup value.</summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>All lookup values.</returns>
    Task<ICollection<LookupTypeReadModel>> GetLookupTypesAsync(CancellationToken cancellationToken);

    /// <summary>Gets the distinct lookup type names.</summary>
    /// <returns>The lookup type names.</returns>
    Task<ICollection<string>> GetLookupTypeNamesAsync();

    /// <summary>Gets the distinct lookup type names.</summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The lookup type names.</returns>
    Task<ICollection<string>> GetLookupTypeNamesAsync(CancellationToken cancellationToken);

    /// <summary>Gets the short codes for a lookup type.</summary>
    /// <param name="lookupTypeName">The lookup type name.</param>
    /// <returns>The matching short codes.</returns>
    Task<ICollection<LookupTypeShortCodeReadModel>> GetLookupTypeShortCodesAsync(string lookupTypeName);

    /// <summary>Gets the short codes for a lookup type.</summary>
    /// <param name="lookupTypeName">The lookup type name.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The matching short codes.</returns>
    Task<ICollection<LookupTypeShortCodeReadModel>> GetLookupTypeShortCodesAsync(string lookupTypeName, CancellationToken cancellationToken);

    /// <summary>Determines whether a lookup short code exists.</summary>
    /// <param name="lookupTypeName">The lookup type name.</param>
    /// <param name="shortCode">The short code.</param>
    /// <returns><see langword="true"/> when the short code exists.</returns>
    Task<bool> LookupTypeShortCodeExistsAsync(string lookupTypeName, string shortCode);

    /// <summary>Determines whether a lookup short code exists.</summary>
    /// <param name="lookupTypeName">The lookup type name.</param>
    /// <param name="shortCode">The short code.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns><see langword="true"/> when the short code exists.</returns>
    Task<bool> LookupTypeShortCodeExistsAsync(string lookupTypeName, string shortCode, CancellationToken cancellationToken);

    /// <summary>Gets all scheduled jobs.</summary>
    /// <returns>The scheduled jobs.</returns>
    Task<ICollection<ScheduledJobReadModel>> GetScheduledJobsAsync();

    /// <summary>Gets all scheduled jobs.</summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The scheduled jobs.</returns>
    Task<ICollection<ScheduledJobReadModel>> GetScheduledJobsAsync(CancellationToken cancellationToken);

    /// <summary>Gets the identifier reserved for a scheduled-job name.</summary>
    /// <param name="jobName">The scheduled-job name.</param>
    /// <returns>The scheduled-job identifier.</returns>
    Task<int> GetScheduledJobIdAsync(string jobName);

    /// <summary>Gets the identifier reserved for a scheduled-job name.</summary>
    /// <param name="jobName">The scheduled-job name.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The scheduled-job identifier.</returns>
    Task<int> GetScheduledJobIdAsync(string jobName, CancellationToken cancellationToken);

    /// <summary>Gets MDI forward-loss ratios.</summary>
    /// <param name="trendDirection">The intrinsic-time trend direction.</param>
    /// <param name="tradeType">The trade type.</param>
    /// <returns>The matching ratios.</returns>
    Task<ICollection<MDIForwardLossRatioReadModel>> GetMDIForwardLossRatiosAsync(IntrinsicTimeTrendType trendDirection, TradeType tradeType);

    /// <summary>Gets MDI forward-loss ratios.</summary>
    /// <param name="trendDirection">The intrinsic-time trend direction.</param>
    /// <param name="tradeType">The trade type.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The matching ratios.</returns>
    Task<ICollection<MDIForwardLossRatioReadModel>> GetMDIForwardLossRatiosAsync(IntrinsicTimeTrendType trendDirection, TradeType tradeType, CancellationToken cancellationToken);

    /// <summary>Gets the current trade-strategy-family catalog.</summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The current family definitions.</returns>
    Task<IReadOnlyList<TradeStrategyFamilyReadModel>> GetTradeStrategyFamiliesAsync(CancellationToken cancellationToken = default);

    /// <summary>Gets legacy trade-strategy-family rows for migration.</summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The legacy family rows.</returns>
    Task<IReadOnlyList<LegacyTradeStrategyFamily>> GetLegacyTradeStrategyFamiliesAsync(CancellationToken cancellationToken = default);

    /// <summary>Gets one trade-strategy-family definition.</summary>
    /// <param name="tradeStrategyFamilyId">The family identifier.</param>
    /// <param name="definitionVersion">The definition version.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The family definition, or <see langword="null"/>.</returns>
    Task<TradeStrategyFamilyReadModel?> GetTradeStrategyFamilyAsync(int tradeStrategyFamilyId, long definitionVersion, CancellationToken cancellationToken = default);
}
