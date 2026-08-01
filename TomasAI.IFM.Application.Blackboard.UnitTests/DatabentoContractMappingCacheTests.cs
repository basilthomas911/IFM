using FluentAssertions;
using TomasAI.IFM.Framework.Caching;
using TomasAI.IFM.Framework.MarketData.DataBento;
using TomasAI.IFM.Framework.Serialization;

namespace TomasAI.IFM.Application.Blackboard.UnitTests;

public class DatabentoContractMappingCacheTests
{
    private const string Dataset = "GLBX.MDP3";
    private static readonly DateTimeOffset InitialTime =
        new(2026, 8, 1, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void SetMapping_WritesBothDirectionsWithRequestedExpirationPolicy()
    {
        var time = new ManualTimeProvider(InitialTime);
        var redis = new InMemoryRedisCache(time);
        var sut = CreateCache(redis, time);

        sut.SetMapping(
            Dataset,
            "ES20260918",
            42140870,
            ContractMappingDirection.ContractIdToInstrumentId);

        redis.Count.Should().Be(2);
        redis.SetExpiries.Should().OnlyContain(
            expiry => expiry == DatabentoContractMappingCache.SlidingTimeToLive);
        var entry = redis.Values
            .Select(value => new SystemTextJsonSerializer()
                .Deserialize<DatabentoContractMappingCacheEntry>(value))
            .Distinct()
            .Single();
        entry.AbsoluteExpirationUtc.Should().Be(
            InitialTime + DatabentoContractMappingCache.AbsoluteExpiration);
        entry.DefinitionDate.Should().Be("20260801");
    }

    [Fact]
    public void Get_RenewsSlidingTtlForBothDirections()
    {
        var time = new ManualTimeProvider(InitialTime);
        var redis = new InMemoryRedisCache(time);
        var sut = CreateCache(redis, time);
        sut.SetMapping(
            Dataset,
            "ES20260918",
            42140870,
            ContractMappingDirection.ContractIdToInstrumentId);
        redis.SetExpiries.Clear();
        time.Advance(TimeSpan.FromMinutes(10));

        sut.TryGetInstrumentId(Dataset, "ES20260918", out var instrumentId)
            .Should().BeTrue();

        instrumentId.Should().Be(42140870);
        redis.SetExpiries.Should().HaveCount(2);
        redis.SetExpiries.Should().OnlyContain(
            expiry => expiry == DatabentoContractMappingCache.SlidingTimeToLive);
    }

    [Fact]
    public void GetByInstrumentId_ReturnsReverseMapping()
    {
        var time = new ManualTimeProvider(InitialTime);
        var redis = new InMemoryRedisCache(time);
        var sut = CreateCache(redis, time);
        sut.SetMapping(
            Dataset,
            "ES20260918C100",
            43030566,
            ContractMappingDirection.InstrumentIdToContractId);

        sut.TryGetContractId(Dataset, 43030566, out var contractId)
            .Should().BeTrue();

        contractId.Should().Be("ES20260918C100");
    }

    [Fact]
    public void Get_AfterSlidingTtlWithoutAccess_IsCacheMiss()
    {
        var time = new ManualTimeProvider(InitialTime);
        var redis = new InMemoryRedisCache(time);
        var sut = CreateCache(redis, time);
        sut.SetMapping(
            Dataset,
            "ES20260918C100",
            43030566,
            ContractMappingDirection.ContractIdToInstrumentId);
        time.Advance(TimeSpan.FromMinutes(16));

        sut.TryGetInstrumentId(Dataset, "ES20260918C100", out _)
            .Should().BeFalse();
    }

    [Fact]
    public void SetMapping_ConflictingPair_EvictsAndThrowsDetailedMappingException()
    {
        var time = new ManualTimeProvider(InitialTime);
        var redis = new InMemoryRedisCache(time);
        var sut = CreateCache(redis, time);
        sut.SetMapping(
            Dataset,
            "ES20260918",
            42140870,
            ContractMappingDirection.ContractIdToInstrumentId);

        var act = () => sut.SetMapping(
            Dataset,
            "ES20260918",
            999,
            ContractMappingDirection.ContractIdToInstrumentId);

        var exception = act.Should()
            .Throw<DatabentoContractMappingException>()
            .Which;
        exception.Message.Should().Contain("conflicts");
        exception.Message.Should().Contain("42140870");
        exception.Message.Should().Contain("999");
        redis.Count.Should().Be(0);
    }

    [Fact]
    public void Get_OnNextUtcDefinitionDate_DoesNotReusePreviousDayMapping()
    {
        var time = new ManualTimeProvider(
            new DateTimeOffset(2026, 8, 1, 23, 55, 0, TimeSpan.Zero));
        var redis = new InMemoryRedisCache(time);
        var sut = CreateCache(redis, time);
        sut.SetMapping(
            Dataset,
            "ES20260918",
            42140870,
            ContractMappingDirection.ContractIdToInstrumentId);
        time.Advance(TimeSpan.FromMinutes(10));

        sut.TryGetInstrumentId(Dataset, "ES20260918", out _)
            .Should().BeFalse();
    }

    [Fact]
    public void ClearMapping_ByContractId_RemovesBothDirections()
    {
        var time = new ManualTimeProvider(InitialTime);
        var redis = new InMemoryRedisCache(time);
        var sut = CreateCache(redis, time);
        sut.SetMapping(
            Dataset,
            "ES20260918",
            42140870,
            ContractMappingDirection.ContractIdToInstrumentId);

        sut.ClearMapping(Dataset, "ES20260918");

        sut.TryGetInstrumentId(Dataset, "ES20260918", out _).Should().BeFalse();
        sut.TryGetContractId(Dataset, 42140870, out _).Should().BeFalse();
        redis.Count.Should().Be(0);
    }

    [Fact]
    public void ClearMapping_ByInstrumentId_RemovesBothDirections()
    {
        var time = new ManualTimeProvider(InitialTime);
        var redis = new InMemoryRedisCache(time);
        var sut = CreateCache(redis, time);
        sut.SetMapping(
            Dataset,
            "ES20260918C100",
            43030566,
            ContractMappingDirection.InstrumentIdToContractId);

        sut.ClearMapping(Dataset, 43030566);

        sut.TryGetInstrumentId(Dataset, "ES20260918C100", out _).Should().BeFalse();
        sut.TryGetContractId(Dataset, 43030566, out _).Should().BeFalse();
        redis.Count.Should().Be(0);
    }

    [Fact]
    public void ClearCurrentMappings_RemovesOnlyRequestedDatasetPartition()
    {
        var time = new ManualTimeProvider(InitialTime);
        var redis = new InMemoryRedisCache(time);
        var sut = CreateCache(redis, time);
        sut.SetMapping(
            Dataset,
            "ES20260918",
            42140870,
            ContractMappingDirection.ContractIdToInstrumentId);
        sut.SetMapping(
            "OTHER.MDP3",
            "NQ20260918",
            12345,
            ContractMappingDirection.ContractIdToInstrumentId);

        sut.ClearCurrentMappings(Dataset);

        sut.TryGetInstrumentId(Dataset, "ES20260918", out _).Should().BeFalse();
        sut.TryGetInstrumentId("OTHER.MDP3", "NQ20260918", out var remaining)
            .Should().BeTrue();
        remaining.Should().Be(12345);
        redis.Count.Should().Be(2);
    }

    private static DatabentoContractMappingCache CreateCache(
        IRedisCache redis,
        TimeProvider time) =>
        new(redis, new SystemTextJsonSerializer(), time);

    private sealed class ManualTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        private DateTimeOffset _utcNow = utcNow;

        public override DateTimeOffset GetUtcNow() => _utcNow;

        public void Advance(TimeSpan amount) => _utcNow += amount;
    }

