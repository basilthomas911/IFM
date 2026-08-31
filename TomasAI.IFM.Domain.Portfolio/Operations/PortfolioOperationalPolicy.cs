namespace TomasAI.IFM.Domain.Portfolio.Operations;

public enum PortfolioOperation
{
    Read = 0,
    AdministerPortfolio = 1,
    AdministerFund = 2,
    DelegateAllocation = 3,
    DelegateRiskEnvelope = 4,
    AssignTemplate = 5,
    ReserveComposition = 6,
    RecordCompositionResult = 7,
    RecordRiskResult = 8,
}

/// <summary>Bounded authorization map. Execution authority is intentionally not representable.</summary>
public static class PortfolioOperationalPolicy
{
    public const string ReaderRole = "PortfolioReader";
    public const string AdministratorRole = "PortfolioAdministrator";
    public const string WorkflowRole = "StrategyWorkflow";

    public static bool IsAuthorized(PortfolioOperation operation, IReadOnlySet<string> roles) => operation switch
    {
        PortfolioOperation.Read => roles.Contains(ReaderRole) || roles.Contains(AdministratorRole) || roles.Contains(WorkflowRole),
        PortfolioOperation.AdministerPortfolio or PortfolioOperation.AdministerFund or
            PortfolioOperation.DelegateAllocation or PortfolioOperation.DelegateRiskEnvelope or
            PortfolioOperation.AssignTemplate => roles.Contains(AdministratorRole),
        PortfolioOperation.ReserveComposition or PortfolioOperation.RecordCompositionResult or
            PortfolioOperation.RecordRiskResult => roles.Contains(WorkflowRole),
        _ => false,
    };

    public static readonly string[] RequiredTraceFields =
    [
        "portfolio.id", "portfolio.version", "fund.id", "fund.version", "workflow.id",
        "command.id", "correlation.id", "causation.id", "reason.code"
    ];

    public static readonly string[] BoundedMetricNames =
    [
        "portfolio.command.outcomes", "portfolio.resolution.failures", "portfolio.projection.lag",
        "portfolio.projection.failures", "portfolio.reservation.duration", "portfolio.reservation.replays",
        "portfolio.query.duration", "portfolio.authorization.checks"
    ];

    public static string RedactHash(string? sha256) =>
        string.IsNullOrWhiteSpace(sha256) ? string.Empty : sha256.Length <= 12 ? "[redacted]" : $"{sha256[..8]}…{sha256[^4..]}";
}
