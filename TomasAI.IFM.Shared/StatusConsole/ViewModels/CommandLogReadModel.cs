using QLNet;
using System;
using System.Collections.Generic;
using System.Text;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Shared.StatusConsole.ViewModels;

public record CommandLogReadModel(
    string CommandId,
    DateTime CommandDate,
    string CommandName,
    string AggrgeateId,
    string RouteTo,
    string CommandData,
    string UserName,
    string ErrorMessage,
    int ErrorCode,
    ErrorType ErrorType,
    string ErrorData)
{
}
