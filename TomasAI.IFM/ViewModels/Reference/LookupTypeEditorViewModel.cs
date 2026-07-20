using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using TomasAI.IFM.Contracts;
using TomasAI.IFM.Models;
using TomasAI.IFM.Shared.Reference;
using TomasAI.IFM.Shared.Reference.ViewModels;
using TomasAI.IFM.Shared.Reference.Events;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Log;

namespace TomasAI.IFM.ViewModels.Reference
{
    public class LookupTypeEditorViewModel : BaseEditorViewModel
    {
        Dictionary<LookupTypeId, LookupTypeReadModel> _lookupTypes;
        List<string> _lookupTypeNames;
        List<LookupTypeShortCodeReadModel> _lookupTypeShortCodes;

        public LookupTypeEditorViewModel(IAppRoot appRoot)
            : base(appRoot)
        {
        }

        public IDictionary<LookupTypeId, LookupTypeReadModel> LookupTypes => _lookupTypes;
        public ICollection<string> LookupTypeNames => _lookupTypeNames;
        public ICollection<LookupTypeShortCodeReadModel> LookupTypeShortCodes => _lookupTypeShortCodes;

        public Action OnLookupTypeNamesLoaded;
        public Action OnLookupTypeShortCodesLoaded;
        public Action<LookupTypeReadModel> OnLookupTypeLoaded;

        /// <summary>
        /// add lookup type
        /// </summary>
        /// <param name="lookupType"></param>
        public void AddLookupType(LookupTypeReadModel lookupType, Action onCompleted)
            => AppRoot.GetModel<ReferenceCommandModel>().Execute(async model => {
                model.OnError((errorCode, errorMsg) => OnError(errorCode, errorMsg));
                await model.AddLookupTypeAsync(lookupType);
                LoadLookupTypes();
                WriteStatusConsole(LogSourceType.MarketData, $"Lookup Type {lookupType.Id} Added");
                onCompleted?.Invoke();
            });

        /// <summary>
        /// change lookup type
        /// </summary>
        /// <param name="lookupTypeId"></param>
        /// <param name="lookupType"></param>
        public void ChangeLookupType(LookupTypeId lookupTypeId, LookupTypeReadModel lookupType, Action onCompleted)
            => AppRoot.GetModel<ReferenceCommandModel>().Execute(async model => {
                model.OnError((errorCode, errorMsg) => OnError(errorCode, errorMsg));
                await model.ChangeLookupTypeAsync(lookupTypeId, lookupType);
                LoadLookupTypes();
                WriteStatusConsole(LogSourceType.MarketData, $"Lookup Type {lookupTypeId} Changed");
                onCompleted?.Invoke();
            });

        /// <summary>
        /// remove lookup type
        /// </summary>
        /// <param name="lookupTypeId"></param>
        public void RemoveLookupType(LookupTypeId lookupTypeId)
            => AppRoot.GetModel<ReferenceCommandModel>().Execute(async model => {
                model.OnError((errorCode, errorMsg) => OnError(errorCode, errorMsg));
                await model.RemoveLookupTypeAsync(lookupTypeId);
                LoadLookupTypes();
                WriteStatusConsole(LogSourceType.MarketData, $"Lookup Type {lookupTypeId} Removed");
            });

        /// <summary>
        /// load lookup type
        /// </summary>
        public void LoadLookupTypes()
            => AppRoot.GetModel<ReferenceQueryModel>().Execute(async model => {
                model.OnError((errorCode, errorMsg) => OnError(errorCode, errorMsg));
                _lookupTypes = new Dictionary<LookupTypeId, LookupTypeReadModel>();
                _lookupTypeNames = new List<string>();
                await model.LoadLookupTypesAsync(async lookupTypes => {
                    await model.LoadLookupTypeNamesAsync(lookupTypeNames => {
                        if ((lookupTypes?.Length ?? 0) > 0)
                        {
                            foreach (var e in lookupTypes)
                                _lookupTypes.Add(e.Id, e);

                        }
                        if ((lookupTypeNames?.Length ?? 0) > 0)
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
            _lookupTypeShortCodes = new List<LookupTypeShortCodeReadModel>();
            await model.LoadLookupTypeShortCodesAsync(lookupTypeName, lookupTypesShortCodes => {
                if ((lookupTypesShortCodes?.Length ?? 0) > 0)
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
            var lookupTypeId = new LookupTypeId(lookupTypeName, lookupTypeShortCode);
            if (_lookupTypes.ContainsKey(lookupTypeId))
            {
                var lookupType = _lookupTypes[lookupTypeId];
                OnLookupTypeLoaded?.Invoke(lookupType);
            }
        }

        /// <summary>
        /// return selected lookup type
        /// </summary>
        /// <param name="lookupTypeNameIndex"></param>
        /// <param name="lookupTypeShortCodeIndex"></param>
        /// <returns></returns>
        public LookupTypeReadModel GetLookupType(
            string lookupTypeName,
            string lookupTypeShortCode)
        {
            var lookupType = default(LookupTypeReadModel);
            var lookupTypeId = new LookupTypeId(lookupTypeName, lookupTypeShortCode);
            if (_lookupTypes != null && _lookupTypes.ContainsKey(lookupTypeId))
                lookupType = _lookupTypes[lookupTypeId];
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
}
