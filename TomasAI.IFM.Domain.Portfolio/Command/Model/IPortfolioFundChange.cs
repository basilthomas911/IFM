namespace TomasAI.IFM.Domain.Portfolio.Command.Model;
/// <summary>Proposed immutable business values for a single event application.</summary>
internal interface IPortfolioFundChange { long Revision { get; } Guid CommandId { get; } }
