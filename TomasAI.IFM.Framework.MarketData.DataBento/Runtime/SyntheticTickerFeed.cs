using System.Diagnostics;
using System.Text;
using TomasAI.IFM.Framework.MarketData.DataBento.Interop;

namespace TomasAI.IFM.Framework.MarketData.DataBento;

internal sealed unsafe class SyntheticTickerFeed : IDatabentoTickerFeed
{
    private sealed class ChannelState
    {
        internal required InstrumentKey Instrument { get; init; }
        internal required BoundedBatchChannel Channel { get; init; }
        internal bool RequiresBaseline { get; init; }
        internal int BaselineReady;
        internal MarketDataBatch64? AssemblyBatch;
    }

    private readonly object _lifecycleGate = new();
    private readonly DatabentoFeedOptions _options;
    private readonly bool _singleChannel;
    private readonly ManualResetEventSlim _drainAllocated = new(false);
    private readonly ManualResetEventSlim _drainStart = new(false);
    private readonly ManualResetEventSlim _drainReady = new(false);
    private readonly SemaphoreSlim _multiplexedReady = new(0);
    private readonly ulong[]? _managedObservedProcessors;
    private readonly Func<bool> _isStopping;
    private TickerSubscription[]? _subscriptions;
    private OptionContractSelection[]? _optionContracts;
    private MarketDataKinds _optionDataKinds;
    private SafeDbFeedHandle? _handle;
    private Thread? _drainThread;
    private MarketRecord64* _readBuffer;
    private Exception? _drainFailure;
    private IDisposable? _gcLease;
    private FeedPlacementLease? _placementLease;
    private IReadOnlyList<TickerInstrumentRegistration> _registrations =
        Array.Empty<TickerInstrumentRegistration>();
    private Dictionary<InstrumentKey, ChannelState> _channels = new();
    private Dictionary<uint, ChannelState> _channelsByInstrumentId = new();
    private ChannelState[] _channelStates = [];
    private bool _started;
    private bool _stopCompleted;
    private bool _disposed;
    private bool _multiplexedReaderLeased;
    private bool _directReaderMode;
    private long _batchesPublished;
    private long _drainAllocatedBytes;
    private long _drainPassLimitHitCount;
    private long _observedManagedDrain = -1;
    private long _managedLastProcessor = -1;
    private long _managedProcessorSampleCount;
    private long _managedProcessorMigrationCount;
    private long _managedOffAssignmentCount;
    private long _nativeReadCallCount;
    private long _lastNativeReadFirstSequence;
    private long _lastNativeReadLastSequence;
    private int _lastNativeReadRecordCount;
    private int _lastNativeReadRecordsRouted;
    private int _currentNativeReadRecordIndex = -1;
    private int _currentRecordKind;
    private int _currentRecordPublisherId;
    private int _currentRecordInstrumentId;
    private int _currentRecordSourceSequence;
    private int _managedBatchPublishActive;
    private int _managedBatchPublishRecordCount;
    private int _managedBatchPublisherId;
    private int _managedBatchInstrumentId;
    private int _drainStage;
    private int _stopRequested;
    private int _managedUniqueProcessorCount;
    private FeedProcessorSelectionKind _processorSelection;
    private LogicalProcessorLocation? _resolvedNativeProducer;
    private LogicalProcessorLocation? _resolvedManagedDrain;
    private LogicalProcessorLocation? _nativeProducerAlternate;
    private LogicalProcessorLocation? _managedDrainAlternate;
    private bool _managedDrainUsingAlternate;
    private string? _platformWarning;

    internal SyntheticTickerFeed(DatabentoFeedOptions options, bool singleChannel = false)
    {
        _options = options;
        _singleChannel = singleChannel;
        _isStopping = IsStopping;
        _managedObservedProcessors = options.ProcessorResidency.EnableTracking
            ? new ulong[64]
            : null;
    }

    public void Subscribe(
        ReadOnlySpan<TickerSubscription> subscriptions,
        TimeSpan timeout)
    {
        ValidateTimeout(timeout);
        lock (_lifecycleGate)
        {
            ThrowIfDisposed();
            if (_started || _subscriptions is not null)
            {
                throw new InvalidOperationException("Ticker subscriptions are immutable after configuration.");
            }
            if (subscriptions.IsEmpty)
            {
                throw new ArgumentException("At least one ticker subscription is required.", nameof(subscriptions));
            }
            var uniqueSubscriptions = new List<TickerSubscription>(subscriptions.Length);
            var symbols = new Dictionary<string, TickerSubscription>(StringComparer.Ordinal);
            foreach (var subscription in subscriptions)
            {
                ArgumentException.ThrowIfNullOrWhiteSpace(subscription.Symbol);
                if (Encoding.UTF8.GetByteCount(subscription.Symbol) > ushort.MaxValue)
                {
                    throw new ArgumentException(
                        "Ticker symbols cannot exceed 65,535 UTF-8 bytes.");
                }
                if (subscription.InputSymbology is not DatabentoInputSymbology.RawSymbol
                    and not DatabentoInputSymbology.InstrumentId)
                {
                    throw new ArgumentException("Only stable raw-symbol and instrument-ID selectors are supported.");
                }
                if (subscription.DataKinds == MarketDataKinds.None
                    || (subscription.DataKinds & ~(MarketDataKinds.Quote
                                                   | MarketDataKinds.Trade
                                                   | MarketDataKinds.MboOrderUpdate
                                                   | MarketDataKinds.Statistics
                                                   | MarketDataKinds.SessionVolume)) != 0
                    || ((subscription.DataKinds & MarketDataKinds.SessionVolume) != 0
                        && (subscription.DataKinds & MarketDataKinds.Trade) == 0))
                {
                    throw new ArgumentException("A subscription contains invalid market-data kinds.");
                }
                if (symbols.TryGetValue(subscription.Symbol, out var existing))
                {
                    if (existing != subscription)
                    {
                        throw new ArgumentException(
                            $"Ticker subscription '{subscription.Symbol}' has conflicting selectors or data kinds.");
                    }
                    continue;
                }
                symbols.Add(subscription.Symbol, subscription);
                uniqueSubscriptions.Add(subscription);
            }
            _subscriptions = uniqueSubscriptions.ToArray();
        }
    }