    private sealed class InMemoryRedisCache(TimeProvider timeProvider) : IRedisCache
    {
        private readonly Dictionary<string, CacheItem> _items = [];
        private readonly object _sync = new();

        public List<TimeSpan> SetExpiries { get; } = [];

        public int Count
        {
            get
            {
                lock (_sync)
                {
                    RemoveExpired();
                    return _items.Count;
                }
            }
        }

        public IReadOnlyList<string> Values
        {
            get
            {
                lock (_sync)
                {
                    RemoveExpired();
                    return _items.Values.Select(item => item.Value).ToArray();
                }
            }
        }

        public void Set(string key, string value) => SetCore(key, value, null);

        public void Set(string key, string value, TimeSpan expiry)
        {
            SetExpiries.Add(expiry);
            SetCore(key, value, expiry);
        }

        public void Set(
            string key,
            string value,
            DateTimeOffset absoluteExpiry,
            TimeSpan ttl)
        {
            SetExpiries.Add(ttl);
            var remaining = absoluteExpiry - timeProvider.GetUtcNow();
            SetCore(key, value, remaining < ttl ? remaining : ttl);
        }

        public string? Get(string key)
        {
            lock (_sync)
            {
                RemoveExpired();
                return _items.TryGetValue(key, out var item) ? item.Value : null;
            }
        }

        public bool TryGet(string key, out string? value)
        {
            value = Get(key);
            return !string.IsNullOrEmpty(value);
        }

        public void Remove(string key)
        {
            lock (_sync)
            {
                _items.Remove(key);
            }
        }

