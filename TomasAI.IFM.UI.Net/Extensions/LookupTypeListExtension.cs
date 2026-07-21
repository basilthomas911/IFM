using TomasAI.IFM.Shared.Reference.ViewModels;

namespace TomasAI.IFM.UI.Net.Extensions;

public static class LookupTypeListExtension
{
    /// <summary>
    /// return lookup type
    /// </summary>
    /// <param name="lookupTypes"></param>
    /// <param name="index"></param>
    /// <returns>lookup type</returns>
    public static LookupTypeReadModel GetLookupType(this List<LookupTypeReadModel> lookupTypes, int index)
    {
        if (lookupTypes == null)
            return LookupTypeReadModel.Default;
        return (index < 0 && lookupTypes.Count > 0)
            ? lookupTypes[0]
            :  lookupTypes[index];
    }

    /// <summary>
    /// return lookup type index
    /// </summary>
    /// <param name="lookupTypes"></param>
    /// <param name="shortCode"></param>
    /// <returns>lookup type index</returns>
    public static int GetLookupTypeIndex(this List<LookupTypeReadModel> lookupTypes, string shortCode)
        => lookupTypes?.Where(e => e.ShortCode.Equals(shortCode, StringComparison.CurrentCultureIgnoreCase))?.Select(e => e.OrderId)?.First() ?? -1;

}
