use std::cell::UnsafeCell;
use std::cmp;
use std::collections::HashSet;
use std::hint;
use std::sync::atomic::{AtomicBool, AtomicI32, AtomicU32, AtomicU64, Ordering};
use std::sync::{Condvar, Mutex, MutexGuard};
use std::thread::{self, JoinHandle};
use std::time::{Duration, Instant};

use crate::abi::*;
use crate::windows::{self, Pages, Signal};

#[derive(Clone)]
pub struct Mapping {
    pub subscription_index: u32,
    pub instrument_id: u32,
    pub publisher_id: u16,
    pub data_kinds: u32,
    pub input_symbology: u32,
    pub requested_symbol: Vec<u8>,
    pub raw_symbol: Vec<u8>,
    pub resolved: bool,
}

#[derive(Clone, Copy)]
struct SyntheticMapping {
    instrument_id: u32,
    publisher_id: u16,
    record_kinds: [u8; 4],
    record_kind_count: usize,
    next_record_kind: usize,
    record_kind_step: usize,
}

impl From<&Mapping> for SyntheticMapping {
    fn from(mapping: &Mapping) -> Self {
        let mut record_kinds = [RECORD_QUOTE; 4];
        let mut record_kind_count = 0usize;
        for (flag, kind) in [
            (MARKET_DATA_QUOTE, RECORD_QUOTE),
            (MARKET_DATA_TRADE, RECORD_TRADE),
            (MARKET_DATA_MBO, RECORD_MBO),
            (MARKET_DATA_STATISTICS, RECORD_STATISTICS),
        ] {
            if mapping.data_kinds & flag != 0 {
                record_kinds[record_kind_count] = kind;
                record_kind_count += 1;
            }
        }
        if record_kind_count == 0 {
            record_kind_count = 1;
        }
        Self {
            instrument_id: mapping.instrument_id,
            publisher_id: mapping.publisher_id,
            record_kinds,
            record_kind_count,
            next_record_kind: 0,
            record_kind_step: 0,
        }
    }
}

impl SyntheticMapping {
    #[inline(always)]
    fn take_record_kind(&mut self) -> u8 {
        let kind = self.record_kinds[self.next_record_kind];
        self.next_record_kind += self.record_kind_step;
        if self.next_record_kind >= self.record_kind_count {
            self.next_record_kind -= self.record_kind_count;
        }
        kind
    }
}

#[repr(transparent)]
struct RingSlot(UnsafeCell<MarketRecord64>);
unsafe impl Sync for RingSlot {}

#[repr(align(64))]
struct RingCursor(AtomicU64);

struct ReadBuffer {
    pages: Pages,
    capacity: u32,
}

pub struct Feed {
    pub config: FeedConfigV1,
    pub dataset: Vec<u8>,
    ring_pages: Pages,
    ring_capacity: u64,
    ring_mask: u64,
    head: RingCursor,
    tail: RingCursor,
    signal: Signal,
    control: Mutex<()>,
    control_cv: Condvar,
    start_deadline: Mutex<Option<Instant>>,
    producer: Mutex<Option<JoinHandle<()>>>,
    stop_requested: AtomicBool,
    producer_done: AtomicBool,
    consumer_ready: AtomicBool,
    pub state: AtomicU32,
    terminal_status: AtomicI32,
    pub mappings: Mutex<Vec<Mapping>>,
    read_buffer: Mutex<Option<ReadBuffer>>,
    ring_high_water: AtomicU64,
    records_consumed: AtomicU64,
    signal_count: AtomicU64,
    wait_count: AtomicU64,
    ring_full_episodes: AtomicU64,
    ring_overruns: AtomicU64,
    subscription_acknowledgements: AtomicU64,
    heartbeat_messages: AtomicU64,
    last_message_monotonic_ns: AtomicU64,
    observed_producer_location: AtomicU32,
    producer_affinity_verified: AtomicU32,
    last_producer_location: AtomicU32,
    producer_processor_sample_count: AtomicU64,
    producer_processor_migration_count: AtomicU64,
    producer_off_assignment_count: AtomicU32,
    observed_processors: Mutex<HashSet<u32>>,
    producer_using_alternate: AtomicBool,
    error: Mutex<Vec<u8>>,
    performance_clock: windows::PerformanceClock,
}

unsafe impl Send for Feed {}
unsafe impl Sync for Feed {}

fn lock<T>(mutex: &Mutex<T>) -> MutexGuard<'_, T> {
    mutex.lock().unwrap_or_else(|error| error.into_inner())
}

