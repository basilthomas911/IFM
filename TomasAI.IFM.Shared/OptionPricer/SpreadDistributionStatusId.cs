using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TomasAI.IFM.Shared.Trade;


namespace TomasAI.IFM.Shared.OptionPricer
{ 
    public class SpreadDistributionStatusId
    {
        readonly int _tradeId;
        readonly TradeType _tradeType;
        readonly TradeStatus _tradeStatus;
        readonly DateOnly _valueDate;
        readonly DateTime _seed;

        public int TradeId => _tradeId;
        public TradeType TradeType => _tradeType;
        public TradeStatus TradeStatus => _tradeStatus;
        public DateOnly ValueDate => _valueDate;

        public SpreadDistributionStatusId(int tradeId, TradeType tradeType, TradeStatus tradeStatus, DateOnly valueDate)
        {
            _tradeId = tradeId;
            _tradeType = tradeType;
            _tradeStatus = tradeStatus;
            _valueDate = new DateOnly(valueDate.Year, valueDate.Month, valueDate.Day);
            _seed = DateTime.Now;
        }

        public override string ToString()
            => $"{_tradeId}|{_tradeType}|{_tradeStatus}|{_valueDate}|{_seed}";

    }
}
