namespace TomasAI.IFM.Domain.Trade;

/// <summary>Compiled structured diagnostics for trade actor, monitoring and workflow boundaries.</summary>
internal static partial class TradeBoundaryDiagnosticLog
{
    [global::Microsoft.Extensions.Logging.LoggerMessage(EventId = 0, Level = global::Microsoft.Extensions.Logging.LogLevel.Error, Message = "{Component}.{Method} Broker order observation handoff failed for {EventId}, {CommandId}, {EntityId}.")]
    internal static partial void Write1(global::Microsoft.Extensions.Logging.ILogger logger, global::System.Exception? exception, string Component, string Method, global::System.Guid EventId, global::System.Guid CommandId, global::TomasAI.IFM.Domain.Trade.Shared.BrokerOrderId EntityId);

    [global::Microsoft.Extensions.Logging.LoggerMessage(EventId = 0, Level = global::Microsoft.Extensions.Logging.LogLevel.Error, Message = "{Component}.{Method} Futures Trade Plan routing failed for {PositionId}.")]
    internal static partial void Write2(global::Microsoft.Extensions.Logging.ILogger logger, global::System.Exception? exception, string Component, string Method, string PositionId);

    [global::Microsoft.Extensions.Logging.LoggerMessage(EventId = 0, Level = global::Microsoft.Extensions.Logging.LogLevel.Error, Message = "{Component}.{Method} Option trade end-of-day valuation failed for {EventId}, {CommandId}, {EntityId}.")]
    internal static partial void Write3(global::Microsoft.Extensions.Logging.ILogger logger, global::System.Exception? exception, string Component, string Method, global::System.Guid EventId, global::System.Guid CommandId, global::TomasAI.IFM.Domain.Trade.Shared.OptionTradeEntityId EntityId);

    [global::Microsoft.Extensions.Logging.LoggerMessage(EventId = 0, Level = global::Microsoft.Extensions.Logging.LogLevel.Error, Message = "{Component}.{Method} Iron Condor Trade Plan routing failed for {PositionId}.")]
    internal static partial void Write4(global::Microsoft.Extensions.Logging.ILogger logger, global::System.Exception? exception, string Component, string Method, string PositionId);

    [global::Microsoft.Extensions.Logging.LoggerMessage(EventId = 0, Level = global::Microsoft.Extensions.Logging.LogLevel.Error, Message = "{Component}.{Method} Vertical Spread Trade Plan routing failed for {PositionId}.")]
    internal static partial void Write5(global::Microsoft.Extensions.Logging.ILogger logger, global::System.Exception? exception, string Component, string Method, string PositionId);

    [global::Microsoft.Extensions.Logging.LoggerMessage(EventId = 0, Level = global::Microsoft.Extensions.Logging.LogLevel.Error, Message = "{Component}.{Method} Market Condition initialization failed for workflow {WorkflowId}")]
    internal static partial void Write6(global::Microsoft.Extensions.Logging.ILogger logger, global::System.Exception? exception, string Component, string Method, global::TomasAI.IFM.Domain.Trade.Shared.StrategyWorkflowId WorkflowId);

    [global::Microsoft.Extensions.Logging.LoggerMessage(EventId = 0, Level = global::Microsoft.Extensions.Logging.LogLevel.Error, Message = "Atomic trade plan write failed; update dropped without retry. CommandId={CommandId} TradeId={TradeId} ValueDate={ValueDate} EventId={EventId} PlanRevision={PlanRevision}")]
    internal static partial void Write7(global::Microsoft.Extensions.Logging.ILogger logger, global::System.Exception? exception, global::System.Guid CommandId, string TradeId, global::System.DateOnly ValueDate, global::System.Guid? EventId, long PlanRevision);

    [global::Microsoft.Extensions.Logging.LoggerMessage(EventId = 0, Level = global::Microsoft.Extensions.Logging.LogLevel.Error, Message = "Committed plan notification dropped. CommandId={CommandId}")]
    internal static partial void Write8(global::Microsoft.Extensions.Logging.ILogger logger, global::System.Exception? exception, global::System.Guid CommandId);

    [global::Microsoft.Extensions.Logging.LoggerMessage(EventId = 0, Level = global::Microsoft.Extensions.Logging.LogLevel.Error, Message = "Trade plan projection dropped after source commit; MethodName={MethodName} CommandId={CommandId} TradeId={TradeId} ValueDate={ValueDate} EventId={EventId} PlanRevision={PlanRevision}")]
    internal static partial void Write9(global::Microsoft.Extensions.Logging.ILogger logger, global::System.Exception? exception, string MethodName, global::System.Guid CommandId, string TradeId, global::System.DateOnly ValueDate, long? EventId, long PlanRevision);

    [global::Microsoft.Extensions.Logging.LoggerMessage(EventId = 0, Level = global::Microsoft.Extensions.Logging.LogLevel.Warning, Message = "{Component}.{Method} Stale or duplicate workflow terminal command {CommandName} ignored for {WorkflowEntityId} {WorkflowId} revision {WorkflowRevision}")]
    internal static partial void Write10(global::Microsoft.Extensions.Logging.ILogger logger, string Component, string Method, string CommandName, string WorkflowEntityId, global::TomasAI.IFM.Domain.Trade.Shared.StrategyWorkflowId? WorkflowId, long? WorkflowRevision);