    internal void SubscribeOptionChain(
        IReadOnlyList<OptionContractSelection> contracts,
        MarketDataKinds dataKinds,
        TimeSpan timeout)
    {
        ValidateTimeout(timeout);
        lock (_lifecycleGate)
        {
            ThrowIfDisposed();
            if (!_singleChannel || _started || _subscriptions is not null)
            {
                throw new InvalidOperationException("The option-chain subscription is immutable.");
            }
            var contractCopy = contracts.ToArray();
            if (contractCopy.Length == 0)
            {
                throw new ArgumentException("At least one option contract is required.", nameof(contracts));
            }
            if (dataKinds == MarketDataKinds.None
                || (dataKinds & ~(MarketDataKinds.Quote
                                  | MarketDataKinds.Trade
                                  | MarketDataKinds.MboOrderUpdate
                                  | MarketDataKinds.Statistics
                                  | MarketDataKinds.SessionVolume)) != 0
                || ((dataKinds & MarketDataKinds.SessionVolume) != 0
                    && (dataKinds & MarketDataKinds.Trade) == 0))
            {
                throw new ArgumentException("Option market-data kinds are invalid.", nameof(dataKinds));
            }
            _optionContracts = contractCopy;
            _optionDataKinds = dataKinds;
            _subscriptions = new TickerSubscription[contractCopy.Length];
            for (var index = 0; index < contractCopy.Length; index++)
            {
                _subscriptions[index] = new TickerSubscription(
                    contractCopy[index].RawSymbol,
                    DatabentoInputSymbology.RawSymbol,
                    dataKinds);
            }
        }
    }

    public void Start(TimeSpan timeout, Action<TimeSpan> startConsumer)
    {
        ValidateTimeout(timeout);
        ArgumentNullException.ThrowIfNull(startConsumer);
        var deadline = new MonotonicDeadline(timeout);
        lock (_lifecycleGate)
        {
            ThrowIfDisposed();
            if (_started || _subscriptions is null)
            {
                throw new InvalidOperationException("Subscribe exactly once before starting the feed.");
            }
            _started = true;
        }

        try
        {
            _gcLease = FeedGcCoordinator.Acquire(_options.GarbageCollection);
            _placementLease = ProcessCoreIsolationCoordinator.Acquire(
                _options.CpuAffinity,
                _options.CoreIsolation,
                _options.Numa,
                _options.ProcessorResidency);
            _processorSelection = _placementLease.SelectionKind;
            _resolvedNativeProducer = _placementLease.NativeProducer;
            _resolvedManagedDrain = _placementLease.ManagedDrain;
            _nativeProducerAlternate = _placementLease.NativeProducerAlternate;
            _managedDrainAlternate = _placementLease.ManagedDrainAlternate;
            _handle = CreateAndSubscribeNative(deadline);
            _drainThread = new Thread(DrainThreadMain)
            {
                IsBackground = true,
                Name = $"Databento drain: {_options.Dataset}",
                Priority = MapThreadPriority(_options.ThreadPriority.ManagedDrain)
            };
            _drainThread.Start();
            if (!_drainAllocated.Wait(deadline.Remaining))
            {
                throw new DatabentoFeedTimeoutException(
                    "Timed out while preparing the managed drain thread.");
            }
            if (_drainFailure is not null)
            {
                throw new DatabentoFeedException(
                    DatabentoFeedStatus.InternalError,
                    "Managed drain startup failed: " + _drainFailure.Message);
            }

            NativeStatus.ThrowIfFailed(
                NativeMethods.FeedStart(_handle, deadline.RemainingMilliseconds),
                _handle,
                "Native feed start");
            BuildMappingsAndChannels();
            startConsumer(deadline.Remaining);
            _drainStart.Set();
            if (!_drainReady.Wait(deadline.Remaining))
            {
                throw new DatabentoFeedTimeoutException(
                    "Timed out while starting the managed drain consumer.");
            }
            if (_drainFailure is not null)
            {
                throw new DatabentoFeedException(
                    DatabentoFeedStatus.InternalError,
                    "Managed drain startup failed: " + _drainFailure.Message);
            }
            NativeStatus.ThrowIfFailed(
                NativeMethods.FeedSetConsumerReady(_handle, deadline.RemainingMilliseconds),
                _handle,
                "Native consumer-ready transition");
            _placementLease.Commit();
        }
        catch
        {
            RollBackStart(deadline);
            throw;
        }
    }

    public void Stop(TimeSpan timeout) => StopCore(timeout, CancellationToken.None, forceManagedUnblock: false);

    public void Stop(TimeSpan timeout, CancellationToken cancellationToken) =>
        StopCore(timeout, cancellationToken, forceManagedUnblock: true);

    private void StopCore(
        TimeSpan timeout,
        CancellationToken cancellationToken,
        bool forceManagedUnblock)
    {
        ValidateTimeout(timeout);
        cancellationToken.ThrowIfCancellationRequested();
        var deadline = new MonotonicDeadline(timeout);
        lock (_lifecycleGate)
        {
            ThrowIfDisposed();
            if (!_started)
            {
                throw new InvalidOperationException("The feed has not started.");
            }
            if (_stopCompleted)
            {
                return;
            }
        }

        if (forceManagedUnblock)
        {
            Volatile.Write(ref _stopRequested, 1);
            _drainStart.Set();
            WakeManagedWaiters();
        }
        if (_handle is not null && !_handle.IsInvalid && !_handle.IsClosed)
        {
            NativeStatus.ThrowIfFailed(
                NativeMethods.FeedStop(_handle, deadline.RemainingMilliseconds),
                _handle,
                "Native feed stop");
        }
        if (_drainThread is not null && !_drainThread.Join(deadline.Remaining))
        {
            throw new FeedStopDrainIncompleteException(
                "The managed final drain did not complete before the stop deadline.");
        }
        if (_drainFailure is not null)
        {
            throw new DatabentoFeedException(
                DatabentoFeedStatus.InternalError,
                "The managed drain failed: " + _drainFailure.Message);
        }
        _gcLease?.Dispose();
        _gcLease = null;
        _placementLease?.Dispose();
        _placementLease = null;
        _stopCompleted = true;
    }

