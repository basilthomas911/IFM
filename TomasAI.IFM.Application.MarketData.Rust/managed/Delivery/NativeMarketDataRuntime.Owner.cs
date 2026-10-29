using System.Text.Json;
using TomasAI.IFM.Application.MarketData.Rust.Interop;

namespace TomasAI.IFM.Application.MarketData.Rust.Delivery;

/// <summary>Copied native control ticket and readiness. This never owns a dataset lease.</summary>
public sealed record NativeDatasetOwnerHealth(MarketDataLane Lane, uint Phase, uint MissingProofs,
    ulong Episode, ulong Revision, ulong Generation, ulong RemainingMilliseconds, ulong PendingFailures);

/// <summary>Retained bounded terminal evidence, consumed only after UI delivery is confirmed.</summary>
public sealed record NativeDatasetOwnerFailure(uint Lane, ulong Ordinal, ulong[] Ticket, string? Root, string? Detail, ulong Replaced);

/// <summary>Actual native publication owner, retained during setup/retirement.
/// Presence of an identity alone does not mean the dataset is healthy.</summary>
public sealed record NativeDatasetOwnerIdentity(MarketDataLane Lane, DateOnly ValueDate, Guid Worker, Guid Generation, long Revision);

/// <summary>Separates accepted feed demand from an existing publication identity.
/// Healthy idle control is not reported as an active provider connection.</summary>
public sealed record NativeDatasetOwnerPublication(MarketDataLane Lane, bool HasDesired, NativeDatasetOwnerIdentity? Identity);

/// <summary>Copied counts of retained old evidence and exact bounded-retention losses.
/// Records are provider trade prints, not user executions or order instructions.</summary>
public sealed record NativeRetiredEvidenceStatus(ulong RetainedGenerations,ulong EvictedGenerations,ulong EvictedRecords,ulong EvictedBytes);

/// <summary>Command/query-only marshaling for native dataset control. No provider
/// lifecycle, renewal, reference preparation, stream drain or recovery timer lives here.</summary>
public sealed partial class NativeMarketDataRuntime
{
    static readonly JsonSerializerOptions OwnerJson = new(JsonSerializerDefaults.Web);
    /// <summary>True only when the package supplies the complete native owner command/query ABI.</summary>
    public bool SupportsDatasetOwner => (library.OwnerFeatures & 0x7fff) == 0x7fff && library.CreateOwner is not null && library.OwnerCatalog is not null
        && library.OwnerCapture is not null && library.OwnerRecoveryRequest is not null && library.OwnerStart is not null && library.OwnerReadSnapshot is not null && library.OwnerBackendStatus is not null && library.OwnerRetiredEvidence is not null && library.OwnerCommand is not null && library.OwnerFutures is not null && library.OwnerHealth is not null
        && library.OwnerFailures is not null && library.OwnerAck is not null && library.OwnerRecover is not null && library.OwnerState is not null && library.OwnerCache is not null && library.OwnerQuoteGreeks is not null && library.OwnerIdentity is not null && library.OwnerRawPrices is not null && library.OwnerTradeGreeks is not null && library.OwnerCurrent is not null && library.OwnerWait is not null && library.OwnerContracts is not null && library.OwnerPreparedCache is not null && library.OwnerQuery is not null && library.OwnerRoute is not null;

    /// <summary>Native committed-journal realization, logical release and copied
    /// command results. This is a kernel capability, not Host acceptance status.</summary>
    public bool SupportsDatasetOwnerBusiness=>SupportsDatasetOwner&&(library.OwnerFeatures&0x8000)!=0&&library.OwnerCapture is not null;
    /// <summary>Complete command-only owner including native browsing and saved statistics.</summary>
    public bool SupportsDatasetOwnerComplete=>SupportsDatasetOwnerBusiness&&(library.OwnerFeatures&0x10000)!=0;


