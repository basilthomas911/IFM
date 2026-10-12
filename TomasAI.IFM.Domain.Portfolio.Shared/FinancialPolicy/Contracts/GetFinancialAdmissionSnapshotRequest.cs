using MessagePack;
using TomasAI.IFM.Domain.Reference.Shared.StrategyCatalog;
namespace TomasAI.IFM.Domain.Portfolio.Shared.Financial;

[MessagePackObject]
public sealed record GetFinancialAdmissionSnapshotRequest([property: Key(0)] CatalogKey DeploymentKey,
    [property: Key(1)] string UnderlyingId);
