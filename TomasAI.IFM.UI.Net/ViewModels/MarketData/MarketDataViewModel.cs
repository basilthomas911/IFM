using TomasAI.IFM.UI.Net.Contracts;
using TomasAI.IFM.UI.Net.Models;
using TomasAI.IFM.Shared.Reference.ViewModels;

namespace TomasAI.IFM.UI.Net.ViewModels.MarketData;

/// <summary>
/// Represents a view model for managing market data definitions.
/// </summary>
/// <remarks>This class provides functionality to load and retrieve market data definition types. It interacts
/// with the application's root model to fetch data asynchronously and stores the results for further use. Use this
/// class to manage and access market data definitions in a structured way.</remarks>
/// <param name="appRoot">The application root object used to access models and execute queries. Cannot be null.</param>
public class MarketDataViewModel(IAppRoot appRoot)
{
    readonly IAppRoot _appRoot = appRoot;
    List<LookupTypeReadModel>? _mktDataDefTypes;

    public Action OnDisableAllButtons = () => { };
    public Action<bool> OnEnableMarketSelector = (enabled) => { };

    public void LoadMarketDefinitionTypes(Action<LookupTypeReadModel[]> onDataLoad)
        => _appRoot.GetModel<ReferenceQueryModel>().Execute(async model =>
            await model.LoadMarketDataDefinitionTypesAsync(mktDataDefTypes => {
                _mktDataDefTypes = [];
                if (mktDataDefTypes?.Count > 0)
                    _mktDataDefTypes.AddRange(mktDataDefTypes);
                onDataLoad?.Invoke([.. mktDataDefTypes!]);
            }));

    public LookupTypeReadModel GetMarketDefinitionType(int index)
        => _mktDataDefTypes?.Count > 0 ? _mktDataDefTypes[index] : LookupTypeReadModel.Default;

}