    /// <summary>Observes native readiness through completion notification. No
    /// polling or C# recovery timer is created. Cancelling this wait does not
    /// release or withdraw accepted native feed demand.</summary>
    public Task WaitDatasetOwnerReadyAsync(MarketDataLane lane,bool requireActive=true,CancellationToken cancellationToken=default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var call=library.OwnerWait??throw new NotSupportedException("Native dataset readiness observation is unavailable.");
        lock(gate)
        {
            EnsureLiveAccess();var operation=checked(++nextOperation);var request=new Pending{Control=new(TaskCreationOptions.RunContinuationsAsynchronously)};pending.Add(operation,request);
            var status=call(handle.Identity,operation,(uint)lane,requireActive?1u:0u);
            if(status!=0){pending.Remove(operation);NativeMarketDataLibrary.Check(status,"observe native dataset readiness");}
            request.Cancellation=cancellationToken.UnsafeRegister(_=>library.Cancel(handle.Identity,operation),null);
            return request.Control.Task;
        }
    }

    /// <summary>Reads exact stale-evidence retention/loss counts without a broker
    /// or persistence dependency. No dataset handle or old-generation replay escapes.</summary>
    public NativeRetiredEvidenceStatus ReadRetiredDatasetEvidence()
    {
        var call=library.OwnerRetiredEvidence??throw new NotSupportedException("Native retired-evidence accounting is unavailable.");
        lock(gate)
        {
            EnsureLiveAccess();NativeMarketDataLibrary.Check(call(handle.Identity,40,out var value),"query native retired evidence");
            if(value.Size!=40||value.Reserved!=0||value.Retained>4)throw new InvalidDataException("Malformed native retired-evidence accounting.");
            return new(value.Retained,value.Generations,value.Records,value.Bytes);
        }
    }

    /// <summary>Observes all configured futures within native control, including
    /// healthy idle lanes. This is completion-driven and creates no C# timer.</summary>
    public Task WaitDatasetOwnerFuturesReadyAsync(CancellationToken cancellationToken=default)
        =>WaitDatasetOwnerReadyAsync((MarketDataLane)0,false,cancellationToken);

    /// <summary>Awaits native retirement of every stopped feed. New concurrent
    /// desired work is an explicit superseded-stop result, never false cleanup.</summary>
    public Task WaitDatasetOwnerFeedsStoppedAsync(CancellationToken cancellationToken=default)
        =>WaitDatasetOwnerReadyAsync((MarketDataLane)4,false,cancellationToken);

    /// <summary>Copies one complete futures-start command. Native code validates
    /// all lanes before accepting intent, then owns discovery and setup.</summary>
    public unsafe void StartDatasetOwnerFutures(ReadOnlySpan<byte> command)
    {
        var call=library.OwnerStart??throw new NotSupportedException("Native owner batch startup is unavailable.");
        ValidateOwnerInput(command);
        lock(gate){EnsureLiveAccess();fixed(byte* input=command)NativeMarketDataLibrary.Check(call(handle.Identity,input,(uint)command.Length),"start native configured datasets");}
    }

    /// <summary>Copies the existing backend-status DTO directly from native
    /// health and desired intent. Host readiness is transport state only; no C#
    /// epoch, admission gate or lifecycle registry supplies dataset health.</summary>
    public unsafe TomasAI.IFM.Application.MarketData.Contracts.MarketDataBackendStatus ReadDatasetOwnerBackendStatus(bool hostReady)
    {
        var call=library.OwnerBackendStatus??throw new NotSupportedException("Native backend status query is unavailable.");
        var bytes=System.Buffers.ArrayPool<byte>.Shared.Rent(65536);
        try{lock(gate){EnsureLiveAccess();uint length;
            fixed(byte* output=bytes)NativeMarketDataLibrary.Check(call(handle.Identity,hostReady?1u:0u,output,65536,out length),"query native backend status");
            if(length is 0 or >65536)throw new InvalidDataException("Native backend status exceeds capacity.");
            var value=MessagePack.MessagePackSerializer.Deserialize<TomasAI.IFM.Application.MarketData.Contracts.MarketDataBackendStatus>(bytes.AsMemory(0,(int)length),MessagePack.MessagePackSerializerOptions.Standard.WithSecurity(MessagePack.MessagePackSecurity.UntrustedData));
            if(value is null||value.Datasets.Count!=3||value.HostInstanceId==Guid.Empty||value.Revision<=0||value.RecoveryOwnership!=TomasAI.IFM.Application.MarketData.Contracts.MarketDataRecoveryOwnership.TokioDatasetOwner)throw new InvalidDataException("Malformed native backend status.");
            return value;
        }}finally{System.Buffers.ArrayPool<byte>.Shared.Return(bytes,clearArray:true);}
    }