impl Feed {
    pub fn new(config: FeedConfigV1, dataset: Vec<u8>) -> Result<Box<Self>, Status> {
        if !config
            .ring_memory_bytes
            .is_multiple_of(size_of::<MarketRecord64>() as u64)
        {
            return Err(INVALID_ARGUMENT);
        }
        let capacity = config.ring_memory_bytes / size_of::<MarketRecord64>() as u64;
        if capacity < 2
            || !capacity.is_power_of_two()
            || config.ring_memory_bytes > usize::MAX as u64
        {
            return Err(INVALID_ARGUMENT);
        }
        let mut ring_pages = Pages::allocate(
            config.ring_memory_bytes as usize,
            config.numa_node,
            config.flags & CONFIG_REQUIRE_NUMA_LOCALITY != 0,
        )?;
        if config.flags & CONFIG_LOCK_RING_MEMORY != 0
            && !ring_pages.lock()
            && config.flags & CONFIG_REQUIRE_LOCKED_MEMORY != 0
        {
            return Err(MEMORY_LOCK_FAILED);
        }
        let signal = Signal::new()?;
        let performance_clock = windows::PerformanceClock::new()?;
        Ok(Box::new(Self {
            config,
            dataset,
            ring_pages,
            ring_capacity: capacity,
            ring_mask: capacity - 1,
            head: RingCursor(AtomicU64::new(0)),
            tail: RingCursor(AtomicU64::new(0)),
            signal,
            control: Mutex::new(()),
            control_cv: Condvar::new(),
            start_deadline: Mutex::new(None),
            producer: Mutex::new(None),
            stop_requested: AtomicBool::new(false),
            producer_done: AtomicBool::new(false),
            consumer_ready: AtomicBool::new(false),
            state: AtomicU32::new(STATE_CREATED),
            terminal_status: AtomicI32::new(OK),
            mappings: Mutex::new(Vec::new()),
            read_buffer: Mutex::new(None),
            ring_high_water: AtomicU64::new(0),
            records_consumed: AtomicU64::new(0),
            signal_count: AtomicU64::new(0),
            wait_count: AtomicU64::new(0),
            ring_full_episodes: AtomicU64::new(0),
            ring_overruns: AtomicU64::new(0),
            subscription_acknowledgements: AtomicU64::new(0),
            heartbeat_messages: AtomicU64::new(0),
            last_message_monotonic_ns: AtomicU64::new(0),
            observed_producer_location: AtomicU32::new(u32::MAX),
            producer_affinity_verified: AtomicU32::new(0),
            last_producer_location: AtomicU32::new(u32::MAX),
            producer_processor_sample_count: AtomicU64::new(0),
            producer_processor_migration_count: AtomicU64::new(0),
            producer_off_assignment_count: AtomicU32::new(0),
            observed_processors: Mutex::new(HashSet::new()),
            producer_using_alternate: AtomicBool::new(false),
            error: Mutex::new(Vec::new()),
            performance_clock,
        }))
    }

    pub fn set_error(&self, status: Status, message: &[u8]) {
        self.terminal_status.store(status, Ordering::Release);
        let mut error = lock(&self.error);
        error.clear();
        error.extend_from_slice(message);
    }
    pub fn notify_control(&self) {
        self.control_cv.notify_all();
    }
    pub fn terminal_status(&self) -> Status {
        self.terminal_status.load(Ordering::Acquire)
    }
    pub fn record_transport_message(&self) {
        self.last_message_monotonic_ns.store(
            self.performance_clock.now_nanoseconds().max(0) as u64, Ordering::Relaxed);
    }
    pub fn record_subscription_acknowledgement(&self) {
        self.subscription_acknowledgements.fetch_add(1, Ordering::Relaxed);
    }
    pub fn record_heartbeat(&self) {
        self.heartbeat_messages.fetch_add(1, Ordering::Relaxed);
    }
    pub fn notify(&self) {
        self.signal_count.fetch_add(1, Ordering::Relaxed);
        self.signal.notify();
    }
    pub fn wait_signal(&self, timeout_ms: u32) -> Status {
        self.wait_count.fetch_add(1, Ordering::Relaxed);
        self.signal.wait(timeout_ms)
    }
    fn ring_ptr(&self) -> *mut RingSlot {
        self.ring_pages.as_ptr()
    }
    pub fn available(&self) -> u64 {
        self.head.0.load(Ordering::Acquire) - self.tail.0.load(Ordering::Acquire)
    }

    pub fn allocate_read_buffer(&self, capacity: u32) -> Result<*mut MarketRecord64, Status> {
        if capacity == 0 {
            return Err(INVALID_ARGUMENT);
        }
        if lock(&self.producer).is_some() {
            return Err(INVALID_STATE);
        }
        let mut read = lock(&self.read_buffer);
        if read.is_some() {
            return Err(INVALID_STATE);
        }
        let bytes = (capacity as usize)
            .checked_mul(size_of::<MarketRecord64>())
            .ok_or(NO_MEMORY)?;
        let pages = Pages::allocate(
            bytes,
            self.config.numa_node,
            self.config.flags & CONFIG_REQUIRE_NUMA_LOCALITY != 0,
        )?;
        let pointer = pages.as_ptr();
        *read = Some(ReadBuffer { pages, capacity });
        Ok(pointer)
    }

