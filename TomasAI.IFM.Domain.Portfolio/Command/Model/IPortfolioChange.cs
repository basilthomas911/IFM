namespace TomasAI.IFM.Domain.Portfolio.Command.Model;

/// <summary>Proposed immutable business values for a single event application.</summary>
internal interface IPortfolioChange { long Revision { get; } Guid CommandId { get; } }
