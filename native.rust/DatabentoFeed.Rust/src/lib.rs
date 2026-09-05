#![cfg_attr(not(windows), allow(dead_code))]
// These unsafe functions are the implementation of the frozen C header; pointer validity
// requirements are defined once by that canonical ABI rather than repeated on every export.
#![allow(clippy::missing_safety_doc)]

mod abi;
#[cfg(windows)]
#[doc(hidden)]
pub mod engine;
#[cfg(all(windows, feature = "live"))]
mod live;
#[cfg(windows)]
mod windows;

pub use abi::*;

#[cfg(not(windows))]
compile_error!("The current DatabentoFeed.Rust implementation targets Windows only");

#[cfg(windows)]
mod exports {
    use std::panic::{AssertUnwindSafe, catch_unwind};
    use std::ptr;
    use std::slice;
    use std::sync::atomic::{AtomicU64, Ordering};
    use std::sync::{Mutex, OnceLock};

    use crate::abi::*;
    use crate::engine::{Feed, Mapping};

    static WATCHDOG_REGISTRY: OnceLock<Mutex<Vec<(u64, usize)>>> = OnceLock::new();
    static WATCHDOG_IDENTITY: AtomicU64 = AtomicU64::new(1);
    static WATCHDOG_SNAPSHOT_SEQUENCE: AtomicU64 = AtomicU64::new(1);
    fn watchdog_registry() -> &'static Mutex<Vec<(u64, usize)>> {
        WATCHDOG_REGISTRY.get_or_init(|| Mutex::new(Vec::new()))
    }

    struct ContractResultEntry {
        detail: ContractDetailV1,
        strings: [Vec<u8>; 9],
    }
    pub struct ContractDetailsResult {
        entries: Vec<ContractResultEntry>,
        error: Vec<u8>,
    }
    pub struct HistoricalResult {
        payload: Vec<u8>,
        error: Vec<u8>,
        records: Vec<HistoricalRecord120>,
        cursor: usize,
        batch_ordinal: u64,
    }

    fn ffi_status(operation: impl FnOnce() -> Status) -> Status {
        catch_unwind(AssertUnwindSafe(operation)).unwrap_or(INTERNAL_ERROR)
    }
    fn valid_struct(actual: u32, expected: usize, version: u32) -> bool {
        actual as usize >= expected && version == ABI_VERSION
    }
    fn valid_range(offset: u32, length: u32, total: u32) -> bool {
        offset <= total && length <= total - offset
    }
    unsafe fn bytes<'a>(pointer: *const u8, length: u32) -> Option<&'a [u8]> {
        if pointer.is_null() {
            None
        } else {
            Some(unsafe { slice::from_raw_parts(pointer, length as usize) })
        }
    }
    unsafe fn feed_ref<'a>(feed: *mut Feed) -> Option<&'a Feed> {
        if feed.is_null() {
            None
        } else {
            Some(unsafe { &*feed })
        }
    }
    fn contains_nul(bytes: &[u8]) -> bool {
        bytes.contains(&0)
    }
    fn valid_historical_request(
        request: *const HistoricalRequestV1,
        symbols: *const Utf8SliceV1,
        blob: *const u8,
        blob_bytes: u32,
    ) -> bool {
        if request.is_null() {
            return false;
        }
        let request = unsafe { &*request };
        if !valid_struct(
            request.struct_size,
            size_of::<HistoricalRequestV1>(),
            request.abi_version,
        ) || request.reserved32 != 0
            || !(HISTORICAL_DEFINITION..=HISTORICAL_OHLCV_1D).contains(&request.schema)
            || request.symbol_count == 0
            || symbols.is_null()
            || blob.is_null()
            || request.dataset.length == 0
            || !valid_range(request.dataset.offset, request.dataset.length, blob_bytes)
            || request.start_ts_ns >= request.end_ts_ns
            || request.timeout_ms == 0
            || request.timeout_ms == WAIT_INFINITE
        {
            return false;
        }
        let symbols = unsafe { slice::from_raw_parts(symbols, request.symbol_count as usize) };
        symbols.iter().all(|symbol| {
            symbol.length != 0 && valid_range(symbol.offset, symbol.length, blob_bytes)
        })
    }
    fn historical_inputs(
        request: &HistoricalRequestV1,
        symbols: *const Utf8SliceV1,
        blob: *const u8,
        blob_bytes: u32,
    ) -> Result<(String, Vec<String>), Status> {
        let input = unsafe { slice::from_raw_parts(blob, blob_bytes as usize) };
        let text = |value: Utf8SliceV1| -> Result<String, Status> {
            let bytes = &input[value.offset as usize..(value.offset + value.length) as usize];
            if contains_nul(bytes) {
                return Err(INVALID_ARGUMENT);
            }
            std::str::from_utf8(bytes)
                .map(str::to_owned)
                .map_err(|_| DATABENTO_ERROR)
        };
        let dataset = text(request.dataset)?;
        let slices = unsafe { slice::from_raw_parts(symbols, request.symbol_count as usize) };
        let requested = slices
            .iter()
            .copied()
            .map(text)
            .collect::<Result<Vec<_>, _>>()?;
        Ok((dataset, requested))
    }
    fn historical_result(payload: &[u8]) -> Box<HistoricalResult> {
        Box::new(HistoricalResult {
            payload: payload.to_vec(),
            error: Vec::new(),
            records: Vec::new(),
            cursor: 0,
            batch_ordinal: 0,
        })
    }
    fn synthetic_historical_record(
        request: &HistoricalRequestV1,
        ordinal: u64,
    ) -> HistoricalRecord120 {
        let price = 5_000_000_000_i64 + ordinal as i64 * 1_000_000;
        let mut symbol = [0_u8; 32];
        symbol[..5].copy_from_slice(b"SYNTH");
        HistoricalRecord120 {
            struct_size: size_of::<HistoricalRecord120>() as u32,
            abi_version: ABI_VERSION,
            record_kind: match request.schema {
                HISTORICAL_TRADES => HISTORICAL_RECORD_TRADE,
                HISTORICAL_DEFINITION => HISTORICAL_RECORD_DEFINITION,
                HISTORICAL_STATISTICS => HISTORICAL_RECORD_STATISTIC,
                _ => HISTORICAL_RECORD_OHLCV,
            },
            schema: request.schema,
            instrument_id: 1000 + ordinal as u32,
            publisher_id: 7,
            condition_flags: 0,
            event_ts_ns: request.start_ts_ns + ordinal as i64 * 60_000_000_000,
            source_sequence: ordinal as i64 + 1,
            open_price: price,
            high_price: price + 2_000_000,
            low_price: price - 2_000_000,
            close_or_trade_price: price + 500_000,
            volume_or_size: 10 + ordinal,
            action: b'T',
            side: if ordinal % 2 == 0 { b'B' } else { b'A' },
            reserved8: [0; 6],
            symbol,
        }
    }

    #[unsafe(no_mangle)]
    pub extern "C" fn dbf_get_abi_version() -> u32 {
        ABI_VERSION
    }

    #[unsafe(no_mangle)]
    pub unsafe extern "C" fn dbf_feed_create(
        config: *const FeedConfigV1,
        utf8_blob: *const u8,
        utf8_blob_bytes: u32,
        result: *mut *mut Feed,
    ) -> Status {
        ffi_status(|| {
            if result.is_null() {
                return INVALID_ARGUMENT;
            }
            unsafe {
                result.write(ptr::null_mut());
            }
            let Some(config) = (!config.is_null()).then(|| unsafe { *config }) else {
                return ABI_MISMATCH;
            };
            if !valid_struct(
                config.struct_size,
                size_of::<FeedConfigV1>(),
                config.abi_version,
            ) {
                return ABI_MISMATCH;
            }
            let known_flags = CONFIG_LOCK_RING_MEMORY
                | CONFIG_REQUIRE_LOCKED_MEMORY
                | CONFIG_REQUIRE_BASE_PAGE_POLICY
                | CONFIG_REQUIRE_PRIORITY
                | CONFIG_REQUIRE_NUMA_LOCALITY
                | CONFIG_TRACK_PROCESSOR_RESIDENCY;
            if config.reserved16 != 0
                || config.reserved32 != 0
                || config.reserved.iter().any(|&v| v != 0)
                || config.flags > known_flags
                || !(0..=2).contains(&config.producer_priority)
                || !(0..=2).contains(&config.drain_priority)
                || config.ring_full_timeout_us == 0
            {
                return INVALID_ARGUMENT;
            }
            if config.forced_migration_interval_records != 0
                && (config.flags & CONFIG_TRACK_PROCESSOR_RESIDENCY == 0
                    || config.data_source != DATA_SOURCE_SYNTHETIC
                    || config.producer_logical_processor == UNPINNED_PROCESSOR
                    || config.producer_alternate_logical_processor == UNPINNED_PROCESSOR
                    || config.drain_logical_processor == UNPINNED_PROCESSOR
                    || config.drain_alternate_logical_processor == UNPINNED_PROCESSOR)
            {
                return INVALID_ARGUMENT;
            }
            if config.data_source != DATA_SOURCE_SYNTHETIC
                && config.data_source != DATA_SOURCE_DATABENTO_LIVE
            {
                return INVALID_ARGUMENT;
            }
            #[cfg(not(feature = "live"))]
            if config.data_source == DATA_SOURCE_DATABENTO_LIVE {
                return NOT_SUPPORTED;
            }
            if config.data_source == DATA_SOURCE_DATABENTO_LIVE
                && (config.heartbeat_interval_ms < 5_000
                    || config.heartbeat_interval_ms % 1_000 != 0)
            {
                return INVALID_ARGUMENT;
            }
            if config.feed_kind != FEED_TICKER && config.feed_kind != FEED_OPTION_CHAIN {
                return INVALID_ARGUMENT;
            }
            if !valid_range(
                config.dataset_offset,
                config.dataset_length,
                utf8_blob_bytes,
            ) || (config.dataset_length != 0 && utf8_blob.is_null())
            {
                return INVALID_ARGUMENT;
            }
            let dataset = if config.dataset_length == 0 {
                Vec::new()
            } else {
                unsafe {
                    slice::from_raw_parts(
                        utf8_blob.add(config.dataset_offset as usize),
                        config.dataset_length as usize,
                    )
                }
                .to_vec()
            };
            match Feed::new(config, dataset) {
                Ok(feed) => {
                    let pointer = Box::into_raw(feed);
                    let identity = WATCHDOG_IDENTITY.fetch_add(1, Ordering::Relaxed);
                    watchdog_registry().lock().unwrap_or_else(|error| error.into_inner())
                        .push((identity, pointer as usize));
                    unsafe {
                        result.write(pointer);
                    }
                    OK
                }
                Err(status) => status,
            }
        })
    }

    #[unsafe(no_mangle)]
    pub unsafe extern "C" fn dbf_feed_subscribe_tickers(
        feed: *mut Feed,
        subscriptions: *const TickerSubscriptionV1,
        subscription_count: u32,
        utf8_blob: *const u8,
        utf8_blob_bytes: u32,
        _timeout_ms: u32,
    ) -> Status {
        ffi_status(|| {
            let Some(feed) = (unsafe { feed_ref(feed) }) else {
                return INVALID_ARGUMENT;
            };
            if subscriptions.is_null() || subscription_count == 0 {
                return INVALID_ARGUMENT;
            }
            if feed.config.feed_kind != FEED_TICKER
                || feed.state.load(Ordering::Acquire) != STATE_CREATED
            {
                return INVALID_STATE;
            }
            let input = match unsafe { bytes(utf8_blob, utf8_blob_bytes) } {
                Some(value) => value,
                None => return INVALID_ARGUMENT,
            };
            let subscriptions =
                unsafe { slice::from_raw_parts(subscriptions, subscription_count as usize) };
            let mut mappings = Vec::new();
            if mappings.try_reserve(subscription_count as usize).is_err() {
                return NO_MEMORY;
            }
            for (index, item) in subscriptions.iter().enumerate() {
                if !valid_struct(
                    item.struct_size,
                    size_of::<TickerSubscriptionV1>(),
                    item.abi_version,
                ) || item.reserved != 0
                {
                    return ABI_MISMATCH;
                }
                if !valid_range(item.symbol_offset, item.symbol_length, utf8_blob_bytes)
                    || item.symbol_length == 0
                    || item.symbol_length > u16::MAX.into()
                    || !(item.input_symbology == 1 || item.input_symbology == 2)
                    || item.data_kinds & 15 == 0
                    || item.data_kinds & !31 != 0
                    || (item.data_kinds & MARKET_DATA_SESSION_VOLUME != 0
                        && item.data_kinds & MARKET_DATA_TRADE == 0)
                {
                    return INVALID_ARGUMENT;
                }
                let symbol = input[item.symbol_offset as usize
                    ..(item.symbol_offset + item.symbol_length) as usize]
                    .to_vec();
                mappings.push(Mapping {
                    subscription_index: index as u32,
                    instrument_id: index as u32 + 1,
                    publisher_id: 1,
                    data_kinds: item.data_kinds & 31,
                    input_symbology: item.input_symbology,
                    requested_symbol: symbol.clone(),
                    raw_symbol: symbol,
                    resolved: true,
                });
            }
            *feed.mappings.lock().unwrap_or_else(|e| e.into_inner()) = mappings;
            feed.state.store(STATE_SUBSCRIBED, Ordering::Release);
            OK
        })
    }

    #[unsafe(no_mangle)]
    pub unsafe extern "C" fn dbf_feed_subscribe_option_chain(
        feed: *mut Feed,
        subscription: *const OptionChainSubscriptionV1,
        contracts: *const OptionContractSelectionV1,
        contract_count: u32,
        utf8_blob: *const u8,
        utf8_blob_bytes: u32,
        _timeout_ms: u32,
    ) -> Status {
        ffi_status(|| {
            let Some(feed) = (unsafe { feed_ref(feed) }) else {
                return INVALID_ARGUMENT;
            };
            if subscription.is_null() || contracts.is_null() || contract_count == 0 {
                return INVALID_ARGUMENT;
            }
            if feed.config.feed_kind != FEED_OPTION_CHAIN
                || feed.state.load(Ordering::Acquire) != STATE_CREATED
            {
                return INVALID_STATE;
            }
            let subscription = unsafe { &*subscription };
            if !valid_struct(
                subscription.struct_size,
                size_of::<OptionChainSubscriptionV1>(),
                subscription.abi_version,
            ) || subscription.contract_count != contract_count
                || subscription.data_kinds & 7 == 0
                || subscription.data_kinds & !7 != 0
                || subscription.reserved.iter().any(|&v| v != 0)
            {
                return INVALID_ARGUMENT;
            }
            let input = match unsafe { bytes(utf8_blob, utf8_blob_bytes) } {
                Some(value) => value,
                None => return INVALID_ARGUMENT,
            };
            let contracts = unsafe { slice::from_raw_parts(contracts, contract_count as usize) };
            let mut mappings = Vec::new();
            if mappings.try_reserve(contract_count as usize).is_err() {
                return NO_MEMORY;
            }
            for (index, item) in contracts.iter().enumerate() {
                if !valid_struct(
                    item.struct_size,
                    size_of::<OptionContractSelectionV1>(),
                    item.abi_version,
                ) || item.reserved != 0
                    || !(item.option_right == 1 || item.option_right == 2)
                    || item.reserved8 != 0
                    || item.instrument_id == 0
                    || item.publisher_id == 0
                    || !valid_range(
                        item.raw_symbol_offset,
                        item.raw_symbol_length,
                        utf8_blob_bytes,
                    )
                    || item.raw_symbol_length == 0
                    || item.raw_symbol_length > u16::MAX.into()
                {
                    return INVALID_ARGUMENT;
                }
                let symbol = input[item.raw_symbol_offset as usize
                    ..(item.raw_symbol_offset + item.raw_symbol_length) as usize]
                    .to_vec();
                mappings.push(Mapping {
                    subscription_index: index as u32,
                    instrument_id: item.instrument_id,
                    publisher_id: item.publisher_id,
                    data_kinds: subscription.data_kinds & 7,
                    input_symbology: 1,
                    requested_symbol: symbol.clone(),
                    raw_symbol: symbol,
                    resolved: true,
                });
            }
            *feed.mappings.lock().unwrap_or_else(|e| e.into_inner()) = mappings;
            feed.state.store(STATE_SUBSCRIBED, Ordering::Release);
            OK
        })
    }

    #[unsafe(no_mangle)]
    pub unsafe extern "C" fn dbf_feed_allocate_read_buffer64(
        feed: *mut Feed,
        record_capacity: u32,
        buffer: *mut *mut MarketRecord64,
    ) -> Status {
        ffi_status(|| {
            if feed.is_null() || buffer.is_null() || record_capacity == 0 {
                return INVALID_ARGUMENT;
            }
            unsafe {
                buffer.write(ptr::null_mut());
            }
            match unsafe { &*feed }.allocate_read_buffer(record_capacity) {
                Ok(pointer) => {
                    unsafe {
                        buffer.write(pointer);
                    }
                    OK
                }
                Err(status) => status,
            }
        })
    }

    #[unsafe(no_mangle)]
    pub unsafe extern "C" fn dbf_feed_start(feed: *mut Feed, timeout_ms: u32) -> Status {
        ffi_status(|| {
            unsafe { feed_ref(feed) }.map_or(INVALID_ARGUMENT, |feed| feed.start(timeout_ms))
        })
    }

    #[unsafe(no_mangle)]
    pub unsafe extern "C" fn dbf_feed_get_ticker_mapping_counts(
        feed: *mut Feed,
        mapping_count: *mut u32,
        utf8_blob_bytes: *mut u32,
    ) -> Status {
        ffi_status(|| {
            let Some(feed) = (unsafe { feed_ref(feed) }) else {
                return INVALID_ARGUMENT;
            };
            if mapping_count.is_null() || utf8_blob_bytes.is_null() {
                return INVALID_ARGUMENT;
            }
            if feed.state.load(Ordering::Acquire) != STATE_CONSUMER_SETUP {
                return INVALID_STATE;
            }
            let mappings = feed.mappings.lock().unwrap_or_else(|e| e.into_inner());
            let bytes: u64 = mappings
                .iter()
                .map(|m| (m.requested_symbol.len() + m.raw_symbol.len()) as u64)
                .sum();
            if bytes > u32::MAX.into() || mappings.len() > u32::MAX as usize {
                return BUFFER_TOO_SMALL;
            }
            unsafe {
                mapping_count.write(mappings.len() as u32);
                utf8_blob_bytes.write(bytes as u32);
            }
            OK
        })
    }

    #[unsafe(no_mangle)]
    pub unsafe extern "C" fn dbf_feed_copy_ticker_mappings(
        feed: *mut Feed,
        output: *mut TickerInstrumentMappingV1,
        mapping_capacity: u32,
        utf8_blob: *mut u8,
        utf8_blob_capacity: u32,
    ) -> Status {
        ffi_status(|| {
            let Some(feed) = (unsafe { feed_ref(feed) }) else {
                return INVALID_ARGUMENT;
            };
            if output.is_null() || utf8_blob.is_null() {
                return INVALID_ARGUMENT;
            }
            if feed.state.load(Ordering::Acquire) != STATE_CONSUMER_SETUP {
                return INVALID_STATE;
            }
            let mappings = feed.mappings.lock().unwrap_or_else(|e| e.into_inner());
            let required_bytes = mappings
                .iter()
                .map(|m| m.requested_symbol.len() + m.raw_symbol.len())
                .sum::<usize>();
            if mapping_capacity < mappings.len() as u32
                || (utf8_blob_capacity as usize) < required_bytes
            {
                return BUFFER_TOO_SMALL;
            }
            let mut offset = 0usize;
            for (index, source) in mappings.iter().enumerate() {
                let destination = TickerInstrumentMappingV1 {
                    struct_size: size_of::<TickerInstrumentMappingV1>() as u32,
                    abi_version: ABI_VERSION,
                    subscription_index: source.subscription_index,
                    instrument_id: source.instrument_id,
                    publisher_id: source.publisher_id,
                    reserved16: 0,
                    requested_symbol_offset: offset as u32,
                    requested_symbol_length: source.requested_symbol.len() as u16,
                    raw_symbol_length: source.raw_symbol.len() as u16,
                    raw_symbol_offset: (offset + source.requested_symbol.len()) as u32,
                };
                unsafe {
                    output.add(index).write(destination);
                    ptr::copy_nonoverlapping(
                        source.requested_symbol.as_ptr(),
                        utf8_blob.add(offset),
                        source.requested_symbol.len(),
                    );
                }
                offset += source.requested_symbol.len();
                unsafe {
                    ptr::copy_nonoverlapping(
                        source.raw_symbol.as_ptr(),
                        utf8_blob.add(offset),
                        source.raw_symbol.len(),
                    );
                }
                offset += source.raw_symbol.len();
            }
            OK
        })
    }

    #[unsafe(no_mangle)]
    pub unsafe extern "C" fn dbf_feed_set_consumer_ready(
        feed: *mut Feed,
        timeout_ms: u32,
    ) -> Status {
        ffi_status(|| {
            let Some(feed) = (unsafe { feed_ref(feed) }) else {
                return INVALID_ARGUMENT;
            };
            feed.set_consumer_ready(timeout_ms)
        })
    }

    #[unsafe(no_mangle)]
    pub unsafe extern "C" fn dbf_feed_wait(
        feed: *mut Feed,
        timeout_ms: u32,
        result: *mut WaitResultV1,
    ) -> Status {
        ffi_status(|| {
            let Some(feed) = (unsafe { feed_ref(feed) }) else {
                return INVALID_ARGUMENT;
            };
            if result.is_null() {
                return INVALID_ARGUMENT;
            }
            let result = unsafe { &mut *result };
            if !valid_struct(
                result.struct_size,
                size_of::<WaitResultV1>(),
                result.abi_version,
            ) {
                return ABI_MISMATCH;
            }
            let mut available = feed.available();
            let mut state = feed.state.load(Ordering::Acquire);
            if available == 0 && state != STATE_STOPPED && state != STATE_FAULTED {
                feed.set_consumer_waiting(true);
                // Recheck after publishing the wait intent. Publication either
                // becomes visible here or observes the intent and signals us.
                available = feed.available();
                state = feed.state.load(Ordering::Acquire);
                if available == 0 && state != STATE_STOPPED && state != STATE_FAULTED {
                    let status = feed.wait_signal(timeout_ms);
                    feed.set_consumer_waiting(false);
                    if status != OK {
                        return status;
                    }
                } else {
                    feed.set_consumer_waiting(false);
                }
                available = feed.available();
                state = feed.state.load(Ordering::Acquire);
            }
            result.flags = if available != 0 { WAIT_DATA } else { 0 };
            if state == STATE_STOPPED || state == STATE_FAULTED {
                result.flags |= WAIT_TERMINAL;
            }
            if state == STATE_FAULTED {
                result.flags |= WAIT_FAULT;
            }
            result.state = state;
            result.available_records = available;
            result.terminal_status = feed.terminal_status();
            result.reserved = 0;
            OK
        })
    }

    #[unsafe(no_mangle)]
    pub unsafe extern "C" fn dbf_feed_read_batch64(
        feed: *mut Feed,
        destination: *mut MarketRecord64,
        destination_record_capacity: u32,
        result: *mut BatchResultV1,
    ) -> Status {
        ffi_status(|| {
            let Some(feed) = (unsafe { feed_ref(feed) }) else {
                return INVALID_ARGUMENT;
            };
            if result.is_null() {
                return INVALID_ARGUMENT;
            }
            let result = unsafe { &mut *result };
            if !valid_struct(
                result.struct_size,
                size_of::<BatchResultV1>(),
                result.abi_version,
            ) {
                return ABI_MISMATCH;
            }
            if destination.is_null() || destination_record_capacity == 0 {
                return INVALID_ARGUMENT;
            }
            unsafe { feed.read_batch(destination, destination_record_capacity, result) }
        })
    }

    #[unsafe(no_mangle)]
    pub unsafe extern "C" fn dbf_feed_stop(feed: *mut Feed, timeout_ms: u32) -> Status {
        ffi_status(|| {
            unsafe { feed_ref(feed) }.map_or(INVALID_ARGUMENT, |feed| feed.stop(timeout_ms))
        })
    }

    #[unsafe(no_mangle)]
    pub unsafe extern "C" fn dbf_feed_free_read_buffer64(
        feed: *mut Feed,
        buffer: *mut MarketRecord64,
    ) -> Status {
        ffi_status(|| {
            unsafe { feed_ref(feed) }.map_or(INVALID_ARGUMENT, |feed| feed.free_read_buffer(buffer))
        })
    }

    #[unsafe(no_mangle)]
    pub unsafe extern "C" fn dbf_feed_get_stats(feed: *mut Feed, stats: *mut StatsV1) -> Status {
        ffi_status(|| {
            let Some(feed) = (unsafe { feed_ref(feed) }) else {
                return INVALID_ARGUMENT;
            };
            if stats.is_null() {
                return INVALID_ARGUMENT;
            }
            let stats = unsafe { &mut *stats };
            if !valid_struct(stats.struct_size, size_of::<StatsV1>(), stats.abi_version) {
                return ABI_MISMATCH;
            }
            feed.fill_stats(stats);
            OK
        })
    }

    #[unsafe(no_mangle)]
    pub unsafe extern "C" fn dbf_get_watchdog_snapshot_v1(
        snapshot: *mut WatchdogSnapshotV1,
        entries: *mut WatchdogFeedStatusV1,
        entry_capacity: u32,
    ) -> Status {
        ffi_status(|| {
            if snapshot.is_null() { return ABI_MISMATCH; }
            let snapshot = unsafe { &mut *snapshot };
            if !valid_struct(snapshot.struct_size, size_of::<WatchdogSnapshotV1>(), snapshot.abi_version) {
                return ABI_MISMATCH;
            }
            let registry = watchdog_registry().lock().unwrap_or_else(|error| error.into_inner());
            let Ok(required) = u32::try_from(registry.len()) else { return BUFFER_TOO_SMALL; };
            snapshot.entry_count = 0;
            snapshot.required_count = required;
            snapshot.observed_monotonic_ns = registry.first().map_or(0, |(_, pointer)|
                unsafe { &*(*pointer as *const Feed) }.monotonic_nanoseconds());
            snapshot.snapshot_sequence = WATCHDOG_SNAPSHOT_SEQUENCE.fetch_add(1, Ordering::Relaxed);
            snapshot.reserved = [0; 4];
            if entry_capacity < required || (required != 0 && entries.is_null()) { return BUFFER_TOO_SMALL; }
            let output = if required == 0 { &mut [] } else {
                unsafe { slice::from_raw_parts_mut(entries, required as usize) }
            };
            for ((identity, pointer), target) in registry.iter().zip(output.iter_mut()) {
                if !valid_struct(target.struct_size, size_of::<WatchdogFeedStatusV1>(), target.abi_version) {
                    return ABI_MISMATCH;
                }
                unsafe { &*(*pointer as *const Feed) }.fill_watchdog(target, *identity);
            }
            snapshot.entry_count = required;
            OK
        })
    }

    fn copy_error(error: &[u8], buffer: *mut u8, capacity: u32, required: *mut u32) -> Status {
        if required.is_null() {
            return INVALID_ARGUMENT;
        }
        let bytes = match u32::try_from(error.len() + 1) {
            Ok(value) => value,
            Err(_) => return BUFFER_TOO_SMALL,
        };
        unsafe {
            required.write(bytes);
        }
        if buffer.is_null() || capacity < bytes {
            return BUFFER_TOO_SMALL;
        }
        unsafe {
            ptr::copy_nonoverlapping(error.as_ptr(), buffer, error.len());
            buffer.add(error.len()).write(0);
        }
        OK
    }

    #[unsafe(no_mangle)]
    pub unsafe extern "C" fn dbf_feed_get_last_error(
        feed: *mut Feed,
        utf8_buffer: *mut u8,
        utf8_buffer_capacity: u32,
        required_bytes: *mut u32,
    ) -> Status {
        ffi_status(|| {
            let Some(feed) = (unsafe { feed_ref(feed) }) else {
                return INVALID_ARGUMENT;
            };
            copy_error(
                &feed.error_bytes(),
                utf8_buffer,
                utf8_buffer_capacity,
                required_bytes,
            )
        })
    }

    #[unsafe(no_mangle)]
    pub unsafe extern "C" fn dbf_feed_destroy(feed: *mut Feed) -> Status {
        ffi_status(|| {
            if feed.is_null() {
                return INVALID_ARGUMENT;
            }
            let feed_ref = unsafe { &*feed };
            if !feed_ref.can_destroy() {
                return INVALID_STATE;
            }
            feed_ref.join_completed();
            {
                let mut registry = watchdog_registry().lock().unwrap_or_else(|error| error.into_inner());
                if let Some(index) = registry.iter().position(|(_, pointer)| *pointer == feed as usize) {
                    registry.swap_remove(index);
                }
            }
            unsafe {
                drop(Box::from_raw(feed));
            }
            OK
        })
    }

    #[unsafe(no_mangle)]
    pub unsafe extern "C" fn dbf_contract_details_query(
        query: *const ContractQueryV1,
        symbols: *const Utf8SliceV1,
        utf8_blob: *const u8,
        utf8_blob_bytes: u32,
        output: *mut *mut ContractDetailsResult,
    ) -> Status {
        ffi_status(|| {
            if output.is_null() {
                return INVALID_ARGUMENT;
            }
            unsafe {
                output.write(ptr::null_mut());
            }
            if query.is_null() {
                return ABI_MISMATCH;
            }
            let query = unsafe { &*query };
            if !valid_struct(
                query.struct_size,
                size_of::<ContractQueryV1>(),
                query.abi_version,
            ) {
                return ABI_MISMATCH;
            }
            if query.reserved32 != 0
                || query.reserved.iter().any(|&v| v != 0)
                || !matches!(
                    query.query_kind,
                    CONTRACT_QUERY_EXACT
                        | CONTRACT_QUERY_TICKER
                        | CONTRACT_QUERY_INSTRUMENT_ID
                        | CONTRACT_QUERY_DATASET
                )
                || query.timeout_ms == 0
                || query.timeout_ms == WAIT_INFINITE
                || query.symbol_count == 0
                || symbols.is_null()
                || utf8_blob.is_null()
                || !valid_range(query.dataset_offset, query.dataset_length, utf8_blob_bytes)
                || query.dataset_length == 0
                || ((query.query_kind == CONTRACT_QUERY_TICKER
                    || query.query_kind == CONTRACT_QUERY_INSTRUMENT_ID
                    || query.query_kind == CONTRACT_QUERY_DATASET)
                    && query.symbol_count != 1)
            {
                return INVALID_ARGUMENT;
            }
            #[cfg(feature = "live")]
            let input = unsafe { slice::from_raw_parts(utf8_blob, utf8_blob_bytes as usize) };
            let symbol_slices =
                unsafe { slice::from_raw_parts(symbols, query.symbol_count as usize) };
            for symbol in symbol_slices {
                if symbol.length == 0 || !valid_range(symbol.offset, symbol.length, utf8_blob_bytes)
                {
                    let result = Box::new(ContractDetailsResult {
                        entries: Vec::new(),
                        error: b"A contract symbol was empty or outside the UTF-8 input buffer"
                            .to_vec(),
                    });
                    unsafe {
                        output.write(Box::into_raw(result));
                    }
                    return INVALID_ARGUMENT;
                }
            }
            #[cfg(feature = "live")]
            let dataset_bytes = &input[query.dataset_offset as usize
                ..(query.dataset_offset + query.dataset_length) as usize];
            #[cfg(feature = "live")]
            {
                let mut result = Box::new(ContractDetailsResult {
                    entries: Vec::new(),
                    error: Vec::new(),
                });
                let parsed = String::from_utf8(dataset_bytes.to_vec()).and_then(|dataset| {
                    symbol_slices
                        .iter()
                        .map(|symbol| {
                            String::from_utf8(
                                input[symbol.offset as usize
                                    ..(symbol.offset + symbol.length) as usize]
                                    .to_vec(),
                            )
                        })
                        .collect::<Result<Vec<_>, _>>()
                        .map(|symbols| (dataset, symbols))
                });
                let status = match parsed {
                    Err(error) => {
                        result.error = error.to_string().into_bytes();
                        DATABENTO_ERROR
                    }
                    Ok((dataset, requested)) => match crate::live::query_contracts(
                        query.query_kind,
                        dataset,
                        requested,
                        query.timeout_ms,
                    ) {
                        Ok(entries) => {
                            result.entries = entries
                                .into_iter()
                                .map(|entry| ContractResultEntry {
                                    detail: entry.detail,
                                    strings: entry.strings,
                                })
                                .collect();
                            OK
                        }
                        Err((status, error)) => {
                            result.error = error.into_bytes();
                            status
                        }
                    },
                };
                unsafe {
                    output.write(Box::into_raw(result));
                }
                status
            }
            #[cfg(not(feature = "live"))]
            {
                let result = Box::new(ContractDetailsResult {
                    entries: Vec::new(),
                    error: b"The native library was built without Databento historical API support"
                        .to_vec(),
                });
                unsafe {
                    output.write(Box::into_raw(result));
                }
                NOT_SUPPORTED
            }
        })
    }

    #[unsafe(no_mangle)]
    pub unsafe extern "C" fn dbf_contract_details_result_get_counts(
        result: *const ContractDetailsResult,
        detail_count: *mut u32,
        utf8_blob_bytes: *mut u32,
    ) -> Status {
        ffi_status(|| {
            if result.is_null() || detail_count.is_null() || utf8_blob_bytes.is_null() {
                return INVALID_ARGUMENT;
            }
            let result = unsafe { &*result };
            let bytes = result
                .entries
                .iter()
                .flat_map(|entry| entry.strings.iter())
                .map(Vec::len)
                .sum::<usize>();
            if result.entries.len() > u32::MAX as usize || bytes > u32::MAX as usize {
                return BUFFER_TOO_SMALL;
            }
            unsafe {
                detail_count.write(result.entries.len() as u32);
                utf8_blob_bytes.write(bytes as u32);
            }
            OK
        })
    }

    #[unsafe(no_mangle)]
    pub unsafe extern "C" fn dbf_contract_details_result_copy(
        result: *const ContractDetailsResult,
        details: *mut ContractDetailV1,
        detail_capacity: u32,
        utf8_blob: *mut u8,
        utf8_blob_capacity: u32,
    ) -> Status {
        ffi_status(|| {
            if result.is_null() {
                return INVALID_ARGUMENT;
            }
            let result = unsafe { &*result };
            let required_bytes = result
                .entries
                .iter()
                .flat_map(|entry| entry.strings.iter())
                .map(Vec::len)
                .sum::<usize>();
            if detail_capacity < result.entries.len() as u32
                || (utf8_blob_capacity as usize) < required_bytes
                || (!result.entries.is_empty() && details.is_null())
                || (required_bytes != 0 && utf8_blob.is_null())
            {
                return BUFFER_TOO_SMALL;
            }
            let mut offset = 0usize;
            for (index, entry) in result.entries.iter().enumerate() {
                let mut detail = entry.detail;
                let mut slices = [
                    &mut detail.raw_symbol,
                    &mut detail.asset,
                    &mut detail.underlying,
                    &mut detail.currency,
                    &mut detail.settlement_currency,
                    &mut detail.exchange,
                    &mut detail.security_type,
                    &mut detail.cfi,
                    &mut detail.unit_of_measure,
                ];
                for (source, destination) in entry.strings.iter().zip(slices.iter_mut()) {
                    destination.offset = offset as u32;
                    destination.length = source.len() as u32;
                    if !source.is_empty() {
                        unsafe {
                            ptr::copy_nonoverlapping(
                                source.as_ptr(),
                                utf8_blob.add(offset),
                                source.len(),
                            );
                        }
                        offset += source.len();
                    }
                }
                unsafe {
                    details.add(index).write(detail);
                }
            }
            OK
        })
    }

    #[unsafe(no_mangle)]
    pub unsafe extern "C" fn dbf_contract_details_result_get_error(
        result: *const ContractDetailsResult,
        utf8_buffer: *mut u8,
        utf8_buffer_capacity: u32,
        required_bytes: *mut u32,
    ) -> Status {
        ffi_status(|| {
            if result.is_null() {
                return INVALID_ARGUMENT;
            }
            copy_error(
                &unsafe { &*result }.error,
                utf8_buffer,
                utf8_buffer_capacity,
                required_bytes,
            )
        })
    }

    #[unsafe(no_mangle)]
    pub unsafe extern "C" fn dbf_contract_details_result_destroy(
        result: *mut ContractDetailsResult,
    ) -> Status {
        ffi_status(|| {
            if result.is_null() {
                return INVALID_ARGUMENT;
            }
            unsafe {
                drop(Box::from_raw(result));
            }
            OK
        })
    }

    #[unsafe(no_mangle)]
    pub unsafe extern "C" fn dbf_get_latest_price(
        request: *const LatestPriceRequestV1,
        timeout_ms: u32,
        result: *mut LatestPriceResult64,
    ) -> Status {
        ffi_status(|| {
            if request.is_null() || result.is_null() {
                return INVALID_ARGUMENT;
            }
            unsafe {
                result.write(LatestPriceResult64::default());
            }
            let request = unsafe { &*request };
            if !valid_struct(
                request.struct_size,
                size_of::<LatestPriceRequestV1>(),
                request.abi_version,
            ) {
                return ABI_MISMATCH;
            }
            let valid_policy = matches!(
                request.selected_policy,
                LATEST_PRICE_LAST_TRADE
                    | LATEST_PRICE_QUOTE_MIDPOINT
                    | LATEST_PRICE_BID
                    | LATEST_PRICE_ASK
            );
            let valid_freshness = matches!(
                request.freshness_policy,
                LATEST_PRICE_NEXT_OBSERVED | LATEST_PRICE_REPLAY_LOOKBACK_THEN_LIVE
            );
            let valid_lookback = (request.freshness_policy == LATEST_PRICE_NEXT_OBSERVED
                && request.replay_lookback_ms == 0)
                || (request.freshness_policy == LATEST_PRICE_REPLAY_LOOKBACK_THEN_LIVE
                    && request.replay_lookback_ms != 0);
            if !valid_policy
                || !valid_freshness
                || !valid_lookback
                || !matches!(request.input_symbology, 1 | 2)
                || timeout_ms == 0
                || timeout_ms == WAIT_INFINITE
                || request.utf8_blob.is_null()
                || request.utf8_blob_bytes == 0
                || request.dataset.length == 0
                || request.symbol.length == 0
                || !valid_range(
                    request.dataset.offset,
                    request.dataset.length,
                    request.utf8_blob_bytes,
                )
                || !valid_range(
                    request.symbol.offset,
                    request.symbol.length,
                    request.utf8_blob_bytes,
                )
                || request.reserved32 != 0
                || request.reserved.iter().any(|&v| v != 0)
            {
                return INVALID_ARGUMENT;
            }
            let input = unsafe {
                slice::from_raw_parts(request.utf8_blob, request.utf8_blob_bytes as usize)
            };
            let dataset = &input[request.dataset.offset as usize
                ..(request.dataset.offset + request.dataset.length) as usize];
            let symbol = &input[request.symbol.offset as usize
                ..(request.symbol.offset + request.symbol.length) as usize];
            if contains_nul(dataset) || contains_nul(symbol) {
                return INVALID_ARGUMENT;
            }
            #[cfg(feature = "live")]
            {
                let Ok(dataset) = std::str::from_utf8(dataset).map(str::to_owned) else {
                    return DATABENTO_ERROR;
                };
                let Ok(symbol) = std::str::from_utf8(symbol).map(str::to_owned) else {
                    return DATABENTO_ERROR;
                };
                match crate::live::latest_price(request, dataset, symbol, timeout_ms) {
                    Ok(price) => {
                        unsafe {
                            result.write(price);
                        }
                        OK
                    }
                    Err(status) => status,
                }
            }
            #[cfg(not(feature = "live"))]
            {
                NOT_SUPPORTED
            }
        })
    }

    #[unsafe(no_mangle)]
    pub unsafe extern "C" fn dbf_historical_estimate(
        request: *const HistoricalRequestV1,
        symbols: *const Utf8SliceV1,
        utf8_blob: *const u8,
        utf8_blob_bytes: u32,
        estimate: *mut HistoricalEstimateV1,
    ) -> Status {
        ffi_status(|| {
            if estimate.is_null() {
                return ABI_MISMATCH;
            }
            let estimate_ref = unsafe { &mut *estimate };
            if !valid_struct(
                estimate_ref.struct_size,
                size_of::<HistoricalEstimateV1>(),
                estimate_ref.abi_version,
            ) {
                return ABI_MISMATCH;
            }
            if !valid_historical_request(request, symbols, utf8_blob, utf8_blob_bytes) {
                return INVALID_ARGUMENT;
            }
            let request = unsafe { &*request };
            if request.flags & HISTORICAL_SYNTHETIC != 0 {
                let duration_minutes =
                    ((request.end_ts_ns - request.start_ts_ns) / 60_000_000_000).max(0) as u64;
                let records = duration_minutes.clamp(1, 10_000) * request.symbol_count as u64;
                estimate_ref.estimated_records = records;
                estimate_ref.estimated_bytes = records * size_of::<HistoricalRecord120>() as u64;
                estimate_ref.estimated_cost_usd = 0.0;
                return OK;
            }
            #[cfg(feature = "live")]
            {
                let Ok((dataset, requested)) =
                    historical_inputs(request, symbols, utf8_blob, utf8_blob_bytes)
                else {
                    return DATABENTO_ERROR;
                };
                match crate::live::historical_estimate(*request, dataset, requested) {
                    Ok(value) => {
                        *estimate_ref = value;
                        OK
                    }
                    Err((status, _)) => status,
                }
            }
            #[cfg(not(feature = "live"))]
            {
                NOT_SUPPORTED
            }
        })
    }

    #[unsafe(no_mangle)]
    pub unsafe extern "C" fn dbf_historical_batch_submit(
        request: *const HistoricalRequestV1,
        symbols: *const Utf8SliceV1,
        utf8_blob: *const u8,
        utf8_blob_bytes: u32,
        output: *mut *mut HistoricalResult,
    ) -> Status {
        ffi_status(|| {
            if output.is_null() {
                return INVALID_ARGUMENT;
            }
            unsafe { output.write(ptr::null_mut()) };
            if !valid_historical_request(request, symbols, utf8_blob, utf8_blob_bytes) {
                return INVALID_ARGUMENT;
            }
            let request = unsafe { &*request };
            if request.flags & HISTORICAL_SYNTHETIC != 0 {
                let result = historical_result(
                    br#"{"providerJobId":"synthetic-job","state":"Completed","costUsd":0,"recordCount":2,"billedBytes":240,"progressPercent":100}"#,
                );
                unsafe { output.write(Box::into_raw(result)) };
                return OK;
            }
            #[cfg(feature = "live")]
            {
                let Ok((dataset, requested)) =
                    historical_inputs(request, symbols, utf8_blob, utf8_blob_bytes)
                else {
                    return DATABENTO_ERROR;
                };
                match crate::live::historical_batch_submit(*request, dataset, requested) {
                    Ok(payload) => {
                        unsafe { output.write(Box::into_raw(historical_result(&payload))) };
                        OK
                    }
                    Err((status, message)) => {
                        let mut result = historical_result(&[]);
                        result.error = message.into_bytes();
                        unsafe { output.write(Box::into_raw(result)) };
                        status
                    }
                }
            }
            #[cfg(not(feature = "live"))]
            {
                NOT_SUPPORTED
            }
        })
    }

    #[unsafe(no_mangle)]
    pub unsafe extern "C" fn dbf_historical_batch_get_status(
        provider_job_id: *const u8,
        provider_job_id_bytes: u32,
        output: *mut *mut HistoricalResult,
    ) -> Status {
        ffi_status(|| {
            if output.is_null() || provider_job_id.is_null() || provider_job_id_bytes == 0 {
                return INVALID_ARGUMENT;
            }
            unsafe { output.write(ptr::null_mut()) };
            let job =
                unsafe { slice::from_raw_parts(provider_job_id, provider_job_id_bytes as usize) };
            if job == b"synthetic-job" {
                let result = historical_result(
                    br#"{"providerJobId":"synthetic-job","state":"Completed","costUsd":0,"recordCount":2,"billedBytes":240,"progressPercent":100}"#,
                );
                unsafe { output.write(Box::into_raw(result)) };
                return OK;
            }
            #[cfg(feature = "live")]
            {
                let Ok(job_id) = std::str::from_utf8(job).map(str::to_owned) else {
                    return DATABENTO_ERROR;
                };
                match crate::live::historical_batch_status(job_id, 60_000) {
                    Ok(payload) => {
                        unsafe { output.write(Box::into_raw(historical_result(&payload))) };
                        OK
                    }
                    Err((status, message)) => {
                        let mut result = historical_result(&[]);
                        result.error = message.into_bytes();
                        unsafe { output.write(Box::into_raw(result)) };
                        status
                    }
                }
            }
            #[cfg(not(feature = "live"))]
            {
                NOT_SUPPORTED
            }
        })
    }

    #[unsafe(no_mangle)]
    pub unsafe extern "C" fn dbf_historical_batch_list_files(
        provider_job_id: *const u8,
        provider_job_id_bytes: u32,
        output: *mut *mut HistoricalResult,
    ) -> Status {
        ffi_status(|| {
            if output.is_null() || provider_job_id.is_null() || provider_job_id_bytes == 0 {
                return INVALID_ARGUMENT;
            }
            unsafe { output.write(ptr::null_mut()) };
            let job =
                unsafe { slice::from_raw_parts(provider_job_id, provider_job_id_bytes as usize) };
            if job == b"synthetic-job" {
                let result = historical_result(
                    br#"{"files":[{"providerFileId":"synthetic.csv","fileName":"synthetic.csv","schema":2}]}"#,
                );
                unsafe { output.write(Box::into_raw(result)) };
                return OK;
            }
            #[cfg(feature = "live")]
            {
                let Ok(job_id) = std::str::from_utf8(job).map(str::to_owned) else {
                    return DATABENTO_ERROR;
                };
                match crate::live::historical_batch_files(job_id, 60_000) {
                    Ok(payload) => {
                        unsafe { output.write(Box::into_raw(historical_result(&payload))) };
                        OK
                    }
                    Err((status, message)) => {
                        let mut result = historical_result(&[]);
                        result.error = message.into_bytes();
                        unsafe { output.write(Box::into_raw(result)) };
                        status
                    }
                }
            }
            #[cfg(not(feature = "live"))]
            {
                NOT_SUPPORTED
            }
        })
    }

    #[unsafe(no_mangle)]
    pub unsafe extern "C" fn dbf_historical_batch_download_file(
        provider_job_id: *const u8,
        provider_job_id_bytes: u32,
        file_name: *const u8,
        file_name_bytes: u32,
        destination_path: *const u8,
        destination_path_bytes: u32,
    ) -> Status {
        ffi_status(|| {
            if provider_job_id.is_null()
                || provider_job_id_bytes == 0
                || file_name.is_null()
                || file_name_bytes == 0
                || destination_path.is_null()
                || destination_path_bytes == 0
            {
                return INVALID_ARGUMENT;
            }
            let job =
                unsafe { slice::from_raw_parts(provider_job_id, provider_job_id_bytes as usize) };
            let path_bytes =
                unsafe { slice::from_raw_parts(destination_path, destination_path_bytes as usize) };
            let Ok(path_text) = std::str::from_utf8(path_bytes) else {
                return OS_ERROR;
            };
            let path = std::path::Path::new(path_text);
            if job == b"synthetic-job" {
                if let Some(parent) = path.parent()
                    && std::fs::create_dir_all(parent).is_err()
                {
                    return OS_ERROR;
                }
                let payload = b"2,SYNTH,1000,7,1770000000000000000,1,5000000000,5002000000,4998000000,5000500000,10,T,B,0\n2,SYNTH,1001,7,1770000060000000000,2,5001000000,5003000000,4999000000,5001500000,11,T,A,0\n";
                return if std::fs::write(path, payload).is_err() {
                    OS_ERROR
                } else {
                    OK
                };
            }
            #[cfg(feature = "live")]
            {
                let file = unsafe { slice::from_raw_parts(file_name, file_name_bytes as usize) };
                let (Ok(job_id), Ok(file_name)) = (
                    std::str::from_utf8(job).map(str::to_owned),
                    std::str::from_utf8(file).map(str::to_owned),
                ) else {
                    return DATABENTO_ERROR;
                };
                match crate::live::historical_batch_download(
                    job_id,
                    file_name,
                    path.to_path_buf(),
                    60_000,
                ) {
                    Ok(()) => OK,
                    Err((status, _)) => status,
                }
            }
            #[cfg(not(feature = "live"))]
            {
                NOT_SUPPORTED
            }
        })
    }

    #[unsafe(no_mangle)]
    pub unsafe extern "C" fn dbf_historical_range_open(
        request: *const HistoricalRequestV1,
        symbols: *const Utf8SliceV1,
        utf8_blob: *const u8,
        utf8_blob_bytes: u32,
        output: *mut *mut HistoricalResult,
    ) -> Status {
        ffi_status(|| {
            if output.is_null() {
                return INVALID_ARGUMENT;
            }
            unsafe { output.write(ptr::null_mut()) };
            if !valid_historical_request(request, symbols, utf8_blob, utf8_blob_bytes) {
                return INVALID_ARGUMENT;
            }
            let request = unsafe { &*request };
            if request.flags & HISTORICAL_SYNTHETIC != 0 {
                let mut result = historical_result(&[]);
                result.records.push(synthetic_historical_record(request, 0));
                result.records.push(synthetic_historical_record(request, 1));
                unsafe { output.write(Box::into_raw(result)) };
                return OK;
            }
            #[cfg(feature = "live")]
            {
                let Ok((dataset, requested)) =
                    historical_inputs(request, symbols, utf8_blob, utf8_blob_bytes)
                else {
                    return DATABENTO_ERROR;
                };
                match crate::live::historical_range_records(*request, dataset, requested) {
                    Ok(records) => {
                        let mut result = historical_result(&[]);
                        result.records = records;
                        unsafe { output.write(Box::into_raw(result)) };
                        OK
                    }
                    Err((status, message)) => {
                        let mut result = historical_result(&[]);
                        result.error = message.into_bytes();
                        unsafe { output.write(Box::into_raw(result)) };
                        status
                    }
                }
            }
            #[cfg(not(feature = "live"))]
            {
                NOT_SUPPORTED
            }
        })
    }

    #[unsafe(no_mangle)]
    pub unsafe extern "C" fn dbf_historical_file_open(
        file_path: *const u8,
        file_path_bytes: u32,
        schema: u32,
        output: *mut *mut HistoricalResult,
    ) -> Status {
        ffi_status(|| {
            if output.is_null()
                || file_path.is_null()
                || file_path_bytes == 0
                || !(HISTORICAL_DEFINITION..=HISTORICAL_OHLCV_1D).contains(&schema)
            {
                return INVALID_ARGUMENT;
            }
            unsafe { output.write(ptr::null_mut()) };
            #[cfg(feature = "live")]
            {
                let path = unsafe { slice::from_raw_parts(file_path, file_path_bytes as usize) };
                let Ok(path) = std::str::from_utf8(path) else {
                    return DATABENTO_ERROR;
                };
                match crate::live::historical_file_records(std::path::Path::new(path), schema) {
                    Ok(records) => {
                        let mut result = historical_result(&[]);
                        result.records = records;
                        unsafe { output.write(Box::into_raw(result)) };
                        OK
                    }
                    Err((status, message)) => {
                        let mut result = historical_result(&[]);
                        result.error = message.into_bytes();
                        unsafe { output.write(Box::into_raw(result)) };
                        status
                    }
                }
            }
            #[cfg(not(feature = "live"))]
            {
                NOT_SUPPORTED
            }
        })
    }

    #[unsafe(no_mangle)]
    pub unsafe extern "C" fn dbf_historical_result_get_payload(
        result: *const HistoricalResult,
        utf8_buffer: *mut u8,
        utf8_buffer_capacity: u32,
        required_bytes: *mut u32,
    ) -> Status {
        ffi_status(|| {
            if result.is_null() {
                return INVALID_ARGUMENT;
            }
            copy_error(
                &unsafe { &*result }.payload,
                utf8_buffer,
                utf8_buffer_capacity,
                required_bytes,
            )
        })
    }

    #[unsafe(no_mangle)]
    pub unsafe extern "C" fn dbf_historical_result_get_next_batch(
        result: *mut HistoricalResult,
        records: *mut HistoricalRecord120,
        record_capacity: u32,
        batch: *mut HistoricalBatchV1,
    ) -> Status {
        ffi_status(|| {
            if result.is_null() || records.is_null() || record_capacity == 0 || batch.is_null() {
                return INVALID_ARGUMENT;
            }
            let batch_ref = unsafe { &mut *batch };
            if !valid_struct(
                batch_ref.struct_size,
                size_of::<HistoricalBatchV1>(),
                batch_ref.abi_version,
            ) {
                return INVALID_ARGUMENT;
            }
            let result = unsafe { &mut *result };
            let remaining = result.records.len().saturating_sub(result.cursor);
            let count = remaining.min(record_capacity as usize);
            if count != 0 {
                unsafe {
                    ptr::copy_nonoverlapping(
                        result.records.as_ptr().add(result.cursor),
                        records,
                        count,
                    )
                };
            }
            result.cursor += count;
            batch_ref.records_read = count as u32;
            batch_ref.more_available = u32::from(result.cursor < result.records.len());
            batch_ref.batch_ordinal = result.batch_ordinal;
            result.batch_ordinal += 1;
            OK
        })
    }

    #[unsafe(no_mangle)]
    pub unsafe extern "C" fn dbf_historical_result_get_error(
        result: *const HistoricalResult,
        utf8_buffer: *mut u8,
        utf8_buffer_capacity: u32,
        required_bytes: *mut u32,
    ) -> Status {
        ffi_status(|| {
            if result.is_null() {
                return INVALID_ARGUMENT;
            }
            copy_error(
                &unsafe { &*result }.error,
                utf8_buffer,
                utf8_buffer_capacity,
                required_bytes,
            )
        })
    }

    #[unsafe(no_mangle)]
    pub unsafe extern "C" fn dbf_historical_result_destroy(
        result: *mut HistoricalResult,
    ) -> Status {
        ffi_status(|| {
            if result.is_null() {
                return INVALID_ARGUMENT;
            }
            unsafe { drop(Box::from_raw(result)) };
            OK
        })
    }
}

#[cfg(windows)]
pub use exports::*;
