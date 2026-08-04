using TomasAI.IFM.Domain.Fund.Shared;
using TomasAI.IFM.Domain.Fund.Shared.ViewModels;
using TomasAI.IFM.Domain.Trade.Shared;

namespace TomasAI.IFM.Application.Storage.FundDb;

internal static class FundTransactionProjection
{
    const int MaxConcurrentPartitionReads = 8;

    public static DateOnly GetMonthBucket(DateOnly valueDate)
        => new(valueDate.Year, valueDate.Month, 1);

    public static int GetAmountSign(decimal amount)
        => amount < 0m ? -1 : 1;

    public static DateTime NormalizeTransactionDate(DateTime transactionDate)
    {
        // The Cassandra driver treats Unspecified as an already-UTC wall clock, converts
        // Local to UTC, stores Unix milliseconds, and deserializes back as Unspecified.
        // Mirror that representation so in-memory keys and CQL keys cannot diverge.
        var utc = transactionDate.Kind == DateTimeKind.Local
            ? transactionDate.ToUniversalTime()
            : DateTime.SpecifyKind(transactionDate, DateTimeKind.Utc);
        return new DateTime(
            utc.Ticks - utc.Ticks % TimeSpan.TicksPerMillisecond,
            DateTimeKind.Utc);
    }

    public static void ValidateLogicalDuplicates(IEnumerable<FundTransactionReadModel> transactions)
    {
        var representatives = new Dictionary<FundTransactionLogicalKey, FundTransactionReadModel>();
        foreach (var transaction in transactions)
        {
            var key = FundTransactionLogicalKey.From(transaction);
            if (representatives.TryGetValue(key, out var existing) && existing != transaction)
            {
                throw new ArgumentException(
                    "Fund transaction inputs contain the same logical key with different payloads.",
                    nameof(transactions));
            }
            representatives.TryAdd(key, transaction);
        }
    }

    public static IReadOnlyList<FundTransactionMonthRange> GetMonthRanges(DateOnly startDate, DateOnly endDate)
    {
        if (endDate < startDate)
            return [];

        var ranges = new List<FundTransactionMonthRange>();
        var month = GetMonthBucket(startDate);

        while (month <= endDate)
        {
            var monthEnd = new DateOnly(month.Year, month.Month, DateTime.DaysInMonth(month.Year, month.Month));
            ranges.Add(new FundTransactionMonthRange(
                month,
                startDate > month ? startDate : month,
                endDate < monthEnd ? endDate : monthEnd));

            if (month.Year == DateOnly.MaxValue.Year && month.Month == DateOnly.MaxValue.Month)
                break;

            month = month.AddMonths(1);
        }

        return ranges;
    }

    public static async Task<List<TResult>> ReadBoundedAsync<TPartition, TResult>(
        IEnumerable<TPartition> partitions,
        Func<TPartition, Task<ICollection<TResult>>> readPartitionAsync)
    {
        var results = new List<TResult>();
        foreach (var batch in partitions.Chunk(MaxConcurrentPartitionReads))
        {
            var pages = await Task.WhenAll(batch.Select(readPartitionAsync)).ConfigureAwait(false);
            foreach (var page in pages)
                results.AddRange(page);
        }

        return results;
    }

    public static async Task<List<FundTransactionPartitionResult<TPartition, TResult>>> ReadBoundedPartitionsAsync<TPartition, TResult>(
        IEnumerable<TPartition> partitions,
        Func<TPartition, Task<ICollection<TResult>>> readPartitionAsync)
    {
        var results = new List<FundTransactionPartitionResult<TPartition, TResult>>();
        foreach (var batch in partitions.Chunk(MaxConcurrentPartitionReads))
        {
            var pages = await Task.WhenAll(batch.Select(async partition =>
                new FundTransactionPartitionResult<TPartition, TResult>(
                    partition,
                    await readPartitionAsync(partition).ConfigureAwait(false)))).ConfigureAwait(false);
            results.AddRange(pages);
        }

        return results;
    }
}

internal readonly record struct FundTransactionMonthRange(
    DateOnly MonthBucket,
    DateOnly StartDate,
    DateOnly EndDate);

internal readonly record struct FundTransactionAmountProjection(
    int FundId,
    DateOnly ValueDate,
    int OrderId,
    int TradeId,
    TradeType TradeType,
    DateTime TransactionDate,
    long TransactionId,
    decimal Amount);

internal readonly record struct FundTransactionWrite(
    FundTransactionReadModel Transaction,
    long TransactionId,
    FundTransactionReadModel? ExistingTransaction = null);

