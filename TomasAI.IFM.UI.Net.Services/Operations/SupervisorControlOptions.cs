namespace TomasAI.IFM.UI.Net.Services.Operations;

/// <summary>Configures the UI's authorized Supervisor command identity and timeout.</summary>
public sealed record SupervisorControlOptions
{
    public bool Enabled { get; init; }
    public string Requester { get; init; } = string.Empty;
    public TimeSpan Timeout { get; init; } = TimeSpan.FromMinutes(2);
}
