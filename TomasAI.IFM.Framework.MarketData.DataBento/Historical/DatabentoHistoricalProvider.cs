using System.Globalization;
using System.Security.Cryptography;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.FuturesMarketPrice.Events;
using TomasAI.IFM.Shared.MarketData.Contracts.Historical;
using TomasAI.IFM.Framework.MarketData.DataBento.Interop;

namespace TomasAI.IFM.Framework.MarketData.DataBento.Historical;

/// <summary>
/// Implements the provider-neutral historical boundary over the native Databento ABI.
/// </summary>
public sealed class DatabentoHistoricalProvider : IMarketDataHistoricalProvider
{
    private const decimal FixedPriceScale = 1_000_000_000m;
    private const string SyntheticFile =
        "2,SYNTH,1000,7,1770000000000000000,1,5000000000,5002000000,4998000000,5000500000,10,T,B,0\n" +
        "2,SYNTH,1001,7,1770000060000000000,2,5001000000,5003000000,4999000000,5001500000,11,T,A,0\n";

    private readonly DatabentoHistoricalProviderOptions options;
    private readonly TimeProvider timeProvider;

    /// <summary>Initializes the historical provider.</summary>
    /// <param name="options">The configuration governing provider or feed operation.</param>
    /// <param name="timeProvider">The clock used for timestamps and elapsed-time measurements.</param>
    public DatabentoHistoricalProvider(
        DatabentoHistoricalProviderOptions options,
        TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(timeProvider);
        options.Validate();
        this.options = options;
        this.timeProvider = timeProvider;
    }

    /// <inheritdoc />
    public async ValueTask<HistoricalProviderEstimate> EstimateAsync(
        HistoricalProviderRequest request,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var nativeEstimate = Task.Run(() => EstimateNative(request), CancellationToken.None);
        try
        {
            return await nativeEstimate
                .WaitAsync(TimeSpan.FromMilliseconds(options.TimeoutMilliseconds), cancellationToken)
                .ConfigureAwait(false);
        }
        catch (TimeoutException exception)
        {
            throw new TimeoutException(
                $"Databento historical estimate exceeded the configured {options.TimeoutMilliseconds} ms timeout.",
                exception);
        }
    }

    private unsafe HistoricalProviderEstimate EstimateNative(HistoricalProviderRequest request)
    {
        using var input = NativeHistoricalInput.Create(request, options);
        var estimate = new NativeHistoricalEstimate
        {
            StructSize = (uint)sizeof(NativeHistoricalEstimate),
            AbiVersion = NativeConstants.AbiVersion
        };
        var nativeRequest = input.Request;
        var status = NativeMethods.HistoricalEstimate(
            &nativeRequest, input.SymbolsPointer, input.BlobPointer,
            checked((uint)input.Blob.Length), ref estimate);
        if (status != DatabentoFeedStatus.Ok)
        {
            var detail = ReadEstimateError();
            throw new DatabentoFeedException(status,
                $"Databento historical estimate failed with {status}: {detail}. Dataset={request.Dataset}; Schema={request.Schema}; "
                + $"StartUtc={request.StartUtc:O}; EndUtc={request.EndUtc:O}; RequestHash={request.RequestHash}");
        }
        return new HistoricalProviderEstimate(
            (decimal)estimate.EstimatedCostUsd,
            checked((long)estimate.EstimatedBytes),
            checked((long)estimate.EstimatedRecords),
            timeProvider.GetUtcNow());
    }

    /// <inheritdoc />
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA2000", Justification = "Ownership is transferred to the returned reader/batch; failed native results are scoped with using before error propagation.")]
    public unsafe ValueTask<HistoricalProviderJob> SubmitBatchAsync(
        HistoricalProviderRequest request,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var input = NativeHistoricalInput.Create(request, options);
        var nativeRequest = input.Request;
        var status = NativeMethods.HistoricalBatchSubmit(
            &nativeRequest, input.SymbolsPointer, input.BlobPointer,
            checked((uint)input.Blob.Length), out var pointer);
        if (status != DatabentoFeedStatus.Ok && pointer != 0)
        {
            using var failedResult = new SafeHistoricalResultHandle(pointer);
            var detail = ReadError(failedResult);
            throw new DatabentoFeedException(
                status,
                string.IsNullOrWhiteSpace(detail)
                    ? $"submit historical batch failed with {status}."
                    : $"submit historical batch failed with {status}: {detail}");
        }
        Throw(status, "submit historical batch");
        using var result = new SafeHistoricalResultHandle(pointer);
        return ValueTask.FromResult(ParseJob(ReadPayload(result)));
    }

