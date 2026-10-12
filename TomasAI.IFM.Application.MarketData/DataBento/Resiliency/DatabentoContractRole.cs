using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Framework.MarketData.DataBento;
using TomasAI.IFM.Framework.MarketData.DataBento.TickAggregation.Contracts;

namespace TomasAI.IFM.Application.MarketData.Databento.Resiliency;

[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1008", Justification = "Zero has existing contract semantics; adding an alias would change validation and serialization names.")]
public enum DatabentoContractRole { EsQuarterly = 1, VxFrontMonth = 2, VxSecondMonth = 3 }
