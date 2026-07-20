using TomasAI.IFM.Contracts;
using TomasAI.IFM.Models;
using TomasAI.IFM.Shared.Reference;
using TomasAI.IFM.Shared.Reference.ViewModels;
using TomasAI.IFM.Shared.StatusConsole;

namespace TomasAI.IFM.ViewModels.Reference;

public class LookupTypeEditorViewModel(IAppRoot appRoot) 
    : BaseEditorViewModel(appRoot)
{
    Dictionary<LookupTypeShortCode, LookupTypeReadModel> _lookupTypes = [];
    List<string> _lookupTypeNames = [];
    List<LookupTypeShortCodeReadModel> _lookupTypeShortCodes = [];

    /// <summary>
    /// add lookup type
    /// </summary>
    /// <param name="lookupType"></param>
    public void AddLookupType(LookupTypeReadModel lookupType, Action onCompleted)
        => AppRoot.GetModel<ReferenceCommandModel>().Execute(async model => {
            model.OnError((errorCode, errorMsg) => {
                onCompleted?.Invoke();
                OnError(errorCode, errorMsg); });
            await model.AddLookupTypeAsync(lookupType);
            LoadLookupTypes();
            WriteStatusConsole(LogSourceType.MarketData, $"Lookup Type {lookupType.Id} Added");
            onCompleted?.Invoke();
        });

    public IDictionary<LookupTypeShortCode, LookupTypeReadModel> LookupTypes => _lookupTypes;
    public ICollection<string> LookupTypeNames => _lookupTypeNames;
    public ICollection<LookupTypeShortCodeReadModel> LookupTypeShortCodes => _lookupTypeShortCodes;

    public Action OnLookupTypeNamesLoaded { get; set; } = () => { };
    public Action OnLookupTypeShortCodesLoaded { get; set; } = () => { };
    public Action<LookupTypeReadModel> OnLookupTypeLoaded { get; set; } = (lookupType) => { };
    public Action OnWaitCursor = () => { };
    public Action OnDefaultCursor = () => { };

    /// <summary>
    /// change lookup type
    /// </summary>
    /// <param name="lookupTypeId"></param>
    /// <param name="lookupType"></param>
    /// <param name="overwrite"></param>
    /// <param name="onCompleted"></param>  
    public void ChangeLookupType(LookupTypeId lookupTypeId, LookupTypeReadModel lookupType, bool overwrite,Action onCompleted)
        => AppRoot.GetModel<ReferenceCommandModel>().Execute(async model => {
            model.OnError((errorCode, errorMsg) =>
            {
                onCompleted?.Invoke();
                OnError?.Invoke(errorCode, errorMsg);
            });
            OnWaitCursor?.Invoke();
            await model.ChangeLookupTypeAsync(lookupTypeId, lookupType, overwrite);
            await Task.Delay(1000); // simulate delay for UI update
            LoadLookupTypes();
            LoadLookupTypeShortCodes(lookupType.LookupTypeName);
            WriteStatusConsole(LogSourceType.MarketData, $"Lookup Type {lookupTypeId} Changed");
            onCompleted?.Invoke();
            OnDefaultCursor?.Invoke();  
        });

    /// <summary>
    /// remove lookup type
    /// </summary>
    /// <param name="lookupTypeId"></param>
    /// <param name="overwrite"></param>
    public void RemoveLookupType(LookupTypeId lookupTypeId, bool overwrite)
        => AppRoot.GetModel<ReferenceCommandModel>().Execute(async model => {
            model.OnError((errorCode, errorMsg) => OnError(errorCode, errorMsg));
            await model.RemoveLookupTypeAsync(lookupTypeId, overwrite);
            LoadLookupTypes();
            LoadLookupTypeShortCodes(lookupTypeId.LookupTypeName);
            WriteStatusConsole(LogSourceType.MarketData, $"Lookup Type {lookupTypeId} Removed");
        });

    /// <summary>
    /// load lookup type
    /// </summary>
    public void LoadLookupTypes()
        => AppRoot.GetModel<ReferenceQueryModel>().Execute(async model => {
            model.OnError((errorCode, errorMsg) => OnError(errorCode, errorMsg));
            _lookupTypes = [];
            _lookupTypeNames = [];
            await model.LoadLookupTypesAsync(async lookupTypes => {
                await model.LoadLookupTypeNamesAsync(lookupTypeNames => {
                    if (lookupTypes?.Count  > 0)
                    {
                        foreach (var e in lookupTypes)
                            _lookupTypes.TryAdd(e.ShortCodeId, e);
                    }
                    if (lookupTypeNames?.Length > 0)
                        _lookupTypeNames.AddRange(lookupTypeNames);
                    OnLookupTypeNamesLoaded?.Invoke();
                });
            });
        });

    /// <summary>
    /// load lookup type short odes
    /// </summary>
    /// <param name="lookupTypeName"></param>
   public void LoadLookupTypeShortCodes(string lookupTypeName)
    => AppRoot.GetModel<ReferenceQueryModel>().Execute(async model => {
        model.OnError((errorCode, errorMsg) => OnError(errorCode, errorMsg));
        _lookupTypeShortCodes = [];
        await model.LoadLookupTypeShortCodesAsync(lookupTypeName, lookupTypesShortCodes => {
            if (lookupTypesShortCodes?.Length > 0)
                _lookupTypeShortCodes.AddRange(lookupTypesShortCodes);
            OnLookupTypeShortCodesLoaded?.Invoke();
        });
    });

    /// <summary>
    /// load lookup type
    /// </summary>
    /// <param name="lookupTypeName"></param>
    /// <param name="lookupTypeShortCode"></param>
    public void LoadLookupType(string lookupTypeName, string lookupTypeShortCode)
    {
        var lookupTypeShortCodeId = new LookupTypeShortCode(lookupTypeName, lookupTypeShortCode);
        if (_lookupTypes.ContainsKey(lookupTypeShortCodeId))
        {
            var lookupType = _lookupTypes[lookupTypeShortCodeId];
            OnLookupTypeLoaded?.Invoke(lookupType);
        }
    }

    /// <summary>
    /// return selected lookup type
    /// </summary>
    /// <param name="lookupTypeNameIndex"></param>
    /// <param name="lookupTypeShortCodeIndex"></param>
    /// <returns></returns>
    public LookupTypeReadModel? GetLookupType(
        string lookupTypeName,
        string lookupTypeShortCode)
    {
        var lookupType = default(LookupTypeReadModel);
        var lookupTypeShortCodeId = new LookupTypeShortCode(lookupTypeName, lookupTypeShortCode);
        if (_lookupTypes != null && _lookupTypes.ContainsKey(lookupTypeShortCodeId))
            lookupType = _lookupTypes[lookupTypeShortCodeId];
        return lookupType;
    }

    public LookupTypeReadModel? GetLookupType(
       string lookupTypeName,
       int orderId)
    {
        var lookupType = default(LookupTypeReadModel);
        var lookupTypeShortCodeId = _lookupTypes
                .Where(e => e.Value.LookupTypeName == lookupTypeName && e.Value.OrderId == orderId)
                .Select(e => e.Key).FirstOrDefault();
        if (lookupTypeShortCodeId is not null)
            lookupType = _lookupTypes[lookupTypeShortCodeId];
        return lookupType;
    }

    /// <summary>
    /// return selected lookup type name
    /// </summary>
    /// <param name="lookupTypeNameIndex"></param>
    /// <returns></returns>
    public string GetLookupTypeName(int lookupTypeNameIndex)
    {
        var lookupTypeName = string.Empty;
        if (_lookupTypeNames != null && lookupTypeNameIndex >= 0 && lookupTypeNameIndex < _lookupTypeNames.Count)
            lookupTypeName = _lookupTypeNames[lookupTypeNameIndex];
        return lookupTypeName;
    }

    /// <summary>
    /// return selected lookup type short code
    /// </summary>
    /// <param name="lookupTypeShortCodeIndex"></param>
    /// <returns></returns>
    public string GetLookupTypeShortCode(int lookupTypeShortCodeIndex)
    {
        var lookupTypeShortCode = string.Empty;
        if (_lookupTypeShortCodes != null && lookupTypeShortCodeIndex >= 0 && lookupTypeShortCodeIndex < _lookupTypeShortCodes.Count)
            lookupTypeShortCode = _lookupTypeShortCodes[lookupTypeShortCodeIndex].ShortCode;
        return lookupTypeShortCode;
    }

}
