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
        ArgumentNullException.ThrowIfNull(optionLegs);
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
        ArgumentNullException.ThrowIfNull(optionLegs);
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
        
    {
        ArgumentNullException.ThrowIfNull(optionLegs);
        SetOptionLeg(optionLegs, tradeId, contractId, optionLeg => optionLeg with { ContractId = newContractId });
    }

    public static void SetStrikePrice(this OptionTradeLegReadModel[] optionLegs, int tradeId, string contractId, decimal newStrikePrice)
        
    {
        ArgumentNullException.ThrowIfNull(optionLegs);
        SetOptionLeg(optionLegs, tradeId, contractId, optionLeg => optionLeg with { StrikePrice = newStrikePrice });
    }

    public static void SetOptionLegType(this OptionTradeLegReadModel[] optionLegs, int tradeId, string contractId, OptionType newOptionLegType)
        
    {
        ArgumentNullException.ThrowIfNull(optionLegs);
        SetOptionLeg(optionLegs, tradeId, contractId, optionLeg => optionLeg with { OptionLegType = newOptionLegType });
    }

    public static void SetOptionLegAction(this OptionTradeLegReadModel[] optionLegs, int tradeId, string contractId, OptionLegAction newOptionLegAction)
        
    {
        ArgumentNullException.ThrowIfNull(optionLegs);
        SetOptionLeg(optionLegs, tradeId, contractId, optionLeg => optionLeg with { OptionLegAction = newOptionLegAction });
    }

    public static void SetQuantity(this OptionTradeLegReadModel[] optionLegs, int tradeId, string contractId, int newQuantity)
        
    {
        ArgumentNullException.ThrowIfNull(optionLegs);
        SetOptionLeg(optionLegs, tradeId, contractId, optionLeg => optionLeg with { Quantity = newQuantity });
    }

    private static void SetOptionLeg(OptionTradeLegReadModel[] optionLegs, int tradeId, string contractId, Func<OptionTradeLegReadModel, OptionTradeLegReadModel> getOptionLeg)
    {
        for (var index = 0; index < optionLegs.Length; index++)
        {
            var optionLeg = optionLegs[index];
            if (optionLeg.TradeId == tradeId && string.Equals(optionLeg.ContractId,contractId,StringComparison.Ordinal))
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
