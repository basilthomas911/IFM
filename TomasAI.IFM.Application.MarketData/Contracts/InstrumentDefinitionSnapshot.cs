using TomasAI.IFM.Framework.MarketData.DataBento;
using TomasAI.IFM.Domain.Reference.Shared.ViewModels;

namespace TomasAI.IFM.Application.MarketData.Contracts;

[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1819", Justification = "The published serializer contract requires its existing array shape.")]
public sealed record InstrumentDefinitionSnapshot(Guid Id, DateTime CompletedUtc, long RecordCount, string[] Datasets);
