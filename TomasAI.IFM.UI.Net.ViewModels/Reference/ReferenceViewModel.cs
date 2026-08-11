using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.Reference.Shared.ViewModels;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.UI.Net.Contracts;
using TomasAI.IFM.UI.Net.Models;
using TomasAI.IFM.UI.Net.ViewModels.Extensions;
using TomasAI.IFM.UI.Net.ViewModels.Operations;
using TomasAI.IFM.UI.Net.ViewModels.Presentation;

namespace TomasAI.IFM.UI.Net.ViewModels.Reference;

/// <summary>
/// Exposes observable reference-data selector state and single-flight load operations.
/// </summary>
public sealed class ReferenceViewModel : ObservableObject
{
    readonly IAppRoot _appRoot;
    IReadOnlyList<LookupTypeReadModel> _referenceDataDefinitionTypes = [];
    IReadOnlyList<MDIForwardLossRatioReadModel> _mdiForwardLossRatios = [];

    public ReferenceViewModel(IAppRoot appRoot)
    {
        _appRoot = appRoot ?? throw new ArgumentNullException(nameof(appRoot));
        LoadReferenceDataDefinitionTypesOperation = new AsyncOperation(
            LoadReferenceDataDefinitionTypesCoreAsync);
    }

    /// <summary>
    /// Gets the available reference-data editors in selector order.
    /// </summary>
    public IReadOnlyList<LookupTypeReadModel> ReferenceDataDefinitionTypes
    {
        get => _referenceDataDefinitionTypes;
        private set => SetProperty(ref _referenceDataDefinitionTypes, value);
    }

    /// <summary>
    /// Gets the most recently loaded forward-loss ratios.
    /// </summary>
    public IReadOnlyList<MDIForwardLossRatioReadModel> MdiForwardLossRatios
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
    public LookupTypeReadModel? GetReferenceDataDefinitionType(int index)
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
        => _appRoot.GetModel<ReferenceQueryModel>().ExecuteObservableAsync(
            async model =>
            {
                MDIForwardLossRatioReadModel[] ratios = [];
                await model.LoadMDIFowardLossRatiosAsync(
                    trendDirection,
                    tradeType,
                    loaded => ratios = loaded ?? []);
                MdiForwardLossRatios = ratios;
            },
            cancellationToken);

    Task LoadReferenceDataDefinitionTypesCoreAsync(CancellationToken cancellationToken)
        => _appRoot.GetModel<ReferenceQueryModel>().ExecuteObservableAsync(
            async model =>
            {
                IReadOnlyList<LookupTypeReadModel> definitions = [];
                await model.LoadReferenceDataDefinitionTypesAsync(
                    loaded => definitions = loaded?.ToArray() ?? []);
                ReferenceDataDefinitionTypes = definitions;
            },
            cancellationToken);
}