    public ISynchronousBatchReader<MarketDataBatch64> GetReader(InstrumentKey instrument)
    {
        lock (_lifecycleGate)
        {
            ThrowIfDisposed();
            if (_multiplexedReaderLeased)
                throw new InvalidOperationException("The multiplexed ticker reader owns the channel consumers.");
            if (!_channels.TryGetValue(instrument, out var state))
            {
                throw new KeyNotFoundException($"No ticker reader exists for {instrument}.");
            }
            _directReaderMode = true;
            return state.Channel;
        }
    }

    public IMultiplexedTickerBatchReader GetMultiplexedReader()
    {
        lock (_lifecycleGate)
        {
            ThrowIfDisposed();
            if (_multiplexedReaderLeased)
                throw new InvalidOperationException("The multiplexed ticker reader is already leased.");
            if (_directReaderMode)
                throw new InvalidOperationException("Direct ticker readers already own the channel consumers.");
            if (_channelStates.Length == 0)
                throw new InvalidOperationException("Start the feed before acquiring its multiplexed reader.");
            _multiplexedReaderLeased = true;
            var readers = _channelStates
                .Select(static state => (state.Instrument, (ISynchronousBatchReader<MarketDataBatch64>)state.Channel))
                .ToArray();
            return new MultiplexedTickerBatchReader(readers, ReleaseMultiplexedReader, _multiplexedReady);
        }
    }

    private void ReleaseMultiplexedReader()
    {
        lock (_lifecycleGate)
            _multiplexedReaderLeased = false;
    }

    private void SignalMultiplexedReader()
    {
        try
        {
            _multiplexedReady.Release();
        }
        catch (ObjectDisposedException)
        {
            // Feed disposal is terminal; no reader remains to wake.
        }
        catch (SemaphoreFullException)
        {
            // A pending signal already guarantees that the reader will rescan.
        }
    }

    public IReadOnlyList<TickerInstrumentRegistration> GetInstruments()
    {
        lock (_lifecycleGate)
        {
            ThrowIfDisposed();
            return _registrations;
        }
    }

    public FeedHealthSnapshot GetHealth()
    {
        var native = new NativeFeedStats
        {
            StructSize = (uint)sizeof(NativeFeedStats),
            AbiVersion = NativeConstants.AbiVersion
        };
        if (_handle is not null && !_handle.IsClosed && !_handle.IsInvalid)
        {
            NativeMethods.FeedGetStats(_handle, ref native);
        }
        ulong channelFull = 0;
        ulong poolMisses = 0;
        var channelCapacity = 0;
        var channelCount = 0;
        var poolCapacity = 0;
        var poolFree = 0;
        var maximumChannelFullWait = TimeSpan.Zero;
        foreach (var state in _channelStates)
        {
            channelFull += state.Channel.FullCount;
            poolMisses += state.Channel.PoolMisses;
            channelCapacity += state.Channel.Capacity;
            channelCount += state.Channel.Count;
            poolCapacity += state.Channel.PoolCapacity;
            poolFree += state.Channel.PoolFreeCount;
            maximumChannelFullWait = TimeSpan.FromTicks(Math.Max(
                maximumChannelFullWait.Ticks,
                state.Channel.MaximumFullWait.Ticks));
        }
        var baselineReady = 0;
        foreach (var state in _channelStates)
        {
            if (!state.RequiresBaseline || Volatile.Read(ref state.BaselineReady) != 0)
            {
                baselineReady++;
            }
        }
        var transportReady = native.State == FeedState.Running
                             && native.TerminalStatus == DatabentoFeedStatus.Ok;
        return new FeedHealthSnapshot(
            native.State,
            native.TerminalStatus,
            native.RingCapacityRecords,
            native.RingUsedRecords,
            native.RingHighWaterRecords,
            native.RecordsProduced,
            native.RecordsConsumed,
            unchecked((ulong)Interlocked.Read(ref _batchesPublished)),
            channelFull,
            poolMisses,
            Interlocked.Read(ref _drainAllocatedBytes),
            _drainFailure?.Message ?? _platformWarning)
        {
            TransportReady = transportReady,
            TradingReady = transportReady && baselineReady == _channelStates.Length,
            BaselineReadyInstrumentCount = baselineReady,
            InstrumentCount = _channelStates.Length,
            ChannelBatchCapacity = channelCapacity,
            ChannelBatchCount = channelCount,
            PoolBatchCapacity = poolCapacity,
            PoolFreeBatchCount = poolFree,
            DrainPassLimitHitCount = unchecked((ulong)Interlocked.Read(
                ref _drainPassLimitHitCount)),
            MaximumChannelFullWait = maximumChannelFullWait,
            ProcessorSelection = _processorSelection,
            ResolvedNativeProducer = _resolvedNativeProducer,
            AlternateNativeProducer = _nativeProducerAlternate,
            ObservedNativeProducer = native.ProducerAffinityVerified != 0
                                     || native.ProducerProcessorSampleCount != 0
                ? new LogicalProcessorLocation(
                    native.ObservedProducerProcessorGroup,
                    native.ObservedProducerLogicalProcessor)
                : null,
            ResolvedManagedDrain = _resolvedManagedDrain,
            AlternateManagedDrain = _managedDrainAlternate,
            ObservedManagedDrain = DecodeProcessorLocation(
                Interlocked.Read(ref _observedManagedDrain)),
            NativeProducerAffinityVerified = native.ProducerAffinityVerified != 0,
            ManagedDrainAffinityVerified = _resolvedManagedDrain is not null
                                            && Interlocked.Read(ref _observedManagedDrain) >= 0,
            NativeProducerProcessorSamples = native.ProducerProcessorSampleCount,
            NativeProducerProcessorMigrations = native.ProducerProcessorMigrationCount,
            NativeProducerUniqueProcessors = native.ProducerUniqueProcessorCount,
            NativeProducerOffAssignmentSamples = native.ProducerOffAssignmentCount,
            ManagedDrainProcessorSamples = unchecked((ulong)Interlocked.Read(
                ref _managedProcessorSampleCount)),
            ManagedDrainProcessorMigrations = unchecked((ulong)Interlocked.Read(
                ref _managedProcessorMigrationCount)),
            ManagedDrainUniqueProcessors = unchecked((uint)Volatile.Read(
                ref _managedUniqueProcessorCount)),
            ManagedDrainOffAssignmentSamples = unchecked((ulong)Interlocked.Read(
                ref _managedOffAssignmentCount)),
            DrainDiagnostics = GetDrainDiagnostics()
        };
    }

