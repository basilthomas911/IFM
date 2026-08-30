namespace TomasAI.IFM.Domain.Portfolio.Shared.Validation;

/// <summary>Stable Portfolio error-code allocation. Values are append-only.</summary>
public static class PortfolioErrorCodes
{
    public const int RangeStart = 34000;
    public const int RangeEnd = 34299;
    public const int InvalidIdentity = 34001;
    public const int ValidationFailed = 34002;
    public const int VersionConflict = 34003;
    public const int NotFound = 34004;
    public const int InvalidStateTransition = 34005;
    public const int IdempotencyConflict = 34006;
    public const int ConfigurationMissing = 34007;
    public const int ConfigurationAmbiguous = 34008;
    public const int ConfigurationInactive = 34009;
    public const int ResultMismatch = 34010;
    public const int ResultExpired = 34011;
    public const int SequenceAllocationFailed = 34012;
    public const int ExecutionBoundaryViolation = 34013;
    public const int Unavailable = 34014;

    public static bool IsReserved(int code) => code is >= RangeStart and <= RangeEnd;
}
