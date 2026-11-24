namespace TomasAI.IFM.Shared.EventModelActor;

/// <summary>Independently enables committed-state residency for qualified actor paths.</summary>
public sealed class ResidentActorPilotOptions
{
    public const string SectionName = "ResidentActorPilots";

    public ResidentActorPolicy Vwap { get; set; } = new();
    public ResidentActorPolicy MarketOutlook { get; set; } = new();
    public ResidentActorPolicy FuturesTradePlan { get; set; } = new();
    public ResidentActorPolicy VerticalSpreadTradePlan { get; set; } = new();
    public ResidentActorPolicy IronCondorTradePlan { get; set; } = new();
    public ResidentActorPolicy FuturesItiSignal { get; set; } = new();
    public bool FuturesItiOrderedIntakeEnabled { get; set; }
    public string FuturesItiJournalDirectory { get; set; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "IFM", "FuturesItiJournal");

    /// <summary>Rejects invalid bounds before actors are started.</summary>
    public ResidentActorPilotOptions Validate()
    {
        Vwap.Validate();
        MarketOutlook.Validate();
        FuturesTradePlan.Validate();
        VerticalSpreadTradePlan.Validate();
        IronCondorTradePlan.Validate();
        FuturesItiSignal.Validate();
        if (FuturesItiOrderedIntakeEnabled && !FuturesItiSignal.Enabled)
            throw new InvalidOperationException("Ordered ITI intake requires resident state.");
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
