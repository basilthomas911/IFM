using MessagePack;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Portfolio.Shared.Financial;

/// <summary>Implemented by the committed Risk result, never by a capacity request supplied by a caller.</summary>
public interface ICapacityAssessmentCompletedEvent : IEvent
{
    QualifiedCapacityAssessment CapacityAssessment { get; }
}
