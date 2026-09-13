using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Domain;

namespace TomasAI.IFM.Domain.Trade.Model;

internal static class TradeCommandResult
{
    public static ServiceResult<GuidResult> Accepted(Guid commandId) => new ServiceOk<GuidResult>(new GuidResult(commandId));
    public static ServiceResult<GuidResult> Rejected<T>(int errorCode, TradeDecision<T> decision) =>
        new ServiceFailed<GuidResult>(errorCode, $"{decision.Code};{decision.Detail}");
}
