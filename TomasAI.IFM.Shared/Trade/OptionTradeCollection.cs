using System;
using System.Collections.Generic;
using System.Collections;
using System.Linq;
using System.Text;
using TomasAI.IFM.Shared.Trade.ViewModels;

namespace TomasAI.IFM.Shared.Trade
{
    public class OptionTradeCollection : IOptionTradeCollection
    {
        private List<OptionTradeReadModel> _optionTrades;
        private OptionTradeReadModel _primaryTrade;

        public OptionTradeCollection() => _optionTrades = new List<OptionTradeReadModel>();

        public OptionTradeCollection(IEnumerable<OptionTradeReadModel> optionTrades) => _optionTrades = new List<OptionTradeReadModel>(optionTrades);

        public int Count => _optionTrades.Count;

        public OptionTradeReadModel this[OptionTradeEntityId key]
            => _optionTrades
                .Where(e => e.OrderId == key.OrderId
                    && e.TradeId == key.TradeId)
                .SingleOrDefault();

        public OptionTradeReadModel PrimaryTrade => GetPrimaryTrade();
        
        public bool Exists(OptionTradeEntityId key) => _optionTrades.Exists(e => e.OrderId == key.OrderId && e.TradeId == key.TradeId);

        public void Add(OptionTradeReadModel optionTrade)
        {
            _optionTrades.Add(optionTrade);
            _primaryTrade = null;
        }

        public void Clear()
        {
            _optionTrades.Clear();
            _primaryTrade = null;
        }

        public IEnumerator<OptionTradeReadModel> GetEnumerator()  => _optionTrades.GetEnumerator();

        public bool Remove(OptionTradeReadModel optionTrade)
        {
            var removed = _optionTrades.Remove(optionTrade);
            _primaryTrade = null;
            return removed;
        }

        IEnumerator IEnumerable.GetEnumerator() => _optionTrades.GetEnumerator();

        private OptionTradeReadModel GetPrimaryTrade()
        {
            _primaryTrade = _primaryTrade ?? _optionTrades.Where(e => e.IsPrimaryTrade).SingleOrDefault();
            return _primaryTrade;
        }
    }
}