    pub fn read_buffer_info(&self) -> Option<(*mut MarketRecord64, u32)> {
        lock(&self.read_buffer)
            .as_ref()
            .map(|buffer| (buffer.pages.as_ptr(), buffer.capacity))
    }

    pub fn free_read_buffer(&self, pointer: *mut MarketRecord64) -> Status {
        if pointer.is_null() {
            return INVALID_ARGUMENT;
        }
        let mut producer = lock(&self.producer);
        if producer.is_some() {
            if !self.producer_done.load(Ordering::Acquire) {
                return INVALID_STATE;
            }
            let handle = producer.take().expect("producer handle disappeared");
            drop(producer);
            let _ = handle.join();
            producer = lock(&self.producer);
        }
        drop(producer);
        let mut read = lock(&self.read_buffer);
        if read
            .as_ref()
            .is_none_or(|buffer| buffer.pages.as_ptr::<MarketRecord64>() != pointer)
        {
            return INVALID_ARGUMENT;
        }
        *read = None;
        OK
    }

    pub fn start(&self, timeout_ms: u32) -> Status {
        if self.read_buffer_info().is_none() {
            return INVALID_ARGUMENT;
        }
        if self.state.load(Ordering::Acquire) != STATE_SUBSCRIBED || lock(&self.mappings).is_empty()
        {
            return INVALID_STATE;
        }
        #[cfg(not(feature = "live"))]
        if self.config.data_source == DATA_SOURCE_DATABENTO_LIVE {
            return NOT_SUPPORTED;
        }
        if self.config.data_source == DATA_SOURCE_DATABENTO_LIVE && timeout_ms == WAIT_INFINITE {
            return INVALID_ARGUMENT;
        }
        *lock(&self.start_deadline) = if timeout_ms == WAIT_INFINITE {
            None
        } else {
            Some(Instant::now() + Duration::from_millis(timeout_ms.into()))
        };
        self.state.store(STATE_STARTING, Ordering::Release);
        let address = self as *const Self as usize;
        let spawned = thread::Builder::new()
            .name("ifm-databento-rust-producer".into())
            .spawn(move || unsafe { (&*(address as *const Feed)).producer_main() });
        let handle = match spawned {
            Ok(handle) => handle,
            Err(_) => {
                self.set_error(OS_ERROR, b"Unable to start synthetic producer thread");
                self.state.store(STATE_FAULTED, Ordering::Release);
                return OS_ERROR;
            }
        };
        *lock(&self.producer) = Some(handle);
        let guard = lock(&self.control);
        let ready = |_: &mut ()| {
            matches!(
                self.state.load(Ordering::Acquire),
                STATE_CONSUMER_SETUP | STATE_FAULTED
            )
        };
        if timeout_ms == WAIT_INFINITE {
            drop(
                self.control_cv
                    .wait_while(guard, |unit| !ready(unit))
                    .unwrap_or_else(|error| error.into_inner()),
            );
        } else {
            let (_guard, timeout) = self
                .control_cv
                .wait_timeout_while(guard, Duration::from_millis(timeout_ms.into()), |unit| {
                    !ready(unit)
                })
                .unwrap_or_else(|error| error.into_inner());
            if timeout.timed_out()
                && !matches!(
                    self.state.load(Ordering::Acquire),
                    STATE_CONSUMER_SETUP | STATE_FAULTED
                )
            {
                return TIMEOUT;
            }
        }
        if self.state.load(Ordering::Acquire) == STATE_FAULTED {
            self.terminal_status.load(Ordering::Acquire)
        } else {
            OK
        }
    }

