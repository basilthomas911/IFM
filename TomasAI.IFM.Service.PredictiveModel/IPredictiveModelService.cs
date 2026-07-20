using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Service.PredictiveModel
{
    public interface IPredictiveModelService
    {
        Task ExecuteAsync(IEvent e);
    }
}
