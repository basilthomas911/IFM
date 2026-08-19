use core::ffi::c_void;

pub const ABI_VERSION: u32 = 2;
pub const WAIT_INFINITE: u32 = u32::MAX;
pub const UNPINNED_PROCESSOR: u16 = u16::MAX;

pub type Status = i32;
pub const OK: Status = 0;
pub const INVALID_ARGUMENT: Status = 1;
pub const INVALID_STATE: Status = 2;
pub const ABI_MISMATCH: Status = 3;
pub const NO_MEMORY: Status = 4;
pub const OS_ERROR: Status = 5;
pub const DATABENTO_ERROR: Status = 6;
pub const TIMEOUT: Status = 7;
pub const BUFFER_TOO_SMALL: Status = 8;
pub const RING_OVERRUN: Status = 9;
pub const CONNECTION_LIMIT: Status = 10;
pub const RATE_LIMIT: Status = 11;
pub const SYMBOL_RESOLUTION_FAILED: Status = 12;
pub const INCOMPLETE_DEFINITIONS: Status = 13;
pub const NOT_SUPPORTED: Status = 14;
pub const INTERNAL_ERROR: Status = 15;
pub const AFFINITY_CONFIGURATION_FAILED: Status = 16;
pub const PRIORITY_CONFIGURATION_FAILED: Status = 17;
pub const MEMORY_LOCK_FAILED: Status = 18;
pub const NUMA_CONFIGURATION_FAILED: Status = 19;
pub const CORE_ISOLATION_FAILED: Status = 20;
pub const STOP_DRAIN_INCOMPLETE: Status = 21;
pub const CONNECTION_HUNG: Status = 22;
pub const PAGE_CONFIGURATION_FAILED: Status = 23;

pub const FEED_TICKER: u32 = 1;
pub const FEED_OPTION_CHAIN: u32 = 2;
pub const DATA_SOURCE_SYNTHETIC: u32 = 1;
pub const DATA_SOURCE_DATABENTO_LIVE: u32 = 2;
pub const RECORD_QUOTE: u8 = 1;
pub const RECORD_TRADE: u8 = 2;
pub const RECORD_MBO: u8 = 3;
pub const RECORD_STATISTICS: u8 = 4;
pub const RECORD_STATISTICS_REPLAY_COMPLETE: u8 = 5;
pub const RECORD_TRADE_REPLAY_COMPLETE: u8 = 6;
pub const MARKET_DATA_QUOTE: u32 = 1;
pub const MARKET_DATA_TRADE: u32 = 2;
pub const MARKET_DATA_MBO: u32 = 4;
pub const MARKET_DATA_STATISTICS: u32 = 8;
pub const MARKET_DATA_SESSION_VOLUME: u32 = 16;
pub const STATE_CREATED: u32 = 1;
pub const STATE_SUBSCRIBED: u32 = 2;
pub const STATE_STARTING: u32 = 3;
pub const STATE_CONSUMER_SETUP: u32 = 4;
pub const STATE_RUNNING: u32 = 5;
pub const STATE_STOPPING: u32 = 6;
pub const STATE_STOPPED: u32 = 7;
pub const STATE_FAULTED: u32 = 8;
pub const WAIT_DATA: u32 = 1;
pub const WAIT_TERMINAL: u32 = 2;
pub const WAIT_FAULT: u32 = 4;
pub const CONFIG_LOCK_RING_MEMORY: u32 = 1;
pub const CONFIG_REQUIRE_LOCKED_MEMORY: u32 = 2;
pub const CONFIG_REQUIRE_BASE_PAGE_POLICY: u32 = 4;
pub const CONFIG_REQUIRE_PRIORITY: u32 = 8;
pub const CONFIG_REQUIRE_NUMA_LOCALITY: u32 = 16;
pub const CONFIG_TRACK_PROCESSOR_RESIDENCY: u32 = 32;
pub const CONTRACT_QUERY_EXACT: u32 = 1;
pub const CONTRACT_QUERY_TICKER: u32 = 2;
pub const CONTRACT_QUERY_INSTRUMENT_ID: u32 = 3;
pub const LATEST_PRICE_LAST_TRADE: u32 = 1;
pub const LATEST_PRICE_QUOTE_MIDPOINT: u32 = 2;
pub const LATEST_PRICE_BID: u32 = 3;
pub const LATEST_PRICE_ASK: u32 = 4;
pub const LATEST_PRICE_NEXT_OBSERVED: u32 = 1;
pub const LATEST_PRICE_REPLAY_LOOKBACK_THEN_LIVE: u32 = 2;

