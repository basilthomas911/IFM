using System.Globalization;
using MessagePack;
using TomasAI.IFM.Domain.Portfolio.Shared.ViewModels;

namespace TomasAI.IFM.Domain.Portfolio.Shared.Contracts;

/// <summary>Creates canonical references for Portfolio Fund order trades.</summary>
public static class FundOrderTradeReference
{
    public static string Create(string baseContractId, DateOnly tradeDate, DateOnly? maturityDate)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(baseContractId);
        return maturityDate.HasValue
            ? $"{baseContractId.Trim()} @ {tradeDate.ToString("yyyyMMdd", CultureInfo.InvariantCulture)} - {maturityDate.Value.ToString("yyyyMMdd", CultureInfo.InvariantCulture)}"
            : $"{baseContractId.Trim()} @ {tradeDate.ToString("yyyyMMdd", CultureInfo.InvariantCulture)}";
    }
}
