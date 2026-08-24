using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.UI.Net.Models.Operations;
using TomasAI.IFM.UI.Net.Models.Reference;
using TomasAI.IFM.UI.Net.Services.Operations;
using TomasAI.IFM.UI.Net.Services.Subscriptions;

namespace TomasAI.IFM.UI.Net.Services.Reference;

/// <summary>Defines reference-data commands, queries, and lookup notifications used by the UI.</summary>
public interface IReferenceDataService
{
    /// <summary>Adds a lookup definition.</summary>
    ValueTask<UiOperationResult<Guid>> AddLookupTypeAsync(
        LookupTypeUiModel lookupType,
        CancellationToken cancellationToken = default);

    /// <summary>Changes a lookup definition identified by name and order.</summary>
    ValueTask<UiOperationResult<Guid>> ChangeLookupTypeAsync(
        string lookupTypeName,
        int orderId,
        LookupTypeUiModel lookupType,
        bool overwrite,
        CancellationToken cancellationToken = default);

    /// <summary>Removes a lookup definition identified by name and order.</summary>
    ValueTask<UiOperationResult<Guid>> RemoveLookupTypeAsync(
        string lookupTypeName,
        int orderId,
        bool overwrite,
        CancellationToken cancellationToken = default);

    /// <summary>Loads every lookup definition.</summary>
    ValueTask<UiOperationResult<IReadOnlyList<LookupTypeUiModel>>> GetLookupTypesAsync(
        CancellationToken cancellationToken = default);

    /// <summary>Loads lookup definitions in one named category.</summary>
    ValueTask<UiOperationResult<IReadOnlyList<LookupTypeUiModel>>> GetLookupTypesAsync(
        string lookupTypeName,
        CancellationToken cancellationToken = default);

    /// <summary>Loads all lookup category names.</summary>
    ValueTask<UiOperationResult<IReadOnlyList<string>>> GetLookupTypeNamesAsync(
        CancellationToken cancellationToken = default);

    /// <summary>Loads short-code selectors for one lookup category.</summary>
    ValueTask<UiOperationResult<IReadOnlyList<LookupTypeShortCodeUiModel>>> GetLookupTypeShortCodesAsync(
        string lookupTypeName,
        CancellationToken cancellationToken = default);

    /// <summary>Loads Market Data definition selectors.</summary>
    ValueTask<UiOperationResult<IReadOnlyList<LookupTypeUiModel>>> GetMarketDataDefinitionTypesAsync(
        CancellationToken cancellationToken = default);

    /// <summary>Loads Reference definition selectors.</summary>
    ValueTask<UiOperationResult<IReadOnlyList<LookupTypeUiModel>>> GetReferenceDataDefinitionTypesAsync(
        CancellationToken cancellationToken = default);

    /// <summary>Loads System Administration function selectors.</summary>
    ValueTask<UiOperationResult<IReadOnlyList<LookupTypeUiModel>>> GetSystemAdminFunctionTypesAsync(
        CancellationToken cancellationToken = default);

    /// <summary>Gets the next Fund identifier.</summary>
    ValueTask<UiOperationResult<int>> GetNextFundIdAsync(CancellationToken cancellationToken = default);

    /// <summary>Gets the next Order identifier.</summary>
    ValueTask<UiOperationResult<int>> GetNextOrderIdAsync(CancellationToken cancellationToken = default);

    /// <summary>Gets the next Trade identifier.</summary>
    ValueTask<UiOperationResult<int>> GetNextTradeIdAsync(CancellationToken cancellationToken = default);

    /// <summary>Loads default futures-contract selector values.</summary>
    ValueTask<UiOperationResult<DefaultFuturesContractDefinitionsUiModel>>
        GetDefaultFuturesContractDefinitionsAsync(CancellationToken cancellationToken = default);

    /// <summary>Loads futures-option strike-price limits.</summary>
    ValueTask<UiOperationResult<FuturesOptionStrikePriceUiModel>>
        GetFuturesOptionStrikePriceDefinitionsAsync(CancellationToken cancellationToken = default);

    /// <summary>Loads MDI forward-loss ratios.</summary>
    ValueTask<UiOperationResult<IReadOnlyList<MdiForwardLossRatioUiModel>>> GetMdiForwardLossRatiosAsync(
        IntrinsicTimeTrendType trendDirection,
        TradeType tradeType,
        CancellationToken cancellationToken = default);

    /// <summary>Creates an independently owned lookup terminal-event subscription.</summary>
    IUiEventSubscription CreateLookupSubscription(
        Func<TerminalNotificationUiModel, ValueTask> handler);
}
