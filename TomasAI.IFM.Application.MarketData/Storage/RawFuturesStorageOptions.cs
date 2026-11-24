namespace TomasAI.IFM.Application.MarketData.Storage;

public sealed record RawFuturesStorageOptions
{
    public bool Enabled { get; init; }
    public bool RequireSourceHealth { get; init; } = true;
    public string Directory { get; init; } = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "IFM", "RawFuturesStorage");
    public string Scope { get; init; } = "development";
    public string NatsUrl { get; init; } = "nats://127.0.0.1:4222";
    public int BatchCount { get; init; } = 64;
    public int FlushMilliseconds { get; init; } = 10;
    public string Subject => $"ifm.{Scope}.rawfutures.storage.v1";
    public string Stream => "IFM_RAW_FUTURES_" + Scope.Replace('-', '_');
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Directory) || Scope.Length is < 1 or > 64
            || Scope.Any(c => !char.IsAsciiLetterOrDigit(c) && c != '-') || BatchCount is < 1 or > 64
            || FlushMilliseconds is < 1 or > 25) throw new InvalidOperationException("Invalid raw futures storage configuration.");
    }
}
