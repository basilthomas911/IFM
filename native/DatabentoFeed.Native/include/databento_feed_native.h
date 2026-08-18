#pragma once

#include <stddef.h>
#include <stdint.h>

#if defined(_WIN32)
#if defined(DBF_NATIVE_BUILD)
#define DBF_API __declspec(dllexport)
#else
#define DBF_API __declspec(dllimport)
#endif
#define DBF_CALL __cdecl
#else
#define DBF_API __attribute__((visibility("default")))
#define DBF_CALL
#endif

#ifdef __cplusplus
extern "C" {
#endif

enum { DBF_ABI_VERSION = 1u, DBF_WAIT_INFINITE = 0xffffffffu };

typedef enum dbf_status {
    DBF_OK = 0,
    DBF_INVALID_ARGUMENT = 1,
    DBF_INVALID_STATE = 2,
    DBF_ABI_MISMATCH = 3,
    DBF_NO_MEMORY = 4,
    DBF_OS_ERROR = 5,
    DBF_DATABENTO_ERROR = 6,
    DBF_TIMEOUT = 7,
    DBF_BUFFER_TOO_SMALL = 8,
    DBF_RING_OVERRUN = 9,
    DBF_CONNECTION_LIMIT = 10,
    DBF_RATE_LIMIT = 11,
    DBF_SYMBOL_RESOLUTION_FAILED = 12,
    DBF_INCOMPLETE_DEFINITIONS = 13,
    DBF_NOT_SUPPORTED = 14,
    DBF_INTERNAL_ERROR = 15,
    DBF_AFFINITY_CONFIGURATION_FAILED = 16,
    DBF_PRIORITY_CONFIGURATION_FAILED = 17,
    DBF_MEMORY_LOCK_FAILED = 18,
    DBF_NUMA_CONFIGURATION_FAILED = 19,
    DBF_CORE_ISOLATION_FAILED = 20,
    DBF_STOP_DRAIN_INCOMPLETE = 21,
    DBF_CONNECTION_HUNG = 22,
    DBF_PAGE_CONFIGURATION_FAILED = 23
} dbf_status;

typedef enum dbf_feed_kind {
    DBF_FEED_TICKER = 1,
    DBF_FEED_OPTION_CHAIN = 2
} dbf_feed_kind;

typedef enum dbf_data_source {
    DBF_DATA_SOURCE_SYNTHETIC = 1,
    DBF_DATA_SOURCE_DATABENTO_LIVE = 2
} dbf_data_source;

typedef enum dbf_record_kind {
    DBF_RECORD_QUOTE = 1,
    DBF_RECORD_TRADE = 2,
    DBF_RECORD_MBO = 3,
    DBF_RECORD_STATISTICS = 4,
    DBF_RECORD_STATISTICS_REPLAY_COMPLETE = 5
} dbf_record_kind;

typedef enum dbf_market_data_kind_flags {
    DBF_MARKET_DATA_QUOTE = 1,
    DBF_MARKET_DATA_TRADE = 2,
    DBF_MARKET_DATA_MBO = 4,
    DBF_MARKET_DATA_STATISTICS = 8
} dbf_market_data_kind_flags;

typedef enum dbf_record_flags {
    DBF_RECORD_FLAG_SNAPSHOT = 1,
    DBF_RECORD_FLAG_REPLAY = 2,
    DBF_RECORD_FLAG_UNDEFINED_PRICE = 4,
    DBF_RECORD_FLAG_TS_OUT_PRESENT = 8
} dbf_record_flags;

typedef enum dbf_feed_state {
    DBF_STATE_CREATED = 1,
    DBF_STATE_SUBSCRIBED = 2,
    DBF_STATE_STARTING = 3,
    DBF_STATE_CONSUMER_SETUP = 4,
    DBF_STATE_RUNNING = 5,
    DBF_STATE_STOPPING = 6,
    DBF_STATE_STOPPED = 7,
    DBF_STATE_FAULTED = 8
} dbf_feed_state;

typedef enum dbf_wait_flags {
    DBF_WAIT_DATA = 1,
    DBF_WAIT_TERMINAL = 2,
    DBF_WAIT_FAULT = 4
} dbf_wait_flags;

typedef enum dbf_config_flags {
    DBF_CONFIG_LOCK_RING_MEMORY = 1,
    DBF_CONFIG_REQUIRE_LOCKED_MEMORY = 2,
    DBF_CONFIG_REQUIRE_BASE_PAGE_POLICY = 4,
    DBF_CONFIG_REQUIRE_PRIORITY = 8,
    DBF_CONFIG_REQUIRE_NUMA_LOCALITY = 16,
    DBF_CONFIG_TRACK_PROCESSOR_RESIDENCY = 32
} dbf_config_flags;

typedef enum dbf_contract_query_kind {
    DBF_CONTRACT_QUERY_EXACT = 1,
    DBF_CONTRACT_QUERY_TICKER = 2,
    DBF_CONTRACT_QUERY_INSTRUMENT_ID = 3
} dbf_contract_query_kind;

typedef enum dbf_contract_kind {
    DBF_CONTRACT_FUTURE = 1,
    DBF_CONTRACT_CALL_OPTION = 2,
    DBF_CONTRACT_PUT_OPTION = 3
} dbf_contract_kind;

typedef enum dbf_contract_detail_flags {
    DBF_CONTRACT_FOUND = 1,
    DBF_CONTRACT_HAS_STRIKE_PRICE = 2,
    DBF_CONTRACT_HAS_MIN_PRICE_INCREMENT = 4,
    DBF_CONTRACT_HAS_EXPIRATION = 8,
    DBF_CONTRACT_HAS_ACTIVATION = 16,
    DBF_CONTRACT_HAS_MATURITY_DATE = 32,
    DBF_CONTRACT_HAS_MULTIPLIER = 64,
    DBF_CONTRACT_HAS_MIN_PRICE_INCREMENT_AMOUNT = 128,
    DBF_CONTRACT_HAS_MATURITY_WEEK = 256
} dbf_contract_detail_flags;

typedef enum dbf_latest_price_policy {
    DBF_LATEST_PRICE_LAST_TRADE = 1,
    DBF_LATEST_PRICE_QUOTE_MIDPOINT = 2,
    DBF_LATEST_PRICE_BID = 3,
    DBF_LATEST_PRICE_ASK = 4
} dbf_latest_price_policy;

typedef enum dbf_latest_price_freshness_policy {
    DBF_LATEST_PRICE_NEXT_OBSERVED = 1,
    DBF_LATEST_PRICE_REPLAY_LOOKBACK_THEN_LIVE = 2
} dbf_latest_price_freshness_policy;

typedef enum dbf_latest_price_result_flags {
    DBF_LATEST_PRICE_BID_VALID = 1,
    DBF_LATEST_PRICE_ASK_VALID = 2,
    DBF_LATEST_PRICE_TRADE_VALID = 4,
    DBF_LATEST_PRICE_REPLAY_CONTRIBUTED = 8,
    DBF_LATEST_PRICE_FINAL_RECORD_LIVE = 16
} dbf_latest_price_result_flags;

typedef struct dbf_record_header32 {
    uint32_t instrument_id;
    uint16_t publisher_id;
    uint8_t record_kind;
    uint8_t flags;
    int64_t ts_event_ns;
    int64_t ts_recv_ns;
    uint32_t sequence;
    uint16_t source_schema;
    uint16_t reserved;
} dbf_record_header32;

typedef struct dbf_quote_record64 {
    dbf_record_header32 header;
    int64_t bid_price;
    int64_t ask_price;
    uint32_t bid_size;
    uint32_t ask_size;
    uint32_t bid_count;
    uint32_t ask_count;
} dbf_quote_record64;

typedef struct dbf_trade_record64 {
    dbf_record_header32 header;
    int64_t price;
    uint32_t size;
    uint8_t action;
    uint8_t side;
    uint8_t dbn_flags;
    uint8_t depth;
    int32_t ts_in_delta_ns;
    uint8_t channel_id;
    uint8_t reserved8[3];
    int64_t ts_out_ns;
} dbf_trade_record64;

typedef struct dbf_mbo_record64 {
    dbf_record_header32 header;
    uint64_t order_id;
    int64_t price;
    uint32_t size;
    int32_t ts_in_delta_ns;
    uint8_t action;
    uint8_t side;
    uint8_t dbn_flags;
    uint8_t channel_id;
    uint32_t reserved32;
} dbf_mbo_record64;

typedef struct dbf_statistics_record64 {
    dbf_record_header32 header;
    int64_t price;
    int64_t ts_ref_ns;
    int32_t ts_in_delta_ns;
    uint16_t stat_type;
    uint16_t channel_id;
    uint8_t update_action;
    uint8_t stat_flags;
    uint16_t reserved16;
    uint32_t reserved32;
} dbf_statistics_record64;

typedef union dbf_market_record64 {
    dbf_record_header32 header;
    dbf_quote_record64 quote;
    dbf_trade_record64 trade;
    dbf_mbo_record64 mbo;
    dbf_statistics_record64 statistics;
} dbf_market_record64;

typedef struct dbf_feed_config_v1 {
    uint32_t struct_size;
    uint32_t abi_version;
    uint32_t data_source;
    uint32_t feed_kind;
    uint64_t ring_memory_bytes;
    uint32_t spin_iterations;
    uint32_t ring_full_timeout_us;
    uint32_t synthetic_record_count;
    uint32_t synthetic_records_per_second;
    uint32_t synthetic_instrument_count;
    uint32_t heartbeat_interval_ms;
    uint32_t flags;
    uint16_t producer_processor_group;
    uint16_t producer_logical_processor;
    uint16_t drain_processor_group;
    uint16_t drain_logical_processor;
    int32_t producer_priority;
    int32_t drain_priority;
    uint16_t numa_node;
    uint16_t reserved16;
    uint32_t dataset_offset;
    uint32_t dataset_length;
    uint64_t synthetic_start_sequence;
    uint32_t forced_migration_interval_records;
    uint16_t producer_alternate_processor_group;
    uint16_t producer_alternate_logical_processor;
    uint16_t drain_alternate_processor_group;
    uint16_t drain_alternate_logical_processor;
    uint32_t reserved32;
    uint64_t statistics_replay_start_ns;
    uint64_t reserved[2];
} dbf_feed_config_v1;

typedef struct dbf_ticker_subscription_v1 {
    uint32_t struct_size;
    uint32_t abi_version;
    uint32_t symbol_offset;
    uint32_t symbol_length;
    uint32_t input_symbology;
    uint32_t data_kinds;
    uint64_t reserved;
} dbf_ticker_subscription_v1;

typedef struct dbf_ticker_instrument_mapping_v1 {
    uint32_t struct_size;
    uint32_t abi_version;
    uint32_t subscription_index;
    uint32_t instrument_id;
    uint16_t publisher_id;
    uint16_t reserved16;
    uint32_t requested_symbol_offset;
    uint16_t requested_symbol_length;
    uint16_t raw_symbol_length;
    uint32_t raw_symbol_offset;
} dbf_ticker_instrument_mapping_v1;

typedef struct dbf_option_chain_subscription_v1 {
    uint32_t struct_size;
    uint32_t abi_version;
    uint32_t data_kinds;
    uint32_t contract_count;
    uint64_t reserved[2];
} dbf_option_chain_subscription_v1;

typedef struct dbf_option_contract_selection_v1 {
    uint32_t struct_size;
    uint32_t abi_version;
    uint32_t instrument_id;
    uint16_t publisher_id;
    uint8_t option_right;
    uint8_t reserved8;
    uint32_t raw_symbol_offset;
    uint32_t raw_symbol_length;
    uint64_t reserved;
} dbf_option_contract_selection_v1;

typedef struct dbf_wait_result_v1 {
    uint32_t struct_size;
    uint32_t abi_version;
    uint32_t flags;
    uint32_t state;
    uint64_t available_records;
    int32_t terminal_status;
    uint32_t reserved;
} dbf_wait_result_v1;

typedef struct dbf_batch_result_v1 {
    uint32_t struct_size;
    uint32_t abi_version;
    uint32_t records_read;
    uint32_t more_available;
    uint64_t first_sequence;
    uint64_t last_sequence;
} dbf_batch_result_v1;

typedef struct dbf_stats_v1 {
    uint32_t struct_size;
    uint32_t abi_version;
    uint32_t state;
    int32_t terminal_status;
    uint64_t ring_capacity_records;
    uint64_t ring_used_records;
    uint64_t ring_high_water_records;
    uint64_t records_produced;
    uint64_t records_consumed;
    uint64_t signal_count;
    uint64_t wait_count;
    uint64_t ring_full_episodes;
    uint64_t ring_overruns;
    uint64_t allocated_read_buffer_records;
    uint16_t observed_producer_processor_group;
    uint16_t observed_producer_logical_processor;
    uint32_t producer_affinity_verified;
    uint64_t producer_processor_sample_count;
    uint64_t producer_processor_migration_count;
    uint32_t producer_off_assignment_count;
    uint32_t producer_unique_processor_count;
} dbf_stats_v1;

typedef struct dbf_utf8_slice_v1 {
    uint32_t offset;
    uint32_t length;
} dbf_utf8_slice_v1;

typedef struct dbf_contract_query_v1 {
    uint32_t struct_size;
    uint32_t abi_version;
    uint32_t query_kind;
    uint32_t timeout_ms;
    uint32_t dataset_offset;
    uint32_t dataset_length;
    uint32_t symbol_count;
    uint32_t reserved32;
    uint64_t reserved[4];
} dbf_contract_query_v1;

typedef struct dbf_contract_detail_v1 {
    uint32_t struct_size;
    uint32_t abi_version;
    uint32_t flags;
    uint32_t instrument_id;
    uint16_t publisher_id;
    uint8_t contract_kind;
    uint8_t maturity_month;
    uint8_t maturity_day;
    uint8_t maturity_week;
    uint16_t maturity_year;
    uint32_t underlying_id;
    int32_t contract_multiplier;
    uint64_t raw_instrument_id;
    int64_t strike_price;
    int64_t min_price_increment;
    int64_t min_price_increment_amount;
    uint64_t expiration_ts_ns;
    uint64_t activation_ts_ns;
    dbf_utf8_slice_v1 raw_symbol;
    dbf_utf8_slice_v1 asset;
    dbf_utf8_slice_v1 underlying;
    dbf_utf8_slice_v1 currency;
    dbf_utf8_slice_v1 settlement_currency;
    dbf_utf8_slice_v1 exchange;
    dbf_utf8_slice_v1 security_type;
    dbf_utf8_slice_v1 cfi;
    dbf_utf8_slice_v1 unit_of_measure;
    uint64_t reserved[5];
} dbf_contract_detail_v1;

typedef struct dbf_latest_price_request_v1 {
    uint32_t struct_size;
    uint32_t abi_version;
    uint32_t selected_policy;
    uint32_t freshness_policy;
    uint32_t input_symbology;
    uint32_t replay_lookback_ms;
    dbf_utf8_slice_v1 dataset;
    dbf_utf8_slice_v1 symbol;
    const uint8_t* utf8_blob;
    uint32_t utf8_blob_bytes;
    uint32_t reserved32;
    uint64_t reserved[4];
} dbf_latest_price_request_v1;

typedef struct dbf_latest_price_result64 {
    uint32_t instrument_id;
    uint16_t publisher_id;
    uint8_t selected_policy;
    uint8_t flags;
    int64_t selected_price;
    int64_t bid_price;
    int64_t ask_price;
    int64_t last_trade_price;
    int64_t ts_event_ns;
    int64_t ts_recv_ns;
    uint32_t bid_size;
    uint32_t ask_size;
} dbf_latest_price_result64;

typedef struct dbf_feed dbf_feed_t;
typedef struct dbf_contract_details_result dbf_contract_details_result_t;

DBF_API uint32_t DBF_CALL dbf_get_abi_version(void);
DBF_API dbf_status DBF_CALL dbf_feed_create(const dbf_feed_config_v1* config,
                                             const uint8_t* utf8_blob,
                                             uint32_t utf8_blob_bytes,
                                             dbf_feed_t** feed);
DBF_API dbf_status DBF_CALL dbf_feed_subscribe_tickers(dbf_feed_t* feed,
                                                        const dbf_ticker_subscription_v1* subscriptions,
                                                        uint32_t subscription_count,
                                                        const uint8_t* utf8_blob,
                                                        uint32_t utf8_blob_bytes,
                                                        uint32_t timeout_ms);
DBF_API dbf_status DBF_CALL dbf_feed_subscribe_option_chain(dbf_feed_t* feed,
                                                             const dbf_option_chain_subscription_v1* subscription,
                                                             const dbf_option_contract_selection_v1* contracts,
                                                             uint32_t contract_count,
                                                             const uint8_t* utf8_blob,
                                                             uint32_t utf8_blob_bytes,
                                                             uint32_t timeout_ms);
DBF_API dbf_status DBF_CALL dbf_feed_allocate_read_buffer64(dbf_feed_t* feed,
                                                             uint32_t record_capacity,
                                                             dbf_market_record64** buffer);
DBF_API dbf_status DBF_CALL dbf_feed_start(dbf_feed_t* feed, uint32_t timeout_ms);
DBF_API dbf_status DBF_CALL dbf_feed_get_ticker_mapping_counts(dbf_feed_t* feed,
                                                                uint32_t* mapping_count,
                                                                uint32_t* utf8_blob_bytes);
DBF_API dbf_status DBF_CALL dbf_feed_copy_ticker_mappings(dbf_feed_t* feed,
                                                           dbf_ticker_instrument_mapping_v1* mappings,
                                                           uint32_t mapping_capacity,
                                                           uint8_t* utf8_blob,
                                                           uint32_t utf8_blob_capacity);
DBF_API dbf_status DBF_CALL dbf_feed_set_consumer_ready(dbf_feed_t* feed, uint32_t timeout_ms);
DBF_API dbf_status DBF_CALL dbf_feed_wait(dbf_feed_t* feed,
                                           uint32_t timeout_ms,
                                           dbf_wait_result_v1* result);
DBF_API dbf_status DBF_CALL dbf_feed_read_batch64(dbf_feed_t* feed,
                                                   dbf_market_record64* destination,
                                                   uint32_t destination_record_capacity,
                                                   dbf_batch_result_v1* result);
DBF_API dbf_status DBF_CALL dbf_feed_stop(dbf_feed_t* feed, uint32_t timeout_ms);
DBF_API dbf_status DBF_CALL dbf_feed_free_read_buffer64(dbf_feed_t* feed,
                                                         dbf_market_record64* buffer);
DBF_API dbf_status DBF_CALL dbf_feed_get_stats(dbf_feed_t* feed, dbf_stats_v1* stats);
DBF_API dbf_status DBF_CALL dbf_feed_get_last_error(dbf_feed_t* feed,
                                                     uint8_t* utf8_buffer,
                                                     uint32_t utf8_buffer_capacity,
                                                     uint32_t* required_bytes);
DBF_API dbf_status DBF_CALL dbf_feed_destroy(dbf_feed_t* feed);
DBF_API dbf_status DBF_CALL dbf_contract_details_query(
    const dbf_contract_query_v1* query,
    const dbf_utf8_slice_v1* symbols,
    const uint8_t* utf8_blob,
    uint32_t utf8_blob_bytes,
    dbf_contract_details_result_t** result);
DBF_API dbf_status DBF_CALL dbf_contract_details_result_get_counts(
    const dbf_contract_details_result_t* result,
    uint32_t* detail_count,
    uint32_t* utf8_blob_bytes);
DBF_API dbf_status DBF_CALL dbf_contract_details_result_copy(
    const dbf_contract_details_result_t* result,
    dbf_contract_detail_v1* details,
    uint32_t detail_capacity,
    uint8_t* utf8_blob,
    uint32_t utf8_blob_capacity);
DBF_API dbf_status DBF_CALL dbf_contract_details_result_get_error(
    const dbf_contract_details_result_t* result,
    uint8_t* utf8_buffer,
    uint32_t utf8_buffer_capacity,
    uint32_t* required_bytes);
DBF_API dbf_status DBF_CALL dbf_contract_details_result_destroy(
    dbf_contract_details_result_t* result);
DBF_API dbf_status DBF_CALL dbf_get_latest_price(
    const dbf_latest_price_request_v1* request,
    uint32_t timeout_ms,
    dbf_latest_price_result64* result);

#ifdef __cplusplus
}