    private FeedDrainDiagnostics GetDrainDiagnostics()
    {
        var currentIndex = Volatile.Read(ref _currentNativeReadRecordIndex);
        return new FeedDrainDiagnostics
        {
            Stage = (FeedDrainStage)Volatile.Read(ref _drainStage),
            NativeReadCallCount = Interlocked.Read(ref _nativeReadCallCount),
            LastNativeReadRecordCount = unchecked((uint)Volatile.Read(
                ref _lastNativeReadRecordCount)),
            LastNativeReadFirstSequence = unchecked((ulong)Interlocked.Read(
                ref _lastNativeReadFirstSequence)),
            LastNativeReadLastSequence = unchecked((ulong)Interlocked.Read(
                ref _lastNativeReadLastSequence)),
            LastNativeReadRecordsRouted = unchecked((uint)Volatile.Read(
                ref _lastNativeReadRecordsRouted)),
            CurrentNativeReadRecordIndex = currentIndex,
            CurrentRecordKind = currentIndex < 0
                ? string.Empty
                : ((MarketRecordKind)Volatile.Read(ref _currentRecordKind)).ToString(),
            CurrentPublisherId = unchecked((ushort)Volatile.Read(ref _currentRecordPublisherId)),
            CurrentInstrumentId = unchecked((uint)Volatile.Read(ref _currentRecordInstrumentId)),
            CurrentSourceSequence = unchecked((uint)Volatile.Read(ref _currentRecordSourceSequence)),
            ManagedBatchPublishActive = Volatile.Read(ref _managedBatchPublishActive) != 0,
            ManagedBatchPublishRecordCount = Volatile.Read(ref _managedBatchPublishRecordCount),
            ManagedBatchPublisherId = unchecked((ushort)Volatile.Read(
                ref _managedBatchPublisherId)),
            ManagedBatchInstrumentId = unchecked((uint)Volatile.Read(
                ref _managedBatchInstrumentId))
        };
    }

    public void Dispose()
    {
        lock (_lifecycleGate)
        {
            if (_disposed)
            {
                return;
            }
            if (_drainThread is { IsAlive: true } || _started && !_stopCompleted)
            {
                throw new InvalidOperationException("Call Stop(timeout) before disposing an active feed.");
            }
            _disposed = true;
        }
        foreach (var state in _channelStates)
        {
            state.Channel.Complete();
            state.Channel.DrainUnread();
        }
        _handle?.Dispose();
        _handle = null;
        _gcLease?.Dispose();
        _gcLease = null;
        _placementLease?.Dispose();
        _placementLease = null;
        _drainAllocated.Dispose();
        _drainStart.Dispose();
        _drainReady.Dispose();
        _multiplexedReady.Dispose();
    }

    private SafeDbFeedHandle CreateAndSubscribeNative(MonotonicDeadline deadline)
    {
        var dataset = Encoding.UTF8.GetBytes(_options.Dataset);
        var config = new NativeFeedConfig
        {
            StructSize = (uint)sizeof(NativeFeedConfig),
            AbiVersion = NativeConstants.AbiVersion,
            DataSource = (uint)_options.DataSource,
            FeedKind = _singleChannel ? 2u : 1u,
            RingMemoryBytes = checked((ulong)_options.RingMemoryBytes),
            SpinIterations = checked((uint)_options.RingBackpressure.SpinIterations),
            RingFullTimeoutMicroseconds = checked((uint)Math.Ceiling(
                _options.RingBackpressure.RingFullTimeout.TotalMicroseconds)),
            SyntheticRecordCount = checked((uint)_options.Synthetic.RecordCount),
            SyntheticRecordsPerSecond = checked((uint)_options.Synthetic.RecordsPerSecond),
            SyntheticInstrumentCount = checked((uint)_subscriptions!.Length),
            HeartbeatIntervalMilliseconds = checked((uint)_options.TransportHealth.HeartbeatInterval.TotalMilliseconds),
            Flags = BuildNativeFlags(
                _options.Memory,
                _options.ThreadPriority,
                _options.Numa,
                _options.ProcessorResidency),
            ProducerProcessorGroup = _placementLease!.NativeProducer?.ProcessorGroup ?? 0,
            ProducerLogicalProcessor = _placementLease.NativeProducer?.LogicalProcessorIndex
                                       ?? NativeConstants.UnpinnedProcessor,
            DrainProcessorGroup = _placementLease.ManagedDrain?.ProcessorGroup ?? 0,
            DrainLogicalProcessor = _placementLease.ManagedDrain?.LogicalProcessorIndex
                                    ?? NativeConstants.UnpinnedProcessor,
            ProducerPriority = (int)_options.ThreadPriority.NativeProducer,
            DrainPriority = (int)_options.ThreadPriority.ManagedDrain,
            NumaNode = _options.Numa.Mode == NumaLocalityMode.Disabled
                ? ushort.MaxValue
                : _options.Numa.Node ?? _placementLease.NumaNode ?? ushort.MaxValue,
            DatasetLength = checked((uint)dataset.Length),
            SyntheticStartSequence = _options.Synthetic.StartSequence,
            ForcedMigrationIntervalRecords = checked((uint)
                _options.ProcessorResidency.ForcedMigrationIntervalRecords),
            StatisticsReplayStartTimestampNanoseconds =
                _options.StatisticsReplayStartTimestampNanoseconds,
            TradeReplayStartTimestampNanoseconds =
                _options.TradeReplayStartTimestampNanoseconds,
            ProducerAlternateProcessorGroup =
                _placementLease.NativeProducerAlternate?.ProcessorGroup ?? 0,
            ProducerAlternateLogicalProcessor =
                _placementLease.NativeProducerAlternate?.LogicalProcessorIndex
                ?? NativeConstants.UnpinnedProcessor,
            DrainAlternateProcessorGroup =
                _placementLease.ManagedDrainAlternate?.ProcessorGroup ?? 0,
            DrainAlternateLogicalProcessor =
                _placementLease.ManagedDrainAlternate?.LogicalProcessorIndex
                ?? NativeConstants.UnpinnedProcessor
        };
        nint rawHandle;
        fixed (byte* datasetPointer = dataset)
        {
            NativeStatus.ThrowIfFailed(
                NativeMethods.FeedCreate(
                    &config,
                    datasetPointer,
                    (uint)dataset.Length,
                    out rawHandle),
                null,
                "Native feed creation");
        }
        var handle = new SafeDbFeedHandle(rawHandle);
        try
        {
            if (_singleChannel)
            {
                SubscribeNativeOptionChain(handle, deadline);
            }
            else
            {
                SubscribeNative(handle, deadline);
            }
            return handle;
        }
        catch
        {
            handle.Dispose();
            throw;
        }
    }