internal readonly record struct FundTransactionLogicalKey(
    int FundId,
    DateOnly ValueDate,
    int OrderId,
    int TradeId,
    TradeType TradeType,
    FundTransactionType TransactionType,
    DateTime TransactionDate)
{
    public static FundTransactionLogicalKey From(FundTransactionReadModel transaction)
        => new(
            transaction.FundId,
            transaction.ValueDate,
            transaction.OrderId,
            transaction.TradeId,
            transaction.TradeType,
            transaction.TransactionType,
            FundTransactionProjection.NormalizeTransactionDate(transaction.TransactionDate));
}

internal readonly record struct FundTransactionIdentityExpectation(
    long TransactionId,
    long CanonicalRows)
{
    public FundTransactionIdentityExpectation Add(long transactionId)
        => new(Math.Min(TransactionId, transactionId), CanonicalRows + 1);

    public long DuplicateCanonicalRows => Math.Max(0, CanonicalRows - 1);
}

internal sealed record FundTransactionIdentityRow(long TransactionId);

internal readonly record struct FundTransactionIdentityReconciliation(
    long LogicalTransactionKeys,
    long IdentityRows,
    long MissingIdentityRows,
    long ConflictingIdentityRows,
    long DuplicateCanonicalRows)
{
    public bool IsReconciled =>
        IdentityRows == LogicalTransactionKeys &&
        MissingIdentityRows == 0 &&
        ConflictingIdentityRows == 0 &&
        DuplicateCanonicalRows == 0;
}

internal readonly record struct FundTransactionProjectionKey(
    int FundId,
    DateOnly ValueDate,
    int OrderId,
    int TradeId,
    string TradeType,
    string TransactionType,
    DateTime TransactionDate,
    long TransactionId);

internal readonly record struct FundTransactionProjectionState(
    Guid Generation,
    bool IsComplete,
    long SourceCount,
    string SourceFingerprint,
    DateTime ReconciledOn);

internal readonly record struct FundTransactionProjectionMutation(
    int FundId,
    DateOnly MonthBucket,
    Guid MutationId);

internal readonly record struct FundTransactionProjectionMutationJournalEntry(
    int FundId,
    DateOnly MonthBucket,
    Guid MutationId,
    DateTime StartedOn);

internal readonly record struct FundTransactionWriteMutationJournalEntry(
    int FundId,
    Guid MutationId,
    DateTime StartedOn);

internal sealed class FundTransactionMutationScope
{
    readonly Dictionary<DateOnly, Guid> _readyGenerations = [];

    public FundTransactionMutationScope(int fundId, IEnumerable<DateOnly> monthBuckets)
    {
        FundId = fundId;
        MutationId = Guid.NewGuid();
        Mutations = monthBuckets
            .Distinct()
            .Order()
            .Select(monthBucket => new FundTransactionProjectionMutation(
                fundId,
                monthBucket,
                MutationId))
            .ToArray();
    }

    public int FundId { get; }
    public Guid MutationId { get; }
    public IReadOnlyList<FundTransactionProjectionMutation> Mutations { get; }
    public bool OwnsWriteOwnership { get; set; }
    public bool OwnershipClaimAttempted { get; set; }
    public IReadOnlyDictionary<DateOnly, Guid> ReadyGenerations => _readyGenerations;

    public void SetReadyGeneration(DateOnly monthBucket, Guid generation)
        => _readyGenerations[monthBucket] = generation;

    public void RemoveReadyGeneration(DateOnly monthBucket)
        => _readyGenerations.Remove(monthBucket);

    public void ClearReadyGenerations()
        => _readyGenerations.Clear();
}

internal readonly record struct FundTransactionMonthReconciliation(
    FundTransactionProjectionMutation Mutation,
    long SourceCount,
    string SourceFingerprint,
    long TimelineCount,
    string TimelineFingerprint,
    long StatusBalanceCount,
    string StatusBalanceFingerprint,
    long TransactionAmountCount,
    string TransactionAmountFingerprint,
    long LogicalTransactionKeys,
    long IdentityRows,
    long MissingIdentityRows,
    long ConflictingIdentityRows,
    long DuplicateCanonicalRows)
{
    public bool KeysMatch =>
        SourceCount == TimelineCount &&
        SourceCount == StatusBalanceCount &&
        SourceCount == TransactionAmountCount &&
        SourceFingerprint == TimelineFingerprint &&
        SourceFingerprint == StatusBalanceFingerprint &&
        SourceFingerprint == TransactionAmountFingerprint;

    public bool IdentitiesMatch =>
        IdentityRows == LogicalTransactionKeys &&
        MissingIdentityRows == 0 &&
        ConflictingIdentityRows == 0 &&
        DuplicateCanonicalRows == 0;

    public bool IsReconciled => KeysMatch && IdentitiesMatch;
}

