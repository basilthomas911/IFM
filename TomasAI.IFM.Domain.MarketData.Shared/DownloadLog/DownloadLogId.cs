using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using MessagePack;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Domain.MarketData.Shared.DownloadLog;

[MessagePackObject]
public sealed record DownloadLogId([property: Key(0)] Guid ImportCommandId) : IActorEntityId
{
    public string Format() => ImportCommandId.ToString("N");
}