    private void SubscribeNative(SafeDbFeedHandle handle, MonotonicDeadline deadline)
    {
        var totalBytes = 0;
        foreach (var subscription in _subscriptions!)
        {
            totalBytes = checked(totalBytes + Encoding.UTF8.GetByteCount(subscription.Symbol));
        }
        var blob = new byte[totalBytes];
        var nativeSubscriptions = new NativeTickerSubscription[_subscriptions!.Length];
        var offset = 0;
        for (var index = 0; index < _subscriptions.Length; index++)
        {
            var subscription = _subscriptions[index];
            var bytes = Encoding.UTF8.GetBytes(subscription.Symbol, blob.AsSpan(offset));
            nativeSubscriptions[index] = new NativeTickerSubscription
            {
                StructSize = (uint)sizeof(NativeTickerSubscription),
                AbiVersion = NativeConstants.AbiVersion,
                SymbolOffset = checked((uint)offset),
                SymbolLength = checked((uint)bytes),
                InputSymbology = (uint)subscription.InputSymbology,
                DataKinds = (uint)subscription.DataKinds
            };
            offset += bytes;
        }
        fixed (NativeTickerSubscription* subscriptionPointer = nativeSubscriptions)
        fixed (byte* blobPointer = blob)
        {
            NativeStatus.ThrowIfFailed(
                NativeMethods.FeedSubscribeTickers(
                    handle,
                    subscriptionPointer,
                    (uint)nativeSubscriptions.Length,
                    blobPointer,
                    (uint)blob.Length,
                    deadline.RemainingMilliseconds),
                handle,
                "Native ticker subscription");
        }
    }

    private void SubscribeNativeOptionChain(
        SafeDbFeedHandle handle,
        MonotonicDeadline deadline)
    {
        var contracts = _optionContracts!;
        var totalBytes = 0;
        foreach (var contract in contracts)
        {
            totalBytes = checked(totalBytes + Encoding.UTF8.GetByteCount(contract.RawSymbol));
        }
        var blob = new byte[totalBytes];
        var nativeContracts = new NativeOptionContractSelection[contracts.Length];
        var offset = 0;
        for (var index = 0; index < contracts.Length; index++)
        {
            var contract = contracts[index];
            var bytes = Encoding.UTF8.GetBytes(contract.RawSymbol, blob.AsSpan(offset));
            nativeContracts[index] = new NativeOptionContractSelection
            {
                StructSize = (uint)sizeof(NativeOptionContractSelection),
                AbiVersion = NativeConstants.AbiVersion,
                InstrumentId = contract.Instrument.InstrumentId,
                PublisherId = contract.Instrument.PublisherId,
                OptionRight = (byte)contract.Right,
                RawSymbolOffset = checked((uint)offset),
                RawSymbolLength = checked((uint)bytes)
            };
            offset += bytes;
        }
        var subscription = new NativeOptionChainSubscription
        {
            StructSize = (uint)sizeof(NativeOptionChainSubscription),
            AbiVersion = NativeConstants.AbiVersion,
            DataKinds = (uint)_optionDataKinds,
            ContractCount = (uint)contracts.Length
        };
        fixed (NativeOptionContractSelection* contractPointer = nativeContracts)
        fixed (byte* blobPointer = blob)
        {
            NativeStatus.ThrowIfFailed(
                NativeMethods.FeedSubscribeOptionChain(
                    handle,
                    &subscription,
                    contractPointer,
                    (uint)nativeContracts.Length,
                    blobPointer,
                    (uint)blob.Length,
                    deadline.RemainingMilliseconds),
                handle,
                "Native option-chain subscription");
        }
    }

    private void BuildMappingsAndChannels()
    {
        NativeStatus.ThrowIfFailed(
            NativeMethods.FeedGetTickerMappingCounts(
                _handle!, out var mappingCount, out var blobBytes),
            _handle,
            "Ticker mapping count");
        var mappings = new NativeTickerInstrumentMapping[mappingCount];
        var blob = new byte[blobBytes];
        fixed (NativeTickerInstrumentMapping* mappingPointer = mappings)
        fixed (byte* blobPointer = blob)
        {
            NativeStatus.ThrowIfFailed(
                NativeMethods.FeedCopyTickerMappings(
                    _handle!, mappingPointer, mappingCount, blobPointer, blobBytes),
                _handle,
                "Ticker mapping copy");
        }

        var registrations = new TickerInstrumentRegistration[mappingCount];
        var channels = new Dictionary<InstrumentKey, ChannelState>(checked((int)mappingCount));
        var channelsByInstrumentId = new Dictionary<uint, ChannelState>(checked((int)mappingCount));
        var channelSlots = _options.ManagedChannelRecordCapacity
                           / _options.ManagedBatchRecordCapacity;
        ChannelState? sharedState = null;
        if (_singleChannel)
        {
            sharedState = new ChannelState
            {
                Instrument = default,
                Channel = new BoundedBatchChannel(
                    channelSlots,
                    _options.ManagedBatchRecordCapacity,
                    SignalMultiplexedReader),
                RequiresBaseline = (_optionDataKinds
                                    & (MarketDataKinds.Quote
                                       | MarketDataKinds.MboOrderUpdate)) != 0
            };
        }
        var states = _singleChannel
            ? [sharedState!]
            : new ChannelState[mappingCount];
        for (var index = 0; index < mappings.Length; index++)
        {
            var mapping = mappings[index];
            var key = new InstrumentKey(mapping.PublisherId, mapping.InstrumentId);
            var state = sharedState ?? new ChannelState
                {
                    Instrument = key,
                    Channel = new BoundedBatchChannel(
                        channelSlots,
                        _options.ManagedBatchRecordCapacity,
                        SignalMultiplexedReader),
                    RequiresBaseline = (_subscriptions![mapping.SubscriptionIndex].DataKinds
                                        & (MarketDataKinds.Quote
                                           | MarketDataKinds.MboOrderUpdate)) != 0
                };
            if (!channels.TryAdd(key, state))
            {
                throw new InvalidOperationException($"Duplicate native instrument mapping {key}.");
            }
            if (channelsByInstrumentId.TryGetValue(key.InstrumentId, out var existingState))
            {
                if (!ReferenceEquals(existingState, state))
                {
                    throw new InvalidOperationException(
                        $"Native instrument {key.InstrumentId} maps to multiple ticker channels.");
                }
            }
            else
            {
                channelsByInstrumentId.Add(key.InstrumentId, state);
            }
            if (!_singleChannel)
            {
                states[index] = state;
            }
            registrations[index] = new TickerInstrumentRegistration(
                Decode(blob, mapping.RequestedSymbolOffset, mapping.RequestedSymbolLength),
                Decode(blob, mapping.RawSymbolOffset, mapping.RawSymbolLength),
                key);
        }
        _channels = channels;
        _channelsByInstrumentId = channelsByInstrumentId;
        _channelStates = states;
        Array.Sort(registrations, static (left, right) =>
        {
            var symbolOrder = StringComparer.Ordinal.Compare(
                left.RequestedSymbol,
                right.RequestedSymbol);
            if (symbolOrder != 0)
            {
                return symbolOrder;
            }
            var publisherOrder = left.Instrument.PublisherId.CompareTo(
                right.Instrument.PublisherId);
            return publisherOrder != 0
                ? publisherOrder
                : left.Instrument.InstrumentId.CompareTo(right.Instrument.InstrumentId);
        });
        _registrations = Array.AsReadOnly(registrations);
    }

