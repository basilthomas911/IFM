using MessagePack;
using Newtonsoft.Json;

namespace TomasAI.IFM.Shared.EventSourcing;

[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1720", Justification = "Guid is the established serialized result member name.")]
[MessagePackObject]
public record GuidResult([property: Key(0)] Guid Guid);
