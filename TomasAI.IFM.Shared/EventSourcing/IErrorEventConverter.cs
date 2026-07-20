using System;
using System.Collections.Generic;
using System.Text;

namespace TomasAI.IFM.Shared.EventSourcing;

/// <summary>
/// Defines a mechanism for converting command execution results and exceptions into error event representations.
/// </summary>
/// <remarks>Implementations of this interface are responsible for translating command failures or exceptions into
/// a standardized error event format. This is typically used to enable consistent error reporting or logging across
/// different command types.</remarks>
public interface IErrorEventConverter
{
    IErrorEvent ToErrorEvent(ICommand command, Exception? ex = null);
}