    /// <summary>Copies one complete native startup/reconnect snapshot. Source
    /// membership, owner fencing and completeness are decided by Rust. This
    /// function does not drain a stream or reconstruct managed subscriptions.</summary>
    public unsafe TomasAI.IFM.Application.MarketData.Contracts.MarketDataReadSnapshot ReadDatasetOwnerSnapshot(bool hostReady)
    {
        var call=library.OwnerReadSnapshot??throw new NotSupportedException("Native backend catch-up is unavailable.");
        const int capacity=16*1024*1024;var bytes=System.Buffers.ArrayPool<byte>.Shared.Rent(capacity);
        try{lock(gate){EnsureLiveAccess();uint length;
            fixed(byte* output=bytes)NativeMarketDataLibrary.Check(call(handle.Identity,hostReady?1u:0u,output,capacity,out length),"query complete native backend catch-up");
            if(length is 0 or >capacity)throw new InvalidDataException("Native backend catch-up exceeds capacity.");
            var value=MessagePack.MessagePackSerializer.Deserialize<TomasAI.IFM.Application.MarketData.Contracts.MarketDataReadSnapshot>(bytes.AsMemory(0,(int)length),MessagePack.MessagePackSerializerOptions.Standard.WithSecurity(MessagePack.MessagePackSecurity.UntrustedData));
            if(value is null||value.Status is null||value.Status.Datasets.Count!=3||value.Status.HostInstanceId==Guid.Empty||value.Manifests is null||value.Contracts is null||value.CurrentValues is null)throw new InvalidDataException("Malformed native backend catch-up.");
            return value;
        }}finally{System.Buffers.ArrayPool<byte>.Shared.Return(bytes,clearArray:true);}
    }

    /// <summary>Creates idle Rust control after configuring the authoritative API clock.
    /// This starts no option catalog lookup, Treasury request or provider connection.</summary>
    public void CreateDatasetOwner()
    {
        if (!SupportsDatasetOwner) throw new NotSupportedException("Native dataset owner commands are unavailable in this package.");
        lock (gate) { EnsureLiveAccess(); NativeMarketDataLibrary.Check(library.CreateOwner!(handle.Identity), "create dataset owner"); }
    }

    /// <summary>Copies a complete raw reviewed catalog command into Rust. It contains
    /// configuration only; Rust acquires/qualifies references and owns all feed resources.</summary>
    public unsafe void ConfigureDatasetOwnerCatalog(ReadOnlySpan<byte> command)
    {
        var call = library.OwnerCatalog ?? throw new NotSupportedException("Native dataset owner catalog is unavailable.");
        ValidateOwnerInput(command);
        lock (gate) { EnsureLiveAccess(); fixed (byte* input = command) NativeMarketDataLibrary.Check(call(handle.Identity, input, (uint)command.Length), "configure native dataset catalog"); }
    }