    [global::Microsoft.Extensions.Logging.LoggerMessage(EventId = 0, Level = global::Microsoft.Extensions.Logging.LogLevel.Warning, Message = "{Component}.{Method} Stale or duplicate workflow terminal command {CommandName} ignored for {WorkflowEntityId} {WorkflowId} revision {WorkflowRevision}")]
    internal static partial void Write11(global::Microsoft.Extensions.Logging.ILogger logger, string Component, string Method, string CommandName, string WorkflowEntityId, global::TomasAI.IFM.Domain.Trade.Shared.StrategyWorkflowId? WorkflowId, long? WorkflowRevision);

    [global::Microsoft.Extensions.Logging.LoggerMessage(EventId = 0, Level = global::Microsoft.Extensions.Logging.LogLevel.Warning, Message = "{Component}.{Method} Workflow deadline took precedence for {WorkflowEntityId} {WorkflowId} revision {WorkflowRevision}")]
    internal static partial void Write12(global::Microsoft.Extensions.Logging.ILogger logger, string Component, string Method, string WorkflowEntityId, global::TomasAI.IFM.Domain.Trade.Shared.StrategyWorkflowId WorkflowId, long WorkflowRevision);

    [global::Microsoft.Extensions.Logging.LoggerMessage(EventId = 0, Level = global::Microsoft.Extensions.Logging.LogLevel.Information, Message = "{Component}.{Method} Portfolio completed workflow {WorkflowId} with {Status}, {OrderCount} Trade Orders and financial revision {FinancialRevision}")]
    internal static partial void Write13(global::Microsoft.Extensions.Logging.ILogger logger, string Component, string Method, global::TomasAI.IFM.Domain.Trade.Shared.StrategyWorkflowId WorkflowId, global::TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.RiskManagement.PortfolioRiskDecisionStatus Status, int OrderCount, long FinancialRevision);

    [global::Microsoft.Extensions.Logging.LoggerMessage(EventId = 0, Level = global::Microsoft.Extensions.Logging.LogLevel.Warning, Message = "{Component}.{Method} Stale or duplicate workflow terminal command {CommandName} ignored for {WorkflowEntityId} {WorkflowId} revision {WorkflowRevision}")]
    internal static partial void Write14(global::Microsoft.Extensions.Logging.ILogger logger, string Component, string Method, string CommandName, string WorkflowEntityId, global::TomasAI.IFM.Domain.Trade.Shared.StrategyWorkflowId? WorkflowId, long? WorkflowRevision);

    [global::Microsoft.Extensions.Logging.LoggerMessage(EventId = 0, Level = global::Microsoft.Extensions.Logging.LogLevel.Warning, Message = "{Component}.{Method} Workflow deadline took precedence for {WorkflowEntityId} {WorkflowId} revision {WorkflowRevision}")]
    internal static partial void Write15(global::Microsoft.Extensions.Logging.ILogger logger, string Component, string Method, string WorkflowEntityId, global::TomasAI.IFM.Domain.Trade.Shared.StrategyWorkflowId WorkflowId, long WorkflowRevision);

    [global::Microsoft.Extensions.Logging.LoggerMessage(EventId = 0, Level = global::Microsoft.Extensions.Logging.LogLevel.Warning, Message = "{Component}.{Method} Workflow Execute rejected as busy for {WorkflowEntityId} {WorkflowId} revision {WorkflowRevision}")]
    internal static partial void Write16(global::Microsoft.Extensions.Logging.ILogger logger, string Component, string Method, string WorkflowEntityId, global::TomasAI.IFM.Domain.Trade.Shared.StrategyWorkflowId WorkflowId, long WorkflowRevision);

    [global::Microsoft.Extensions.Logging.LoggerMessage(EventId = 0, Level = global::Microsoft.Extensions.Logging.LogLevel.Warning, Message = "{Component}.{Method} Expired workflow {ExpiredWorkflowId} was lazily closed and replaced by {WorkflowId} for {WorkflowEntityId}")]
    internal static partial void Write17(global::Microsoft.Extensions.Logging.ILogger logger, string Component, string Method, global::TomasAI.IFM.Domain.Trade.Shared.StrategyWorkflowId ExpiredWorkflowId, global::TomasAI.IFM.Domain.Trade.Shared.StrategyWorkflowId WorkflowId, string WorkflowEntityId);

    [global::Microsoft.Extensions.Logging.LoggerMessage(EventId = 0, Level = global::Microsoft.Extensions.Logging.LogLevel.Warning, Message = "{Component}.{Method} Fund outcome for workflow {WorkflowId} awaits reconciliation.")]
    internal static partial void Write18(global::Microsoft.Extensions.Logging.ILogger logger, global::System.Exception? exception, string Component, string Method, global::TomasAI.IFM.Domain.Trade.Shared.StrategyWorkflowId WorkflowId);