        public long RemoveByPrefix(string prefix)
        {
            lock (_sync)
            {
                var keys = _items.Keys
                    .Where(key => key.StartsWith(prefix, StringComparison.Ordinal))
                    .ToArray();
                foreach (var key in keys)
                {
                    _items.Remove(key);
                }
                return keys.LongLength;
            }
        }

        public Task SetAsync(string key, string value)
        {
            Set(key, value);
            return Task.CompletedTask;
        }

        public Task SetAsync(string key, string value, TimeSpan expiry)
        {
            Set(key, value, expiry);
            return Task.CompletedTask;
        }

        public Task SetAsync(
            string key,
            string value,
            DateTimeOffset absoluteExpiry,
            TimeSpan ttl)
        {
            Set(key, value, absoluteExpiry, ttl);
            return Task.CompletedTask;
        }

        public Task<string?> GetAsync(string key) => Task.FromResult(Get(key));

        public long Increment(string key)
        {
            lock (_sync)
            {
                var value = long.TryParse(Get(key), out var current) ? current + 1 : 1;
                Set(key, value.ToString());
                return value;
            }
        }

        public void DeleteAllKeys()
        {
            lock (_sync)
            {
                _items.Clear();
            }
        }

        private void SetCore(string key, string value, TimeSpan? expiry)
        {
            lock (_sync)
            {
                _items[key] = new CacheItem(
                    value,
                    expiry is null ? null : timeProvider.GetUtcNow() + expiry.Value);
            }
        }

        private void RemoveExpired()
        {
            var now = timeProvider.GetUtcNow();
            foreach (var key in _items
                .Where(item => item.Value.ExpiresUtc <= now)
                .Select(item => item.Key)
                .ToArray())
            {
                _items.Remove(key);
            }
        }

        private sealed record CacheItem(string Value, DateTimeOffset? ExpiresUtc);
    }
}

public class CachedDatabentoMarketDataQueriesTests
{
    private const string Dataset = "GLBX.MDP3";

    [Fact]
    public void ForwardLiveLookup_PopulatesBothDirections()
    {
        var cache = new DictionaryMappingCache();
        var source = new StubQueries
        {
            ContractResolver = _ => 42140870,
            InstrumentResolver = _ => throw new InvalidOperationException("Should use cache.")
        };
        var sut = new CachedDatabentoMarketDataQueries(source, cache, Dataset);

        var instrumentId = sut.ContractIdToInstrumentId("ES20260918");
        var contractId = sut.InstrumentIdToContractId(instrumentId);

        instrumentId.Should().Be(42140870);
        contractId.Should().Be("ES20260918");
        source.ContractCalls.Should().Be(1);
        source.InstrumentCalls.Should().Be(0);
    }

    [Fact]
    public void ReverseLiveLookup_PopulatesBothDirections()
    {
        var cache = new DictionaryMappingCache();
        var source = new StubQueries
        {
            ContractResolver = _ => throw new InvalidOperationException("Should use cache."),
            InstrumentResolver = _ => "ES20260918C100"
        };
        var sut = new CachedDatabentoMarketDataQueries(source, cache, Dataset);

        var contractId = sut.InstrumentIdToContractId(43030566);
        var instrumentId = sut.ContractIdToInstrumentId(contractId);

        contractId.Should().Be("ES20260918C100");
        instrumentId.Should().Be(43030566);
        source.InstrumentCalls.Should().Be(1);
        source.ContractCalls.Should().Be(0);
    }

    [Fact]
    public async Task ConcurrentContractMisses_AreCoalescedToOneLiveLookup()
    {
        var cache = new DictionaryMappingCache();
        using var entered = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        var source = new StubQueries
        {
            ContractResolver = _ =>
            {
                entered.Set();
                release.Wait();
                return 42140870;
            }
        };
        var sut = new CachedDatabentoMarketDataQueries(source, cache, Dataset);
        var first = Task.Run(() => sut.ContractIdToInstrumentId("ES20260918"));
        entered.Wait();
        var followers = Enumerable.Range(0, 7)
            .Select(_ => Task.Run(() => sut.ContractIdToInstrumentId("ES20260918")))
            .ToArray();
        release.Set();

        var results = await Task.WhenAll(followers.Prepend(first));

        results.Should().OnlyContain(value => value == 42140870);
        source.ContractCalls.Should().Be(1);
    }

