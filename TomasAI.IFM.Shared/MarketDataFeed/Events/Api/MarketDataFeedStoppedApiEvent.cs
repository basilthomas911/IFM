using System;
using System.Collections.Generic;
using System.Text;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Shared.MarketDataFeed.Events.Api
{
    public record MarketDataFeedStoppedApiEvent : ServiceApiEvent

    {
        public const int ErrorCode = 5011;

        public DateTime StoppedOn { get; init; }
        public string StoppedBy { get; init; }

        public ICompleteEvent ToCompletedEvent() => new MarketDataFeedStoppedCompleteApiEvent
        {
            CommandId = this.CommandId,
            StoppedOn = this.StoppedOn,
            StoppedBy = this.StoppedBy
        };

        public IErrorEvent ToFailedEvent(Exception ex) => new MarketDataFeedStoppedFailApiEvent
        {
            CommandId = this.CommandId,
            ErrorMessage = ex.Message,
            ErrorType = ErrorType.Command,
            ErrorCode = ErrorCode
        };
    }

    public record MarketDataFeedStoppedCompleteApiEvent : CompleteEvent
    {
        public DateTime StoppedOn { get; init; }
        public string StoppedBy { get; init; }
    }

    public record MarketDataFeedStoppedFailApiEvent : ErrorEvent
    {
    }
}
