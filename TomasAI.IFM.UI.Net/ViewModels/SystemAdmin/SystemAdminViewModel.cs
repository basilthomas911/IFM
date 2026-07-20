using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TomasAI.IFM.Contracts;
using TomasAI.IFM.Models;
using TomasAI.IFM.Shared.Reference.ViewModels;

namespace TomasAI.IFM.ViewModels.SystemAdmin
{
    public class SystemAdminViewModel
    {
        private readonly IAppRoot _appRoot;
        private List<LookupTypeReadModel> _sysAdminFuncTypes = null!;

        public SystemAdminViewModel(IAppRoot appRoot)
        {
            _appRoot = appRoot;
        }

        public void LoadSystemAdminFunctionTypes(Action<LookupTypeReadModel[]> onDataLoad)
            => _appRoot.GetModel<ReferenceQueryModel>().Execute(async model =>
                await model.LoadSystemAdminFunctionTypesAsync(sysAdminFuncType => {
                    _sysAdminFuncTypes = new List<LookupTypeReadModel>();
                    if (sysAdminFuncType != null && sysAdminFuncType.Count > 0)
                        _sysAdminFuncTypes.AddRange(sysAdminFuncType);
                    onDataLoad([.. sysAdminFuncType!]);
                }));

        public LookupTypeReadModel? GetSystemAdminFunctionType(int index)
            => _sysAdminFuncTypes != null && _sysAdminFuncTypes.Count > 0 ? _sysAdminFuncTypes[index] : null;
    }
}
