using System;
using System.Collections.Generic;
using System.Linq;
using TomasAI.IFM.Shared.MarketDataFeed.ViewModels;

namespace TomasAI.IFM.Shared.MarketDataFeed
{
    public class FuturesOptionQuoteContracts
    {
        readonly int _quoteId;
        Dictionary<int, FuturesOptionQuoteReadModel> _quoteContracts;

        public FuturesOptionQuoteContracts(int quoteId) 
        {
            _quoteId = quoteId; 
            _quoteContracts = new();
        }

        public int QuoteId => _quoteId;
        public Dictionary<int, FuturesOptionQuoteReadModel> QuoteContracts => _quoteContracts;

        public void Add(int requestId, Guid streamId,  string contractId, string createdBy, DateTime createdOn) 
        { 
            if (!_quoteContracts.ContainsKey(requestId))
                _quoteContracts.Add(requestId, new FuturesOptionQuoteReadModel(_quoteId, contractId,  requestId,  createdBy, createdOn));
        }

        public FuturesOptionQuoteReadModel[] ToArray()
            => _quoteContracts.Values.ToArray();
    }

}