    unsafe fn producer_main(&self) {
        self.apply_thread_settings();
        if self.terminal_status.load(Ordering::Acquire) != OK {
            self.state.store(STATE_FAULTED, Ordering::Release);
            self.finish_producer();
            return;
        }
        #[cfg(feature = "live")]
        if self.config.data_source == DATA_SOURCE_DATABENTO_LIVE {
            crate::live::run_feed(self);
            self.finish_producer();
            return;
        }
        self.state.store(STATE_CONSUMER_SETUP, Ordering::Release);
        self.control_cv.notify_all();
        let guard = lock(&self.control);
        drop(
            self.control_cv
                .wait_while(guard, |_| {
                    !self.consumer_ready.load(Ordering::Acquire)
                        && !self.stop_requested.load(Ordering::Acquire)
                })
                .unwrap_or_else(|error| error.into_inner()),
        );
        if !self.stop_requested.load(Ordering::Acquire) {
            self.enter_running();
        }
        let record_count = if self.config.synthetic_record_count == 0 {
            100_000
        } else {
            self.config.synthetic_record_count
        };
        let start_sequence = if self.config.synthetic_start_sequence == 0 {
            1
        } else {
            self.config.synthetic_start_sequence
        };
        // Synthetic subscriptions are immutable after start. Snapshot once so the hot path
        // never takes the mapping mutex or clones symbol buffers per record.
        let mut mappings: Vec<_> = lock(&self.mappings)
            .iter()
            .map(SyntheticMapping::from)
            .collect();
        let mapping_count = mappings.len();
        for (index, mapping) in mappings.iter_mut().enumerate() {
            mapping.next_record_kind =
                start_sequence.wrapping_add(index as u64) as usize % mapping.record_kind_count;
            mapping.record_kind_step = mapping_count % mapping.record_kind_count;
        }
        let mut next_due = Instant::now();
        let mut mapping_index = 0usize;
        for index in 0..u64::from(record_count) {
            if self.stop_requested.load(Ordering::Acquire) {
                break;
            }
            let mapping = &mut mappings[mapping_index];
            mapping_index += 1;
            if mapping_index == mapping_count {
                mapping_index = 0;
            }
            let sequence = start_sequence.wrapping_add(index);
            let timestamp = self.performance_clock.now_nanoseconds();
            let record_kind = mapping.take_record_kind();
            if !self.publish(make_synthetic_record(
                mapping,
                record_kind,
                sequence,
                timestamp,
            )) {
                break;
            }
            if self.config.synthetic_records_per_second != 0 {
                next_due += Duration::from_nanos(
                    1_000_000_000 / u64::from(self.config.synthetic_records_per_second),
                );
                if let Some(delay) = next_due.checked_duration_since(Instant::now()) {
                    thread::sleep(delay);
                }
            }
        }
        if self.state.load(Ordering::Acquire) != STATE_FAULTED {
            self.state.store(STATE_STOPPED, Ordering::Release);
        }
        self.finish_producer();
    }

    fn finish_producer(&self) {
        if self.state.load(Ordering::Acquire) != STATE_FAULTED {
            self.state.store(STATE_STOPPED, Ordering::Release);
        }
        self.producer_done.store(true, Ordering::Release);
        self.control_cv.notify_all();
        self.notify();
    }

    #[cfg(feature = "live")]
    pub(crate) fn remaining_start_milliseconds(&self) -> u32 {
        lock(&self.start_deadline).map_or(u32::MAX, |deadline| {
            let now = Instant::now();
            if now >= deadline {
                0
            } else {
                deadline
                    .duration_since(now)
                    .as_millis()
                    .clamp(1, u128::from(u32::MAX)) as u32
            }
        })
    }

    #[cfg(feature = "live")]
    pub(crate) fn stop_requested(&self) -> bool {
        self.stop_requested.load(Ordering::Acquire)
    }

    #[cfg(feature = "live")]
    pub(crate) fn mappings_snapshot(&self) -> Vec<Mapping> {
        lock(&self.mappings).clone()
    }

    #[cfg(feature = "live")]
    pub(crate) fn is_session_volume_instrument(&self, instrument_id: u32) -> bool {
        lock(&self.mappings).iter().any(|mapping| {
            mapping.instrument_id == instrument_id
                && mapping.data_kinds & MARKET_DATA_SESSION_VOLUME != 0
        })
    }

    #[cfg(feature = "live")]
    pub(crate) fn all_mappings_resolved(&self) -> bool {
        lock(&self.mappings).iter().all(|mapping| mapping.resolved)
    }

    #[cfg(feature = "live")]
    pub(crate) fn resolve_mapping(
        &self,
        requested: &[u8],
        instrument_id: u32,
        publisher_id: u16,
        allow_new: bool,
    ) -> Result<(), (Status, &'static [u8])> {
        if instrument_id == 0 {
            return Err((
                SYMBOL_RESOLUTION_FAILED,
                b"Databento returned a symbol mapping without an instrument ID",
            ));
        }
        let mut mappings = lock(&self.mappings);
        let mut found = false;
        for mapping in mappings
            .iter_mut()
            .filter(|mapping| mapping.requested_symbol == requested)
        {
            found = true;
            if mapping.instrument_id != 0 && mapping.instrument_id != instrument_id {
                return Err((
                    SYMBOL_RESOLUTION_FAILED,
                    b"A resolved symbol remapped to a different instrument",
                ));
            }
            if mapping.publisher_id != 0
                && publisher_id != 0
                && mapping.publisher_id != publisher_id
            {
                return Err((
                    SYMBOL_RESOLUTION_FAILED,
                    b"A resolved symbol remapped to a different publisher",
                ));
            }
            mapping.instrument_id = instrument_id;
            if publisher_id != 0 {
                mapping.publisher_id = publisher_id;
            }
            mapping.raw_symbol = mapping.requested_symbol.clone();
            mapping.resolved = mapping.publisher_id != 0;
        }
        if !found && !allow_new {
            return Err((
                SYMBOL_RESOLUTION_FAILED,
                b"Databento returned an unexpected ticker mapping",
            ));
        }
        Ok(())
    }

