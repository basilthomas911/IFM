namespace TomasAI.IFM.Framework.MarketData.DataBento;

public sealed class TimestampReplayCursor
{
    private readonly ulong _savedTimestamp;
    private readonly uint _savedCount;
    private uint _seenAtSavedTimestamp;

    /// <summary>Initializes a new TimestampReplayCursor instance.</summary>
    /// <param name="savedTimestamp">The timestamp of the saved replay cursor.</param>
    /// <param name="savedCount">The number of records already accepted at the saved timestamp.</param>
    public TimestampReplayCursor(ulong savedTimestamp, uint savedCount)
    {
        _savedTimestamp = savedTimestamp;
        _savedCount = savedCount;
    }

    /// <summary>Determines whether the timestamped replay record follows the saved replay cursor.</summary>
    /// <param name="eventTimestamp">The timestamp of the source market event.</param>
    /// <returns>True when the operation succeeds or the requested condition holds; otherwise, false.</returns>
    public bool ShouldAccept(ulong eventTimestamp)
    {
        if (eventTimestamp < _savedTimestamp)
        {
            return false;
        }
        if (eventTimestamp > _savedTimestamp)
        {
            return true;
        }
        _seenAtSavedTimestamp++;
        return _seenAtSavedTimestamp > _savedCount;
    }
}
