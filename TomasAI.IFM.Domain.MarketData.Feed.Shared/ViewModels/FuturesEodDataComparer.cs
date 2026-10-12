using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.ViewModels;
namespace TomasAI.IFM.Domain.MarketData.Feed.Shared.ViewModels;

public class FuturesEodDataComparer : IEqualityComparer<FuturesEodDataV2ReadModel>
{
    public bool Equals(FuturesEodDataV2ReadModel x, FuturesEodDataV2ReadModel y) 
    {
        ArgumentNullException.ThrowIfNull(x);
        ArgumentNullException.ThrowIfNull(y);
        return string.Equals(x.ContractId,y.ContractId,StringComparison.Ordinal) && x.ValueDate == y.ValueDate;
    }
    public int GetHashCode(FuturesEodDataV2ReadModel e) 
    {
        ArgumentNullException.ThrowIfNull(e);
        return $"{e.ContractId}|{e.ValueDate:yyyy-MM-dd}".GetHashCode(StringComparison.Ordinal);
    }
}
