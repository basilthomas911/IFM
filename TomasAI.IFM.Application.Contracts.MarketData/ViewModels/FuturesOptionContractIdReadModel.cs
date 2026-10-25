using System;
using System.Collections.Generic;
using System.Text;

namespace TomasAI.IFM.Domain.MarketData.Shared.ViewModels
{
    /// <summary>
    /// futures option contract id
    /// </summary>
    public class FuturesOptionContractIdReadModel
    {

        /// <summary>
        /// futures contract id constructor
        /// </summary>
        /// <param name="symbol"></param>
        /// <param name="contractMonth"></param>
        /// <param name="optionType"></param>
        /// <param name="strikePrice"></param>
        public FuturesOptionContractIdReadModel(
            string symbol,
            DateOnly contractMonth,
            OptionType optionType,
            double strikePrice)
        {
            Symbol = symbol;
            ContractMonth = contractMonth;
            OptionType = optionType;
            StrikePrice = strikePrice;
        }

        /// <summary>
        /// futures contract id constructor from string contract id
        /// </summary>
        /// <param name="contractId"></param>
        public FuturesOptionContractIdReadModel(string contractId)
        {
            ArgumentNullException.ThrowIfNull(contractId);
            Symbol = contractId.Substring(0, 2);
            ContractMonth = new DateOnly(int.Parse(contractId.AsSpan(2, 4), System.Globalization.CultureInfo.InvariantCulture), int.Parse(contractId.AsSpan(6, 2), System.Globalization.CultureInfo.InvariantCulture), int.Parse(contractId.AsSpan(8, 2), System.Globalization.CultureInfo.InvariantCulture));
            OptionType = string.Equals(contractId.Substring(10, 1),"P",StringComparison.Ordinal) ? OptionType.Put : OptionType.Call;
            StrikePrice = double.Parse(contractId.AsSpan(11, 4), System.Globalization.CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// return symbol
        /// </summary>
        public string Symbol { get; }

        /// <summary>
        /// return contract month
        /// </summary>
        public DateOnly ContractMonth { get; }

        /// <summary>
        /// return option type
        /// </summary>
        public OptionType OptionType { get; }

        /// <summary>
        /// return option strike price
        /// </summary>
        public double StrikePrice { get; }

        public override string ToString() => $"{Symbol}{ContractMonth:yyyyMMdd}{OptionType.ToString().ToUpperInvariant().Substring(0, 1)}{StrikePrice:F0}";

    }
}
