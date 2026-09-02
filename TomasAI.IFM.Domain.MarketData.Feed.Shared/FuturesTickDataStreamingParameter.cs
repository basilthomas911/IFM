using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;

namespace TomasAI.IFM.Domain.MarketData.Feed.Shared;

public record struct FuturesTickDataStreamingParameter
        
{
        public int RequestId { get; init; }
        public DateOnly ValueDate { get; init; }
        public FuturesContractV3ReadModel FuturesContract { get; init; }
        
        public FuturesTickDataStreamingParameter(int requestId, DateOnly valueDate, FuturesContractV3ReadModel futuresContract)
        {
                RequestId = requestId;
                ValueDate = valueDate;
                FuturesContract = futuresContract;
        }

        public FuturesTickDataStreamingParameter(){ }

        public readonly bool IsValid 
                => RequestId > 0 && ValueDate != default && FuturesContract != default; 
}

