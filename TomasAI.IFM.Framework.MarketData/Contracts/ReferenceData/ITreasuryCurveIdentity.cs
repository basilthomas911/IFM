namespace TomasAI.IFM.Framework.MarketData.Contracts;

/// <summary>Identifies acquisition attempts, including failures before a snapshot exists.</summary>
public interface ITreasuryCurveIdentity
{
    string DownloadLogProvider { get; }
}