    private void DrainThreadMain()
    {
        try
        {
            ApplyManagedDrainSettings();
            NativeStatus.ThrowIfFailed(
                NativeMethods.FeedAllocateReadBuffer(
                    _handle!,
                    checked((uint)_options.Drain.NativeReadRecordCapacity),
                    out _readBuffer),
                _handle,
                "Native read-buffer allocation");
            _drainAllocated.Set();
            _drainStart.Wait();
            _drainReady.Set();
            Interlocked.Exchange(ref _drainAllocatedBytes, DrainLoop());
        }
        catch (OperationCanceledException) when (IsStopping())
        {
            Volatile.Write(ref _drainStage, (int)FeedDrainStage.Completed);
            _drainAllocated.Set();
            _drainReady.Set();
            foreach (var state in _channelStates)
            {
                state.Channel.Complete();
            }
        }
        catch (Exception exception)
        {
            Volatile.Write(ref _drainStage, (int)FeedDrainStage.Faulted);
            _drainFailure = exception;
            _drainAllocated.Set();
            _drainReady.Set();
            foreach (var state in _channelStates)
            {
                state.Channel.Complete(exception);
            }
        }
        finally
        {
            if (_readBuffer != null && _handle is not null
                && !_handle.IsInvalid && !_handle.IsClosed)
            {
                var status = NativeMethods.FeedFreeReadBuffer(_handle, _readBuffer);
                if (status != DatabentoFeedStatus.Ok && _drainFailure is null)
                {
                    _drainFailure = new DatabentoFeedException(
                        status,
                        "Native read-buffer release failed.");
                }
                _readBuffer = null;
            }
        }
    }

    private long DrainLoop()
    {
        long allocationBaseline = -1;
        while (true)
        {
            Volatile.Write(ref _drainStage, (int)FeedDrainStage.WaitingForNativeSignal);
            var wait = new NativeWaitResult
            {
                StructSize = (uint)sizeof(NativeWaitResult),
                AbiVersion = NativeConstants.AbiVersion
            };
            NativeStatus.ThrowIfFailed(
                NativeMethods.FeedWait(_handle!, NativeConstants.WaitInfinite, ref wait),
                _handle,
                "Native feed wait");
            var recordsThisPass = 0;
            while (recordsThisPass < _options.Drain.MaxRecordsPerDrainPass)
            {
                Volatile.Write(ref _drainStage, (int)FeedDrainStage.ReadingNativeBatch);
                var readCapacity = Math.Min(
                    _options.Drain.NativeReadRecordCapacity,
                    _options.Drain.MaxRecordsPerDrainPass - recordsThisPass);
                var batch = new NativeBatchResult
                {
                    StructSize = (uint)sizeof(NativeBatchResult),
                    AbiVersion = NativeConstants.AbiVersion
                };
                NativeStatus.ThrowIfFailed(
                    NativeMethods.FeedReadBatch(
                        _handle!, _readBuffer, checked((uint)readCapacity), ref batch),
                    _handle,
                    "Native batch read");
                Interlocked.Increment(ref _nativeReadCallCount);
                if (batch.RecordsRead != 0)
                {
                    Volatile.Write(
                        ref _lastNativeReadRecordCount,
                        checked((int)batch.RecordsRead));
                    Interlocked.Exchange(
                        ref _lastNativeReadFirstSequence,
                        unchecked((long)batch.FirstSequence));
                    Interlocked.Exchange(
                        ref _lastNativeReadLastSequence,
                        unchecked((long)batch.LastSequence));
                    Volatile.Write(ref _lastNativeReadRecordsRouted, 0);
                }
                Volatile.Write(ref _drainStage, (int)FeedDrainStage.RoutingNativeRecord);
                for (var index = 0u; index < batch.RecordsRead; index++)
                {
                    var record = _readBuffer[index];
                    Volatile.Write(ref _currentNativeReadRecordIndex, checked((int)index));
                    Volatile.Write(ref _currentRecordKind, (int)record.Header.RecordKind);
                    Volatile.Write(ref _currentRecordPublisherId, record.Header.PublisherId);
                    Volatile.Write(
                        ref _currentRecordInstrumentId,
                        unchecked((int)record.Header.InstrumentId));
                    Volatile.Write(
                        ref _currentRecordSourceSequence,
                        unchecked((int)record.Header.Sequence));
                    RecordManagedProcessorResidency();
                    RouteRecord(record);
                    Volatile.Write(ref _lastNativeReadRecordsRouted, checked((int)index + 1));
                }
                Volatile.Write(ref _currentNativeReadRecordIndex, -1);
                Volatile.Write(ref _currentRecordKind, 0);
                Volatile.Write(ref _currentRecordPublisherId, 0);
                Volatile.Write(ref _currentRecordInstrumentId, 0);
                Volatile.Write(ref _currentRecordSourceSequence, 0);
                if (allocationBaseline < 0 && batch.RecordsRead != 0)
                {
                    var warmStats = new NativeFeedStats
                    {
                        StructSize = (uint)sizeof(NativeFeedStats),
                        AbiVersion = NativeConstants.AbiVersion
                    };
                    NativeStatus.ThrowIfFailed(
                        NativeMethods.FeedGetStats(_handle!, ref warmStats),
                        _handle,
                        "Native feed statistics warm-up");
                    allocationBaseline = GC.GetAllocatedBytesForCurrentThread();
                }
                recordsThisPass += checked((int)batch.RecordsRead);
                if (batch.RecordsRead == 0 || batch.MoreAvailable == 0)
                {
                    break;
                }
            }
            Volatile.Write(ref _drainStage, (int)FeedDrainStage.FlushingPartialBatches);
            FlushPartialBatches();

            if (recordsThisPass == _options.Drain.MaxRecordsPerDrainPass)
            {
                Interlocked.Increment(ref _drainPassLimitHitCount);
            }

            Volatile.Write(ref _drainStage, (int)FeedDrainStage.ReadingNativeStatistics);
            var stats = new NativeFeedStats
            {
                StructSize = (uint)sizeof(NativeFeedStats),
                AbiVersion = NativeConstants.AbiVersion
            };
            NativeStatus.ThrowIfFailed(
                NativeMethods.FeedGetStats(_handle!, ref stats),
                _handle,
                "Native feed statistics");
            if (stats.RingUsedRecords == 0
                && stats.State is FeedState.Stopped or FeedState.Faulted)
            {
                Exception? error = stats.State == FeedState.Faulted
                    ? new DatabentoFeedException(
                        stats.TerminalStatus,
                        $"Native Databento feed faulted with {stats.TerminalStatus}.")
                    : null;
                foreach (var state in _channelStates)
                {
                    state.Channel.Complete(error);
                }
                Volatile.Write(ref _drainStage, (int)FeedDrainStage.Completed);
                return allocationBaseline < 0
                    ? 0
                    : GC.GetAllocatedBytesForCurrentThread() - allocationBaseline;
            }
        }
    }