    #[cfg(feature = "live")]
    pub(crate) fn resolve_mapping_publisher(
        &self,
        instrument_id: u32,
        publisher_id: u16,
    ) -> Result<(), (Status, &'static [u8])> {
        if instrument_id == 0 || publisher_id == 0 {
            return Ok(());
        }
        for mapping in lock(&self.mappings)
            .iter_mut()
            .filter(|mapping| mapping.instrument_id == instrument_id)
        {
            if mapping.publisher_id != 0 && mapping.publisher_id != publisher_id {
                return Err((
                    SYMBOL_RESOLUTION_FAILED,
                    b"A resolved instrument produced data from a different publisher",
                ));
            }
            mapping.publisher_id = publisher_id;
            mapping.resolved = true;
        }
        Ok(())
    }

    #[cfg(feature = "live")]
    pub(crate) fn publish_live(&self, record: MarketRecord64) -> bool {
        self.publish(record)
    }

    #[cfg(feature = "live")]
    pub(crate) fn fail_live(&self, status: Status, message: impl AsRef<[u8]>) {
        if self.terminal_status.load(Ordering::Acquire) == OK {
            self.set_error(status, message.as_ref());
        }
        self.state.store(STATE_FAULTED, Ordering::Release);
        self.control_cv.notify_all();
        self.notify();
    }

    #[cfg(feature = "live")]
    pub(crate) fn enter_consumer_setup(&self) {
        if !self.stop_requested() {
            self.state.store(STATE_CONSUMER_SETUP, Ordering::Release);
        }
        self.control_cv.notify_all();
    }

    #[cfg(feature = "live")]
    pub(crate) fn wait_for_consumer(&self) {
        let guard = lock(&self.control);
        drop(
            self.control_cv
                .wait_while(guard, |_| {
                    !self.consumer_ready.load(Ordering::Acquire) && !self.stop_requested()
                })
                .unwrap_or_else(|error| error.into_inner()),
        );
    }

    pub(crate) fn enter_running(&self) {
        self.state.store(STATE_RUNNING, Ordering::Release);
        self.control_cv.notify_all();
    }

    #[cfg(feature = "live")]
    pub(crate) fn startup_record_capacity(&self) -> usize {
        self.ring_capacity as usize
    }

    pub fn set_consumer_ready(&self, timeout_ms: u32) -> Status {
        if self.state.load(Ordering::Acquire) != STATE_CONSUMER_SETUP {
            return INVALID_STATE;
        }
        self.consumer_ready.store(true, Ordering::Release);
        self.control_cv.notify_all();
        let guard = lock(&self.control);
        let ready = |_: &mut ()| {
            matches!(
                self.state.load(Ordering::Acquire),
                STATE_RUNNING | STATE_FAULTED
            ) || self.stop_requested.load(Ordering::Acquire)
        };
        if timeout_ms == WAIT_INFINITE {
            drop(
                self.control_cv
                    .wait_while(guard, |unit| !ready(unit))
                    .unwrap_or_else(|error| error.into_inner()),
            );
        } else {
            let (_guard, timeout) = self
                .control_cv
                .wait_timeout_while(guard, Duration::from_millis(timeout_ms.into()), |unit| {
                    !ready(unit)
                })
                .unwrap_or_else(|error| error.into_inner());
            if timeout.timed_out() && !ready(&mut ()) {
                return TIMEOUT;
            }
        }
        if self.state.load(Ordering::Acquire) == STATE_FAULTED {
            self.terminal_status.load(Ordering::Acquire)
        } else {
            OK
        }
    }

