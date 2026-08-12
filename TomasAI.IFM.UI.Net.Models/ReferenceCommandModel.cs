using TomasAI.IFM.Domain.Reference.Shared.ServiceApi;
using TomasAI.IFM.Domain.Reference.Shared.ViewModels;
using TomasAI.IFM.Domain.Reference.Shared;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TomasAI.IFM.Domain.Reference.Shared.ViewModels;
using TomasAI.IFM.Domain.Reference.Shared.ServiceApi;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.UI.Net.Models
{
    public class ReferenceCommandModel : BaseModel<ReferenceCommandModel>
    {
        readonly IReferenceCommandApi _commandApi;

        /// <summary>
        /// create reference model
        /// </summary>
        /// <param name="commandApi"></param>
        public ReferenceCommandModel(IReferenceCommandApi commandApi)
        {
            _commandApi = commandApi ?? throw new ArgumentNullException(nameof(commandApi));
        }

        /// <summary>
        /// add lookup type
        /// </summary>
        /// <param name="lookupType"></param>
        public async Task<Guid> AddLookupTypeAsync(LookupTypeReadModel lookupType)
            => await ExecuteCommandAsync(() => _commandApi.AddLookupTypeAsync(lookupType));

        /// <summary>
        /// change lookup type
        /// </summary>
        /// <param name="lookupTypeId"></param>
        /// <param name="lookupType"></param>
        /// <param name="overwrite"></param>
        public async Task<Guid> ChangeLookupTypeAsync(LookupTypeId lookupTypeId, LookupTypeReadModel lookupType, bool overwrite)
            => await ExecuteCommandAsync(() => _commandApi.ChangeLookupTypeAsync(lookupTypeId, lookupType, overwrite));

        /// <summary>
        /// remove lookup type
        /// </summary>
        /// <param name="lookupTypeId"></param>
        /// <param name="overwrite"></param>
        public async Task<Guid> RemoveLookupTypeAsync(LookupTypeId lookupTypeId, bool overwrite)
            => await ExecuteCommandAsync(() => _commandApi.RemoveLookupTypeAsync(lookupTypeId, overwrite));

    }
}
