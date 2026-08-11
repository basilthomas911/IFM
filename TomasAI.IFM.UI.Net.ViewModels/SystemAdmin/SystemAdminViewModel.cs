using TomasAI.IFM.Domain.Reference.Shared.ViewModels;
using TomasAI.IFM.UI.Net.Contracts;
using TomasAI.IFM.UI.Net.Models;
using TomasAI.IFM.UI.Net.ViewModels.Extensions;
using TomasAI.IFM.UI.Net.ViewModels.Operations;
using TomasAI.IFM.UI.Net.ViewModels.Presentation;

namespace TomasAI.IFM.UI.Net.ViewModels.SystemAdmin;

/// <summary>
/// Exposes observable System Admin selector state and its single-flight load operation.
/// </summary>
public sealed class SystemAdminViewModel : ObservableObject
{
    readonly IAppRoot _appRoot;
    IReadOnlyList<LookupTypeReadModel> _functionTypes = [];

    public SystemAdminViewModel(IAppRoot appRoot)
    {
        _appRoot = appRoot ?? throw new ArgumentNullException(nameof(appRoot));
        LoadFunctionTypesOperation = new AsyncOperation(LoadFunctionTypesCoreAsync);
    }

    /// <summary>
    /// Gets the available System Admin functions in selector order.
    /// </summary>
    public IReadOnlyList<LookupTypeReadModel> FunctionTypes
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
    public LookupTypeReadModel? GetFunctionType(int index)
        => index >= 0 && index < FunctionTypes.Count ? FunctionTypes[index] : null;

    Task LoadFunctionTypesCoreAsync(CancellationToken cancellationToken)
        => _appRoot.GetModel<ReferenceQueryModel>().ExecuteObservableAsync(
            async model =>
            {
                IReadOnlyList<LookupTypeReadModel> functionTypes = [];
                await model.LoadSystemAdminFunctionTypesAsync(
                    loaded => functionTypes = loaded?.ToArray() ?? []);
                FunctionTypes = functionTypes;
            },
            cancellationToken);
}
