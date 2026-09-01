using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using DatabentoFeed.Native.Interop;

namespace DatabentoFeed.Native.ComparisonTests;

public sealed unsafe class NativeParityTests
{
    private static string CppPath => RequiredPath("DBF_CPP_DLL");
    private static string RustPath => RequiredPath("DBF_RUST_DLL");

    [Fact]
    public void Both_libraries_expose_the_complete_frozen_abi()
    {
        using JsonDocument document = JsonDocument.Parse(
            File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "abi-v3-capabilities.json")));
        JsonElement manifest = document.RootElement;
        Assert.Equal(Dbf.AbiVersion, manifest.GetProperty("abiVersion").GetUInt32());
        Assert.Equal(
            NativeApi.CanonicalExports,
            manifest.GetProperty("exports").EnumerateArray().Select(value => value.GetString()).ToArray());
        using var cpp = new NativeApi(CppPath);
        using var rust = new NativeApi(RustPath);
        Assert.Equal(Dbf.AbiVersion, cpp.GetAbiVersion());
        Assert.Equal(cpp.GetAbiVersion(), rust.GetAbiVersion());
        Assert.All(NativeApi.CanonicalExports, name =>
        {
            Assert.True(cpp.HasExport(name), $"C++ is missing {name}");
            Assert.True(rust.HasExport(name), $"Rust is missing {name}");
        });
        Assert.Equal(128, sizeof(FeedConfigV1));
        Assert.Equal(32, sizeof(TickerSubscriptionV1));
        Assert.Equal(32, sizeof(TickerInstrumentMappingV1));
        Assert.Equal(32, sizeof(OptionChainSubscriptionV1));
        Assert.Equal(32, sizeof(OptionContractSelectionV1));
        Assert.Equal(32, sizeof(WaitResultV1));
        Assert.Equal(32, sizeof(BatchResultV1));
        Assert.Equal(128, sizeof(StatsV1));
        Assert.Equal(64, sizeof(ContractQueryV1));
        Assert.Equal(192, sizeof(ContractDetailV1));
        Assert.Equal(88, sizeof(LatestPriceRequestV1));
        Assert.Equal(64, sizeof(LatestPriceResult64));
        Assert.Equal(64, sizeof(HistoricalRequestV1));
        Assert.Equal(32, sizeof(HistoricalEstimateV1));
        Assert.Equal(120, sizeof(HistoricalRecord120));
        Assert.Equal(24, sizeof(HistoricalBatchV1));
        Assert.Equal(64, sizeof(MarketRecord64));
        var actualSizes = new Dictionary<string, int>
        {
            [nameof(FeedConfigV1)] = sizeof(FeedConfigV1),
            [nameof(TickerSubscriptionV1)] = sizeof(TickerSubscriptionV1),
            [nameof(TickerInstrumentMappingV1)] = sizeof(TickerInstrumentMappingV1),
            [nameof(OptionChainSubscriptionV1)] = sizeof(OptionChainSubscriptionV1),
            [nameof(OptionContractSelectionV1)] = sizeof(OptionContractSelectionV1),
            [nameof(WaitResultV1)] = sizeof(WaitResultV1),
            [nameof(BatchResultV1)] = sizeof(BatchResultV1),
            [nameof(StatsV1)] = sizeof(StatsV1),
            [nameof(ContractQueryV1)] = sizeof(ContractQueryV1),
            [nameof(ContractDetailV1)] = sizeof(ContractDetailV1),
            [nameof(LatestPriceRequestV1)] = sizeof(LatestPriceRequestV1),
            [nameof(LatestPriceResult64)] = sizeof(LatestPriceResult64),
            [nameof(HistoricalRequestV1)] = sizeof(HistoricalRequestV1),
            [nameof(HistoricalEstimateV1)] = sizeof(HistoricalEstimateV1),
            [nameof(HistoricalRecord120)] = sizeof(HistoricalRecord120),
            [nameof(HistoricalBatchV1)] = sizeof(HistoricalBatchV1),
            [nameof(MarketRecord64)] = sizeof(MarketRecord64)
        };
        foreach (JsonProperty structure in manifest.GetProperty("structureSizes").EnumerateObject())
            Assert.Equal(structure.Value.GetInt32(), actualSizes[structure.Name]);
    }

    [Fact]
    public void Every_export_has_matching_validation_and_non_live_status_semantics()
    {
        using var cpp = new NativeApi(CppPath);
        using var rust = new NativeApi(RustPath);
        Assert.Equal(InvokeValidationMatrix(cpp), InvokeValidationMatrix(rust));
    }

    [Fact]
    public void Synthetic_lifecycle_mappings_records_and_stats_match_cpp()
    {
        using var cpp = new NativeApi(CppPath);
        using var rust = new NativeApi(RustPath);
        SyntheticRun expected = SyntheticFeedRunner.Run(cpp, 20_000, 512);
        SyntheticRun actual = SyntheticFeedRunner.Run(rust, 20_000, 512);
        Assert.Equal(expected.MappingCount, actual.MappingCount);
        Assert.Equal(expected.MappingBlob, actual.MappingBlob);
        Assert.Equal(expected.Mappings.Select(MappingIdentity), actual.Mappings.Select(MappingIdentity));
        Assert.Equal(expected.Records.Length, actual.Records.Length);
        for (int i = 0; i < expected.Records.Length; i++)
            Assert.Equal(RecordIdentity(expected.Records[i]), RecordIdentity(actual.Records[i]));
        Assert.Equal(expected.Stats.RingCapacityRecords, actual.Stats.RingCapacityRecords);
        Assert.Equal(expected.Stats.RecordsProduced, actual.Stats.RecordsProduced);
        Assert.Equal(expected.Stats.RecordsConsumed, actual.Stats.RecordsConsumed);
        Assert.Equal(expected.Stats.RingOverruns, actual.Stats.RingOverruns);
    }

    [Fact]
    public void Synthetic_historical_estimate_and_records_match_cpp()
    {
        using var cpp = new NativeApi(CppPath);
        using var rust = new NativeApi(RustPath);
        HistoricalRun expected = RunHistorical(cpp);
        HistoricalRun actual = RunHistorical(rust);
        Assert.Equal(expected, actual);
    }

    private static string MappingIdentity(TickerInstrumentMappingV1 value) =>
        $"{value.StructSize}:{value.AbiVersion}:{value.SubscriptionIndex}:{value.InstrumentId}:" +
        $"{value.PublisherId}:{value.RequestedSymbolOffset}:{value.RequestedSymbolLength}:" +
        $"{value.RawSymbolOffset}:{value.RawSymbolLength}";

    // Synthetic timestamps are deliberately generated from each library's monotonic clock.
    private static string RecordIdentity(MarketRecord64 value) =>
        $"{value.InstrumentId}:{value.PublisherId}:{value.RecordKind}:{value.Flags}:" +
        $"{value.Sequence}:{value.SourceSchema}:{value.Value0}:{value.Value1}:{value.Value2}:" +
        $"{(value.RecordKind == 2 ? 0 : value.Value3)}";

    private static HistoricalRun RunHistorical(NativeApi api)
    {
        byte[] blob = Encoding.UTF8.GetBytes("GLBX.MDP3ES.c.0");
        fixed (byte* blobPointer = blob)
        {
            var request = new HistoricalRequestV1
            {
                StructSize = (uint)sizeof(HistoricalRequestV1),
                AbiVersion = Dbf.AbiVersion,
                Schema = Dbf.HistoricalOhlcv1D,
                InputSymbology = 2,
                Flags = Dbf.HistoricalSynthetic,
                SymbolCount = 1,
                Dataset = new Utf8SliceV1 { Offset = 0, Length = 9 },
                StartTimestampNanoseconds = 1_770_000_000_000_000_000,
                EndTimestampNanoseconds = 1_770_086_400_000_000_000,
                RecordLimit = 10,
                TimeoutMilliseconds = 1_000
            };
            var symbol = new Utf8SliceV1 { Offset = 9, Length = 6 };
            var estimate = new HistoricalEstimateV1
            {
                StructSize = (uint)sizeof(HistoricalEstimateV1),
                AbiVersion = Dbf.AbiVersion
            };
            Assert.Equal(Dbf.Ok, api.HistoricalEstimate(
                &request, &symbol, blobPointer, (uint)blob.Length, &estimate));
            nint result = 0;
            Assert.Equal(Dbf.Ok, api.HistoricalRangeOpen(
                &request, &symbol, blobPointer, (uint)blob.Length, &result));
            HistoricalRecord120* records = stackalloc HistoricalRecord120[2];
            var batch = new HistoricalBatchV1
            {
                StructSize = (uint)sizeof(HistoricalBatchV1),
                AbiVersion = Dbf.AbiVersion
            };
            Assert.Equal(Dbf.Ok, api.HistoricalGetNextBatch(result, records, 2, &batch));
            byte[] bytes = new ReadOnlySpan<byte>(records, 2 * sizeof(HistoricalRecord120)).ToArray();
            Assert.Equal(Dbf.Ok, api.HistoricalDestroy(result));
            return new(
                estimate.EstimatedCostUsd,
                estimate.EstimatedBytes,
                estimate.EstimatedRecords,
                batch.RecordsRead,
                batch.MoreAvailable,
                batch.BatchOrdinal,
                Convert.ToHexString(bytes));
        }
    }

    private static int[] InvokeValidationMatrix(NativeApi api)
    {
        var statuses = new List<int>();
        nint handle = 0;
        uint count = 0, bytes = 0;
        MarketRecord64* records = null;
        FeedConfigV1 config = default;
        TickerSubscriptionV1 subscription = default;
        OptionChainSubscriptionV1 chain = default;
        OptionContractSelectionV1 selection = default;
        WaitResultV1 wait = default;
        BatchResultV1 batch = default;
        StatsV1 stats = default;
        ContractQueryV1 query = default;
        Utf8SliceV1 symbol = default;
        ContractDetailV1 detail = default;
        LatestPriceRequestV1 latest = default;
        LatestPriceResult64 latestResult = default;
        HistoricalRequestV1 historical = default;
        HistoricalEstimateV1 historicalEstimate = default;
        HistoricalRecord120 historicalRecord = default;
        HistoricalBatchV1 historicalBatch = default;
        byte value = 0;
        statuses.Add((int)api.GetAbiVersion());
        statuses.Add(api.FeedCreate(&config, &value, 0, &handle));
        statuses.Add(api.SubscribeTickers(0, &subscription, 1, &value, 0, 1));
        statuses.Add(api.SubscribeOptionChain(0, &chain, &selection, 1, &value, 0, 1));
        statuses.Add(api.AllocateReadBuffer(0, 1, &records));
        statuses.Add(api.Start(0, 1));
        statuses.Add(api.GetMappingCounts(0, &count, &bytes));
        statuses.Add(api.CopyMappings(0, null, 0, null, 0));
        statuses.Add(api.SetConsumerReady(0, 1));
        statuses.Add(api.Wait(0, 1, &wait));
        statuses.Add(api.ReadBatch(0, records, 1, &batch));
        statuses.Add(api.Stop(0, 1));
        statuses.Add(api.FreeReadBuffer(0, records));
        statuses.Add(api.GetStats(0, &stats));
        statuses.Add(api.GetLastError(0, &value, 1, &bytes));
        statuses.Add(api.Destroy(0));
        statuses.Add(api.ContractQuery(&query, &symbol, &value, 1, &handle));
        statuses.Add(api.ContractGetCounts(0, &count, &bytes));
        statuses.Add(api.ContractCopy(0, &detail, 1, &value, 1));
        statuses.Add(api.ContractGetError(0, &value, 1, &bytes));
        statuses.Add(api.ContractDestroy(0));
        statuses.Add(api.GetLatestPrice(&latest, 1, &latestResult));
        statuses.Add(api.HistoricalEstimate(
            &historical, &symbol, &value, 1, &historicalEstimate));
        statuses.Add(api.HistoricalBatchSubmit(&historical, &symbol, &value, 1, &handle));
        statuses.Add(api.HistoricalBatchGetStatus(null, 0, &handle));
        statuses.Add(api.HistoricalBatchListFiles(null, 0, &handle));
        statuses.Add(api.HistoricalBatchDownloadFile(null, 0, null, 0, null, 0));
        statuses.Add(api.HistoricalRangeOpen(&historical, &symbol, &value, 1, &handle));
        statuses.Add(api.HistoricalFileOpen(null, 0, 0, &handle));
        statuses.Add(api.HistoricalGetPayload(0, &value, 1, &bytes));
        statuses.Add(api.HistoricalGetNextBatch(0, &historicalRecord, 1, &historicalBatch));
        statuses.Add(api.HistoricalGetError(0, &value, 1, &bytes));
        statuses.Add(api.HistoricalDestroy(0));
        return [.. statuses];
    }

    private static string RequiredPath(string variable)
    {
        string? value = Environment.GetEnvironmentVariable(variable);
        if (string.IsNullOrWhiteSpace(value)) throw new InvalidOperationException($"Set {variable}.");
        return value;
    }

    private sealed record HistoricalRun(
        double CostUsd,
        ulong Bytes,
        ulong Records,
        uint RecordsRead,
        uint MoreAvailable,
        ulong BatchOrdinal,
        string PayloadHex);
}
