using TomasAI.IFM.Shared.MarketData.ViewModels;

namespace TomasAI.IFM.Shared.MarketDataFeed;

public record struct FuturesTickDataStreamingParameter
        
{
        public int RequestId { get; init; }
        public DateOnly ValueDate { get; init; }
        public FuturesContractV2ReadModel FuturesContract { get; init; } 
        
        public FuturesTickDataStreamingParameter(int requestId, DateOnly valueDate, FuturesContractV2ReadModel futuresContract)
        {
                RequestId = requestId;
                ValueDate = valueDate;
                FuturesContract = futuresContract;
        }

        public FuturesTickDataStreamingParameter(){ }

        public readonly bool IsValid 
                => RequestId > 0 && ValueDate != default && FuturesContract != default; 
}

