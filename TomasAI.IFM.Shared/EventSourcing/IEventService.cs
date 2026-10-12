namespace TomasAI.IFM.Shared.EventSourcing
{
    public interface IEventService
    {
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1716", Justification = "Preserve the existing public virtual/interface member and named argument contract used by actor implementations.")]
        Task ExecuteAsync(IEvent @event);
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1716", Justification = "Preserve the existing public virtual/interface member and named argument contract used by actor implementations.")]
        Task ExecuteAsync(IEvent @event, IEventService eventService);
    }
}