    #[inline(always)]
    fn publish(&self, record: MarketRecord64) -> bool {
        let head = self.head.0.load(Ordering::Relaxed);
        if !self.apply_forced_migration(head) {
            self.set_error(
                AFFINITY_CONFIGURATION_FAILED,
                b"Native producer forced migration failed",
            );
            self.state.store(STATE_FAULTED, Ordering::Release);
            self.notify();
            return false;
        }
        let mut tail = self.tail.0.load(Ordering::Acquire);
        if head - tail == self.ring_capacity {
            self.ring_full_episodes.fetch_add(1, Ordering::Relaxed);
            let deadline =
                Instant::now() + Duration::from_micros(u64::from(self.config.ring_full_timeout_us));
            let mut spins = 0;
            loop {
                if self.stop_requested.load(Ordering::Acquire) {
                    return false;
                }
                if spins < self.config.spin_iterations {
                    hint::spin_loop();
                    spins += 1;
                } else {
                    thread::yield_now();
                }
                tail = self.tail.0.load(Ordering::Acquire);
                if head - tail < self.ring_capacity || Instant::now() >= deadline {
                    break;
                }
            }
            if head - tail == self.ring_capacity {
                self.ring_overruns.fetch_add(1, Ordering::Relaxed);
                self.set_error(
                    RING_OVERRUN,
                    b"Synthetic producer exhausted the native ring deadline",
                );
                self.state.store(STATE_FAULTED, Ordering::Release);
                self.notify();
                return false;
            }
        }
        let was_empty = head == tail;
        unsafe {
            (*self
                .ring_ptr()
                .add((head & self.ring_mask) as usize)
                .cast::<RingSlot>())
            .0
            .get()
            .write(record);
        }
        self.head.0.store(head + 1, Ordering::Release);
        let ring_used = head + 1 - tail;
        if ring_used > self.ring_high_water.load(Ordering::Relaxed) {
            // The ring has exactly one producer, so this monotonic statistic does
            // not require a compare/exchange loop on every new high-water mark.
            self.ring_high_water.store(ring_used, Ordering::Relaxed);
        }
        self.record_processor_residency();
        if was_empty {
            self.notify();
        }
        true
    }

    pub unsafe fn read_batch(
        &self,
        destination: *mut MarketRecord64,
        capacity: u32,
        result: &mut BatchResultV1,
    ) -> Status {
        let Some((registered, registered_capacity)) = self.read_buffer_info() else {
            return INVALID_ARGUMENT;
        };
        if destination != registered || capacity == 0 || capacity > registered_capacity {
            return INVALID_ARGUMENT;
        }
        let mut tail = self.tail.0.load(Ordering::Relaxed);
        let head = self.head.0.load(Ordering::Acquire);
        let available = head - tail;
        let count = cmp::min(available, u64::from(capacity)) as u32;
        result.records_read = count;
        result.more_available = u32::from(available > u64::from(count));
        result.first_sequence = if count == 0 {
            0
        } else {
            unsafe {
                (*(*self.ring_ptr().add((tail & self.ring_mask) as usize))
                    .0
                    .get())
                .header
                .sequence
                .into()
            }
        };
        for index in 0..count as usize {
            let record = unsafe {
                *(*self
                    .ring_ptr()
                    .add(((tail + index as u64) & self.ring_mask) as usize))
                .0
                .get()
            };
            unsafe {
                destination.add(index).write(record);
            }
        }
        result.last_sequence = if count == 0 {
            0
        } else {
            unsafe {
                (*destination.add(count as usize - 1))
                    .header
                    .sequence
                    .into()
            }
        };
        if count != 0 {
            tail += u64::from(count);
            self.tail.0.store(tail, Ordering::Release);
            self.records_consumed
                .fetch_add(u64::from(count), Ordering::Relaxed);
        }
        OK
    }

    pub fn stop(&self, timeout_ms: u32) -> Status {
        self.stop_requested.store(true, Ordering::Release);
        let prior = self.state.load(Ordering::Acquire);
        if prior != STATE_STOPPED && prior != STATE_FAULTED {
            self.state.store(STATE_STOPPING, Ordering::Release);
        }
        self.control_cv.notify_all();
        self.notify();
        let mut producer = lock(&self.producer);
        if producer.is_some() {
            let guard = lock(&self.control);
            if timeout_ms == WAIT_INFINITE {
                drop(
                    self.control_cv
                        .wait_while(guard, |_| !self.producer_done.load(Ordering::Acquire))
                        .unwrap_or_else(|error| error.into_inner()),
                );
            } else {
                let (_guard, timeout) = self
                    .control_cv
                    .wait_timeout_while(guard, Duration::from_millis(timeout_ms.into()), |_| {
                        !self.producer_done.load(Ordering::Acquire)
                    })
                    .unwrap_or_else(|error| error.into_inner());
                if timeout.timed_out() && !self.producer_done.load(Ordering::Acquire) {
                    return TIMEOUT;
                }
            }
            let handle = producer.take().expect("producer handle disappeared");
            drop(producer);
            let _ = handle.join();
        }
        if self.state.load(Ordering::Acquire) != STATE_FAULTED {
            self.state.store(STATE_STOPPED, Ordering::Release);
        }
        OK
    }

    pub fn can_destroy(&self) -> bool {
        lock(&self.producer)
            .as_ref()
            .is_none_or(|_| self.producer_done.load(Ordering::Acquire))
    }
    pub fn join_completed(&self) {
        let handle = lock(&self.producer).take();
        if let Some(handle) = handle {
            let _ = handle.join();
        }
    }

