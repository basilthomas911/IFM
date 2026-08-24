using TomasAI.IFM.UI.Net.Contracts;
using TomasAI.IFM.UI.Net.Models.Reference;
using TomasAI.IFM.UI.Net.Services.Operations;
using TomasAI.IFM.UI.Net.Services.Reference;
using TomasAI.IFM.UI.Net.ViewModels.Operations;
using TomasAI.IFM.UI.Net.ViewModels.Presentation;

namespace TomasAI.IFM.UI.Net.ViewModels.MarketData;

/// <summary>
/// Exposes observable Market Data editor-selection state.
/// </summary>
public sealed class MarketDataViewModel : ObservableObject
{
    readonly IReferenceDataService _referenceDataService;
    IReadOnlyList<LookupTypeUiModel> _definitionTypes = [];
    bool _isEditorBusy;

    /// <summary>Creates the selector workflow with its explicit Reference service.</summary>
    public MarketDataViewModel(IReferenceDataService referenceDataService)
    {
        _referenceDataService = referenceDataService
            ?? throw new ArgumentNullException(nameof(referenceDataService));
        LoadDefinitionTypesOperation = new AsyncOperation(LoadDefinitionTypesCoreAsync);
    }

    public IReadOnlyList<LookupTypeUiModel> DefinitionTypes
    {
        get => _definitionTypes;
        private set => SetProperty(ref _definitionTypes, value);
    }

    /// <summary>
    /// Gets whether the active specialized editor is executing work that disables shell interaction.
    /// </summary>
    public bool IsEditorBusy
    {
        get => _isEditorBusy;
        private set => SetProperty(ref _isEditorBusy, value);
    }

    public IAsyncOperation LoadDefinitionTypesOperation { get; }

    public void SetEditorBusy(bool isBusy) => IsEditorBusy = isBusy;

    public LookupTypeUiModel? GetDefinitionType(int index)
        => index >= 0 && index < DefinitionTypes.Count ? DefinitionTypes[index] : null;

    async Task LoadDefinitionTypesCoreAsync(CancellationToken cancellationToken)
        => DefinitionTypes = (await _referenceDataService.GetMarketDataDefinitionTypesAsync(cancellationToken))
            .RequireValue();
}