    [global::Microsoft.Extensions.Logging.LoggerMessage(EventId = 0, Level = global::Microsoft.Extensions.Logging.LogLevel.Warning, Message = "{Component}.{Method} Stale or duplicate workflow terminal command {CommandName} ignored for {WorkflowEntityId} {WorkflowId} revision {WorkflowRevision}")]
    internal static partial void Write19(global::Microsoft.Extensions.Logging.ILogger logger, string Component, string Method, string CommandName, string WorkflowEntityId, global::TomasAI.IFM.Domain.Trade.Shared.StrategyWorkflowId? WorkflowId, long? WorkflowRevision);

    [global::Microsoft.Extensions.Logging.LoggerMessage(EventId = 0, Level = global::Microsoft.Extensions.Logging.LogLevel.Warning, Message = "{Component}.{Method} Workflow deadline took precedence for {WorkflowEntityId} {WorkflowId} revision {WorkflowRevision}")]
    internal static partial void Write20(global::Microsoft.Extensions.Logging.ILogger logger, string Component, string Method, string WorkflowEntityId, global::TomasAI.IFM.Domain.Trade.Shared.StrategyWorkflowId WorkflowId, long WorkflowRevision);

    [global::Microsoft.Extensions.Logging.LoggerMessage(EventId = 0, Level = global::Microsoft.Extensions.Logging.LogLevel.Warning, Message = "{Component}.{Method} Workflow deadline took precedence for {WorkflowEntityId} {WorkflowId} revision {WorkflowRevision}")]
    internal static partial void Write21(global::Microsoft.Extensions.Logging.ILogger logger, string Component, string Method, string WorkflowEntityId, global::TomasAI.IFM.Domain.Trade.Shared.StrategyWorkflowId WorkflowId, long WorkflowRevision);

    [global::Microsoft.Extensions.Logging.LoggerMessage(EventId = 0, Level = global::Microsoft.Extensions.Logging.LogLevel.Error, Message = "{Component}.{Method} Strategy Workflow UI notification failed for {WorkflowId} revision {WorkflowRevision}")]
    internal static partial void Write22(global::Microsoft.Extensions.Logging.ILogger logger, global::System.Exception? exception, string Component, string Method, global::TomasAI.IFM.Domain.Trade.Shared.StrategyWorkflowId WorkflowId, long WorkflowRevision);

    [global::Microsoft.Extensions.Logging.LoggerMessage(EventId = 0, Level = global::Microsoft.Extensions.Logging.LogLevel.Warning, Message = "{Component}.{Method} Stale or duplicate workflow terminal command {CommandName} ignored for {WorkflowEntityId} {WorkflowId} revision {WorkflowRevision}")]
    internal static partial void Write23(global::Microsoft.Extensions.Logging.ILogger logger, string Component, string Method, string CommandName, string WorkflowEntityId, global::TomasAI.IFM.Domain.Trade.Shared.StrategyWorkflowId? WorkflowId, long? WorkflowRevision);

    [global::Microsoft.Extensions.Logging.LoggerMessage(EventId = 0, Level = global::Microsoft.Extensions.Logging.LogLevel.Warning, Message = "{Component}.{Method} Workflow deadline took precedence for {WorkflowEntityId} {WorkflowId} revision {WorkflowRevision}")]
    internal static partial void Write24(global::Microsoft.Extensions.Logging.ILogger logger, string Component, string Method, string WorkflowEntityId, global::TomasAI.IFM.Domain.Trade.Shared.StrategyWorkflowId WorkflowId, long WorkflowRevision);

    [global::Microsoft.Extensions.Logging.LoggerMessage(EventId = 0, Level = global::Microsoft.Extensions.Logging.LogLevel.Warning, Message = "{Component}.{Method} Workflow deadline took precedence for {WorkflowEntityId} {WorkflowId} revision {WorkflowRevision}")]
    internal static partial void Write25(global::Microsoft.Extensions.Logging.ILogger logger, string Component, string Method, string WorkflowEntityId, global::TomasAI.IFM.Domain.Trade.Shared.StrategyWorkflowId WorkflowId, long WorkflowRevision);

    [global::Microsoft.Extensions.Logging.LoggerMessage(EventId = 0, Level = global::Microsoft.Extensions.Logging.LogLevel.Information, Message = "{Component}.{Method} Risk latency observation trace {TraceId} for {WorkflowId}/{InvocationId}: environment {Environment}, candidate age {CandidateAgeMilliseconds} ms, oldest quote age {OldestQuoteAgeMilliseconds} ms, age enforcement {AgeLimitEnforced}")]
    internal static partial void Write26(global::Microsoft.Extensions.Logging.ILogger logger, string Component, string Method, string TraceId, global::TomasAI.IFM.Domain.Trade.Shared.StrategyWorkflowId WorkflowId, global::System.Guid InvocationId, string Environment, double CandidateAgeMilliseconds, double? OldestQuoteAgeMilliseconds, bool AgeLimitEnforced);