#[repr(C)]
#[derive(Clone, Copy, Default)]
pub struct RecordHeader32 {
    pub instrument_id: u32,
    pub publisher_id: u16,
    pub record_kind: u8,
    pub flags: u8,
    pub ts_event_ns: i64,
    pub ts_recv_ns: i64,
    pub sequence: u32,
    pub source_schema: u16,
    pub reserved: u16,
}

#[repr(C)]
#[derive(Clone, Copy, Default)]
pub struct QuoteRecord64 {
    pub header: RecordHeader32,
    pub bid_price: i64,
    pub ask_price: i64,
    pub bid_size: u32,
    pub ask_size: u32,
    pub bid_count: u32,
    pub ask_count: u32,
}

#[repr(C)]
#[derive(Clone, Copy, Default)]
pub struct TradeRecord64 {
    pub header: RecordHeader32,
    pub price: i64,
    pub size: u32,
    pub action: u8,
    pub side: u8,
    pub dbn_flags: u8,
    pub depth: u8,
    pub ts_in_delta_ns: i32,
    pub channel_id: u8,
    pub reserved8: [u8; 3],
    pub ts_out_ns: i64,
}

#[repr(C)]
#[derive(Clone, Copy, Default)]
pub struct MboRecord64 {
    pub header: RecordHeader32,
    pub order_id: u64,
    pub price: i64,
    pub size: u32,
    pub ts_in_delta_ns: i32,
    pub action: u8,
    pub side: u8,
    pub dbn_flags: u8,
    pub channel_id: u8,
    pub reserved32: u32,
}

#[repr(C)]
#[derive(Clone, Copy, Default)]
pub struct StatisticsRecord64 {
    pub header: RecordHeader32,
    pub price: i64,
    pub quantity: i64,
    pub ts_ref_ns: i64,
    pub stat_type: u16,
    pub channel_id: u16,
    pub update_action: u8,
    pub stat_flags: u8,
    pub reserved16: u16,
}

#[repr(C)]
#[derive(Clone, Copy)]
pub union MarketRecord64 {
    pub header: RecordHeader32,
    pub quote: QuoteRecord64,
    pub trade: TradeRecord64,
    pub mbo: MboRecord64,
    pub statistics: StatisticsRecord64,
}

impl Default for MarketRecord64 {
    fn default() -> Self {
        Self {
            quote: QuoteRecord64::default(),
        }
    }
}

#[repr(C)]
#[derive(Clone, Copy, Default)]
pub struct FeedConfigV1 {
    pub struct_size: u32,
    pub abi_version: u32,
    pub data_source: u32,
    pub feed_kind: u32,
    pub ring_memory_bytes: u64,
    pub spin_iterations: u32,
    pub ring_full_timeout_us: u32,
    pub synthetic_record_count: u32,
    pub synthetic_records_per_second: u32,
    pub synthetic_instrument_count: u32,
    pub heartbeat_interval_ms: u32,
    pub flags: u32,
    pub producer_processor_group: u16,
    pub producer_logical_processor: u16,
    pub drain_processor_group: u16,
    pub drain_logical_processor: u16,
    pub producer_priority: i32,
    pub drain_priority: i32,
    pub numa_node: u16,
    pub reserved16: u16,
    pub dataset_offset: u32,
    pub dataset_length: u32,
    pub synthetic_start_sequence: u64,
    pub forced_migration_interval_records: u32,
    pub producer_alternate_processor_group: u16,
    pub producer_alternate_logical_processor: u16,
    pub drain_alternate_processor_group: u16,
    pub drain_alternate_logical_processor: u16,
    pub reserved32: u32,
    pub statistics_replay_start_ns: u64,
    pub trade_replay_start_ns: u64,
    pub reserved: [u64; 1],
}