    private void RouteRecord(in MarketRecord64 record)
    {
        var key = new InstrumentKey(record.Header.PublisherId, record.Header.InstrumentId);
        if (!_channels.TryGetValue(key, out var state)
            && !_channelsByInstrumentId.TryGetValue(key.InstrumentId, out state))
        {
            throw new InvalidDataException($"Native record referenced unknown instrument {key}.");
        }
        if (record.Header.RecordKind is MarketRecordKind.Quote or MarketRecordKind.Mbo)
        {
            Volatile.Write(ref state.BaselineReady, 1);
        }
        state.AssemblyBatch ??= state.Channel.RentBatch(_isStopping);
        state.AssemblyBatch.Add(record);
        if (state.AssemblyBatch.IsFull)
        {
            Publish(state);
        }
    }

    private void FlushPartialBatches()
    {
        foreach (var state in _channelStates)
        {
            if (state.AssemblyBatch is { Count: > 0 })
            {
                Publish(state);
            }
        }
    }

    private void Publish(ChannelState state)
    {
        var batch = state.AssemblyBatch!;
        state.AssemblyBatch = null;
        var priorStage = Volatile.Read(ref _drainStage);
        var batchInstrument = batch.Count == 0
            ? state.Instrument
            : new InstrumentKey(
                batch.Records[0].Header.PublisherId,
                batch.Records[0].Header.InstrumentId);
        Volatile.Write(ref _managedBatchPublishRecordCount, batch.Count);
        Volatile.Write(ref _managedBatchPublisherId, batchInstrument.PublisherId);
        Volatile.Write(
            ref _managedBatchInstrumentId,
            unchecked((int)batchInstrument.InstrumentId));
        Volatile.Write(ref _managedBatchPublishActive, 1);
        Volatile.Write(ref _drainStage, (int)FeedDrainStage.PublishingManagedBatch);
        try
        {
            if (!state.Channel.Publish(batch, _isStopping))
            {
                state.Channel.ReturnUnpublished(batch);
                throw new OperationCanceledException("Managed channel publication was interrupted.");
            }
            Interlocked.Increment(ref _batchesPublished);
        }
        finally
        {
            Volatile.Write(ref _drainStage, priorStage);
            Volatile.Write(ref _managedBatchPublishActive, 0);
        }
    }

    private void ApplyManagedDrainSettings()
    {
        if (OperatingSystem.IsWindows())
        {
            if (_placementLease?.ManagedDrain is { } location)
            {
                RecordObservedManagedDrain(WindowsThreadAffinity.Apply(location));
            }
            try
            {
                Thread.CurrentThread.Priority = MapThreadPriority(
                    _options.ThreadPriority.ManagedDrain);
            }
            catch (Exception exception) when (!_options.ThreadPriority.RequireConfiguredPriority)
            {
                _platformWarning = "Managed drain priority was not applied: " + exception.Message;
            }
            return;
        }
        if (OperatingSystem.IsLinux())
        {
            try
            {
                var observed = LinuxThreadConfiguration.Apply(
                    _placementLease?.ManagedDrain,
                    _options.ThreadPriority.ManagedDrain);
                if (observed is { } location)
                {
                    RecordObservedManagedDrain(location);
                }
            }
            catch (Exception exception) when (!_options.ThreadPriority.RequireConfiguredPriority)
            {
                _platformWarning = "Managed drain Linux settings were not fully applied: "
                                   + exception.Message;
            }
            return;
        }
        if (_options.ThreadPriority.RequireConfiguredPriority
            || _placementLease?.ManagedDrain is not null)
        {
            throw new PlatformNotSupportedException(
                "Managed drain affinity and priority are supported only on Windows and Linux.");
        }
    }