    [global::Microsoft.Extensions.Logging.LoggerMessage(EventId = 0, Level = global::Microsoft.Extensions.Logging.LogLevel.Error, Message = "{Component}.{Method} Regime Discovery initialization failed for workflow {WorkflowId}")]
    internal static partial void Write27(global::Microsoft.Extensions.Logging.ILogger logger, global::System.Exception? exception, string Component, string Method, global::TomasAI.IFM.Domain.Trade.Shared.StrategyWorkflowId WorkflowId);

    [global::Microsoft.Extensions.Logging.LoggerMessage(EventId = 0, Level = global::Microsoft.Extensions.Logging.LogLevel.Warning, Message = "{Component}.{Method} Workflow deadline took precedence for {WorkflowEntityId} {WorkflowId} revision {WorkflowRevision}")]
    internal static partial void Write28(global::Microsoft.Extensions.Logging.ILogger logger, string Component, string Method, string WorkflowEntityId, global::TomasAI.IFM.Domain.Trade.Shared.StrategyWorkflowId WorkflowId, long WorkflowRevision);

    [global::Microsoft.Extensions.Logging.LoggerMessage(EventId = 0, Level = global::Microsoft.Extensions.Logging.LogLevel.Error, Message = "{Component}.{Method} Workflow stream is migration-blocked: {StreamId} contains {EventCount} legacy event(s)")]
    internal static partial void Write29(global::Microsoft.Extensions.Logging.ILogger logger, string Component, string Method, string StreamId, int EventCount);

    [global::Microsoft.Extensions.Logging.LoggerMessage(EventId = 0, Level = global::Microsoft.Extensions.Logging.LogLevel.Error, Message = "{Component}.{Method} Workflow stream is migration-blocked: {StreamId} contains unsupported events among {EventCount} event(s)")]
    internal static partial void Write30(global::Microsoft.Extensions.Logging.ILogger logger, string Component, string Method, string StreamId, int EventCount);

    [global::Microsoft.Extensions.Logging.LoggerMessage(EventId = 0, Level = global::Microsoft.Extensions.Logging.LogLevel.Warning, Message = "{Component}.{Method} Financial workflow recovery scan remains pending.")]
    internal static partial void Write31(global::Microsoft.Extensions.Logging.ILogger logger, global::System.Exception? exception, string Component, string Method);

    [global::Microsoft.Extensions.Logging.LoggerMessage(EventId = 0, Level = global::Microsoft.Extensions.Logging.LogLevel.Error, Message = "{Component}.{Method} Trade Selection initialization failed for workflow {WorkflowId}")]
    internal static partial void Write32(global::Microsoft.Extensions.Logging.ILogger logger, global::System.Exception? exception, string Component, string Method, global::TomasAI.IFM.Domain.Trade.Shared.StrategyWorkflowId WorkflowId);

    [global::Microsoft.Extensions.Logging.LoggerMessage(EventId = 0, Level = global::Microsoft.Extensions.Logging.LogLevel.Error, Message = "Monitoring input notification dropped; MethodName={MethodName} PositionId={PositionId} ValueDate={ValueDate}")]
    internal static partial void Write33(global::Microsoft.Extensions.Logging.ILogger logger, global::System.Exception? exception, string MethodName, string PositionId, global::System.DateOnly ValueDate);

    [global::Microsoft.Extensions.Logging.LoggerMessage(EventId = 0, Level = global::Microsoft.Extensions.Logging.LogLevel.Error, Message = "Monitoring observer failed; MethodName={MethodName}")]
    internal static partial void Write34(global::Microsoft.Extensions.Logging.ILogger logger, global::System.Exception? exception, string MethodName);

    [global::Microsoft.Extensions.Logging.LoggerMessage(EventId = 0, Level = global::Microsoft.Extensions.Logging.LogLevel.Warning, Message = "{Component}.{Method} Workflow deadline took precedence for {WorkflowEntityId} {WorkflowId} revision {WorkflowRevision}")]
    internal static partial void Write35(global::Microsoft.Extensions.Logging.ILogger logger, string Component, string Method, string WorkflowEntityId, global::TomasAI.IFM.Domain.Trade.Shared.StrategyWorkflowId WorkflowId, long WorkflowRevision);

    [global::Microsoft.Extensions.Logging.LoggerMessage(EventId = 0, Level = global::Microsoft.Extensions.Logging.LogLevel.Error, Message = "{Component}.{Method} Assessment execution failed at {Stage}, Workflow={WorkflowId}")]
    internal static partial void Write36(global::Microsoft.Extensions.Logging.ILogger logger, global::System.Exception? exception, string Component, string Method, global::TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.MarketCondition.Model.MarketConditionFailureCategory Stage, global::TomasAI.IFM.Domain.Trade.Shared.StrategyWorkflowId WorkflowId);

    [global::Microsoft.Extensions.Logging.LoggerMessage(EventId = 0, Level = global::Microsoft.Extensions.Logging.LogLevel.Error, Message = "{Component}.{Method} Workflow observation is migration-blocked for {WorkflowEntityId} {StreamId}")]
    internal static partial void Write37(global::Microsoft.Extensions.Logging.ILogger logger, global::System.Exception? exception, string Component, string Method, string WorkflowEntityId, string StreamId);