static_assert(sizeof(dbf_record_header32) == 32);
static_assert(sizeof(dbf_quote_record64) == 64);
static_assert(sizeof(dbf_trade_record64) == 64);
static_assert(sizeof(dbf_mbo_record64) == 64);
static_assert(sizeof(dbf_statistics_record64) == 64);
static_assert(sizeof(dbf_market_record64) == 64);
static_assert(sizeof(dbf_feed_config_v1) == 128);
static_assert(sizeof(dbf_ticker_subscription_v1) == 32);
static_assert(sizeof(dbf_ticker_instrument_mapping_v1) == 32);
static_assert(sizeof(dbf_option_chain_subscription_v1) == 32);
static_assert(sizeof(dbf_option_contract_selection_v1) == 32);
static_assert(sizeof(dbf_wait_result_v1) == 32);
static_assert(sizeof(dbf_batch_result_v1) == 32);
static_assert(sizeof(dbf_stats_v1) == 128);
static_assert(sizeof(dbf_utf8_slice_v1) == 8);
static_assert(sizeof(dbf_contract_query_v1) == 64);
static_assert(sizeof(dbf_contract_detail_v1) == 192);
static_assert(sizeof(dbf_latest_price_request_v1) == 88);
static_assert(sizeof(dbf_latest_price_result64) == 64);
#endif