    /// <inheritdoc />
    public unsafe ValueTask<HistoricalProviderJob> GetBatchJobAsync(
        string providerJobId,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var bytes = RequiredUtf8(providerJobId, nameof(providerJobId));
        fixed (byte* pointer = bytes)
        {
            var status = NativeMethods.HistoricalBatchGetStatus(
                pointer, checked((uint)bytes.Length), out var nativeResult);
            Throw(status, "read historical batch status");
            using var result = new SafeHistoricalResultHandle(nativeResult);
            return ValueTask.FromResult(ParseJob(ReadPayload(result)));
        }
    }

    /// <inheritdoc />
    public unsafe ValueTask<IReadOnlyList<HistoricalProviderFile>> ListBatchFilesAsync(
        string providerJobId,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var bytes = RequiredUtf8(providerJobId, nameof(providerJobId));
        fixed (byte* pointer = bytes)
        {
            var status = NativeMethods.HistoricalBatchListFiles(
                pointer, checked((uint)bytes.Length), out var nativeResult);
            Throw(status, "list historical batch files");
            using var result = new SafeHistoricalResultHandle(nativeResult);
            using var document = JsonDocument.Parse(ReadPayload(result));
            var files = new List<HistoricalProviderFile>();
            foreach (var item in document.RootElement.GetProperty("files").EnumerateArray())
            {
                var schema = (HistoricalDataSchema)item.GetProperty("schema").GetByte();
                var name = item.GetProperty("fileName").GetString()!;
                var fileBytes = options.UseSyntheticProvider
                    ? Encoding.UTF8.GetBytes(SyntheticFile)
                    : [];
                var sizeBytes = options.UseSyntheticProvider
                    ? fileBytes.LongLength
                    : item.GetProperty("sizeBytes").GetInt64();
                var sha256 = options.UseSyntheticProvider
                    ? System.Convert.ToHexString(SHA256.HashData(fileBytes))
                    : item.GetProperty("sha256").GetString()!;
                files.Add(new HistoricalProviderFile(
                    item.GetProperty("providerFileId").GetString()!,
                    name,
                    schema,
                    sizeBytes,
                    sha256));
            }
            return ValueTask.FromResult<IReadOnlyList<HistoricalProviderFile>>(files);
        }
    }

