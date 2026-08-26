using System.Security.Cryptography;
using System.Text;
using TomasAI.IFM.Application.MarketData.Contracts.Historical;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Common;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesTradeSessionBarPublisher;
using TomasAI.IFM.Framework.MarketData.Contracts.Historical;

namespace TomasAI.IFM.Application.MarketData.Databento.Historical;

/// <summary>
/// Implements cost-controlled, resumable Databento historical acquisition while exposing only domain-neutral data.
/// </summary>
public sealed class DatabentoHistoricalApi : IMarketDataHistoricalApi
{
    readonly IMarketDataHistoricalProvider _provider;
    readonly IHistoricalSeriesRequestResolver _resolver;
    readonly IMarketSessionCalendar _calendar;
    readonly DatabentoHistoricalOptions _options;
    readonly TimeProvider _timeProvider;

    /// <summary>Initializes the historical application adapter.</summary>
    public DatabentoHistoricalApi(
        IMarketDataHistoricalProvider provider,
        IHistoricalSeriesRequestResolver resolver,
        IMarketSessionCalendar calendar,
        DatabentoHistoricalOptions options,
        TimeProvider timeProvider)
    {
        _provider = provider ?? throw new ArgumentNullException(nameof(provider));
        _resolver = resolver ?? throw new ArgumentNullException(nameof(resolver));
        _calendar = calendar ?? throw new ArgumentNullException(nameof(calendar));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        ValidateOptions(options);
    }