#[repr(C)]
#[derive(Clone, Copy, Default)]
pub struct TickerSubscriptionV1 {
    pub struct_size: u32,
    pub abi_version: u32,
    pub symbol_offset: u32,
    pub symbol_length: u32,
    pub input_symbology: u32,
    pub data_kinds: u32,
    pub reserved: u64,
}
#[repr(C)]
#[derive(Clone, Copy, Default)]
pub struct TickerInstrumentMappingV1 {
    pub struct_size: u32,
    pub abi_version: u32,
    pub subscription_index: u32,
    pub instrument_id: u32,
    pub publisher_id: u16,
    pub reserved16: u16,
    pub requested_symbol_offset: u32,
    pub requested_symbol_length: u16,
    pub raw_symbol_length: u16,
    pub raw_symbol_offset: u32,
}
#[repr(C)]
#[derive(Clone, Copy, Default)]
pub struct OptionChainSubscriptionV1 {
    pub struct_size: u32,
    pub abi_version: u32,
    pub data_kinds: u32,
    pub contract_count: u32,
    pub reserved: [u64; 2],
}
#[repr(C)]
#[derive(Clone, Copy, Default)]
pub struct OptionContractSelectionV1 {
    pub struct_size: u32,
    pub abi_version: u32,
    pub instrument_id: u32,
    pub publisher_id: u16,
    pub option_right: u8,
    pub reserved8: u8,
    pub raw_symbol_offset: u32,
    pub raw_symbol_length: u32,
    pub reserved: u64,
}
#[repr(C)]
#[derive(Clone, Copy, Default)]
pub struct WaitResultV1 {
    pub struct_size: u32,
    pub abi_version: u32,
    pub flags: u32,
    pub state: u32,
    pub available_records: u64,
    pub terminal_status: i32,
    pub reserved: u32,
}
#[repr(C)]
#[derive(Clone, Copy, Default)]
pub struct BatchResultV1 {
    pub struct_size: u32,
    pub abi_version: u32,
    pub records_read: u32,
    pub more_available: u32,
    pub first_sequence: u64,
    pub last_sequence: u64,
}
#[repr(C)]
#[derive(Clone, Copy, Default)]
pub struct StatsV1 {
    pub struct_size: u32,
    pub abi_version: u32,
    pub state: u32,
    pub terminal_status: i32,
    pub ring_capacity_records: u64,
    pub ring_used_records: u64,
    pub ring_high_water_records: u64,
    pub records_produced: u64,
    pub records_consumed: u64,
    pub signal_count: u64,
    pub wait_count: u64,
    pub ring_full_episodes: u64,
    pub ring_overruns: u64,
    pub allocated_read_buffer_records: u64,
    pub observed_producer_processor_group: u16,
    pub observed_producer_logical_processor: u16,
    pub producer_affinity_verified: u32,
    pub producer_processor_sample_count: u64,
    pub producer_processor_migration_count: u64,
    pub producer_off_assignment_count: u32,
    pub producer_unique_processor_count: u32,
}
#[repr(C)]
#[derive(Clone, Copy, Default)]
pub struct Utf8SliceV1 {
    pub offset: u32,
    pub length: u32,
}
#[repr(C)]
#[derive(Clone, Copy, Default)]
pub struct ContractQueryV1 {
    pub struct_size: u32,
    pub abi_version: u32,
    pub query_kind: u32,
    pub timeout_ms: u32,
    pub dataset_offset: u32,
    pub dataset_length: u32,
    pub symbol_count: u32,
    pub reserved32: u32,
    pub reserved: [u64; 4],
}
#[repr(C)]
#[derive(Clone, Copy, Default)]
pub struct ContractDetailV1 {
    pub struct_size: u32,
    pub abi_version: u32,
    pub flags: u32,
    pub instrument_id: u32,
    pub publisher_id: u16,
    pub contract_kind: u8,
    pub maturity_month: u8,
    pub maturity_day: u8,
    pub maturity_week: u8,
    pub maturity_year: u16,
    pub underlying_id: u32,
    pub contract_multiplier: i32,
    pub raw_instrument_id: u64,
    pub strike_price: i64,
    pub min_price_increment: i64,
    pub min_price_increment_amount: i64,
    pub expiration_ts_ns: u64,
    pub activation_ts_ns: u64,
    pub raw_symbol: Utf8SliceV1,
    pub asset: Utf8SliceV1,
    pub underlying: Utf8SliceV1,
    pub currency: Utf8SliceV1,
    pub settlement_currency: Utf8SliceV1,
    pub exchange: Utf8SliceV1,
    pub security_type: Utf8SliceV1,
    pub cfi: Utf8SliceV1,
    pub unit_of_measure: Utf8SliceV1,
    pub reserved: [u64; 5],
}
#[repr(C)]
#[derive(Clone, Copy)]
pub struct LatestPriceRequestV1 {
    pub struct_size: u32,
    pub abi_version: u32,
    pub selected_policy: u32,
    pub freshness_policy: u32,
    pub input_symbology: u32,
    pub replay_lookback_ms: u32,
    pub dataset: Utf8SliceV1,
    pub symbol: Utf8SliceV1,
    pub utf8_blob: *const u8,
    pub utf8_blob_bytes: u32,
    pub reserved32: u32,
    pub reserved: [u64; 4],
}
impl Default for LatestPriceRequestV1 {
    fn default() -> Self {
        Self {
            struct_size: 0,
            abi_version: 0,
            selected_policy: 0,
            freshness_policy: 0,
            input_symbology: 0,
            replay_lookback_ms: 0,
            dataset: Utf8SliceV1::default(),
            symbol: Utf8SliceV1::default(),
            utf8_blob: core::ptr::null(),
            utf8_blob_bytes: 0,
            reserved32: 0,
            reserved: [0; 4],
        }
    }
}
#[repr(C)]
#[derive(Clone, Copy, Default)]
pub struct LatestPriceResult64 {
    pub instrument_id: u32,
    pub publisher_id: u16,
    pub selected_policy: u8,
    pub flags: u8,
    pub selected_price: i64,
    pub bid_price: i64,
    pub ask_price: i64,
    pub last_trade_price: i64,
    pub ts_event_ns: i64,
    pub ts_recv_ns: i64,
    pub bid_size: u32,
    pub ask_size: u32,
}

