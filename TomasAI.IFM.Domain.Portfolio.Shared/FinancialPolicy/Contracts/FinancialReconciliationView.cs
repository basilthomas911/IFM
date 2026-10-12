using MessagePack;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Portfolio.Shared.Financial;
[MessagePackObject]
public sealed record FinancialReconciliationView([property: Key(0)] Guid ReconciliationId, [property: Key(1)] string SourceCut,
    [property: Key(2)] string Status, [property: Key(3)] string ContentHash, [property: Key(4)] string DifferencesJson);