    /// <summary>Accepts copied futures configuration. Actual readiness is reported
    /// by Rust; disappearance of this command caller cannot withdraw accepted intent.</summary>
    public unsafe void StartDatasetOwnerFutures(MarketDataLane lane, ReadOnlySpan<byte> command)
    {
        var call = library.OwnerFutures ?? throw new NotSupportedException("Native futures owner command is unavailable.");
        ValidateOwnerInput(command);
        lock (gate) { EnsureLiveAccess(); fixed (byte* input = command) NativeMarketDataLibrary.Check(call(handle.Identity, (uint)lane, input, (uint)command.Length), "start native futures intent"); }
    }

    /// <summary>Copies a configure/enable/disable/stop command. Stable business-owner
    /// identities select native intent; C# does not keep a physical lease or renewal task.</summary>
    public unsafe void CommandDatasetOwner(ReadOnlySpan<byte> command)
    {
        var call = library.OwnerCommand ?? throw new NotSupportedException("Native dataset owner commands are unavailable.");
        ValidateOwnerInput(command);
        lock (gate) { EnsureLiveAccess(); fixed (byte* input = command) NativeMarketDataLibrary.Check(call(handle.Identity, input, (uint)command.Length), "native dataset owner command"); }
    }

    static void ValidateOwnerInput(ReadOnlySpan<byte> command)
    {
        if (command.Length is < 1 or > 2 * 1024 * 1024) throw new ArgumentOutOfRangeException(nameof(command));
    }

    /// <summary>Explicit status query. No timer, polling service, native resource
    /// callback or stream consumer is created by querying this copied evidence.</summary>
    public NativeDatasetOwnerHealth ReadDatasetOwnerHealth(MarketDataLane lane)
    {
        var call = library.OwnerHealth ?? throw new NotSupportedException("Native dataset owner health is unavailable.");
        lock (gate)
        {
            EnsureLiveAccess(); NativeMarketDataLibrary.Check(call(handle.Identity, (uint)lane, 64, out var value), "query native dataset owner health");
            if (value.Size != 64 || value.Lane != (uint)lane || value.Phase is < 1 or > 5 || (value.Missing & ~63u) != 0
                || value.Reserved != 0 || value.PendingFailures > 16 || value.Episode == 0 || value.Revision == 0 || value.Generation == 0)
                throw new InvalidDataException("Malformed native dataset owner health.");
            return new(lane, value.Phase, value.Missing, value.Episode, value.Revision, value.Generation, value.RemainingMilliseconds, value.PendingFailures);
        }
    }

    /// <summary>Copies the real native publication identity for Host status and
    /// admission mapping. No PID, resource handle or lease ownership is fabricated.</summary>
    public NativeDatasetOwnerIdentity? ReadDatasetOwnerIdentity(MarketDataLane lane) => ReadDatasetOwnerPublication(lane).Identity;

    /// <summary>Reads demand and actual source identity independently of healthy
    /// prices. Retained identity after stop must not imply Live Feed is enabled.</summary>
    public NativeDatasetOwnerPublication ReadDatasetOwnerPublication(MarketDataLane lane)
    {
        var call = library.OwnerIdentity ?? throw new NotSupportedException("Native publication identity query is unavailable.");
        lock (gate)
        {
            EnsureLiveAccess(); NativeMarketDataLibrary.Check(call(handle.Identity, (uint)lane, 56, out var value), "query native publication identity");
            if (value.Size != 56 || value.Lane != (uint)lane || value.Flags > 3) throw new InvalidDataException("Malformed native publication identity.");
            var desired = (value.Flags & 2) != 0;
            if ((value.Flags & 1) == 0) return new(lane,desired,null);
            if (value.ValueDate is < 0 or > 3652058 || value.Revision <= 0 || (value.WorkerLow | value.WorkerHigh) == 0 || (value.GenerationLow | value.GenerationHigh) == 0)
                throw new InvalidDataException("Invalid native publication identity values.");
            return new(lane,desired,new(lane, DateOnly.FromDayNumber(value.ValueDate), Identity(value.WorkerLow,value.WorkerHigh), Identity(value.GenerationLow,value.GenerationHigh), value.Revision));
        }
    }