    [Fact]
    public void ProviderFailures_AreNotCached()
    {
        var cache = new DictionaryMappingCache();
        var source = new StubQueries
        {
            ContractResolver = _ => throw new DatabentoContractMappingException(
                ContractMappingDirection.ContractIdToInstrumentId,
                "not found")
        };
        var sut = new CachedDatabentoMarketDataQueries(source, cache, Dataset);

        for (var attempt = 0; attempt < 2; ++attempt)
        {
            var act = () => sut.ContractIdToInstrumentId("ES20990101");
            act.Should().Throw<DatabentoContractMappingException>();
        }

        source.ContractCalls.Should().Be(2);
        cache.MappingCount.Should().Be(0);
    }

    [Fact]
    public void CacheInfrastructureFailure_FallsBackToVerifiedLiveMapping()
    {
        var source = new StubQueries { ContractResolver = _ => 42140870 };
        var sut = new CachedDatabentoMarketDataQueries(
            source,
            new FailingMappingCache(),
            Dataset);

        sut.ContractIdToInstrumentId("ES20260918").Should().Be(42140870);
        source.ContractCalls.Should().Be(1);
    }

    private sealed class DictionaryMappingCache : IDatabentoContractMappingCache
    {
        private readonly Dictionary<string, uint> _byContract = [];
        private readonly Dictionary<uint, string> _byInstrument = [];
        private readonly object _sync = new();

        public int MappingCount
        {
            get
            {
                lock (_sync)
                {
                    return _byContract.Count;
                }
            }
        }

        public bool TryGetInstrumentId(string dataset, string contractId, out uint instrumentId)
        {
            lock (_sync)
            {
                return _byContract.TryGetValue(contractId, out instrumentId);
            }
        }

        public bool TryGetContractId(string dataset, uint instrumentId, out string? contractId)
        {
            lock (_sync)
            {
                return _byInstrument.TryGetValue(instrumentId, out contractId);
            }
        }

        public void SetMapping(
            string dataset,
            string contractId,
            uint instrumentId,
            ContractMappingDirection sourceDirection)
        {
            lock (_sync)
            {
                _byContract[contractId] = instrumentId;
                _byInstrument[instrumentId] = contractId;
            }
        }

        public void ClearMapping(string dataset, string contractId)
        {
            lock (_sync)
            {
                if (_byContract.Remove(contractId, out var instrumentId))
                {
                    _byInstrument.Remove(instrumentId);
                }
            }
        }

        public void ClearMapping(string dataset, uint instrumentId)
        {
            lock (_sync)
            {
                if (_byInstrument.Remove(instrumentId, out var contractId))
                {
                    _byContract.Remove(contractId);
                }
            }
        }

        public void ClearCurrentMappings(string dataset)
        {
            lock (_sync)
            {
                _byContract.Clear();
                _byInstrument.Clear();
            }
        }
    }

    private sealed class FailingMappingCache : IDatabentoContractMappingCache
    {
        public bool TryGetInstrumentId(string dataset, string contractId, out uint instrumentId) =>
            throw new InvalidOperationException("Redis unavailable.");

        public bool TryGetContractId(string dataset, uint instrumentId, out string? contractId) =>
            throw new InvalidOperationException("Redis unavailable.");

        public void SetMapping(
            string dataset,
            string contractId,
            uint instrumentId,
            ContractMappingDirection sourceDirection) =>
            throw new InvalidOperationException("Redis unavailable.");

        public void ClearMapping(string dataset, string contractId) =>
            throw new InvalidOperationException("Redis unavailable.");

        public void ClearMapping(string dataset, uint instrumentId) =>
            throw new InvalidOperationException("Redis unavailable.");

        public void ClearCurrentMappings(string dataset) =>
            throw new InvalidOperationException("Redis unavailable.");
    }

    private sealed class StubQueries : IDatabentoMarketDataQueries
    {
        private int _contractCalls;
        private int _instrumentCalls;

        public Func<string, uint> ContractResolver { get; init; } = _ => 0;
        public Func<uint, string> InstrumentResolver { get; init; } = _ => string.Empty;
        public int ContractCalls => _contractCalls;
        public int InstrumentCalls => _instrumentCalls;

        public uint ContractIdToInstrumentId(string contractId, TimeSpan? timeout = null)
        {
            Interlocked.Increment(ref _contractCalls);
            return ContractResolver(contractId);
        }

        public string InstrumentIdToContractId(uint instrumentId, TimeSpan? timeout = null)
        {
            Interlocked.Increment(ref _instrumentCalls);
            return InstrumentResolver(instrumentId);
        }

        public ContractDetail? GetContractDetail(string contractName, TimeSpan? timeout = null) => null;

        public IReadOnlyList<ContractDetail> GetContractDetails(
            string ticker,
            TimeSpan? timeout = null) => [];

        public IReadOnlyList<ContractDetail?> GetContractDetails(
            string[] contractNames,
            TimeSpan? timeout = null) => [];
    }
}
