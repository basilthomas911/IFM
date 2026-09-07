using TomasAI.IFM.Domain.MarketData.Shared.DownloadLog;
using TomasAI.IFM.Domain.MarketData.DownloadLog.Command.State;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.MarketData.DownloadLog.Command;

public static class InsertMarketDataDownloadLog
{
    public static ServiceResult<GuidResult> Execute(this InsertMarketDataDownloadLogCommand command, DownloadLogCommandState state)
    {
        if (state.VerifyDuplicate(command)) return new ServiceOk<GuidResult>(new GuidResult(command.CommandId));
        return command.UpdateResult(() => state.Update(new MarketDataDownloadLogInsertedEvent
        {
            CommandId = command.CommandId, EntityId = command.EntityId,
            Subject = new ActorSubject(ActorType.Event, MarketDataDownloadLogInsertedEvent.Actor,
                MarketDataDownloadLogInsertedEvent.Verb, command.EntityId.Format()),
            Outcome = command.Outcome, PayloadSha256 = command.PayloadSha256
        }, command));
    }
}