    /// <summary>Copies exact native market/session/statistics state. This is an
    /// explicit query, not a stream drain; no subscription or native lease is acquired.</summary>
    public LivePublication ReadDatasetOwnerState(MarketDataLane lane, uint instrument, ushort publisher)
    {
        var call = library.OwnerState ?? throw new NotSupportedException("Native dataset owner state is unavailable.");
        lock (gate)
        {
            EnsureLiveAccess(); NativeMarketDataLibrary.Check(call(handle.Identity, (uint)lane, instrument, publisher, 656, out var state), "query native dataset owner state");
            if (state.Size != 656 || state.Reserved != 0 || state.Flags > 63 || state.Source.Kind != 0 || state.Source.Size != 128
                || state.Source.Lane != lane || state.Source.Instrument != instrument)
                throw new InvalidDataException("Malformed native dataset owner state.");
            return state;
        }
    }

    /// <summary>Copies current futures contract DTOs already resolved inside the
    /// native dataset. This query performs no additional provider lookup or local selection.</summary>
    public unsafe TomasAI.IFM.Domain.MarketData.Shared.ViewModels.FuturesContractV3ReadModel[] ReadDatasetOwnerContracts(MarketDataLane lane)
    {
        if(lane is not(MarketDataLane.FuturesGlbx or MarketDataLane.FuturesXcbf))throw new ArgumentOutOfRangeException(nameof(lane));
        var call=library.OwnerContracts??throw new NotSupportedException("Native contract query is unavailable.");
        const int capacity=1024*1024;var bytes=System.Buffers.ArrayPool<byte>.Shared.Rent(capacity);
        try
        {
            lock(gate)
            {
                EnsureLiveAccess();uint count;
                fixed(byte* output=bytes)NativeMarketDataLibrary.Check(call(handle.Identity,(uint)lane,output,capacity,out count),"query native current contracts");
                if(count>capacity)throw new InvalidDataException("Native contract query exceeds capacity.");
                if(count==0)return [];
                var models=MessagePack.MessagePackSerializer.Deserialize<TomasAI.IFM.Domain.MarketData.Shared.ViewModels.FuturesContractV3ReadModel[]>(bytes.AsMemory(0,(int)count),
                    MessagePack.MessagePackSerializerOptions.Standard.WithSecurity(MessagePack.MessagePackSecurity.UntrustedData));
                if(models is null||models.Length>64||models.Any(m=>m is null))throw new InvalidDataException("Malformed native current contracts.");
                return models;
            }
        }
        finally{System.Buffers.ArrayPool<byte>.Shared.Return(bytes,clearArray:true);}
    }

    /// <summary>Copies an explicit bounded startup/reconnect snapshot page.
    /// This method creates no subscription, timer or streaming receiver.</summary>
    public unsafe TomasAI.IFM.Application.MarketData.Databento.Workers.DatasetPublicationEnvelope[] ReadDatasetOwnerCurrent(MarketDataLane lane,long after=0,uint maximum=256)
    {
        if(after<0 || maximum is <1 or >256)throw new ArgumentOutOfRangeException(nameof(maximum));
        var call=library.OwnerCurrent??throw new NotSupportedException("Native current-publication query is unavailable.");
        const int capacity=4*1024*1024;var bytes=System.Buffers.ArrayPool<byte>.Shared.Rent(capacity);
        try
        {
            lock(gate)
            {
                EnsureLiveAccess();uint count;
                fixed(byte* output=bytes)NativeMarketDataLibrary.Check(call(handle.Identity,(uint)lane,after,maximum,output,capacity,out count),"query native current publications");
                if(count>capacity)throw new InvalidDataException("Native current publication page exceeds capacity.");
                if(count==0)return [];
                var frames=MessagePack.MessagePackSerializer.Deserialize<TomasAI.IFM.Application.MarketData.Databento.Workers.DatasetPublicationEnvelope[]>(bytes.AsMemory(0,(int)count),
                    MessagePack.MessagePackSerializerOptions.Standard.WithSecurity(MessagePack.MessagePackSecurity.UntrustedData));
                if(frames is null||frames.Length>maximum||frames.Any(f=>f is null||f.WorkerInstanceId==Guid.Empty||f.GenerationId==Guid.Empty||f.PublicationSequence<=after))throw new InvalidDataException("Malformed native current publication page.");
                return frames;
            }
        }
        finally{System.Buffers.ArrayPool<byte>.Shared.Return(bytes,clearArray:true);}
    }

