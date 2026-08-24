using TomasAI.IFM.UI.Net.Contracts;
using TomasAI.IFM.UI.Net.Models.Reference;
using TomasAI.IFM.UI.Net.Services.Operations;
using TomasAI.IFM.UI.Net.Services.Reference;
using TomasAI.IFM.UI.Net.ViewModels.Operations;
using TomasAI.IFM.UI.Net.ViewModels.Presentation;

namespace TomasAI.IFM.UI.Net.ViewModels.SystemAdmin;

/// <summary>
/// Exposes observable System Admin selector state and its single-flight load operation.
/// </summary>
public sealed class SystemAdminViewModel : ObservableObject
{
    readonly IReferenceDataService _referenceDataService;
    IReadOnlyList<LookupTypeUiModel> _functionTypes = [];

    /// <summary>Creates the selector workflow with its explicit Reference service.</summary>
    public SystemAdminViewModel(IReferenceDataService referenceDataService)
    {
        _referenceDataService = referenceDataService
            ?? throw new ArgumentNullException(nameof(referenceDataService));
        LoadFunctionTypesOperation = new AsyncOperation(LoadFunctionTypesCoreAsync);
    }

    /// <summary>
    /// Gets the available System Admin functions in selector order.
    /// </summary>
    public IReadOnlyList<LookupTypeUiModel> FunctionTypes
    {
        get => _functionTypes;
        private set => SetProperty(ref _functionTypes, value);
    }

    /// <summary>
    /// Gets the single-flight operation that loads the function selector.
    /// </summary>
    public IAsyncOperation LoadFunctionTypesOperation { get; }

    /// <summary>
    /// Gets a function type by index, or <see langword="null"/> when the selection is invalid.
    /// </summary>
    public LookupTypeUiModel? GetFunctionType(int index)
        => index >= 0 && index < FunctionTypes.Count ? FunctionTypes[index] : null;

    async Task LoadFunctionTypesCoreAsync(CancellationToken cancellationToken)
        => FunctionTypes = (await _referenceDataService.GetSystemAdminFunctionTypesAsync(cancellationToken))
            .RequireValue();
}
