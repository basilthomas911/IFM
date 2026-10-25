using Microsoft.Extensions.Logging;
using System.Threading.Channels;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Shared.EventChannel;

/// <summary>
///  event channel logger type
/// </summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1724", Justification = "EventChannel is the existing logging-category marker in the EventChannel namespace; preserve ILogger category identity.")]
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "MA0049", Justification = "EventChannel is the existing logging-category marker in the EventChannel namespace; preserve ILogger category identity.")]
public class EventChannel { }