#[repr(C)]
pub struct FeedOpaque {
    _private: [u8; 0],
    _marker: core::marker::PhantomData<(*mut u8, core::marker::PhantomPinned)>,
}
#[repr(C)]
pub struct ContractDetailsResultOpaque {
    _private: [u8; 0],
    _marker: core::marker::PhantomData<(*mut u8, core::marker::PhantomPinned)>,
}

pub type RawHandle = *mut c_void;

const _: () = {
    assert!(size_of::<RecordHeader32>() == 32);
    assert!(size_of::<QuoteRecord64>() == 64);
    assert!(size_of::<TradeRecord64>() == 64);
    assert!(size_of::<MboRecord64>() == 64);
    assert!(size_of::<StatisticsRecord64>() == 64);
    assert!(size_of::<MarketRecord64>() == 64);
    assert!(size_of::<FeedConfigV1>() == 128);
    assert!(size_of::<TickerSubscriptionV1>() == 32);
    assert!(size_of::<TickerInstrumentMappingV1>() == 32);
    assert!(size_of::<OptionChainSubscriptionV1>() == 32);
    assert!(size_of::<OptionContractSelectionV1>() == 32);
    assert!(size_of::<WaitResultV1>() == 32);
    assert!(size_of::<BatchResultV1>() == 32);
    assert!(size_of::<StatsV1>() == 128);
    assert!(size_of::<Utf8SliceV1>() == 8);
    assert!(size_of::<ContractQueryV1>() == 64);
    assert!(size_of::<ContractDetailV1>() == 192);
    assert!(size_of::<LatestPriceRequestV1>() == 88);
    assert!(size_of::<LatestPriceResult64>() == 64);
};
