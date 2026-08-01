using System.Diagnostics;
using System.Text;
using TomasAI.IFM.Framework.MarketData.DataBento.Interop;

namespace TomasAI.IFM.Framework.MarketData.DataBento;

internal sealed unsafe class SyntheticTickerFeed : IDatabentoTickerFeed
{
    private static readonly Func<bool> NeverStopping = static () => false;

    private sealed class ChannelState
    {
        internal required InstrumentKey Instrument { get; init; }
        internal required BoundedBatchChannel Channel { get; init; }
        internal MarketDataBatch64? AssemblyBatch;
    }

    private readonly object _lifecycleGate = new();
    private readonly DatabentoFeedOptions _options;
    private readonly bool _singleChannel;
    private readonly ManualResetEventSlim _drainAllocated = new(false);
    private readonly ManualResetEventSlim _drainStart = new(false);
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
    private ChannelState[] _channelStates = [];
    private bool _started;
    private bool _stopCompleted;
    private bool _disposed;
    private long _batchesPublished;
    private long _drainAllocatedBytes;
    private string? _platformWarning;

    internal SyntheticTickerFeed(DatabentoFeedOptions options, bool singleChannel = false)
    {
        _options = options;
        _singleChannel = singleChannel;
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
            var copy = subscriptions.ToArray();
            var symbols = new HashSet<string>(StringComparer.Ordinal);
            foreach (var subscription in copy)
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
                                                   | MarketDataKinds.MboOrderUpdate)) != 0)
                {
                    throw new ArgumentException("A subscription contains invalid market-data kinds.");
                }
                if (!symbols.Add(subscription.Symbol))
                {
                    throw new ArgumentException($"Duplicate ticker subscription '{subscription.Symbol}'.");
                }
            }
            _subscriptions = copy;
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
                                  | MarketDataKinds.MboOrderUpdate)) != 0)
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

    public void Start(TimeSpan timeout)
    {
        ValidateTimeout(timeout);
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
                _options.Numa);
            _handle = CreateAndSubscribeNative(deadline);
            _drainThread = new Thread(DrainThreadMain)
            {
                IsBackground = true,
                Name = $"Databento synthetic drain: {_options.Dataset}",
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
            NativeStatus.ThrowIfFailed(
                NativeMethods.FeedSetConsumerReady(_handle, deadline.RemainingMilliseconds),
                _handle,
                "Native consumer-ready transition");
            _placementLease.Commit();
            _drainStart.Set();
        }
        catch
        {
            RollBackStart(deadline);
            throw;
        }
    }

    public void Stop(TimeSpan timeout)
    {
        ValidateTimeout(timeout);
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

        _drainStart.Set();
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
            if (!_channels.TryGetValue(instrument, out var state))
            {
                throw new KeyNotFoundException($"No ticker reader exists for {instrument}.");
            }
            return state.Channel;
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
        foreach (var state in _channelStates)
        {
            channelFull += state.Channel.FullCount;
            poolMisses += state.Channel.PoolMisses;
        }
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
            _drainFailure?.Message ?? _platformWarning);
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
    }

    private SafeDbFeedHandle CreateAndSubscribeNative(MonotonicDeadline deadline)
    {
        var dataset = Encoding.UTF8.GetBytes(_options.Dataset);
        var config = new NativeFeedConfig
        {
            StructSize = (uint)sizeof(NativeFeedConfig),
            AbiVersion = NativeConstants.AbiVersion,
            DataSource = 1,
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
                _options.Numa),
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
            SyntheticStartSequence = _options.Synthetic.StartSequence
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
                    _options.ManagedBatchRecordCapacity)
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
                        _options.ManagedBatchRecordCapacity)
                };
            if (!channels.TryAdd(key, state))
            {
                throw new InvalidOperationException($"Duplicate native instrument mapping {key}.");
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
        _channelStates = states;
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
            Interlocked.Exchange(ref _drainAllocatedBytes, DrainLoop());
        }
        catch (Exception exception)
        {
            _drainFailure = exception;
            _drainAllocated.Set();
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
                for (var index = 0u; index < batch.RecordsRead; index++)
                {
                    RouteRecord(_readBuffer[index]);
                }
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
            FlushPartialBatches();

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
                        $"Native synthetic feed faulted with {stats.TerminalStatus}.")
                    : null;
                foreach (var state in _channelStates)
                {
                    state.Channel.Complete(error);
                }
                return allocationBaseline < 0
                    ? 0
                    : GC.GetAllocatedBytesForCurrentThread() - allocationBaseline;
            }
        }
    }

    private void RouteRecord(in MarketRecord64 record)
    {
        var key = new InstrumentKey(record.Header.PublisherId, record.Header.InstrumentId);
        if (!_channels.TryGetValue(key, out var state))
        {
            throw new InvalidDataException($"Native record referenced unknown instrument {key}.");
        }
        state.AssemblyBatch ??= state.Channel.RentBatch(NeverStopping);
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
        if (!state.Channel.Publish(batch, NeverStopping))
        {
            state.Channel.ReturnUnpublished(batch);
            throw new OperationCanceledException("Managed channel publication was interrupted.");
        }
        Interlocked.Increment(ref _batchesPublished);
    }

    private void ApplyManagedDrainSettings()
    {
        if (OperatingSystem.IsWindows())
        {
            if (_placementLease?.ManagedDrain is { } location)
            {
                WindowsThreadAffinity.Apply(location);
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
                LinuxThreadConfiguration.Apply(
                    _placementLease?.ManagedDrain,
                    _options.ThreadPriority.ManagedDrain);
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

    private static uint BuildNativeFlags(
        FeedMemoryOptions memory,
        FeedThreadPriorityOptions priority,
        FeedNumaOptions numa)
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
        _drainStart.Set();
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
