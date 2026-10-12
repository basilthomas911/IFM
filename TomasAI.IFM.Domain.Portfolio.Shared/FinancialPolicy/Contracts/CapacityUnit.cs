using MessagePack;
using TomasAI.IFM.Domain.Reference.Shared.StrategyCatalog;

namespace TomasAI.IFM.Domain.Portfolio.Shared.Financial;
public enum CapacityUnit { Undefined = 0, Usd = 1, Contracts = 2, Positions = 3, NormalizedDelta = 4, NormalizedGamma = 5, NormalizedVega = 6 }
