using System;
using System.Collections.Generic;
using System.Text;
using TomasAI.IFM.Shared.Trade;

namespace TomasAI.IFM.Shared.MarketData.ViewModels
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
            Symbol = contractId.Substring(0, 2);
            ContractMonth = new DateOnly(int.Parse(contractId.Substring(2, 4)), int.Parse(contractId.Substring(6, 2)), int.Parse(contractId.Substring(8, 2)));
            OptionType = contractId.Substring(10, 1) == "P" ? OptionType.Put : OptionType.Call;
            StrikePrice = Double.Parse(contractId.Substring(11, 4));
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

        public override string ToString() => $"{Symbol}{ContractMonth:yyyyMMdd}{OptionType.ToString().ToUpper().Substring(0, 1)}{StrikePrice:F0}";
        
    }
}
