namespace TomasAI.IFM.Framework.SequenceId.Postgres;

/// <summary>
/// Provides functionality to generate unique sequence IDs using a PostgreSQL-backed sequence mechanism.
/// </summary>
/// <remarks>This class retrieves and manages sequence IDs from a PostgreSQL database through the provided <see
/// cref="ISequenceIdDbContext"/>. It maintains an internal cache of current and maximum sequence IDs for each sequence
/// type to minimize database calls.</remarks>
/// <param name="sequenceIdDb"></param>
public class PostgresSequenceIdGenerator(ISequenceIdDbContext sequenceIdDb) : ISequenceIdGenerator
{
    static readonly Dictionary<SequenceName, long> _curSequenceIdMap = [];
    static readonly Dictionary<SequenceName, long> _maxSequenceIdMap = [];
    readonly ISequenceIdDbContext _sequenceIdDb = sequenceIdDb;

    /// <summary>
    /// Get current sequence id
    /// </summary>
    /// <param name="sequenceIdType"></param>
    /// <returns></returns>
    public async Task<long> GetSequenceIdAsync(SequenceName sequenceIdType)
    {
        if (!_curSequenceIdMap.ContainsKey(sequenceIdType))
            _curSequenceIdMap.Add(sequenceIdType, await _sequenceIdDb.GetNextSequenceIdAsync(sequenceIdType));
        if (!_maxSequenceIdMap.ContainsKey(sequenceIdType))
            _maxSequenceIdMap.Add(sequenceIdType, await _sequenceIdDb.GetNextSequenceIdAsync(sequenceIdType));
        var curSequenceId = _curSequenceIdMap[sequenceIdType];
        Interlocked.Increment(ref curSequenceId);
        if (curSequenceId > _maxSequenceIdMap[sequenceIdType])
            _maxSequenceIdMap[sequenceIdType] = await _sequenceIdDb.GetNextSequenceIdAsync(sequenceIdType);
        _curSequenceIdMap[sequenceIdType] = curSequenceId;
        return curSequenceId;
    }

}
