namespace TomasAI.IFM.Domain.MarketData.Feed.Shared;

/// <summary>
/// Maintains the one-to-one mapping between broker request identifiers and contract identifiers.
/// </summary>
public sealed class StreamIdCollection : IStreamIdCollection
{
    readonly object _sync = new();
    readonly Dictionary<int, string> _contractsByStreamId = [];
    readonly Dictionary<string, int> _streamIdsByContract = new(StringComparer.Ordinal);
    int _nextStreamId;

    public int this[string contractId]
    {
        get
        {
            if (string.IsNullOrWhiteSpace(contractId))
                return -1;

            lock (_sync)
                return _streamIdsByContract.TryGetValue(contractId, out var streamId) ? streamId : -1;
        }
    }

    public int Count
    {
        get
        {
            lock (_sync)
                return _contractsByStreamId.Count;
        }
    }

    public int Add(string contractId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(contractId);

        lock (_sync)
        {
            if (_streamIdsByContract.TryGetValue(contractId, out var existingStreamId))
                return existingStreamId;

            int streamId;
            do
            {
                streamId = checked(++_nextStreamId);
            }
            while (_contractsByStreamId.ContainsKey(streamId));

            _contractsByStreamId.Add(streamId, contractId);
            _streamIdsByContract.Add(contractId, streamId);
            return streamId;
        }
    }

    public void Clear()
    {
        lock (_sync)
        {
            _contractsByStreamId.Clear();
            _streamIdsByContract.Clear();
            _nextStreamId = 0;
        }
    }

    public bool Exists(int streamId)
    {
        lock (_sync)
            return _contractsByStreamId.ContainsKey(streamId);
    }

    public void Remove(int streamId)
    {
        lock (_sync)
        {
            if (!_contractsByStreamId.Remove(streamId, out var contractId))
                return;

            _streamIdsByContract.Remove(contractId);
        }
    }
}
