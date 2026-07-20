using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TomasAI.IFM.Shared.MarketDataFeed
{
    public class FuturesOptionTickDataPrimaryKey
    {
        public static FuturesOptionTickDataPrimaryKey Create(string symbol, string contractMonth, double strikePrice, string optionType)
            => new FuturesOptionTickDataPrimaryKey(symbol, contractMonth, strikePrice, optionType);

        public FuturesOptionTickDataPrimaryKey(
            string symbol,
            string contractMonth,
            double strikePrice,
            string optionType)
        {
            Symbol = symbol;
            ContractMonth = contractMonth;
            StrikePrice = strikePrice;
            OptionType = optionType;
        }

        public string Symbol { get; }
        public string ContractMonth { get; }
        public double StrikePrice { get; }
        public string OptionType { get; }

        public override string ToString() => $"{Symbol}{ContractMonth}{OptionType.Substring(0, 1)}{StrikePrice:###0}";
    }
}