    private static ThreadPriority MapThreadPriority(FeedThreadPriority priority) => priority switch
    {
        FeedThreadPriority.Normal => ThreadPriority.Normal,
        FeedThreadPriority.AboveNormal => ThreadPriority.AboveNormal,
        FeedThreadPriority.Highest => ThreadPriority.Highest,
        _ => throw new ArgumentOutOfRangeException(nameof(priority))
    };

    private void RecordObservedManagedDrain(LogicalProcessorLocation location)
    {
        var packed = ((long)location.ProcessorGroup << 32)
                     | location.LogicalProcessorIndex;
        Interlocked.Exchange(ref _observedManagedDrain, packed);
    }

    private void RecordManagedProcessorResidency()
    {
        if (_managedObservedProcessors is null)
        {
            return;
        }
        ApplyManagedForcedMigrationIfRequired();
        var location = CurrentProcessor.Get();
        var packed = ((long)location.ProcessorGroup << 32)
                     | location.LogicalProcessorIndex;
        Interlocked.CompareExchange(ref _observedManagedDrain, packed, -1);
        var previous = _managedLastProcessor;
        if (previous >= 0 && previous != packed)
        {
            _managedProcessorMigrationCount++;
        }
        _managedLastProcessor = packed;
        var processorId = location.ProcessorGroup * 64 + location.LogicalProcessorIndex;
        var wordIndex = processorId / 64;
        if (wordIndex < _managedObservedProcessors.Length)
        {
            var mask = 1UL << (processorId % 64);
            if ((_managedObservedProcessors[wordIndex] & mask) == 0)
            {
                _managedObservedProcessors[wordIndex] |= mask;
                _managedUniqueProcessorCount++;
            }
        }
        if (_resolvedManagedDrain is { } assigned && assigned != location)
        {
            _managedOffAssignmentCount++;
        }
        _managedProcessorSampleCount++;
    }

    private void ApplyManagedForcedMigrationIfRequired()
    {
        var interval = _options.ProcessorResidency.ForcedMigrationIntervalRecords;
        if (interval <= 0
            || _managedProcessorSampleCount == 0
            || _managedProcessorSampleCount % interval != 0)
        {
            return;
        }
        _managedDrainUsingAlternate = !_managedDrainUsingAlternate;
        var target = _managedDrainUsingAlternate
            ? _managedDrainAlternate
            : _resolvedManagedDrain;
        if (target is not { } location)
        {
            throw new InvalidOperationException(
                "Managed forced migration has no resolved processor target.");
        }
        if (OperatingSystem.IsWindows())
        {
            WindowsThreadAffinity.Apply(location);
        }
        else
        {
            LinuxThreadConfiguration.ApplyAffinity(location);
        }
    }

    private static LogicalProcessorLocation? DecodeProcessorLocation(long packed) =>
        packed < 0
            ? null
            : new LogicalProcessorLocation(
                checked((ushort)(packed >> 32)),
                checked((ushort)(packed & ushort.MaxValue)));

    private static uint BuildNativeFlags(
        FeedMemoryOptions memory,
        FeedThreadPriorityOptions priority,
        FeedNumaOptions numa,
        FeedProcessorResidencyOptions processorResidency)
    {
        uint flags = 0;
        if (memory.LockRingMemory)
        {
            flags |= 1;
        }
        if (memory.RequireLockedMemory)
        {
            flags |= 2;
        }
        if (memory.RequireBasePagePolicy)
        {
            flags |= 4;
        }
        if (priority.RequireConfiguredPriority)
        {
            flags |= 8;
        }
        if (numa.RequireNumaLocality)
        {
            flags |= 16;
        }
        if (processorResidency.EnableTracking)
        {
            flags |= 32;
        }
        return flags;
    }

    private static string Decode(byte[] blob, uint offset, ushort length)
    {
        if (offset > blob.Length || length > blob.Length - offset)
        {
            throw new InvalidDataException("Native ticker mapping contained an invalid UTF-8 range.");
        }
        return Encoding.UTF8.GetString(blob, checked((int)offset), length);
    }

    private void RollBackStart(MonotonicDeadline deadline)
    {
        Volatile.Write(ref _stopRequested, 1);
        _drainStart.Set();
        WakeManagedWaiters();
        if (_handle is not null && !_handle.IsInvalid && !_handle.IsClosed)
        {
            NativeMethods.FeedStop(_handle, deadline.RemainingMilliseconds);
        }
        _drainThread?.Join(deadline.Remaining);
        _gcLease?.Dispose();
        _gcLease = null;
        _placementLease?.Dispose();
        _placementLease = null;
        if (_drainThread is not { IsAlive: true })
        {
            _handle?.Dispose();
            _handle = null;
            _stopCompleted = true;
        }
    }

    private static void ValidateTimeout(TimeSpan timeout)
    {
        if (timeout <= TimeSpan.Zero && timeout != Timeout.InfiniteTimeSpan)
        {
            throw new ArgumentOutOfRangeException(nameof(timeout));
        }
    }

    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(_disposed, this);

    private bool IsStopping() => Volatile.Read(ref _stopRequested) != 0;

    private void WakeManagedWaiters()
    {
        foreach (var state in _channelStates)
        {
            state.Channel.WakeWaiters();
        }
    }
}

internal readonly struct MonotonicDeadline
{
    private readonly long _started;
    private readonly TimeSpan _timeout;

    internal MonotonicDeadline(TimeSpan timeout)
    {
        _started = Stopwatch.GetTimestamp();
        _timeout = timeout;
    }

    internal TimeSpan Remaining
    {
        get
        {
            if (_timeout == Timeout.InfiniteTimeSpan)
            {
                return Timeout.InfiniteTimeSpan;
            }
            var remaining = _timeout - Stopwatch.GetElapsedTime(_started);
            return remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero;
        }
    }

    internal uint RemainingMilliseconds
    {
        get
        {
            var remaining = Remaining;
            if (remaining == Timeout.InfiniteTimeSpan)
            {
                return NativeConstants.WaitInfinite;
            }
            if (remaining <= TimeSpan.Zero)
            {
                return 0;
            }
            return checked((uint)Math.Clamp(
                Math.Ceiling(remaining.TotalMilliseconds),
                1,
                uint.MaxValue - 1d));
        }
    }
}
