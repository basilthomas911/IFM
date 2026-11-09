namespace TomasAI.IFM.Shared.EventModelActor;

/// <summary>Independently enables the first five committed-state residency pilots.</summary>
public sealed class ResidentActorPilotOptions
{
    public const string SectionName = "ResidentActorPilots";

    public ResidentActorPolicy Vwap { get; set; } = new();
    public ResidentActorPolicy MarketOutlook { get; set; } = new();
    public ResidentActorPolicy FuturesTradePlan { get; set; } = new();
    public ResidentActorPolicy VerticalSpreadTradePlan { get; set; } = new();
    public ResidentActorPolicy IronCondorTradePlan { get; set; } = new();

    /// <summary>Rejects invalid bounds before actors are started.</summary>
    public ResidentActorPilotOptions Validate()
    {
        Vwap.Validate();
        MarketOutlook.Validate();
        FuturesTradePlan.Validate();
        VerticalSpreadTradePlan.Validate();
        IronCondorTradePlan.Validate();
        return this;
    }
}

/// <summary>Bounds one actor's disposable, committed-only state cache.</summary>
public sealed class ResidentActorPolicy
{
    public bool Enabled { get; set; }
    public int MaximumResidentStreams { get; set; } = 256;
    public TimeSpan IdleTimeout { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>Validates admission and idle-retention bounds.</summary>
    public ResidentActorPolicy Validate()
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(MaximumResidentStreams);
        if (IdleTimeout <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(IdleTimeout));
        return this;
    }
}
