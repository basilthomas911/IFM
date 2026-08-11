using TomasAI.IFM.Domain.Reference.Shared.ViewModels;
using TomasAI.IFM.UI.Net.Contracts;
using TomasAI.IFM.UI.Net.Models;
using TomasAI.IFM.UI.Net.ViewModels.Extensions;
using TomasAI.IFM.UI.Net.ViewModels.Operations;
using TomasAI.IFM.UI.Net.ViewModels.Presentation;

namespace TomasAI.IFM.UI.Net.ViewModels.MarketData;

/// <summary>
/// Exposes observable Market Data editor-selection state.
/// </summary>
public sealed class MarketDataViewModel : ObservableObject
{
    readonly IAppRoot _appRoot;
    IReadOnlyList<LookupTypeReadModel> _definitionTypes = [];
    bool _isEditorBusy;

    public MarketDataViewModel(IAppRoot appRoot)
    {
        _appRoot = appRoot ?? throw new ArgumentNullException(nameof(appRoot));
        LoadDefinitionTypesOperation = new AsyncOperation(LoadDefinitionTypesCoreAsync);
    }

    public IReadOnlyList<LookupTypeReadModel> DefinitionTypes
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

    public LookupTypeReadModel? GetDefinitionType(int index)
        => index >= 0 && index < DefinitionTypes.Count ? DefinitionTypes[index] : null;

    Task LoadDefinitionTypesCoreAsync(CancellationToken cancellationToken)
        => _appRoot.GetModel<ReferenceQueryModel>().ExecuteObservableAsync(
            async model =>
            {
                IReadOnlyList<LookupTypeReadModel> definitions = [];
                await model.LoadMarketDataDefinitionTypesAsync(
                    loaded => definitions = loaded?.ToArray() ?? []);
                DefinitionTypes = definitions;
            },
            cancellationToken);
}
