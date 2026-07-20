using System;
using System.Collections.Generic;
using System.Text;
using MessagePack;

namespace TomasAI.IFM.Shared.StatusConsole.ViewModels;

[MessagePackObject(true)]
public record StatusConsoleLogReadModel(
    DateTime StatusDate,
    int StatusCode,
    LogSourceType Source,
    string Message,
    string DataType="",
    string Data="")
{
}
