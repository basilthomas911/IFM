using System;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Shared.StatusConsole.ViewModels;

public record QueryLogReadModel(
    string CommandId,
    DateTime QueryDate,
    string QueryName,
    string QueryData,
    string UserName,
    string ErrorMessage,
    int ErrorCode,
    ErrorType ErrorType,
    string ErrorData)
{
}