    [global::Microsoft.Extensions.Logging.LoggerMessage(EventId = 0, Level = global::Microsoft.Extensions.Logging.LogLevel.Warning, Message = "{Component}.{Method} Workflow is expired but not closed for {WorkflowEntityId} {WorkflowId} revision {WorkflowRevision}")]
    internal static partial void Write38(global::Microsoft.Extensions.Logging.ILogger logger, string Component, string Method, string WorkflowEntityId, global::TomasAI.IFM.Domain.Trade.Shared.StrategyWorkflowId WorkflowId, long WorkflowRevision);
    [global::Microsoft.Extensions.Logging.LoggerMessage(EventId = 0, Level = global::Microsoft.Extensions.Logging.LogLevel.Error, Message = "Current position notification dropped; MethodName={MethodName} PositionId={PositionId} PositionSequence={PositionSequence} EventId={EventId} CommandId={CommandId} Destination={Destination}")]
    internal static partial void Write39(global::Microsoft.Extensions.Logging.ILogger logger, global::System.Exception? exception, string MethodName, string PositionId, long PositionSequence, long EventId, global::System.Guid CommandId, global::TomasAI.IFM.Shared.EventModelActor.ActorMailboxId Destination);

    [global::Microsoft.Extensions.Logging.LoggerMessage(EventId = 0, Level = global::Microsoft.Extensions.Logging.LogLevel.Error, Message = "{Component}.{Method} Committed business source was quarantined. EventId={EventId} EventName={EventName} Reason={Reason}")]
    internal static partial void Write40(global::Microsoft.Extensions.Logging.ILogger logger, string Component, string Method, long EventId, string EventName, string Reason);

    [global::Microsoft.Extensions.Logging.LoggerMessage(EventId = 0, Level = global::Microsoft.Extensions.Logging.LogLevel.Error, Message = "{Component}.{Method} Committed business source was quarantined. EventId={EventId} EventName={EventName} Reason={Reason}")]
    internal static partial void Write41(global::Microsoft.Extensions.Logging.ILogger logger, string Component, string Method, long EventId, string EventName, string Reason);

    [global::Microsoft.Extensions.Logging.LoggerMessage(EventId = 0, Level = global::Microsoft.Extensions.Logging.LogLevel.Warning, Message = "{Component}.{Method} Restored missing projections for development Portfolio {PortfolioId} from {EventCount} authoritative events through source event {SourceEventId}")]
    internal static partial void Write42(global::Microsoft.Extensions.Logging.ILogger logger, string Component, string Method, int PortfolioId, int EventCount, long SourceEventId);

    [global::Microsoft.Extensions.Logging.LoggerMessage(EventId = 0, Level = global::Microsoft.Extensions.Logging.LogLevel.Error, Message = "{Component}.{Method} Risk history source conflict quarantined. EventId={EventId} WorkflowId={WorkflowId} InvocationId={InvocationId} Revision={Revision}; the immutable stored definition was preserved.")]
    internal static partial void Write43(global::Microsoft.Extensions.Logging.ILogger logger, string Component, string Method, long EventId, global::System.Guid WorkflowId, global::System.Guid InvocationId, long Revision);

    [global::Microsoft.Extensions.Logging.LoggerMessage(EventId = 0, Level = global::Microsoft.Extensions.Logging.LogLevel.Error, Message = "{Component}.{Method} Risk history source conflict quarantined. EventId={EventId} WorkflowId={WorkflowId} InvocationId={InvocationId} Revision={Revision}; the immutable stored definition was preserved.")]
    internal static partial void Write44(global::Microsoft.Extensions.Logging.ILogger logger, string Component, string Method, long EventId, global::System.Guid WorkflowId, global::System.Guid InvocationId, long Revision);

    [global::Microsoft.Extensions.Logging.LoggerMessage(EventId = 0, Level = global::Microsoft.Extensions.Logging.LogLevel.Information, Message = "{Component}.{Method} Development paper Portfolio {PortfolioId} is ready with {FundCount} Funds, {DeploymentCount} published deployments and {Capital} {Currency} separately posted capital")]
    internal static partial void Write45(global::Microsoft.Extensions.Logging.ILogger logger, string Component, string Method, int PortfolioId, int FundCount, int DeploymentCount, decimal Capital, string Currency);

    [global::Microsoft.Extensions.Logging.LoggerMessage(EventId = 0, Level = global::Microsoft.Extensions.Logging.LogLevel.Error, Message = "Monitoring input capture failed; MethodName={MethodName} TradeId={TradeId} ValueDate={ValueDate}")]
    internal static partial void Write46(global::Microsoft.Extensions.Logging.ILogger logger, global::System.Exception? exception, string MethodName, string TradeId, global::System.DateOnly ValueDate);

