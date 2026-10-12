using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.TradeSelection;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Commands;
using System.Collections.Immutable;
using MessagePack;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Events;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Events;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Identity;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Model;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Command.State;
/// <summary>
/// Represents the bounded committed instruction required to dispatch or redispatch the current strategy pipeline.
/// </summary>
/// <param name = "Stage">Pipeline stage selected by the Workflow Command actor.</param>
/// <param name = "ActorType">Target pipeline actor type.</param>
/// <param name = "ActorName">Target pipeline actor name.</param>
/// <param name = "BoundedContext">Target pipeline bounded context.</param>
/// <param name = "CommandId">Deterministic pipeline command identity.</param>
/// <param name = "WorkflowState">Immutable workflow input snapshot.</param>
/// <param name = "TriggerEvent">Original ITI trigger retained for pipeline input.</param>
/// <param name = "RequestedAtUtc">UTC pipeline request timestamp.</param>
/// <param name = "ExpectedCompletionAtUtc">Optional UTC stage deadline.</param>
internal sealed record IntrinsicTimeStrategyWorkflowDispatchInstruction(StrategyWorkflowStage Stage, ActorType ActorType, string ActorName, BoundedContextName BoundedContext, Guid CommandId, IntrinsicTimeStrategyWorkflowState WorkflowState, FuturesItiSignalGeneratedEvent TriggerEvent, DateTime RequestedAtUtc, DateTime? ExpectedCompletionAtUtc);
