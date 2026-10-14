


using TomasAI.IFM.Domain.Portfolio.Shared.Financial;





namespace TomasAI.IFM.Domain.Portfolio.Financial.Repository;

public delegate void CapacityAdmissionCalculation(CapacityReservationRequest request, QualifiedCapacityAssessment assessment,
    FinancialBookConfiguration book, decimal availableCash, IReadOnlyList<CapacityUsed> used, DateTime nowUtc);