    [global::Microsoft.Extensions.Logging.LoggerMessage(EventId = 0, Level = global::Microsoft.Extensions.Logging.LogLevel.Error, Message = "OptionCalculator refresh failed; MethodName={MethodName} TradeId={TradeId} ValueDate={ValueDate}")]
    internal static partial void Write47(global::Microsoft.Extensions.Logging.ILogger logger, global::System.Exception? exception, string MethodName, string TradeId, global::System.DateOnly ValueDate);

    [global::Microsoft.Extensions.Logging.LoggerMessage(EventId = 0, Level = global::Microsoft.Extensions.Logging.LogLevel.Error, Message = "{Component}.{Method} Financial workflow stream {StreamId} has an invalid latest snapshot; recovery requires repair.")]
    internal static partial void Write48(global::Microsoft.Extensions.Logging.ILogger logger, string Component, string Method, long StreamId);

    [global::Microsoft.Extensions.Logging.LoggerMessage(EventId = 0, Level = global::Microsoft.Extensions.Logging.LogLevel.Warning, Message = "{Component}.{Method} Financial workflow {WorkflowId} recovery remains pending.")]
    internal static partial void Write49(global::Microsoft.Extensions.Logging.ILogger logger, global::System.Exception? exception, string Component, string Method, global::TomasAI.IFM.Domain.Trade.Shared.StrategyWorkflowId WorkflowId);

    [global::Microsoft.Extensions.Logging.LoggerMessage(EventId = 0, Level = global::Microsoft.Extensions.Logging.LogLevel.Warning, Message = "{Component}.{Method} Risk history unavailable; reading committed invocation {Invocation}")]
    internal static partial void Write50(global::Microsoft.Extensions.Logging.ILogger logger, global::System.Exception? exception, string Component, string Method, global::System.Guid Invocation);

    [global::Microsoft.Extensions.Logging.LoggerMessage(EventId = 0, Level = global::Microsoft.Extensions.Logging.LogLevel.Warning, Message = "{Component}.{Method} Risk committed source unavailable for {Invocation}")]
    internal static partial void Write51(global::Microsoft.Extensions.Logging.ILogger logger, global::System.Exception? exception, string Component, string Method, global::System.Guid Invocation);

    [global::Microsoft.Extensions.Logging.LoggerMessage(EventId = 0, Level = global::Microsoft.Extensions.Logging.LogLevel.Warning, Message = "{Component}.{Method} Risk current workflow unavailable for {Invocation}")]
    internal static partial void Write52(global::Microsoft.Extensions.Logging.ILogger logger, global::System.Exception? exception, string Component, string Method, global::System.Guid Invocation);

    [global::Microsoft.Extensions.Logging.LoggerMessage(EventId = 0, Level = global::Microsoft.Extensions.Logging.LogLevel.Warning, Message = "{Component}.{Method} Current Fund state unavailable for {Invocation}")]
    internal static partial void Write53(global::Microsoft.Extensions.Logging.ILogger logger, global::System.Exception? exception, string Component, string Method, global::System.Guid Invocation);

    [global::Microsoft.Extensions.Logging.LoggerMessage(EventId = 0, Level = global::Microsoft.Extensions.Logging.LogLevel.Warning, Message = "{Component}.{Method} Current financial authority unavailable for {Invocation}")]
    internal static partial void Write54(global::Microsoft.Extensions.Logging.ILogger logger, global::System.Exception? exception, string Component, string Method, global::System.Guid Invocation);

    [global::Microsoft.Extensions.Logging.LoggerMessage(EventId = 0, Level = global::Microsoft.Extensions.Logging.LogLevel.Warning, Message = "{Component}.{Method} Regime terminal notification was not accepted by workflow {WorkflowEntityId} {WorkflowId} source {SourceEventId}")]
    internal static partial void Write55(global::Microsoft.Extensions.Logging.ILogger logger, string Component, string Method, string WorkflowEntityId, global::TomasAI.IFM.Domain.Trade.Shared.StrategyWorkflowId WorkflowId, global::System.Guid SourceEventId);

    [global::Microsoft.Extensions.Logging.LoggerMessage(EventId = 0, Level = global::Microsoft.Extensions.Logging.LogLevel.Warning, Message = "{Component}.{Method} Market Condition terminal notification was not accepted by workflow {WorkflowEntityId} {WorkflowId} source {SourceEventId}")]
    internal static partial void Write56(global::Microsoft.Extensions.Logging.ILogger logger, string Component, string Method, string WorkflowEntityId, global::TomasAI.IFM.Domain.Trade.Shared.StrategyWorkflowId WorkflowId, global::System.Guid SourceEventId);
    [global::Microsoft.Extensions.Logging.LoggerMessage(EventId = 0, Level = global::Microsoft.Extensions.Logging.LogLevel.Warning, Message = "{Component}.{Method} Stale or duplicate workflow terminal command {CommandName} ignored for {WorkflowEntityId} {WorkflowId} revision {WorkflowRevision}")]
    internal static partial void Write57(global::Microsoft.Extensions.Logging.ILogger logger, string Component, string Method, string CommandName, string WorkflowEntityId, global::TomasAI.IFM.Domain.Trade.Shared.StrategyWorkflowId? WorkflowId, long? WorkflowRevision);

