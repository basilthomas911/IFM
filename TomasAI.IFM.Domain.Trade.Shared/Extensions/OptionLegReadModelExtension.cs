using TomasAI.IFM.Domain.MarketData.Shared;
using TomasAI.IFM.Domain.Trade.Shared.ViewModels;

namespace TomasAI.IFM.Domain.Trade.Shared.Extensions;

/// <summary>
/// Provides extension methods for working with arrays of <see cref="OptionTradeLegReadModel"/> objects.
/// </summary>
/// <remarks>This static class includes methods for retrieving, updating, and manipulating option leg data based
/// on specific criteria such as contract ID, option type, and action. These methods are designed to simplify common
/// operations on collections of <see cref="OptionTradeLegReadModel"/> instances.</remarks>
public static class OptionLegReadModelExtension
{
    public static OptionTradeLegReadModel? Get(this OptionTradeLegReadModel[] optionLegs, string contractId)
    {
        OptionTradeLegReadModel? result = null;
        foreach (var optionLeg in optionLegs)
        {
            if (!string.Equals(optionLeg.ContractId, contractId, StringComparison.Ordinal))
                continue;
            EnsureUnique(result);
            result = optionLeg;
        }
        return result;
    }

    public static OptionTradeLegReadModel? Get(this OptionTradeLegReadModel[] optionLegs, OptionLegAction optionLegAction, OptionType optionType)
    {
        OptionTradeLegReadModel? result = null;
        foreach (var optionLeg in optionLegs)
        {
            if (optionLeg.OptionLegType != optionType || optionLeg.OptionLegAction != optionLegAction)
                continue;
            EnsureUnique(result);
            result = optionLeg;
        }
        return result;
    }

    public static string? GetContractId(this OptionTradeLegReadModel[] optionLegs, OptionLegAction optionLegAction, OptionType optionType)
    {
        var item = optionLegs.Get(optionLegAction, optionType);
        return item?.ContractId;
    }


    public static int? GetQuantity(this OptionTradeLegReadModel[] optionLegs, OptionLegAction optionLegAction, OptionType optionType)
    {
         var item = optionLegs.Get(optionLegAction, optionType);
         return item?.Quantity;
    }

    public static decimal? GetStrikePrice(this OptionTradeLegReadModel[] optionLegs, OptionLegAction optionLegAction, OptionType optionType)
    {
          var item = optionLegs.Get(optionLegAction, optionType);
          return item?.StrikePrice;
    }

    public static void SetContractId(this OptionTradeLegReadModel[] optionLegs, int tradeId, string contractId, string newContractId)
        => SetOptionLeg(optionLegs, tradeId, contractId, optionLeg => optionLeg = optionLeg with { ContractId = newContractId });

    public static void SetStrikePrice(this OptionTradeLegReadModel[] optionLegs, int tradeId, string contractId, decimal newStrikePrice)
        => SetOptionLeg(optionLegs, tradeId, contractId, optionLeg => optionLeg with { StrikePrice = newStrikePrice });

    public static void SetOptionLegType(this OptionTradeLegReadModel[] optionLegs, int tradeId, string contractId, OptionType newOptionLegType)
        => SetOptionLeg(optionLegs, tradeId, contractId, optionLeg => optionLeg with { OptionLegType = newOptionLegType });

    public static void SetOptionLegAction(this OptionTradeLegReadModel[] optionLegs, int tradeId, string contractId, OptionLegAction newOptionLegAction)
        => SetOptionLeg(optionLegs, tradeId, contractId, optionLeg => optionLeg with { OptionLegAction = newOptionLegAction });

    public static void SetQuantity(this OptionTradeLegReadModel[] optionLegs, int tradeId, string contractId, int newQuantity)
        => SetOptionLeg(optionLegs, tradeId, contractId, optionLeg => optionLeg with { Quantity = newQuantity });

    private static void SetOptionLeg(OptionTradeLegReadModel[] optionLegs, int tradeId, string contractId, Func<OptionTradeLegReadModel, OptionTradeLegReadModel> getOptionLeg)
    {
        for (var index = 0; index < optionLegs.Length; index++)
        {
            var optionLeg = optionLegs[index];
            if (optionLeg.TradeId == tradeId && optionLeg.ContractId == contractId)
            {
                optionLegs[index] = getOptionLeg(optionLeg);
                break;
            }
        }
    }

    static void EnsureUnique(OptionTradeLegReadModel? current)
    {
        if (current is not null)
            throw new InvalidOperationException("Sequence contains more than one matching element");
    }
}

public static class OptionLegDataReadModelExtension
{
    public static OptionTradeLegDataReadModel? Get(this OptionTradeLegDataReadModel[] optionLegData, string contractId)
    {
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
        => SetOptionLegData(optionLegs,  optionLegId, optionLegData => optionLegData with { BidPrice = bidPrice });

    public static void SetAskPrice(this OptionTradeLegDataReadModel[] optionLegs, string optionLegId, decimal askPrice)
        => SetOptionLegData(optionLegs, optionLegId, optionLegData => optionLegData with { AskPrice = askPrice });

    public static void Set(this OptionTradeLegDataReadModel[] optionLegs, string optionLegId, OptionTradeLegDataReadModel optionLegData)
        => SetOptionLegData(optionLegs, optionLegId, e => optionLegData);

    private static void SetOptionLegData(OptionTradeLegDataReadModel[] optionLegs, string optionLegId, Func<OptionTradeLegDataReadModel, OptionTradeLegDataReadModel> getOptionLegData)
    {
        for (var index = 0; index < optionLegs.Length; index++)
        {
            var optionLegData = optionLegs[index];
            if (optionLegData.OptionLegId == optionLegId)
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