    /// <summary>Queries complete native raw snapshots. No C# price conversion,
    /// midpoint, source selection, context preparation or resource lifetime is involved.</summary>
    public unsafe Publication.NativeRawPrices? ReadDatasetOwnerPrices(MarketDataLane lane, string contract, DateOnly date)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(contract);
        var name = System.Text.Encoding.UTF8.GetBytes(contract);
        if (name.Length > 1024) throw new ArgumentOutOfRangeException(nameof(contract));
        var call = library.OwnerRawPrices ?? throw new NotSupportedException("Native raw price query is unavailable.");
        var bytes = System.Buffers.ArrayPool<byte>.Shared.Rent(4096);
        try
        {
            lock (gate)
            {
                EnsureLiveAccess(); uint count;
                fixed (byte* input = name) fixed (byte* output = bytes)
                    NativeMarketDataLibrary.Check(call(handle.Identity, (uint)lane, input, (uint)name.Length, (uint)date.DayNumber, output, 4096, out count), "query native raw prices");
                if (count > 4096) throw new InvalidDataException("Native raw price query exceeded its bound.");
                if (count == 0) return null;
                var result = MessagePack.MessagePackSerializer.Deserialize<Publication.NativeRawPrices>(bytes.AsMemory(0, (int)count),
                    MessagePack.MessagePackSerializerOptions.Standard.WithSecurity(MessagePack.MessagePackSecurity.UntrustedData));
                if (result is null || result.Generation == Guid.Empty || result.Trade is {} trade && (trade.ContractId != contract || trade.ValueDate != date)
                    || result.Quote is {} quote && (quote.ContractId != contract || quote.ValueDate != date)) throw new InvalidDataException("Native raw query identity mismatch.");
                return result;
            }
        }
        finally { System.Buffers.ArrayPool<byte>.Shared.Return(bytes, clearArray: true); }
    }

    /// <summary>Copies already-calculated native quote Greeks. The native owner
    /// qualifies context/source identity; this adapter performs no financial calculations.</summary>
    public unsafe Publication.NativeQuoteGreeksResult? ReadDatasetOwnerQuoteGreeks(uint instrument, ushort publisher)
    {
        if (instrument == 0) throw new ArgumentOutOfRangeException(nameof(instrument));
        var call = library.OwnerQuoteGreeks ?? throw new NotSupportedException("Native quote Greeks query is unavailable.");
        const int maximum = 16 * 1024;
        var bytes = System.Buffers.ArrayPool<byte>.Shared.Rent(maximum);
        try
        {
            lock (gate)
            {
                EnsureLiveAccess(); uint count;
                fixed (byte* output = bytes)
                    NativeMarketDataLibrary.Check(call(handle.Identity, instrument, publisher, output, maximum, out count), "query native quote Greeks");
                if (count > maximum) throw new InvalidDataException("Native quote Greeks exceeds capacity.");
                if (count == 0) return null;
                var value = MessagePack.MessagePackSerializer.Deserialize<Publication.NativeQuoteGreeksResult>(bytes.AsMemory(0, (int)count),
                    MessagePack.MessagePackSerializerOptions.Standard.WithSecurity(MessagePack.MessagePackSecurity.UntrustedData));
                if (value is null || value.Greeks is null || value.Quote is null || value.PhysicalGeneration == Guid.Empty
                    || string.IsNullOrWhiteSpace(value.Quote.ContractId) || value.Greeks.FailureReason is < 0 or > 14 || value.Greeks.PriceSource is < 0 or > 2)
                    throw new InvalidDataException("Malformed native quote Greeks projection.");
                return value;
            }
        }
        finally { System.Buffers.ArrayPool<byte>.Shared.Return(bytes, clearArray: true); }
    }

    /// <summary>Copies the exact first acknowledged native trade calculation.
    /// Unacknowledged, replaced or superseded observations remain absent.</summary>
    public unsafe Publication.NativeTradeGreeksResult? ReadDatasetOwnerTradeGreeks(uint instrument, ushort publisher)
    {
        if(instrument==0) throw new ArgumentOutOfRangeException(nameof(instrument));
        var call=library.OwnerTradeGreeks ?? throw new NotSupportedException("Native trade Greeks query is unavailable.");
        const int maximum=16*1024; var bytes=System.Buffers.ArrayPool<byte>.Shared.Rent(maximum);
        try
        {
            lock(gate)
            {
                EnsureLiveAccess(); uint count;
                fixed(byte* output=bytes) NativeMarketDataLibrary.Check(call(handle.Identity,instrument,publisher,output,maximum,out count),"query native trade Greeks");
                if(count>maximum) throw new InvalidDataException("Native trade Greeks exceeded capacity.");
                if(count==0) return null;
                var value=MessagePack.MessagePackSerializer.Deserialize<Publication.NativeTradeGreeksResult>(bytes.AsMemory(0,(int)count),
                    MessagePack.MessagePackSerializerOptions.Standard.WithSecurity(MessagePack.MessagePackSecurity.UntrustedData));
                if(value is null || value.Trade is null || value.Greeks is null || value.PhysicalGeneration==Guid.Empty || value.Greeks.PriceSource!=2 || value.Greeks.FailureReason is <0 or >14)
                    throw new InvalidDataException("Malformed native trade Greek projection.");
                return value;
            }
        }
        finally {System.Buffers.ArrayPool<byte>.Shared.Return(bytes,clearArray:true);}
    }

    /// <summary>Copies a prepared-cache query result. Eligibility policy, current
    /// generation and time checks execute in Rust; this creates no managed clock or lease.</summary>
    internal unsafe T QueryDatasetOwnerPreparedCache<T>(ReadOnlySpan<byte> command)
    {
        if(command.Length is <1 or >65536)throw new ArgumentOutOfRangeException(nameof(command));
        var call=library.OwnerPreparedCache??throw new NotSupportedException("Native prepared-cache queries are unavailable.");
        const int capacity=4*1024*1024+4096;var bytes=System.Buffers.ArrayPool<byte>.Shared.Rent(capacity);
        try
        {
            lock(gate)
            {
                EnsureLiveAccess();uint count;
                fixed(byte* input=command)fixed(byte* output=bytes)NativeMarketDataLibrary.Check(call(handle.Identity,input,(uint)command.Length,output,capacity,out count),"query native prepared cache");
                if(count is 0 or >capacity)throw new InvalidDataException("Native prepared cache query exceeds capacity.");
                return MessagePack.MessagePackSerializer.Deserialize<T>(bytes.AsMemory(0,(int)count),MessagePack.MessagePackSerializerOptions.Standard.WithSecurity(MessagePack.MessagePackSecurity.UntrustedData));
            }
        }
        finally{System.Buffers.ArrayPool<byte>.Shared.Return(bytes,clearArray:true);}
    }

    /// <summary>Queries existing five-key prepared-chain bytes. A missing, stale
    /// or recovering scope is absent. These copied bytes carry no resource lifetime.</summary>
    public unsafe byte[]? ReadDatasetOwnerCache(string scope)
    {
        if (string.IsNullOrWhiteSpace(scope) || scope.Length > 128) throw new ArgumentOutOfRangeException(nameof(scope));
        var call = library.OwnerCache ?? throw new NotSupportedException("Native cache query is unavailable.");
        var name = System.Text.Encoding.UTF8.GetBytes(scope);
        const int maximum = 4 * 1024 * 1024 + 4096;
        var bytes = System.Buffers.ArrayPool<byte>.Shared.Rent(maximum);
        try
        {
            lock (gate)
            {
                EnsureLiveAccess(); uint count;
                fixed (byte* input = name) fixed (byte* output = bytes)
                    NativeMarketDataLibrary.Check(call(handle.Identity, input, (uint)name.Length, output, maximum, out count), "query native prepared cache");
                if (count > maximum) throw new InvalidDataException("Native prepared cache exceeds capacity.");
                return count == 0 ? null : bytes.AsSpan(0, (int)count).ToArray();
            }
        }
        finally { System.Buffers.ArrayPool<byte>.Shared.Return(bytes, clearArray: true); }
    }

    /// <summary>Queries retained terminal messages without consuming them. This is
    /// a command/query fallback; normal streaming notification integration uses a separate native delivery path.</summary>
    public unsafe NativeDatasetOwnerFailure[] ReadDatasetOwnerFailures()
    {
        var call = library.OwnerFailures ?? throw new NotSupportedException("Native dataset owner failures are unavailable.");
        lock (gate)
        {
            EnsureLiveAccess(); var status = call(handle.Identity, null, 0, out var required);
            if (status != 4) NativeMarketDataLibrary.Check(status, "size native terminal messages");
            if (required > 4 * 1024 * 1024) throw new InvalidDataException("Native terminal messages exceed capacity.");
            // The journal can grow between sizing and copying. One hard-bounded
            // buffer avoids repeated sizing/retry loops during a failure burst.
            var bytes = System.Buffers.ArrayPool<byte>.Shared.Rent(4 * 1024 * 1024);
            try
            {
            uint count;
            fixed (byte* output = bytes) NativeMarketDataLibrary.Check(call(handle.Identity, output, 4 * 1024 * 1024, out count), "query native terminal messages");
            if (count > 4 * 1024 * 1024) throw new InvalidDataException("Native terminal output exceeded capacity.");
            var records = JsonSerializer.Deserialize<NativeDatasetOwnerFailure[]>(bytes.AsSpan(0, (int)count), OwnerJson)
                ?? throw new InvalidDataException("Native terminal messages are absent.");
            if (records.Length > 48 || records.Any(x => x.Lane is < 1 or > 3 || x.Ordinal == 0 || x.Ticket is not { Length: 3 }
                || x.Ticket.Any(n => n == 0) || x.Root?.Length > 4096 || x.Detail?.Length > 4096))
                throw new InvalidDataException("Malformed native terminal message.");
            return records;
            }
            finally { System.Buffers.ArrayPool<byte>.Shared.Return(bytes, clearArray: true); }
        }
    }

    /// <summary>Acknowledges terminal messages only after the UI delivery path
    /// confirms display; broker flush alone must never consume the journal.</summary>
    public void AcknowledgeDatasetOwnerFailure(MarketDataLane lane, ulong ordinal)
    {
        var call = library.OwnerAck ?? throw new NotSupportedException("Native terminal acknowledgment is unavailable.");
        lock (gate) { EnsureLiveAccess(); NativeMarketDataLibrary.Check(call(handle.Identity, (uint)lane, ordinal), "acknowledge native terminal message"); }
    }

    /// <summary>Requests native recovery while preserving native business claims.
    /// Rust owns and enforces the original two-minute dataset recovery deadline.</summary>
    public void RecoverDatasetOwner(MarketDataLane lane)
    {
        var call = library.OwnerRecover ?? throw new NotSupportedException("Native dataset owner recovery is unavailable.");
        lock (gate) { EnsureLiveAccess(); NativeMarketDataLibrary.Check(call(handle.Identity, (uint)lane), "recover native dataset"); }
    }
}