    pub fn fill_stats(&self, stats: &mut StatsV1) {
        let location = self.observed_producer_location.load(Ordering::Acquire);
        stats.state = self.state.load(Ordering::Acquire);
        stats.terminal_status = self.terminal_status.load(Ordering::Acquire);
        stats.ring_capacity_records = self.ring_capacity;
        stats.ring_used_records = self.available();
        stats.ring_high_water_records = self.ring_high_water.load(Ordering::Relaxed);
        stats.records_produced = self.head.0.load(Ordering::Acquire);
        stats.records_consumed = self.records_consumed.load(Ordering::Relaxed);
        stats.signal_count = self.signal_count.load(Ordering::Relaxed);
        stats.wait_count = self.wait_count.load(Ordering::Relaxed);
        stats.ring_full_episodes = self.ring_full_episodes.load(Ordering::Relaxed);
        stats.ring_overruns = self.ring_overruns.load(Ordering::Relaxed);
        stats.allocated_read_buffer_records = self
            .read_buffer_info()
            .map_or(0, |(_, capacity)| u64::from(capacity));
        stats.observed_producer_processor_group = (location >> 16) as u16;
        stats.observed_producer_logical_processor = location as u16;
        stats.producer_affinity_verified = self.producer_affinity_verified.load(Ordering::Acquire);
        stats.producer_processor_sample_count =
            self.producer_processor_sample_count.load(Ordering::Relaxed);
        stats.producer_processor_migration_count = self
            .producer_processor_migration_count
            .load(Ordering::Relaxed);
        stats.producer_off_assignment_count =
            self.producer_off_assignment_count.load(Ordering::Relaxed);
        stats.producer_unique_processor_count = lock(&self.observed_processors).len() as u32;
    }

    pub fn fill_watchdog(&self, target: &mut WatchdogFeedStatusV1, instance_id: u64) {
        let mut stats = StatsV1::default();
        self.fill_stats(&mut stats);
        let mappings = lock(&self.mappings);
        let ready = self.consumer_ready.load(Ordering::Acquire);
        let expected = mappings.len().min(u32::MAX as usize) as u32;
        let received = if self.config.data_source == DATA_SOURCE_SYNTHETIC && ready {
            expected
        } else {
            self.subscription_acknowledgements.load(Ordering::Relaxed).min(u64::from(expected)) as u32
        };
        let alive = stats.state == STATE_RUNNING && !self.producer_done.load(Ordering::Acquire);
        let operational = alive && ready && stats.terminal_status == OK && received >= expected;
        *target = WatchdogFeedStatusV1::default();
        target.struct_size = size_of::<WatchdogFeedStatusV1>() as u32;
        target.abi_version = ABI_VERSION;
        target.feed_instance_id = instance_id;
        target.generation_id = instance_id;
        target.feed_kind = self.config.feed_kind;
        target.major_status = if operational { MAJOR_UP } else if matches!(stats.state,
            STATE_STARTING | STATE_CONSUMER_SETUP | STATE_STOPPING) { MAJOR_RESETTING } else { MAJOR_DOWN };
        target.state = stats.state;
        target.terminal_status = stats.terminal_status;
        target.producer_alive = alive as u32;
        target.consumer_ready = ready as u32;
        target.expected_subscriptions = expected;
        target.received_subscriptions = received;
        target.heartbeat_count = self.heartbeat_messages.load(Ordering::Relaxed);
        target.provider_message_count = stats.records_produced;
        target.last_heartbeat_monotonic_ns = self.last_message_monotonic_ns.load(Ordering::Relaxed);
        target.last_provider_message_monotonic_ns = target.last_heartbeat_monotonic_ns;
        target.records_produced = stats.records_produced;
        target.records_consumed = stats.records_consumed;
        target.ring_capacity_records = stats.ring_capacity_records;
        target.ring_used_records = stats.ring_used_records;
        target.ring_high_water_records = stats.ring_high_water_records;
        target.ring_overruns = stats.ring_overruns;
        let dataset_length = self.dataset.len().min(target.dataset.len() - 1);
        target.dataset[..dataset_length].copy_from_slice(&self.dataset[..dataset_length]);
        let error = lock(&self.error);
        let error_length = error.len().min(target.failure_detail.len() - 1);
        target.failure_detail[..error_length].copy_from_slice(&error[..error_length]);
    }

    pub fn monotonic_nanoseconds(&self) -> u64 {
        self.performance_clock.now_nanoseconds().max(0) as u64
    }

    pub fn error_bytes(&self) -> Vec<u8> {
        lock(&self.error).clone()
    }

    fn apply_thread_settings(&self) {
        if self.config.producer_logical_processor != UNPINNED_PROCESSOR {
            if !windows::set_thread_affinity(
                self.config.producer_processor_group,
                self.config.producer_logical_processor,
            ) {
                self.set_error(
                    AFFINITY_CONFIGURATION_FAILED,
                    b"Unable to apply native producer affinity",
                );
                return;
            }
            self.observed_producer_location.store(
                (u32::from(self.config.producer_processor_group) << 16)
                    | u32::from(self.config.producer_logical_processor),
                Ordering::Release,
            );
            self.producer_affinity_verified.store(1, Ordering::Release);
        }
        if !windows::set_thread_priority(self.config.producer_priority)
            && self.config.flags & CONFIG_REQUIRE_PRIORITY != 0
        {
            self.set_error(
                PRIORITY_CONFIGURATION_FAILED,
                b"Unable to apply native producer priority",
            );
        }
    }

