using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Application.PredictiveModel
{
    public interface IPredictiveModelService
    {
        Task ExecuteAsync(IEvent e);
    }
}