internal struct FundTransactionKeyFingerprint
{
    const ulong Offset = 14695981039346656037UL;
    const ulong Prime = 1099511628211UL;
    ulong _xor;
    ulong _sum;
    ulong _sumSquares;

    public long Count { get; private set; }

    public void Add(FundTransactionProjectionKey key)
    {
        var hash = Offset;
        AddInt64(ref hash, key.FundId);
        AddInt64(ref hash, key.ValueDate.DayNumber);
        AddInt64(ref hash, key.OrderId);
        AddInt64(ref hash, key.TradeId);
        AddString(ref hash, key.TradeType);
        AddString(ref hash, key.TransactionType);
        AddInt64(ref hash, FundTransactionProjection.NormalizeTransactionDate(key.TransactionDate).Ticks);
        AddInt64(ref hash, key.TransactionId);
        var mixed = Mix(hash);
        _xor ^= mixed;
        _sum = unchecked(_sum + mixed);
        _sumSquares = unchecked(_sumSquares + mixed * mixed);
        Count++;
    }

    public void Merge(FundTransactionKeyFingerprint other)
    {
        _xor ^= other._xor;
        _sum = unchecked(_sum + other._sum);
        _sumSquares = unchecked(_sumSquares + other._sumSquares);
        Count += other.Count;
    }

    public readonly string Value => $"{_xor:x16}:{_sum:x16}:{_sumSquares:x16}";

    static void AddInt64(ref ulong hash, long value)
    {
        var bits = unchecked((ulong)value);
        for (var index = 0; index < sizeof(long); index++)
        {
            hash ^= (byte)bits;
            hash *= Prime;
            bits >>= 8;
        }
    }

    static void AddString(ref ulong hash, string value)
    {
        foreach (var character in value)
        {
            hash ^= (byte)character;
            hash *= Prime;
            hash ^= (byte)(character >> 8);
            hash *= Prime;
        }
        hash ^= 0xff;
        hash *= Prime;
    }

    static ulong Mix(ulong value)
    {
        value ^= value >> 30;
        value *= 0xbf58476d1ce4e5b9UL;
        value ^= value >> 27;
        value *= 0x94d049bb133111ebUL;
        return value ^ (value >> 31);
    }
}

internal readonly record struct FundTransactionAmountPartition(
    FundTransactionMonthRange Range,
    string TransactionType,
    int AmountSign);

internal readonly record struct FundTransactionPartitionResult<TPartition, TResult>(
    TPartition Partition,
    ICollection<TResult> Rows);

public readonly record struct FundTransactionProjectionBackfillResult(
    long TransactionsRead,
    long TransactionsProjected,
    int BatchesExecuted,
    long TimelineRows,
    long StatusBalanceRows,
    long TransactionAmountRows,
    string SourceFingerprint,
    string TimelineFingerprint,
    string StatusBalanceFingerprint,
    string TransactionAmountFingerprint,
    int CompletedMonths,
    int TotalMonths,
    long LogicalTransactionKeys = 0,
    long IdentityRows = 0,
    long MissingIdentityRows = 0,
    long ConflictingIdentityRows = 0,
    long DuplicateCanonicalRows = 0)
{
    public long TimelineMismatchCount => Math.Abs(TransactionsRead - TimelineRows);
    public long StatusBalanceMismatchCount => Math.Abs(TransactionsRead - StatusBalanceRows);
    public long TransactionAmountMismatchCount => Math.Abs(TransactionsRead - TransactionAmountRows);
    public bool KeysMatch =>
        TimelineMismatchCount == 0 &&
        StatusBalanceMismatchCount == 0 &&
        TransactionAmountMismatchCount == 0 &&
        SourceFingerprint == TimelineFingerprint &&
        SourceFingerprint == StatusBalanceFingerprint &&
        SourceFingerprint == TransactionAmountFingerprint;
    public bool IdentitiesMatch =>
        IdentityRows == LogicalTransactionKeys &&
        MissingIdentityRows == 0 &&
        ConflictingIdentityRows == 0 &&
        DuplicateCanonicalRows == 0;
    public bool IsReconciled => KeysMatch && IdentitiesMatch && CompletedMonths == TotalMonths;
}
