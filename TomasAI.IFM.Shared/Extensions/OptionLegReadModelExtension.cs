using TomasAI.IFM.Shared.Trade;
using TomasAI.IFM.Shared.Trade.ViewModels;

namespace TomasAI.IFM.Shared.Extensions;

/// <summary>
/// Provides extension methods for working with arrays of <see cref="OptionTradeLegReadModel"/> objects.
/// </summary>
/// <remarks>This static class includes methods for retrieving, updating, and manipulating option leg data based
/// on specific criteria such as contract ID, option type, and action. These methods are designed to simplify common
/// operations on collections of <see cref="OptionTradeLegReadModel"/> instances.</remarks>
public static class OptionLegReadModelExtension
{
    public static OptionTradeLegReadModel? Get(this OptionTradeLegReadModel[] optionLegs, string contractId)
       => optionLegs
           .Where(o => o.ContractId == contractId)
           .SingleOrDefault();

    public static OptionTradeLegReadModel? Get(this OptionTradeLegReadModel[] optionLegs, OptionLegAction optionLegAction, OptionType optionType)
       => optionLegs
           .Where(o => o.OptionLegType == optionType && o.OptionLegAction == optionLegAction)
           .SingleOrDefault();

    public static string? GetContractId(this OptionTradeLegReadModel[] optionLegs, OptionLegAction optionLegAction, OptionType optionType)
    {
        var item = optionLegs
            .Where(o => o.OptionLegType == optionType && o.OptionLegAction == optionLegAction)
            .SingleOrDefault();
        return item?.ContractId;
    }


    public static int? GetQuantity(this OptionTradeLegReadModel[] optionLegs, OptionLegAction optionLegAction, OptionType optionType)
    {
         var item = optionLegs
             .Where(o => o.OptionLegType == optionType && o.OptionLegAction == optionLegAction)
             .SingleOrDefault();
         return item?.Quantity;
    }

    public static decimal? GetStrikePrice(this OptionTradeLegReadModel[] optionLegs, OptionLegAction optionLegAction, OptionType optionType)
    {
          var item = optionLegs
              .Where(o => o.OptionLegType == optionType && o.OptionLegAction == optionLegAction)
              .SingleOrDefault();
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
}

public static class OptionLegDataReadModelExtension
{
    public static OptionTradeLegDataReadModel? Get(this OptionTradeLegDataReadModel[] optionLegData, string contractId)
        => optionLegData
            .Where(o => o.OptionLeg!.ContractId == contractId)
            .SingleOrDefault();

    public static OptionTradeLegDataReadModel? Get(this OptionTradeLegDataReadModel[] optionLegData, OptionLegAction optionLegAction, OptionType optionType)
        => optionLegData
            .Where(o => o.OptionLeg!.OptionLegType == optionType && o.OptionLeg.OptionLegAction == optionLegAction)
            .SingleOrDefault();

    public static decimal? GetBidPrice(this OptionTradeLegDataReadModel[] optionLegData, OptionLegAction optionLegAction, OptionType optionType)
    {
        var item = optionLegData
            .Where(o => o.OptionLeg!.OptionLegType == optionType && o.OptionLeg.OptionLegAction == optionLegAction)
            .SingleOrDefault();
        return item?.BidPrice;
    }

    public static decimal? GetAskPrice(this OptionTradeLegDataReadModel[] optionLegData, OptionLegAction optionLegAction, OptionType optionType)
    {
        var item = optionLegData
            .Where(o => o.OptionLeg!.OptionLegType == optionType && o.OptionLeg.OptionLegAction == optionLegAction)
            .SingleOrDefault();
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

}
