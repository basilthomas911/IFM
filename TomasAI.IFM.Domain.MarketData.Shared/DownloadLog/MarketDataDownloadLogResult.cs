using MessagePack;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Domain.MarketData.Shared.DownloadLog;

[MessagePackObject]
public sealed record MarketDataDownloadLogResult([property: Key(0)] MarketDataDownloadLogReadModel? Attempt)
{
    [IgnoreMember] public bool Found => Attempt is not null;
}
