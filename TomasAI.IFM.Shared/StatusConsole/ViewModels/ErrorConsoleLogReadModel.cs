using System;
using System.Collections.Generic;
using System.Text;
using MessagePack;

namespace TomasAI.IFM.Shared.StatusConsole.ViewModels;

[MessagePackObject(true)]
public partial class ErrorConsoleLogReadModel
{
    public LogSourceType LogSource { get; set; }
    public DateTime ErrorDate { get; set; }
    public string ErrorMessage { get; set; }
    public int ErrorCode { get; set; }
    public string ErrorData { get; set; }
    public string ExtendedErrorData { get; set; }
    public string Tag { get; set; }
}
