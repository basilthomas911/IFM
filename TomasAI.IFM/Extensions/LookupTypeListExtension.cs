using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TomasAI.IFM.Shared.Reference.ViewModels;

namespace TomasAI.IFM.Extensions
{
    public static class LookupTypeListExtension
    {
        /// <summary>
        /// return lookup type
        /// </summary>
        /// <param name="lookupTypes"></param>
        /// <param name="index"></param>
        /// <returns>lookup type</returns>
        public static LookupTypeReadModel GetLookupType(this List<LookupTypeReadModel> lookupTypes, int index)
            => lookupTypes?.Count > index ? lookupTypes[index] : LookupTypeReadModel.Default;

        /// <summary>
        /// return lookup type index
        /// </summary>
        /// <param name="lookupTypes"></param>
        /// <param name="shortCode"></param>
        /// <returns>lookup type index</returns>
        public static int GetLookupTypeIndex(this List<LookupTypeReadModel> lookupTypes, string shortCode)
            => lookupTypes?.Where(e => e.ShortCode.ToUpper() == shortCode.ToUpper())?.Select(e => e.OrderId)?.Single() ?? -1;

    }
}
