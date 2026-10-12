using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.Storage.EventSourceDb;
using TomasAI.IFM.Application.Storage.TradeDb;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Events;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Persistence;
/// <summary>Exposes the repository's native transaction capability to the Function lifecycle.</summary>
public interface INativeTradeFunctionRepository
{
    bool UsesAtomicPersistence { get; }
}
