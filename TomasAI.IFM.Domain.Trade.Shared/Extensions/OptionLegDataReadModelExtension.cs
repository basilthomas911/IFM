using TomasAI.IFM.Domain.MarketData.Shared;
using TomasAI.IFM.Domain.Trade.Shared.ViewModels;

namespace TomasAI.IFM.Domain.Trade.Shared.Extensions;

public static class OptionLegDataReadModelExtension
{
    public static OptionTradeLegDataReadModel? Get(this OptionTradeLegDataReadModel[] optionLegData, string contractId)
    {
        ArgumentNullException.ThrowIfNull(optionLegData);
        OptionTradeLegDataReadModel? result = null;
        foreach (var optionLegDataItem in optionLegData)
        {
            if (!string.Equals(optionLegDataItem.OptionLeg!.ContractId, contractId, StringComparison.Ordinal))
                continue;
            EnsureUnique(result);
            result = optionLegDataItem;
        }
        return result;
    }

    public static OptionTradeLegDataReadModel? Get(this OptionTradeLegDataReadModel[] optionLegData, OptionLegAction optionLegAction, OptionType optionType)
    {
        ArgumentNullException.ThrowIfNull(optionLegData);
        OptionTradeLegDataReadModel? result = null;
        foreach (var optionLegDataItem in optionLegData)
        {
            if (optionLegDataItem.OptionLeg!.OptionLegType != optionType || optionLegDataItem.OptionLeg.OptionLegAction != optionLegAction)
                continue;
            EnsureUnique(result);
            result = optionLegDataItem;
        }
        return result;
    }

    public static decimal? GetBidPrice(this OptionTradeLegDataReadModel[] optionLegData, OptionLegAction optionLegAction, OptionType optionType)
    {
        var item = optionLegData.Get(optionLegAction, optionType);
        return item?.BidPrice;
    }

    public static decimal? GetAskPrice(this OptionTradeLegDataReadModel[] optionLegData, OptionLegAction optionLegAction, OptionType optionType)
    {
        var item = optionLegData.Get(optionLegAction, optionType);
        return item?.AskPrice;
    }

    public static void SetBidPrice(this OptionTradeLegDataReadModel[] optionLegs, string optionLegId, decimal bidPrice)
        
    {
        ArgumentNullException.ThrowIfNull(optionLegs);
        SetOptionLegData(optionLegs, optionLegId, optionLegData => optionLegData with { BidPrice = bidPrice });
    }

    public static void SetAskPrice(this OptionTradeLegDataReadModel[] optionLegs, string optionLegId, decimal askPrice)
        
    {
        ArgumentNullException.ThrowIfNull(optionLegs);
        SetOptionLegData(optionLegs, optionLegId, optionLegData => optionLegData with { AskPrice = askPrice });
    }

    public static void Set(this OptionTradeLegDataReadModel[] optionLegs, string optionLegId, OptionTradeLegDataReadModel optionLegData)
        
    {
        ArgumentNullException.ThrowIfNull(optionLegs);
        SetOptionLegData(optionLegs, optionLegId, e => optionLegData);
    }

    private static void SetOptionLegData(OptionTradeLegDataReadModel[] optionLegs, string optionLegId, Func<OptionTradeLegDataReadModel, OptionTradeLegDataReadModel> getOptionLegData)
    {
        for (var index = 0; index < optionLegs.Length; index++)
        {
            var optionLegData = optionLegs[index];
            if (string.Equals(optionLegData.OptionLegId,optionLegId,StringComparison.Ordinal))
            {
                optionLegs[index] = getOptionLegData(optionLegData);
                break;
            }
        }
    }

    static void EnsureUnique(OptionTradeLegDataReadModel? current)
    {
        if (current is not null)
            throw new InvalidOperationException("Sequence contains more than one matching element");
    }

}
