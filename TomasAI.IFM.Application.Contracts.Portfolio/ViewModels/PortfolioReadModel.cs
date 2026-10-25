using MessagePack;
using TomasAI.IFM.Domain.Portfolio.Shared.Contracts;

namespace TomasAI.IFM.Domain.Portfolio.Shared.ViewModels;

[MessagePackObject(AllowPrivate = true)]
public sealed record PortfolioReadModel
{
    [Key(0)] public int PortfolioId { get; init; }
    // MessagePack key 1 was PortfolioCode in the pre-v1 contract. It is deliberately
    // reserved so removing that legacy business field never renumbers the wire shape.
    [Key(2)] public string Name { get; init; } = string.Empty;
    [Key(3)] public long PortfolioVersion { get; init; }
    [Key(4)] public int SchemaVersion { get; init; } = 2;
    [Key(5)] public string BaseCurrency { get; init; } = "USD";
    [Key(6)] public PortfolioOperatingState OperatingState { get; init; }
    [Key(7)] public DateTime EffectiveFromUtc { get; init; }
    [Key(8)] public DateTime? EffectiveUntilUtc { get; init; }
    [Key(9)] public int ActivePolicyId { get; init; }
    [Key(10)] public long ActivePolicyVersion { get; init; }
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1819", Justification = "Preserve the existing serialized array payload and public financial model contract.")]
    [Key(11)] public string[] BrokerAccountRefs { get; init; } = [];
    [Key(12)] public DateTime CreatedOnUtc { get; init; }
    [Key(13)] public string CreatedBy { get; init; } = string.Empty;
    [Key(14)] public DateTime? SupersededOnUtc { get; init; }
    [Key(15)] public string SupersededBy { get; init; } = string.Empty;

    public IReadOnlyList<string> Validate(bool requireActivePolicy = true)
    {
        List<string> errors = [];
        if (PortfolioId <= 0) errors.Add("PortfolioId must be greater than zero.");
        if (string.IsNullOrWhiteSpace(Name)) errors.Add("Name is required.");
        if (PortfolioVersion <= 0) errors.Add("PortfolioVersion must be greater than zero.");
        if (SchemaVersion <= 0) errors.Add("SchemaVersion must be greater than zero.");
        if (string.IsNullOrWhiteSpace(BaseCurrency)) errors.Add("BaseCurrency is required.");
        if (EffectiveFromUtc.Kind != DateTimeKind.Utc) errors.Add("EffectiveFromUtc must be UTC.");
        if (EffectiveUntilUtc is { } until && (until.Kind != DateTimeKind.Utc || until <= EffectiveFromUtc))
            errors.Add("EffectiveUntilUtc must be UTC and after EffectiveFromUtc.");
        if (CreatedOnUtc.Kind != DateTimeKind.Utc) errors.Add("CreatedOnUtc must be UTC.");
        if (string.IsNullOrWhiteSpace(CreatedBy)) errors.Add("CreatedBy is required.");
        if (OperatingState == PortfolioOperatingState.Unknown) errors.Add("OperatingState is required.");
        if (requireActivePolicy && OperatingState == PortfolioOperatingState.Active)
        {
            if (ActivePolicyId <= 0) errors.Add("An active Portfolio requires a positive ActivePolicyId.");
            if (ActivePolicyVersion <= 0) errors.Add("An active Portfolio requires a positive ActivePolicyVersion.");
        }
        if (BrokerAccountRefs.Any(string.IsNullOrWhiteSpace)) errors.Add("BrokerAccountRefs cannot contain blanks.");
        return errors;
    }

    public PortfolioReadModel DefensiveCopy() => this with { BrokerAccountRefs = [.. BrokerAccountRefs] };
}