    #[inline(always)]
    fn apply_forced_migration(&self, produced: u64) -> bool {
        let interval = self.config.forced_migration_interval_records;
        if interval == 0 || produced == 0 || !produced.is_multiple_of(u64::from(interval)) {
            return true;
        }
        let alternate = !self
            .producer_using_alternate
            .fetch_xor(true, Ordering::Relaxed);
        let (group, processor) = if alternate {
            (
                self.config.producer_alternate_processor_group,
                self.config.producer_alternate_logical_processor,
            )
        } else {
            (
                self.config.producer_processor_group,
                self.config.producer_logical_processor,
            )
        };
        windows::set_thread_affinity(group, processor)
    }

    #[inline(always)]
    fn record_processor_residency(&self) {
        if self.config.flags & CONFIG_TRACK_PROCESSOR_RESIDENCY == 0 {
            return;
        }
        let location = windows::current_processor_location();
        let _ = self.observed_producer_location.compare_exchange(
            u32::MAX,
            location,
            Ordering::Release,
            Ordering::Relaxed,
        );
        let previous = self
            .last_producer_location
            .swap(location, Ordering::Relaxed);
        if previous != u32::MAX && previous != location {
            self.producer_processor_migration_count
                .fetch_add(1, Ordering::Relaxed);
        }
        lock(&self.observed_processors).insert(location);
        if self.config.producer_logical_processor != UNPINNED_PROCESSOR {
            let assigned = (u32::from(self.config.producer_processor_group) << 16)
                | u32::from(self.config.producer_logical_processor);
            if assigned != location {
                self.producer_off_assignment_count
                    .fetch_add(1, Ordering::Relaxed);
            }
        }
        self.producer_processor_sample_count
            .fetch_add(1, Ordering::Relaxed);
    }
}

#[inline(always)]
fn make_synthetic_record(
    mapping: &SyntheticMapping,
    kind: u8,
    sequence: u64,
    timestamp: i64,
) -> MarketRecord64 {
    let header = RecordHeader32 {
        instrument_id: mapping.instrument_id,
        publisher_id: mapping.publisher_id,
        record_kind: kind,
        flags: 0,
        ts_event_ns: timestamp,
        ts_recv_ns: timestamp,
        sequence: sequence as u32,
        source_schema: u16::from(kind),
        reserved: 0,
    };
    let price = 100_000_000_000i64.wrapping_add((sequence as i64).wrapping_mul(1_000_000));
    match kind {
        RECORD_QUOTE => MarketRecord64 {
            quote: QuoteRecord64 {
                header,
                bid_price: price - 500_000,
                ask_price: price + 500_000,
                bid_size: 10 + (sequence % 100) as u32,
                ask_size: 12 + (sequence % 100) as u32,
                bid_count: 1,
                ask_count: 1,
            },
        },
        RECORD_TRADE => MarketRecord64 {
            trade: TradeRecord64 {
                header,
                price,
                size: 1 + (sequence % 50) as u32,
                action: b'T',
                side: if sequence & 1 == 0 { b'B' } else { b'A' },
                dbn_flags: 0,
                depth: 0,
                ts_in_delta_ns: 0,
                channel_id: 0,
                reserved8: [0; 3],
                ts_out_ns: timestamp,
            },
        },
        RECORD_MBO => MarketRecord64 {
            mbo: MboRecord64 {
                header,
                order_id: sequence + 1,
                price,
                size: 1 + (sequence % 25) as u32,
                ts_in_delta_ns: 0,
                action: b'A',
                side: if sequence & 1 == 0 { b'B' } else { b'A' },
                dbn_flags: 0,
                channel_id: 0,
                reserved32: 0,
            },
        },
        _ => {
            // Statistics is normally the third record in a quote/trade/statistics
            // synthetic cycle. Divide by the cycle width so open/high/low rotate
            // instead of always selecting the same statistic type.
            let statistic_index = (sequence / 3) % 3;
            MarketRecord64 {
                statistics: StatisticsRecord64 {
                    header,
                    price: match statistic_index {
                        1 => price - 1_000_000_000,
                        2 => price + 1_000_000_000,
                        _ => price,
                    },
                    quantity: 0,
                    ts_ref_ns: timestamp,
                    stat_type: match statistic_index {
                        1 => 4,
                        2 => 5,
                        _ => 1,
                    },
                    channel_id: 0,
                    update_action: 1,
                    stat_flags: 0,
                    reserved16: 0,
                },
            }
        }
    }
}
