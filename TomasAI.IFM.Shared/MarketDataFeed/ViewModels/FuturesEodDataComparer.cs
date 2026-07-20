namespace TomasAI.IFM.Shared.MarketDataFeed.ViewModels;

public class FuturesEodDataComparer : IEqualityComparer<FuturesEodDataV2ReadModel>
{
    public bool Equals(FuturesEodDataV2ReadModel x, FuturesEodDataV2ReadModel y) => x.ContractId == y.ContractId && x.ValueDate == y.ValueDate;
    public int GetHashCode(FuturesEodDataV2ReadModel e) => $"{e.ContractId}|{e.ValueDate:yyyy-MM-dd}".GetHashCode();
}
