using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using TomasAI.IFM.Domain.MarketData.Feed.Shared;

namespace TomasAI.IFM.Domain.MarketData.Feed.Benchmarks;

[MemoryDiagnoser]
[ThreadingDiagnoser]
[SimpleJob(RuntimeMoniker.Net10_0, warmupCount: 3, iterationCount: 8)]
public class StreamIdCollectionBenchmarks
{
    readonly BeforeStreamIdCollection _before = new();
    readonly StreamIdCollection _after = new();
    string _target = string.Empty;

    [Params(128, 4096, 32768)]
    public int Count { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        _before.Clear();
        _after.Clear();
        for (var index = 0; index < Count; index++)
        {
            var contract = $"ES-{index:D6}";
            _before.Add(contract);
            _after.Add(contract);
        }
        _target = $"ES-{Count - 1:D6}";
    }

    [Benchmark(Baseline = true)]
    public int BeforeLookup() => _before[_target];

    [Benchmark]
    public int AfterLookup() => _after[_target];

    sealed class BeforeStreamIdCollection
    {
        readonly Dictionary<int, string> _streamIds = [];

        public int this[string contractId]
        {
            get
            {
                lock (_streamIds)
                {
                    return _streamIds.Any(item => item.Value == contractId)
                        ? _streamIds.Single(item => item.Value == contractId).Key
                        : -1;
                }
            }
        }

        public int Add(string contractId)
        {
            lock (_streamIds)
            {
                var streamId = this[contractId];
                if (streamId != -1)
                    return streamId;
                streamId = Math.Abs(contractId.GetHashCode());
                _streamIds.Add(streamId, contractId);
                return streamId;
            }
        }

        public void Clear()
        {
            lock (_streamIds)
                _streamIds.Clear();
        }
    }
}