    [global::Microsoft.Extensions.Logging.LoggerMessage(EventId = 0, Level = global::Microsoft.Extensions.Logging.LogLevel.Warning, Message = "{Component}.{Method} Stale or duplicate workflow terminal command {CommandName} ignored for {WorkflowEntityId} {WorkflowId} revision {WorkflowRevision}")]
    internal static partial void Write58(global::Microsoft.Extensions.Logging.ILogger logger, string Component, string Method, string CommandName, string WorkflowEntityId, global::TomasAI.IFM.Domain.Trade.Shared.StrategyWorkflowId? WorkflowId, long? WorkflowRevision);

    [global::Microsoft.Extensions.Logging.LoggerMessage(EventId = 0, Level = global::Microsoft.Extensions.Logging.LogLevel.Warning, Message = "{Component}.{Method} Stale or duplicate workflow terminal command {CommandName} ignored for {WorkflowEntityId} {WorkflowId} revision {WorkflowRevision}")]
    internal static partial void Write59(global::Microsoft.Extensions.Logging.ILogger logger, string Component, string Method, string CommandName, string WorkflowEntityId, global::TomasAI.IFM.Domain.Trade.Shared.StrategyWorkflowId? WorkflowId, long? WorkflowRevision);

    [global::Microsoft.Extensions.Logging.LoggerMessage(EventId = 0, Level = global::Microsoft.Extensions.Logging.LogLevel.Warning, Message = "{Component}.{Method} Stale or duplicate workflow terminal command {CommandName} ignored for {WorkflowEntityId} {WorkflowId} revision {WorkflowRevision}")]
    internal static partial void Write60(global::Microsoft.Extensions.Logging.ILogger logger, string Component, string Method, string CommandName, string WorkflowEntityId, global::TomasAI.IFM.Domain.Trade.Shared.StrategyWorkflowId? WorkflowId, long? WorkflowRevision);

    [global::Microsoft.Extensions.Logging.LoggerMessage(EventId = 0, Level = global::Microsoft.Extensions.Logging.LogLevel.Warning, Message = "{Component}.{Method} Stale or duplicate workflow terminal command {CommandName} ignored for {WorkflowEntityId} {WorkflowId} revision {WorkflowRevision}")]
    internal static partial void Write61(global::Microsoft.Extensions.Logging.ILogger logger, string Component, string Method, string CommandName, string WorkflowEntityId, global::TomasAI.IFM.Domain.Trade.Shared.StrategyWorkflowId? WorkflowId, long? WorkflowRevision);

    [global::Microsoft.Extensions.Logging.LoggerMessage(EventId = 0, Level = global::Microsoft.Extensions.Logging.LogLevel.Warning, Message = "{Component}.{Method} Workflow deadline took precedence for {WorkflowEntityId} {WorkflowId} revision {WorkflowRevision}")]
    internal static partial void Write62(global::Microsoft.Extensions.Logging.ILogger logger, string Component, string Method, string WorkflowEntityId, global::TomasAI.IFM.Domain.Trade.Shared.StrategyWorkflowId WorkflowId, long WorkflowRevision);

    [global::Microsoft.Extensions.Logging.LoggerMessage(EventId = 0, Level = global::Microsoft.Extensions.Logging.LogLevel.Warning, Message = "{Component}.{Method} Stale or duplicate workflow terminal command {CommandName} ignored for {WorkflowEntityId} {WorkflowId} revision {WorkflowRevision}")]
    internal static partial void Write63(global::Microsoft.Extensions.Logging.ILogger logger, string Component, string Method, string CommandName, string WorkflowEntityId, global::TomasAI.IFM.Domain.Trade.Shared.StrategyWorkflowId? WorkflowId, long? WorkflowRevision);

    [global::Microsoft.Extensions.Logging.LoggerMessage(EventId = 0, Level = global::Microsoft.Extensions.Logging.LogLevel.Warning, Message = "{Component}.{Method} Workflow deadline took precedence for {WorkflowEntityId} {WorkflowId} revision {WorkflowRevision}")]
    internal static partial void Write64(global::Microsoft.Extensions.Logging.ILogger logger, string Component, string Method, string WorkflowEntityId, global::TomasAI.IFM.Domain.Trade.Shared.StrategyWorkflowId WorkflowId, long WorkflowRevision);

    [global::Microsoft.Extensions.Logging.LoggerMessage(EventId = 0, Level = global::Microsoft.Extensions.Logging.LogLevel.Warning, Message = "{Component}.{Method} Stale or duplicate workflow terminal command {CommandName} ignored for {WorkflowEntityId} {WorkflowId} revision {WorkflowRevision}")]
    internal static partial void Write65(global::Microsoft.Extensions.Logging.ILogger logger, string Component, string Method, string CommandName, string WorkflowEntityId, global::TomasAI.IFM.Domain.Trade.Shared.StrategyWorkflowId? WorkflowId, long? WorkflowRevision);

