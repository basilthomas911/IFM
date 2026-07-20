using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TomasAI.IFM.Shared.StatusConsole;

namespace TomasAI.IFM.Shared.StatusConsole.ServiceApi;

public interface IStatusConsoleWriter
{
    Task WriteConsoleAsync(LogSourceType logSourceType, string statusMsg);
    Task WriteConsoleAsync(LogSourceType logSourceType, int errorCode, string errorMsg, string dataType="", string data="");
}
