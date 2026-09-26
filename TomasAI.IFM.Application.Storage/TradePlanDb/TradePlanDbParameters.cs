using TomasAI.IFM.Framework.Storage;

namespace TomasAI.IFM.Application.Storage.TradePlanDb;

internal readonly record struct ValueDateParameter(DateOnly ValueDate) : IBindValue
{
    public object Bind() => new object?[] { ValueDate };
}

internal readonly record struct GetExactTradePlan(
    int PortfolioId,
    int FundId,
    int OrderId,
    int TradeId,
    Guid PositionId,
    DateOnly ValueDate,
    long PlanRevision) : IBindValue
{
    public object Bind() =>
        new object?[] { PortfolioId, FundId, OrderId, TradeId, PositionId, ValueDate, PlanRevision };
}

internal readonly record struct GetTradePlan(
    int PortfolioId,
    int FundId,
    int OrderId,
    int TradeId,
    Guid PositionId,
    DateOnly ValueDate) : IBindValue
{
    public object Bind() =>
        new object?[] { PortfolioId, FundId, OrderId, TradeId, PositionId, ValueDate };
}

internal readonly record struct InsertTradePlan(
    int PortfolioId,
    int FundId,
    int OrderId,
    int TradeId,
    Guid PositionId,
    DateOnly ValueDate,
    long PlanRevision,
    DateTime CalculatedAtUtc,
    string State,
    bool RequiresExit,
    string ContentHash,
    byte[] Payload) : IBindValue
{
    public object Bind() =>
        new object?[]
        {
            PortfolioId,
            FundId,
            OrderId,
            TradeId,
            PositionId,
            ValueDate,
            PlanRevision,
            CalculatedAtUtc,
            State,
            RequiresExit,
            ContentHash,
            Payload
        };
}

internal readonly record struct InsertTradePlanActivity(
    DateOnly ValueDate,
    DateTime CalculatedAtUtc,
    int PortfolioId,
    int FundId,
    int OrderId,
    int TradeId,
    Guid PositionId,
    sbyte StrategyKind,
    long PlanRevision,
    sbyte State,
    bool RequiresExit,
    string ContentHash,
    byte[] Payload) : IBindValue
{
    public object Bind() =>
        new object?[]
        {
            ValueDate,
            CalculatedAtUtc,
            PortfolioId,
            FundId,
            OrderId,
            TradeId,
            PositionId,
            StrategyKind,
            PlanRevision,
            State,
            RequiresExit,
            ContentHash,
            Payload
        };
}

internal readonly record struct GetExactExitPositionWorkflow(
    int PortfolioId,
    int FundId,
    int OrderId,
    int TradeId,
    Guid PositionId,
    DateOnly ValueDate,
    DateTime UpdatedAtUtc,
    Guid ExitDecisionId,
    long StageRevision) : IBindValue
{
    public object Bind() =>
        new object?[]
        {
            PortfolioId,
            FundId,
            OrderId,
            TradeId,
            PositionId,
            ValueDate,
            UpdatedAtUtc,
            ExitDecisionId,
            StageRevision
        };
}

internal readonly record struct GetExitPositionWorkflow(
    int PortfolioId,
    int FundId,
    int OrderId,
    int TradeId,
    Guid PositionId,
    DateOnly ValueDate) : IBindValue
{
    public object Bind() =>
        new object?[] { PortfolioId, FundId, OrderId, TradeId, PositionId, ValueDate };
}

internal readonly record struct InsertExitPositionWorkflow(
    int PortfolioId,
    int FundId,
    int OrderId,
    int TradeId,
    Guid PositionId,
    DateOnly ValueDate,
    DateTime UpdatedAtUtc,
    Guid ExitDecisionId,
    long StageRevision,
    sbyte StrategyKind,
    sbyte State,
    Guid SourcePlanEventId,
    byte[] Payload) : IBindValue
{
    public object Bind() =>
        new object?[]
        {
            PortfolioId,
            FundId,
            OrderId,
            TradeId,
            PositionId,
            ValueDate,
            UpdatedAtUtc,
            ExitDecisionId,
            StageRevision,
            StrategyKind,
            State,
            SourcePlanEventId,
            Payload
        };
}
