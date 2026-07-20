using System;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Shared.StatusConsole.ViewModels;

public record EventServiceLogReadModel(
    string CommandId,
    DateTime EventDate,
    string EventName,
    string EventData,
    string UserName,
    string ErrorMessage,
    int ErrorCode,
    ErrorType ErrorType,
    string ErrorData)
{
}
