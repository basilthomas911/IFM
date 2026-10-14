


using TomasAI.IFM.Domain.Portfolio.Shared.Financial;





namespace TomasAI.IFM.Domain.Portfolio.Financial.Repository;
public delegate CapacityTransition CapacityLifecycleCalculation(ReservationSnapshot current, CapacityLifecycleRequest change,
    bool consumption, DateTime nowUtc);
