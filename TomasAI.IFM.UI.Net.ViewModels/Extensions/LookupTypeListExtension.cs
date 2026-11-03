using TomasAI.IFM.Domain.Reference.Shared.ViewModels;
using TomasAI.IFM.Domain.Reference.Shared.ViewModels;

namespace TomasAI.IFM.UI.Net.Extensions;

public static class LookupTypeListExtension
{

    /// <param name="lookupTypes">
    ///
    /// </param>
    extension(List<LookupTypeReadModel> lookupTypes)
    {
        /// <summary>
        /// return lookup type
        /// </summary>
        ///
        /// <param name="index"></param>
        /// <returns>lookup type</returns>
        public LookupTypeReadModel GetLookupType(int index)
        {
            if (lookupTypes == null)
                return LookupTypeReadModel.Default;
            return (index < 0 && lookupTypes.Count > 0)
                ? lookupTypes[0]
                : lookupTypes[index];
        }

        /// <summary>
        /// return lookup type index
        /// </summary>
        ///
        /// <param name="shortCode"></param>
        /// <returns>lookup type index</returns>
        public int GetLookupTypeIndex(string shortCode)
            => lookupTypes?.Where(e => e.ShortCode.Equals(shortCode, StringComparison.CurrentCultureIgnoreCase))?.Select(e => e.OrderId)?.First() ?? -1;
    }

}