    /// <inheritdoc/>
    public async ValueTask<MarketDataHistoricalEstimate> EstimateAsync(
        MarketDataHistoricalRequest request,
        CancellationToken cancellationToken)
    {
        ValidateRequest(request);
        decimal cost = 0;
        long bytes = 0;
        long records = 0;
        foreach (var series in request.Series)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var estimate = await _provider.EstimateAsync(
                _resolver.Resolve(request, series), cancellationToken).ConfigureAwait(false);
            cost = checked(cost + estimate.EstimatedCostUsd);
            bytes = checked(bytes + estimate.EstimatedBytes);
            records = checked(records + estimate.EstimatedRecords);
        }
        return new(
            request.DataLoadAttemptId,
            cost,
            bytes,
            records,
            ComputeRequestHash(request),
            _timeProvider.GetUtcNow());
    }

    /// <inheritdoc/>
    public async ValueTask<MarketDataHistoricalManifest> AcquireAsync(
        MarketDataHistoricalRequest request,
        HistoricalAcquisitionCheckpoint checkpoint,
        IHistoricalObservationSink sink,
        CancellationToken cancellationToken)
    {
        ValidateRequest(request);
        ArgumentNullException.ThrowIfNull(checkpoint);
        ArgumentNullException.ThrowIfNull(sink);
        if (checkpoint.DataLoadAttemptId != request.DataLoadAttemptId)
            throw new ArgumentException("Checkpoint attempt does not match the request.", nameof(checkpoint));

        var estimate = await EstimateAsync(request, cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(request.ApprovedOverrideId)
            && (estimate.EstimatedCostUsd > request.MaximumCostUsd
                || estimate.EstimatedBytes > request.MaximumBytes))
            throw new HistoricalBudgetExceededException(estimate, request.MaximumCostUsd, request.MaximumBytes);

        var stagingRoot = ResolveStagingRoot(_options.StagingRoot);
        Directory.CreateDirectory(stagingRoot);
        var jobIds = new List<string>();
        long observationCount = 0;
        long tradeCount = 0;
        DateOnly firstValueDate = DateOnly.MaxValue;
        DateOnly lastValueDate = DateOnly.MinValue;
        using var normalizedHash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);

        for (var seriesIndex = 0; seriesIndex < request.Series.Length; seriesIndex++)
        {
            var series = request.Series[seriesIndex];
            var providerRequest = _resolver.Resolve(request, series);
            var job = seriesIndex == 0 && !string.IsNullOrWhiteSpace(checkpoint.ProviderJobId)
                ? await _provider.GetBatchJobAsync(checkpoint.ProviderJobId, cancellationToken).ConfigureAwait(false)
                : await _provider.SubmitBatchAsync(providerRequest, cancellationToken).ConfigureAwait(false);
            if (sink is IHistoricalAcquisitionCheckpointSink checkpointSink)
            {
                await checkpointSink.CheckpointAsync(new HistoricalAcquisitionCheckpoint
                {
                    DataLoadAttemptId = request.DataLoadAttemptId,
                    Stage = HistoricalAcquisitionStage.Submitted,
                    ProviderJobId = job.ProviderJobId,
                    ProviderFileId = checkpoint.ProviderFileId,
                    BatchOrdinal = checkpoint.BatchOrdinal,
                    SourcePosition = checkpoint.SourcePosition
                }, cancellationToken).ConfigureAwait(false);
            }
            job = await WaitForCompletionAsync(job, cancellationToken).ConfigureAwait(false);
            jobIds.Add(job.ProviderJobId);

            var files = await _provider.ListBatchFilesAsync(
                job.ProviderJobId, cancellationToken).ConfigureAwait(false);
            foreach (var file in files)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var path = ResolveStagingPath(stagingRoot, request.DataLoadAttemptId, file.FileName);
                await _provider.DownloadBatchFileAsync(
                    job.ProviderJobId, file, path, cancellationToken).ConfigureAwait(false);
                await VerifyFileAsync(path, file, cancellationToken).ConfigureAwait(false);
                await using var reader = await _provider.OpenFileAsync(
                    path, file.Schema, _options.MaximumBatchRecords, cancellationToken).ConfigureAwait(false);
                while (await reader.ReadNextAsync(cancellationToken).ConfigureAwait(false) is { } sourceBatch)
                {
                    if (sourceBatch.BatchOrdinal <= checkpoint.BatchOrdinal
                        && string.Equals(file.ProviderFileId, checkpoint.ProviderFileId, StringComparison.Ordinal))
                        continue;
                    var normalized = NormalizeBatch(
                        request,
                        series,
                        file.ProviderFileId,
                        sourceBatch);
                    await sink.AcceptAsync(normalized, cancellationToken).ConfigureAwait(false);
                    observationCount = checked(observationCount + normalized.Observations.Count);
                    tradeCount = checked(tradeCount + normalized.Trades.Count);
                    foreach (var observation in normalized.Observations)
                    {
                        firstValueDate = Min(firstValueDate, observation.ValueDate);
                        lastValueDate = Max(lastValueDate, observation.ValueDate);
                    }
                    foreach (var trade in normalized.Trades)
                    {
                        firstValueDate = Min(firstValueDate, trade.ValueDate);
                        lastValueDate = Max(lastValueDate, trade.ValueDate);
                    }
                    normalizedHash.AppendData(Convert.FromHexString(normalized.NormalizedSha256));
                }
            }
        }

        return new MarketDataHistoricalManifest
        {
            ManifestId = DeterministicGuid($"{estimate.RequestSha256}|{string.Join('|', jobIds)}"),
            DataLoadAttemptId = request.DataLoadAttemptId,
            ProviderJobId = string.Join(',', jobIds),
            RequestSha256 = estimate.RequestSha256,
            NormalizedSha256 = Convert.ToHexString(normalizedHash.GetHashAndReset()),
            ObservationCount = observationCount,
            TradeCount = tradeCount,
            FirstValueDate = firstValueDate == DateOnly.MaxValue ? default : firstValueDate,
            LastValueDate = lastValueDate == DateOnly.MinValue ? default : lastValueDate,
            CompletedAtUtc = _timeProvider.GetUtcNow()
        };
    }

    async ValueTask<HistoricalProviderJob> WaitForCompletionAsync(
        HistoricalProviderJob initial,
        CancellationToken cancellationToken)
    {
        var job = initial;
        var deadline = _timeProvider.GetUtcNow() + _options.JobTimeout;
        while (job.State is HistoricalProviderJobState.Queued or HistoricalProviderJobState.Processing)
        {
            if (_timeProvider.GetUtcNow() >= deadline)
                throw new TimeoutException($"Historical provider job {job.ProviderJobId} did not complete in time.");
            await Task.Delay(_options.PollInterval, _timeProvider, cancellationToken).ConfigureAwait(false);
            job = await _provider.GetBatchJobAsync(job.ProviderJobId, cancellationToken).ConfigureAwait(false);
        }
        if (job.State != HistoricalProviderJobState.Completed)
            throw new InvalidOperationException(
                $"Historical provider job {job.ProviderJobId} ended as {job.State}: {job.ErrorMessage}");
        return job;
    }

    NormalizedHistoricalBatch NormalizeBatch(
        MarketDataHistoricalRequest request,
        MarketDataHistoricalSeriesRequest series,
        string providerFileId,
        HistoricalProviderRecordBatch source)
    {
        var observations = new List<FuturesTradeSessionBarReadModel>();
        var trades = new List<NormalizedHistoricalTrade>();
        foreach (var record in source.Records)
        {
            var contractId = _resolver.ResolveContractId(series, record);
            var valueDate = _calendar.GetValueDate(record.EventTimestampUtc);
            if (!_calendar.IsTradingDate(valueDate)) continue;
            if (record.Kind == HistoricalProviderRecordKind.Ohlcv)
            {
                var intervalStart = record.EventTimestampUtc;
                var intervalEnd = intervalStart.AddMinutes(1);
                var observationId = FuturesTradeSessionBarId.Create(
                    series.SeriesIdentity,
                    TimeFrameType.OneMinute,
                    intervalEnd,
                    record.SourceSequence);
                observations.Add(new FuturesTradeSessionBarReadModel
                {
                    MarketSeriesIdentity = series.SeriesIdentity,
                    ObservationId = observationId,
                    ContractId = contractId,
                    ValueDate = valueDate,
                    TimeFrame = TimeFrameType.OneMinute,
                    IntervalStartUtc = intervalStart,
                    IntervalEndUtc = intervalEnd,
                    Open = record.Open,
                    High = record.High,
                    Low = record.Low,
                    Close = record.CloseOrPrice,
                    Volume = record.VolumeOrSize,
                    TradeCount = 0,
                    PriceVolumeSum = record.CloseOrPrice * record.VolumeOrSize,
                    FirstSourceSequence = record.SourceSequence,
                    LastSourceSequence = record.SourceSequence,
                    FirstMarketEventUtc = record.EventTimestampUtc,
                    LastMarketEventUtc = record.EventTimestampUtc,
                    CalculatedAtUtc = _timeProvider.GetUtcNow(),
                    SchemaVersion = 1,
                    CalculationVersion = request.NormalizationVersion,
                    IsComplete = true,
                    IsValid = true,
                    ValidationIssues = [],
                    CalculationMethod = MarketSignalCalculationMethod.NormalizedHistoricalAggregate
                });
            }
            else if (record.Kind == HistoricalProviderRecordKind.Trade)
            {
                trades.Add(new NormalizedHistoricalTrade
                {
                    ContractId = contractId,
                    ValueDate = valueDate,
                    Price = record.CloseOrPrice,
                    Size = record.VolumeOrSize,
                    EventTimestampUtc = record.EventTimestampUtc,
                    SourceSequence = record.SourceSequence,
                    Action = record.Action,
                    Side = record.Side,
                    Conditions = record.Conditions,
                    ProviderInstrumentId = record.InstrumentId
                });
            }
        }

        var hash = ComputeNormalizedHash(observations, trades);
        return new(
            request.DataLoadAttemptId,
            providerFileId,
            source.BatchOrdinal,
            source.SourcePosition,
            observations,
            trades,
            hash,
            source.IsFinal);
    }

    static async ValueTask VerifyFileAsync(
        string path,
        HistoricalProviderFile file,
        CancellationToken cancellationToken)
    {
        var info = new FileInfo(path);
        if (!info.Exists || info.Length != file.SizeBytes)
            throw new InvalidDataException($"Historical file {file.FileName} has an unexpected size.");
        await using var stream = new FileStream(
            path, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, FileOptions.Asynchronous);
        var actual = Convert.ToHexString(await SHA256.HashDataAsync(stream, cancellationToken).ConfigureAwait(false));
        if (!string.Equals(actual, file.Sha256, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException($"Historical file {file.FileName} failed SHA-256 verification.");
    }

    static string ComputeNormalizedHash(
        IReadOnlyList<FuturesTradeSessionBarReadModel> observations,
        IReadOnlyList<NormalizedHistoricalTrade> trades)
    {
        var canonical = new StringBuilder();
        foreach (var x in observations)
            canonical.Append(x.ObservationId).Append('|').Append(x.ContractId).Append('|')
                .Append(x.Close).Append('|').Append(x.Volume).AppendLine();
        foreach (var x in trades)
            canonical.Append(x.ContractId).Append('|').Append(x.EventTimestampUtc.ToString("O"))
                .Append('|').Append(x.SourceSequence).Append('|').Append(x.Price).Append('|')
                .Append(x.Size).AppendLine();
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical.ToString())));
    }

    static string ComputeRequestHash(MarketDataHistoricalRequest request)
    {
        var canonical = new StringBuilder()
            .Append(request.StartDate.ToString("O")).Append('|')
            .Append(request.EndDate.ToString("O")).Append('|')
            .Append(request.NormalizationVersion);
        foreach (var x in request.Series.OrderBy(x => x.SeriesIdentity.Format(), StringComparer.Ordinal))
            canonical.Append('|').Append(x.SeriesIdentity.Format()).Append('|').Append(x.ContractId)
                .Append('|').Append(x.Schema).Append('|').Append(x.ExactTradesRequired);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical.ToString())));
    }

    static Guid DeterministicGuid(string value) =>
        new(SHA256.HashData(Encoding.UTF8.GetBytes(value)).AsSpan(0, 16));

    static string ResolveStagingRoot(string configuredRoot)
    {
        if (!Path.IsPathFullyQualified(configuredRoot))
            throw new InvalidOperationException("Historical staging root must be absolute.");
        return Path.TrimEndingDirectorySeparator(Path.GetFullPath(configuredRoot));
    }

    static string ResolveStagingPath(string root, Guid attemptId, string fileName)
    {
        if (!string.Equals(Path.GetFileName(fileName), fileName, StringComparison.Ordinal))
            throw new InvalidDataException("Provider file name must not contain a path.");
        var directory = Path.GetFullPath(Path.Combine(root, attemptId.ToString("N")));
        if (!directory.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Resolved staging directory escaped the configured root.");
        Directory.CreateDirectory(directory);
        return Path.Combine(directory, fileName);
    }

    static void ValidateOptions(DatabentoHistoricalOptions options)
    {
        if (options.MaximumBatchRecords is < 1 or > 65_536)
            throw new ArgumentOutOfRangeException(nameof(options.MaximumBatchRecords));
        if (options.PollInterval <= TimeSpan.Zero || options.JobTimeout <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(options));
        _ = ResolveStagingRoot(options.StagingRoot);
    }

    static void ValidateRequest(MarketDataHistoricalRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.DataLoadAttemptId == Guid.Empty) throw new ArgumentException("DataLoadAttemptId is required.");
        if (request.Series is not { Length: > 0 }) throw new ArgumentException("At least one series is required.");
        if (request.StartDate > request.EndDate) throw new ArgumentException("StartDate must not follow EndDate.");
        if (request.MaximumCostUsd < 0 || request.MaximumBytes < 0) throw new ArgumentException("Budgets cannot be negative.");
        ArgumentException.ThrowIfNullOrWhiteSpace(request.NormalizationVersion);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.RequestedBy);
    }

    static DateOnly Min(DateOnly left, DateOnly right) => left <= right ? left : right;
    static DateOnly Max(DateOnly left, DateOnly right) => left >= right ? left : right;
}

/// <summary>Thrown before provider submission when an unapproved estimate exceeds request limits.</summary>
public sealed class HistoricalBudgetExceededException : InvalidOperationException
{
    /// <summary>Initializes the budget failure.</summary>
    public HistoricalBudgetExceededException(
        MarketDataHistoricalEstimate estimate,
        decimal maximumCostUsd,
        long maximumBytes)
        : base($"Historical estimate {estimate.EstimatedCostUsd:C} / {estimate.EstimatedBytes} bytes exceeds "
               + $"the approved {maximumCostUsd:C} / {maximumBytes} byte budget.")
    {
        Estimate = estimate;
    }

    /// <summary>Gets the rejected estimate.</summary>
    public MarketDataHistoricalEstimate Estimate { get; }
}
