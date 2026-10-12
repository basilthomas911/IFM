using TomasAI.IFM.Application.MarketData.Databento.Resiliency;
using TomasAI.IFM.Application.MarketData.MarketOutlook;

namespace TomasAI.IFM.Application.MarketData.OperationsHealth;

[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1028", Justification = "The serialized message enum has a fixed underlying width; preserve its wire representation.")]
public enum MarketDataOperationsStatus : byte
{
    Inactive = 0,
    Green = 1,
    Yellow = 2,
    Orange = 3,
    Red = 4
}