    [global::Microsoft.Extensions.Logging.LoggerMessage(EventId = 0, Level = global::Microsoft.Extensions.Logging.LogLevel.Warning, Message = "{Component}.{Method} Workflow deadline took precedence for {WorkflowEntityId} {WorkflowId} revision {WorkflowRevision}")]
    internal static partial void Write66(global::Microsoft.Extensions.Logging.ILogger logger, string Component, string Method, string WorkflowEntityId, global::TomasAI.IFM.Domain.Trade.Shared.StrategyWorkflowId WorkflowId, long WorkflowRevision);

    [global::Microsoft.Extensions.Logging.LoggerMessage(EventId = 0, Level = global::Microsoft.Extensions.Logging.LogLevel.Warning, Message = "{Component}.{Method} Stale or duplicate workflow terminal command {CommandName} ignored for {WorkflowEntityId} {WorkflowId} revision {WorkflowRevision}")]
    internal static partial void Write67(global::Microsoft.Extensions.Logging.ILogger logger, string Component, string Method, string CommandName, string WorkflowEntityId, global::TomasAI.IFM.Domain.Trade.Shared.StrategyWorkflowId? WorkflowId, long? WorkflowRevision);

    [global::Microsoft.Extensions.Logging.LoggerMessage(EventId = 0, Level = global::Microsoft.Extensions.Logging.LogLevel.Warning, Message = "{Component}.{Method} Workflow deadline took precedence for {WorkflowEntityId} {WorkflowId} revision {WorkflowRevision}")]
    internal static partial void Write68(global::Microsoft.Extensions.Logging.ILogger logger, string Component, string Method, string WorkflowEntityId, global::TomasAI.IFM.Domain.Trade.Shared.StrategyWorkflowId WorkflowId, long WorkflowRevision);

    [global::Microsoft.Extensions.Logging.LoggerMessage(EventId = 0, Level = global::Microsoft.Extensions.Logging.LogLevel.Warning, Message = "{Component}.{Method} Stale or duplicate workflow terminal command {CommandName} ignored for {WorkflowEntityId} {WorkflowId} revision {WorkflowRevision}")]
    internal static partial void Write69(global::Microsoft.Extensions.Logging.ILogger logger, string Component, string Method, string CommandName, string WorkflowEntityId, global::TomasAI.IFM.Domain.Trade.Shared.StrategyWorkflowId? WorkflowId, long? WorkflowRevision);

    [global::Microsoft.Extensions.Logging.LoggerMessage(EventId = 0, Level = global::Microsoft.Extensions.Logging.LogLevel.Warning, Message = "{Component}.{Method} Workflow deadline took precedence for {WorkflowEntityId} {WorkflowId} revision {WorkflowRevision}")]
    internal static partial void Write70(global::Microsoft.Extensions.Logging.ILogger logger, string Component, string Method, string WorkflowEntityId, global::TomasAI.IFM.Domain.Trade.Shared.StrategyWorkflowId WorkflowId, long WorkflowRevision);

    [global::Microsoft.Extensions.Logging.LoggerMessage(EventId = 0, Level = global::Microsoft.Extensions.Logging.LogLevel.Warning, Message = "{Component}.{Method} Workflow deadline took precedence for {WorkflowEntityId} {WorkflowId} revision {WorkflowRevision}")]
    internal static partial void Write71(global::Microsoft.Extensions.Logging.ILogger logger, string Component, string Method, string WorkflowEntityId, global::TomasAI.IFM.Domain.Trade.Shared.StrategyWorkflowId WorkflowId, long WorkflowRevision);

    [global::Microsoft.Extensions.Logging.LoggerMessage(EventId = 0, Level = global::Microsoft.Extensions.Logging.LogLevel.Warning, Message = "{Component}.{Method} Stale or duplicate workflow terminal command {CommandName} ignored for {WorkflowEntityId} {WorkflowId} revision {WorkflowRevision}")]
    internal static partial void Write72(global::Microsoft.Extensions.Logging.ILogger logger, string Component, string Method, string CommandName, string WorkflowEntityId, global::TomasAI.IFM.Domain.Trade.Shared.StrategyWorkflowId? WorkflowId, long? WorkflowRevision);
    [global::Microsoft.Extensions.Logging.LoggerMessage(EventId = 0, Level = global::Microsoft.Extensions.Logging.LogLevel.Error, Message = "{Component}.{Method} Failed to report an event exception for {ActorName}.")]
    internal static partial void EventExceptionReportFailed(global::Microsoft.Extensions.Logging.ILogger logger, global::System.Exception exception, string Component, string Method, string ActorName);
    [global::Microsoft.Extensions.Logging.LoggerMessage(EventId = 0, Level = global::Microsoft.Extensions.Logging.LogLevel.Error, Message = "{Component}.{Method} Late selection reservation requires redispatch reconciliation for workflow {WorkflowId}")]
    internal static partial void LateReservationFailed(global::Microsoft.Extensions.Logging.ILogger logger, global::System.Exception exception, string Component, string Method, global::TomasAI.IFM.Domain.Trade.Shared.StrategyWorkflowId WorkflowId);
}
