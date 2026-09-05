using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.UI.Net.Contracts;
using TomasAI.IFM.UI.Net.Models.Reference;
using TomasAI.IFM.UI.Net.Services.Operations;
using TomasAI.IFM.UI.Net.Services.Reference;
using TomasAI.IFM.UI.Net.ViewModels.Operations;
using TomasAI.IFM.UI.Net.ViewModels.Presentation;

namespace TomasAI.IFM.UI.Net.ViewModels.Reference;

/// <summary>
/// Exposes observable reference-data selector state and single-flight load operations.
/// </summary>
public sealed class ReferenceViewModel : ObservableObject
{
    readonly IReferenceDataService _referenceDataService;
    IReadOnlyList<LookupTypeUiModel> _referenceDataDefinitionTypes = [];
    IReadOnlyList<MdiForwardLossRatioUiModel> _mdiForwardLossRatios = [];

    /// <summary>Creates the Reference selector workflow with its explicit service dependency.</summary>
    public ReferenceViewModel(IReferenceDataService referenceDataService)
    {
        _referenceDataService = referenceDataService
            ?? throw new ArgumentNullException(nameof(referenceDataService));
        LoadReferenceDataDefinitionTypesOperation = new AsyncOperation(
            LoadReferenceDataDefinitionTypesCoreAsync);
    }

    /// <summary>
    /// Gets the available reference-data editors in selector order.
    /// </summary>
    public IReadOnlyList<LookupTypeUiModel> ReferenceDataDefinitionTypes
    {
        get => _referenceDataDefinitionTypes;
        private set => SetProperty(ref _referenceDataDefinitionTypes, value);
    }

    /// <summary>
    /// Gets the most recently loaded forward-loss ratios.
    /// </summary>
    public IReadOnlyList<MdiForwardLossRatioUiModel> MdiForwardLossRatios
    {
        get => _mdiForwardLossRatios;
        private set => SetProperty(ref _mdiForwardLossRatios, value);
    }

    /// <summary>
    /// Gets the single-flight operation that loads selector definitions.
    /// </summary>
    public IAsyncOperation LoadReferenceDataDefinitionTypesOperation { get; }

    /// <summary>
    /// Gets a selector item by index, or <see langword="null"/> when the selection is invalid.
    /// </summary>
    public LookupTypeUiModel? GetReferenceDataDefinitionType(int index)
        => index >= 0 && index < ReferenceDataDefinitionTypes.Count
            ? ReferenceDataDefinitionTypes[index]
            : null;

    /// <summary>
    /// Loads forward-loss ratios into observable state.
    /// </summary>
    public Task LoadMdiForwardLossRatiosAsync(
        IntrinsicTimeTrendType trendDirection,
        TradeType tradeType,
        CancellationToken cancellationToken = default)
        => LoadMdiForwardLossRatiosCoreAsync(trendDirection, tradeType, cancellationToken);

    async Task LoadMdiForwardLossRatiosCoreAsync(
        IntrinsicTimeTrendType trendDirection,
        TradeType tradeType,
        CancellationToken cancellationToken)
        => MdiForwardLossRatios = (await _referenceDataService.GetMdiForwardLossRatiosAsync(
            trendDirection,
            tradeType,
            cancellationToken)).RequireValue();

    async Task LoadReferenceDataDefinitionTypesCoreAsync(CancellationToken cancellationToken)
        => ReferenceDataDefinitionTypes =
            (await _referenceDataService.GetReferenceDataDefinitionTypesAsync(cancellationToken))
            .RequireValue()
            // Calendar events are managed by the FMP import workflow, not this editor.
            // Filter before binding so selector indexes still match the displayed definitions.
            .Where(definition => definition.ShortCode != "EconomicCalendar")
            .ToArray();
}