    /// <inheritdoc />
    public unsafe ValueTask DownloadBatchFileAsync(
        string providerJobId,
        HistoricalProviderFile file,
        string destinationPath,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(file);
        cancellationToken.ThrowIfCancellationRequested();
        var job = RequiredUtf8(providerJobId, nameof(providerJobId));
        var name = RequiredUtf8(file.FileName, nameof(file));
        var destination = RequiredUtf8(Path.GetFullPath(destinationPath), nameof(destinationPath));
        fixed (byte* jobPointer = job)
        fixed (byte* namePointer = name)
        fixed (byte* destinationPointer = destination)
        {
            Throw(NativeMethods.HistoricalBatchDownloadFile(
                jobPointer, checked((uint)job.Length),
                namePointer, checked((uint)name.Length),
                destinationPointer, checked((uint)destination.Length)),
                "download historical batch file");
        }
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA2000", Justification = "Ownership is transferred to the returned reader/batch; failed native results are scoped with using before error propagation.")]
    public unsafe ValueTask<IHistoricalRecordReader> OpenRangeAsync(
        HistoricalProviderRequest request,
        int maximumBatchRecords,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ValidateBatchSize(maximumBatchRecords);
        using var input = NativeHistoricalInput.Create(request, options);
        var nativeRequest = input.Request;
        var status = NativeMethods.HistoricalRangeOpen(
            &nativeRequest, input.SymbolsPointer, input.BlobPointer,
            checked((uint)input.Blob.Length), out var pointer);
        Throw(status, "open historical range");
        return ValueTask.FromResult<IHistoricalRecordReader>(
            new NativeHistoricalRecordReader(
                new SafeHistoricalResultHandle(pointer), maximumBatchRecords));
    }

    /// <inheritdoc />
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA2000", Justification = "Ownership is transferred to the returned reader/batch; failed native results are scoped with using before error propagation.")]
    public unsafe ValueTask<IHistoricalRecordReader> OpenFileAsync(
        string path,
        HistoricalDataSchema schema,
        int maximumBatchRecords,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ValidateBatchSize(maximumBatchRecords);
        if (!File.Exists(path))
        {
            throw new FileNotFoundException("Historical file was not found.", path);
        }
        if (options.UseSyntheticProvider)
        {
            return ValueTask.FromResult<IHistoricalRecordReader>(
                new CsvHistoricalRecordReader(path, maximumBatchRecords));
        }
        var filePath = RequiredUtf8(Path.GetFullPath(path), nameof(path));
        fixed (byte* pointer = filePath)
        {
            Throw(NativeMethods.HistoricalFileOpen(
                pointer, checked((uint)filePath.Length), (uint)schema, out var result),
                "open historical DBN file");
            return ValueTask.FromResult<IHistoricalRecordReader>(
                new NativeHistoricalRecordReader(
                    new SafeHistoricalResultHandle(result), maximumBatchRecords));
        }
    }

    private void ValidateBatchSize(int maximumBatchRecords)
    {
        if (maximumBatchRecords is < 1 || maximumBatchRecords > options.MaximumBatchRecords)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumBatchRecords));
        }
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S3236", Justification = "Validation names the public request that owns the invalid property rather than an inaccessible nested field.")]
    private static byte[] RequiredUtf8(string value, string parameterName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, parameterName);
        return Encoding.UTF8.GetBytes(value);
    }

    private static HistoricalProviderJob ParseJob(string json)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        return new HistoricalProviderJob(
            root.GetProperty("providerJobId").GetString()!,
            Enum.Parse<HistoricalProviderJobState>(root.GetProperty("state").GetString()!, true),
            root.GetProperty("costUsd").GetDecimal(),
            root.GetProperty("recordCount").GetInt64(),
            root.GetProperty("billedBytes").GetInt64(),
            root.GetProperty("progressPercent").GetByte(),
            root.TryGetProperty("errorMessage", out var error) ? error.GetString() ?? string.Empty : string.Empty);
    }

    private static unsafe string ReadPayload(SafeHistoricalResultHandle result)
    {
        var status = NativeMethods.HistoricalResultGetPayload(result, null, 0, out var required);
        if (status != DatabentoFeedStatus.BufferTooSmall || required < 1)
        {
            Throw(status, "size historical result payload");
        }
        var bytes = new byte[required];
        fixed (byte* pointer = bytes)
        {
            Throw(NativeMethods.HistoricalResultGetPayload(
                result, pointer, required, out _), "copy historical result payload");
        }
        return Encoding.UTF8.GetString(bytes.AsSpan(0, checked((int)required - 1)));
    }

    private static unsafe string ReadError(SafeHistoricalResultHandle result)
    {
        var status = NativeMethods.HistoricalResultGetError(result, null, 0, out var required);
        if (status == DatabentoFeedStatus.Ok && required == 1)
            return string.Empty;
        if (status != DatabentoFeedStatus.BufferTooSmall || required < 1)
            return string.Empty;
        var bytes = new byte[required];
        fixed (byte* pointer = bytes)
        {
            status = NativeMethods.HistoricalResultGetError(result, pointer, required, out _);
        }
        return status == DatabentoFeedStatus.Ok
            ? Encoding.UTF8.GetString(bytes.AsSpan(0, checked((int)required - 1)))
            : string.Empty;
    }

    private static void Throw(DatabentoFeedStatus status, string operation) =>
        NativeStatus.ThrowIfFailed(status, null, operation);

    private static unsafe string ReadEstimateError()
    {
        try
        {
            var status = NativeMethods.HistoricalGetLastError(null, 0, out var required);
            if (status != DatabentoFeedStatus.BufferTooSmall || required <= 1) return "Native provider returned no error detail";
            var buffer = new byte[required];
            fixed (byte* pointer = buffer)
                status = NativeMethods.HistoricalGetLastError(pointer, required, out _);
            return status == DatabentoFeedStatus.Ok
                ? Encoding.UTF8.GetString(buffer.AsSpan(0, checked((int)required - 1))) : "Native error detail could not be read";
        }
        catch (EntryPointNotFoundException)
        {
            return "Native library does not expose historical error detail; rebuild the Databento native library";
        }
    }

    private sealed unsafe class NativeHistoricalInput : IDisposable
    {
        private readonly GCHandle blobHandle;
        private readonly GCHandle symbolsHandle;

        /// <summary>Initializes a new NativeHistoricalInput instance.</summary>
        /// <param name="blob">The native request storage retained for the input's lifetime.</param>
        /// <param name="symbols">The requested provider symbols.</param>
        /// <param name="request">The parameters identifying the requested provider data or session.</param>
        private NativeHistoricalInput(
            byte[] blob,
            NativeUtf8Slice[] symbols,
            NativeHistoricalRequest request)
        {
            Blob = blob;
            Symbols = symbols;
            Request = request;
            blobHandle = GCHandle.Alloc(blob, GCHandleType.Pinned);
            symbolsHandle = GCHandle.Alloc(symbols, GCHandleType.Pinned);
        }

        internal byte[] Blob { get; }
        internal NativeUtf8Slice[] Symbols { get; }
        internal NativeHistoricalRequest Request;
        internal byte* BlobPointer => (byte*)blobHandle.AddrOfPinnedObject();
        internal NativeUtf8Slice* SymbolsPointer =>
            (NativeUtf8Slice*)symbolsHandle.AddrOfPinnedObject();

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S3236", Justification = "Validation names the public request that owns the invalid property rather than an inaccessible nested field.")]
        internal static NativeHistoricalInput Create(
            HistoricalProviderRequest request,
            DatabentoHistoricalProviderOptions options)
        {
            ArgumentNullException.ThrowIfNull(request);
            ArgumentException.ThrowIfNullOrWhiteSpace(request.Dataset, nameof(request));
            if (request.Symbols is not { Length: > 0 }
                || request.Symbols.Any(string.IsNullOrWhiteSpace)
                || request.StartUtc >= request.EndUtc)
            {
                throw new ArgumentException("Historical provider range and symbols must be valid.", nameof(request));
            }
            var offsets = new NativeUtf8Slice[request.Symbols.Length];
            using var stream = new MemoryStream();
            var dataset = Encoding.UTF8.GetBytes(request.Dataset);
            stream.Write(dataset);
            for (var index = 0; index < request.Symbols.Length; index++)
            {
                var bytes = Encoding.UTF8.GetBytes(request.Symbols[index]);
                var offset = checked((uint)stream.Position);
                stream.Write(bytes);
                offsets[index] = new NativeUtf8Slice
                {
                    Offset = offset,
                    Length = checked((uint)bytes.Length)
                };
            }
            var blob = stream.ToArray();
            var native = new NativeHistoricalRequest
            {
                StructSize = (uint)sizeof(NativeHistoricalRequest),
                AbiVersion = NativeConstants.AbiVersion,
                Schema = (NativeHistoricalSchema)request.Schema,
                InputSymbology = (uint)request.Symbology,
                Flags = options.UseSyntheticProvider
                    ? NativeHistoricalFlags.Synthetic
                    : NativeHistoricalFlags.None,
                SymbolCount = checked((uint)request.Symbols.Length),
                Dataset = new NativeUtf8Slice { Offset = 0, Length = checked((uint)dataset.Length) },
                StartTimestampNanoseconds = ToUnixNanoseconds(request.StartUtc),
                EndTimestampNanoseconds = ToUnixNanoseconds(request.EndUtc),
                RecordLimit = checked((ulong)Math.Max(request.RecordLimit, 0)),
                TimeoutMilliseconds = options.TimeoutMilliseconds
            };
            return new NativeHistoricalInput(blob, offsets, native);
        }

        /// <summary>Releases the resources owned by this instance.</summary>
        public void Dispose()
        {
            if (symbolsHandle.IsAllocated) symbolsHandle.Free();
            if (blobHandle.IsAllocated) blobHandle.Free();
        }
    }

    private sealed class NativeHistoricalRecordReader : IHistoricalRecordReader
    {
        private readonly SafeHistoricalResultHandle result;
        private readonly int maximumBatchRecords;
        private bool finished;

        /// <summary>Initializes a new NativeHistoricalRecordReader instance.</summary>
        /// <param name="result">The native result or operation result wrapped by this instance.</param>
        /// <param name="maximumBatchRecords">The maximum number of records returned per historical batch.</param>
        internal NativeHistoricalRecordReader(
            SafeHistoricalResultHandle result,
            int maximumBatchRecords)
        {
            this.result = result;
            this.maximumBatchRecords = maximumBatchRecords;
        }

        /// <summary>Reads the next bounded batch of historical records asynchronously.</summary>
        /// <param name="cancellationToken">The token used to cancel the operation.</param>
        /// <returns>An awaitable containing the next historical batch, or null when the input is exhausted.</returns>
        public unsafe ValueTask<HistoricalProviderRecordBatch?> ReadNextAsync(
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (finished) return ValueTask.FromResult<HistoricalProviderRecordBatch?>(null);
            var source = new NativeHistoricalRecord[maximumBatchRecords];
            var batch = new NativeHistoricalBatch
            {
                StructSize = (uint)sizeof(NativeHistoricalBatch),
                AbiVersion = NativeConstants.AbiVersion
            };
            fixed (NativeHistoricalRecord* pointer = source)
            {
                Throw(NativeMethods.HistoricalResultGetNextBatch(
                    result, pointer, checked((uint)source.Length), ref batch),
                    "read historical range batch");
            }
            finished = batch.MoreAvailable == 0;
            var records = new HistoricalProviderRecord[batch.RecordsRead];
            for (var index = 0; index < records.Length; index++)
            {
                records[index] = Convert(source[index]);
            }
            return ValueTask.FromResult<HistoricalProviderRecordBatch?>(new(
                records, checked((long)batch.BatchOrdinal), batch.BatchOrdinal.ToString(CultureInfo.InvariantCulture), finished));
        }

        /// <summary>Asynchronously stops processing and releases the resources owned by this instance.</summary>
        /// <returns>An awaitable that completes when the operation finishes.</returns>
        public ValueTask DisposeAsync()
        {
            result.Dispose();
            return ValueTask.CompletedTask;
        }
    }

    private sealed class CsvHistoricalRecordReader : IHistoricalRecordReader
    {
        private readonly StreamReader reader;
        private readonly int maximumBatchRecords;
        private long batchOrdinal;
        private string? pendingLine;

        /// <summary>Initializes a new CsvHistoricalRecordReader instance.</summary>
        /// <param name="path">The path of the historical CSV file to read.</param>
        /// <param name="maximumBatchRecords">The maximum number of records returned per historical batch.</param>
        internal CsvHistoricalRecordReader(string path, int maximumBatchRecords)
        {
            reader = new StreamReader(path, Encoding.UTF8, true);
            this.maximumBatchRecords = maximumBatchRecords;
        }

        /// <summary>Reads the next bounded batch of historical records asynchronously.</summary>
        /// <param name="cancellationToken">The token used to cancel the operation.</param>
        /// <returns>An awaitable containing the next historical batch, or null when the input is exhausted.</returns>
        public async ValueTask<HistoricalProviderRecordBatch?> ReadNextAsync(CancellationToken cancellationToken)
        {
            var records = new List<HistoricalProviderRecord>(maximumBatchRecords);
            if (pendingLine is not null)
            {
                records.Add(ParseCsv(pendingLine));
                pendingLine = null;
            }
            while (records.Count < maximumBatchRecords)
            {
                var line = await (reader.ReadLineAsync(cancellationToken)).ConfigureAwait(false);
                if (line is null) break;
                if (!string.IsNullOrWhiteSpace(line)) records.Add(ParseCsv(line));
            }
            if (records.Count == 0) return null;
            pendingLine = await (reader.ReadLineAsync(cancellationToken)).ConfigureAwait(false);
            var final = pendingLine is null;
            return new(records, batchOrdinal, (batchOrdinal++).ToString(CultureInfo.InvariantCulture), final);
        }

        /// <summary>Asynchronously stops processing and releases the resources owned by this instance.</summary>
        /// <returns>An awaitable that completes when the operation finishes.</returns>
        public ValueTask DisposeAsync()
        {
            reader.Dispose();
            return ValueTask.CompletedTask;
        }
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S3398", Justification = "Keep provider decoding and shared process configuration helpers with the component that owns their state.")]
    private static unsafe HistoricalProviderRecord Convert(NativeHistoricalRecord value)
    {
        string symbol;
        byte* pointer = value.Symbol;
        var length = 0;
        while (length < 32 && pointer[length] != 0) length++;
        symbol = Encoding.UTF8.GetString(pointer, length);
        return new HistoricalProviderRecord
        {
            Kind = (HistoricalProviderRecordKind)value.RecordKind,
            Symbol = symbol,
            InstrumentId = value.InstrumentId.ToString(CultureInfo.InvariantCulture),
            PublisherId = value.PublisherId.ToString(CultureInfo.InvariantCulture),
            EventTimestampUtc = FromUnixNanoseconds(value.EventTimestampNanoseconds),
            SourceSequence = value.SourceSequence,
            Open = value.OpenPrice / FixedPriceScale,
            High = value.HighPrice / FixedPriceScale,
            Low = value.LowPrice / FixedPriceScale,
            CloseOrPrice = value.CloseOrTradePrice / FixedPriceScale,
            VolumeOrSize = checked((long)value.VolumeOrSize),
            Action = value.Action == (byte)'T' ? NormalizedTradeAction.New : NormalizedTradeAction.Unknown,
            Side = (value.Side == (byte)'B') switch { true => NormalizedTradeSide.Buy, false => (value.Side == (byte)'A') switch { true => NormalizedTradeSide.Sell, false => NormalizedTradeSide.Unknown } },
            Conditions = (NormalizedTradeConditionFlags)value.ConditionFlags
        };
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S3398", Justification = "Keep provider decoding and shared process configuration helpers with the component that owns their state.")]
    private static HistoricalProviderRecord ParseCsv(string line)
    {
        var values = line.Split(',');
        if (values.Length != 14) throw new InvalidDataException("Historical CSV record has an invalid field count.");
        return new HistoricalProviderRecord
        {
            Kind = (HistoricalProviderRecordKind)byte.Parse(values[0], CultureInfo.InvariantCulture),
            Symbol = values[1],
            InstrumentId = values[2],
            PublisherId = values[3],
            EventTimestampUtc = FromUnixNanoseconds(long.Parse(values[4], CultureInfo.InvariantCulture)),
            SourceSequence = long.Parse(values[5], CultureInfo.InvariantCulture),
            Open = long.Parse(values[6], CultureInfo.InvariantCulture) / FixedPriceScale,
            High = long.Parse(values[7], CultureInfo.InvariantCulture) / FixedPriceScale,
            Low = long.Parse(values[8], CultureInfo.InvariantCulture) / FixedPriceScale,
            CloseOrPrice = long.Parse(values[9], CultureInfo.InvariantCulture) / FixedPriceScale,
            VolumeOrSize = long.Parse(values[10], CultureInfo.InvariantCulture),
            Action = string.Equals(values[11],"T",StringComparison.Ordinal) ? NormalizedTradeAction.New : NormalizedTradeAction.Unknown,
            Side = (string.Equals(values[12],"B",StringComparison.Ordinal)) switch { true => NormalizedTradeSide.Buy, false => (string.Equals(values[12],"A",StringComparison.Ordinal)) switch { true => NormalizedTradeSide.Sell, false => NormalizedTradeSide.Unknown } },
            Conditions = (NormalizedTradeConditionFlags)ushort.Parse(values[13], CultureInfo.InvariantCulture)
        };
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S3398", Justification = "Keep provider serialization helpers with the provider component that owns the native contract.")]
    private static long ToUnixNanoseconds(DateTimeOffset value) =>
        checked((value.UtcTicks - DateTimeOffset.UnixEpoch.UtcTicks) * 100L);

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S3398", Justification = "Keep provider decoding and shared process configuration helpers with the component that owns their state.")]
    private static DateTimeOffset FromUnixNanoseconds(long value) =>
        DateTimeOffset.UnixEpoch.AddTicks(value / 100L);
}
