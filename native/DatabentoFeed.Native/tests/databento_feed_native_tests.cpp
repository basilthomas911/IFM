#include "databento_feed_native.h"
#include "latest_price_session_guard.hpp"

#include <array>
#if defined(NDEBUG)
#undef NDEBUG
#endif
#include <cassert>
#include <chrono>
#include <cstdint>
#include <cstring>
#include <iostream>
#include <limits>
#include <stdexcept>
#include <string_view>
#include <thread>
#include <vector>

#if defined(_WIN32)
#define NOMINMAX
#include <Windows.h>
#else
#include <sched.h>
#endif

#if defined(DBF_ENABLE_LIVE)
#include "databento_live_normalization.hpp"
#include <databento/record.hpp>
#endif

namespace {

dbf_feed_config_v1 make_config(std::uint32_t record_count,
                               std::uint64_t ring_bytes = 1u << 20) {
    dbf_feed_config_v1 config{};
    config.struct_size = sizeof(config);
    config.abi_version = DBF_ABI_VERSION;
    config.data_source = DBF_DATA_SOURCE_SYNTHETIC;
    config.feed_kind = DBF_FEED_TICKER;
    config.ring_memory_bytes = ring_bytes;
    config.spin_iterations = 256;
    config.ring_full_timeout_us = 2'000;
    config.synthetic_record_count = record_count;
    config.synthetic_instrument_count = 2;
    config.heartbeat_interval_ms = 5'000;
    config.producer_logical_processor = 0xffffu;
    config.drain_logical_processor = 0xffffu;
    config.dataset_length = 9;
    config.synthetic_start_sequence = 1;
    return config;
}

void require(dbf_status actual, dbf_status expected = DBF_OK) {
    if (actual != expected) {
        std::cerr << "Expected status " << expected << " but received " << actual << '\n';
        std::abort();
    }
}

dbf_feed_t* create_subscribed_feed(std::uint32_t record_count,
                                   std::uint64_t ring_bytes = 1u << 20,
                                   std::uint16_t processor_group = 0,
                                   std::uint16_t logical_processor = 0xffffu,
                                   bool track_processor_residency = false) {
    constexpr std::string_view dataset = "SYNTHETIC";
    auto config = make_config(record_count, ring_bytes);
    config.producer_processor_group = processor_group;
    config.producer_logical_processor = logical_processor;
    if (track_processor_residency) {
        config.flags |= DBF_CONFIG_TRACK_PROCESSOR_RESIDENCY;
    }
    dbf_feed_t* feed{};
    require(dbf_feed_create(
        &config,
        reinterpret_cast<const std::uint8_t*>(dataset.data()),
        static_cast<std::uint32_t>(dataset.size()),
        &feed));

    constexpr std::string_view symbols = "ESM6NQM6";
    std::array<dbf_ticker_subscription_v1, 2> subscriptions{};
    for (std::uint32_t index = 0; index < subscriptions.size(); ++index) {
        subscriptions[index].struct_size = sizeof(dbf_ticker_subscription_v1);
        subscriptions[index].abi_version = DBF_ABI_VERSION;
        subscriptions[index].symbol_offset = index * 4;
        subscriptions[index].symbol_length = 4;
        subscriptions[index].input_symbology = 1;
        subscriptions[index].data_kinds = DBF_MARKET_DATA_QUOTE
                                          | DBF_MARKET_DATA_TRADE
                                          | DBF_MARKET_DATA_MBO;
    }
    require(dbf_feed_subscribe_tickers(
        feed,
        subscriptions.data(),
        static_cast<std::uint32_t>(subscriptions.size()),
        reinterpret_cast<const std::uint8_t*>(symbols.data()),
        static_cast<std::uint32_t>(symbols.size()),
        1'000));
    return feed;
}

void test_native_producer_affinity_is_verified() {
    std::uint16_t processor_group{};
    std::uint16_t logical_processor{};
#if defined(_WIN32)
    PROCESSOR_NUMBER processor{};
    GetCurrentProcessorNumberEx(&processor);
    processor_group = processor.Group;
    logical_processor = processor.Number;
#else
    const auto processor = sched_getcpu();
    assert(processor >= 0 && processor <= std::numeric_limits<std::uint16_t>::max());
    logical_processor = static_cast<std::uint16_t>(processor);
#endif

    auto* feed = create_subscribed_feed(
        1, 1u << 20, processor_group, logical_processor, true);
    dbf_market_record64* buffer{};
    require(dbf_feed_allocate_read_buffer64(feed, 8, &buffer));
    require(dbf_feed_start(feed, 2'000));

    dbf_stats_v1 stats{};
    stats.struct_size = sizeof(stats);
    stats.abi_version = DBF_ABI_VERSION;
    require(dbf_feed_get_stats(feed, &stats));
    assert(stats.producer_affinity_verified == 1);
    assert(stats.observed_producer_processor_group == processor_group);
    assert(stats.observed_producer_logical_processor == logical_processor);

    require(dbf_feed_set_consumer_ready(feed, 2'000));
    dbf_wait_result_v1 wait{};
    wait.struct_size = sizeof(wait);
    wait.abi_version = DBF_ABI_VERSION;
    require(dbf_feed_wait(feed, 2'000, &wait));
    require(dbf_feed_get_stats(feed, &stats));
    assert(stats.producer_processor_sample_count == 1);
    assert(stats.producer_processor_migration_count == 0);
    assert(stats.producer_unique_processor_count == 1);
    assert(stats.producer_off_assignment_count == 0);
    require(dbf_feed_stop(feed, 2'000));
    require(dbf_feed_free_read_buffer64(feed, buffer));
    require(dbf_feed_destroy(feed));
}

void test_layouts() {
    static_assert(sizeof(dbf_record_header32) == 32);
    static_assert(sizeof(dbf_quote_record64) == 64);
    static_assert(sizeof(dbf_trade_record64) == 64);
    static_assert(sizeof(dbf_mbo_record64) == 64);
    static_assert(sizeof(dbf_market_record64) == 64);
    static_assert(sizeof(dbf_utf8_slice_v1) == 8);
    static_assert(sizeof(dbf_contract_query_v1) == 64);
    static_assert(sizeof(dbf_contract_detail_v1) == 192);
    static_assert(sizeof(dbf_latest_price_request_v1) == 88);
    static_assert(sizeof(dbf_latest_price_result64) == 64);
    assert(dbf_get_abi_version() == DBF_ABI_VERSION);
}

struct fake_latest_price_session {
    int stop_count{};
    bool throw_on_stop{};

    void Stop() {
        ++stop_count;
        if (throw_on_stop) {
            throw std::runtime_error{"expected stop failure"};
        }
    }
};

void test_latest_price_session_guard_closes_every_path() {
    fake_latest_price_session success{};
    {
        dbf_latest::session_guard guard{success};
        guard.stop();
        guard.stop();
    }
    assert(success.stop_count == 1);

    fake_latest_price_session error_or_timeout{};
    {
        dbf_latest::session_guard guard{error_or_timeout};
    }
    assert(error_or_timeout.stop_count == 1);

    fake_latest_price_session throwing_cleanup{0, true};
    {
        dbf_latest::session_guard guard{throwing_cleanup};
    }
    assert(throwing_cleanup.stop_count == 1);
}

#if !defined(DBF_ENABLE_LIVE)

void test_contract_query_reports_missing_historical_support() {
    constexpr std::string_view blob = "GLBX.MDP3ESU6";
    dbf_contract_query_v1 query{};
    query.struct_size = sizeof(query);
    query.abi_version = DBF_ABI_VERSION;
    query.query_kind = DBF_CONTRACT_QUERY_EXACT;
    query.timeout_ms = 1'000;
    query.dataset_length = 9;
    query.symbol_count = 1;
    dbf_utf8_slice_v1 symbol{9, 4};
    dbf_contract_details_result_t* result{};
    require(dbf_contract_details_query(
                &query, &symbol,
                reinterpret_cast<const std::uint8_t*>(blob.data()),
                static_cast<std::uint32_t>(blob.size()), &result),
            DBF_NOT_SUPPORTED);
    assert(result != nullptr);
    std::uint32_t required{};
    require(dbf_contract_details_result_get_error(result, nullptr, 0, &required),
            DBF_BUFFER_TOO_SMALL);
    assert(required > 1);
    require(dbf_contract_details_result_destroy(result));
}

void test_latest_price_reports_missing_live_support() {
    constexpr std::string_view blob = "GLBX.MDP3ESU6";
    dbf_latest_price_request_v1 request{};
    request.struct_size = sizeof(request);
    request.abi_version = DBF_ABI_VERSION;
    request.selected_policy = DBF_LATEST_PRICE_LAST_TRADE;
    request.freshness_policy = DBF_LATEST_PRICE_NEXT_OBSERVED;
    request.input_symbology = 1;
    request.dataset = {0, 9};
    request.symbol = {9, 4};
    request.utf8_blob = reinterpret_cast<const std::uint8_t*>(blob.data());
    request.utf8_blob_bytes = static_cast<std::uint32_t>(blob.size());
    dbf_latest_price_result64 result{};
    require(dbf_get_latest_price(&request, 1'000, &result), DBF_NOT_SUPPORTED);

    request.selected_policy = 0;
    require(dbf_get_latest_price(&request, 1'000, &result), DBF_INVALID_ARGUMENT);
    request.selected_policy = DBF_LATEST_PRICE_LAST_TRADE;
    request.struct_size = 0;
    require(dbf_get_latest_price(&request, 1'000, &result), DBF_ABI_MISMATCH);
}

#endif

void test_invalid_config_is_rejected_before_allocation() {
    constexpr std::string_view dataset = "SYNTHETIC";
    dbf_feed_t* feed{};

    auto config = make_config(1, sizeof(dbf_market_record64) * 3);
    require(dbf_feed_create(
                &config,
                reinterpret_cast<const std::uint8_t*>(dataset.data()),
                static_cast<std::uint32_t>(dataset.size()),
                &feed),
            DBF_INVALID_ARGUMENT);
    assert(feed == nullptr);

    config = make_config(1);
    config.reserved[0] = 1;
    require(dbf_feed_create(
                &config,
                reinterpret_cast<const std::uint8_t*>(dataset.data()),
                static_cast<std::uint32_t>(dataset.size()),
                &feed),
            DBF_INVALID_ARGUMENT);
    assert(feed == nullptr);
}

void test_option_chain_subscription_preserves_resolved_mappings() {
    constexpr std::string_view dataset = "SYNTHETIC";
    constexpr std::string_view symbols = "ESM6 C5000ESM6 P5000";
    auto config = make_config(10);
    config.feed_kind = DBF_FEED_OPTION_CHAIN;
    dbf_feed_t* feed{};
    require(dbf_feed_create(
        &config,
        reinterpret_cast<const std::uint8_t*>(dataset.data()),
        static_cast<std::uint32_t>(dataset.size()),
        &feed));

    dbf_option_chain_subscription_v1 subscription{};
    subscription.struct_size = sizeof(subscription);
    subscription.abi_version = DBF_ABI_VERSION;
    subscription.data_kinds = DBF_MARKET_DATA_QUOTE | DBF_MARKET_DATA_TRADE;
    subscription.contract_count = 2;
    std::array<dbf_option_contract_selection_v1, 2> contracts{};
    for (std::uint32_t index = 0; index < contracts.size(); ++index) {
        contracts[index].struct_size = sizeof(dbf_option_contract_selection_v1);
        contracts[index].abi_version = DBF_ABI_VERSION;
        contracts[index].instrument_id = 101 + index;
        contracts[index].publisher_id = 1;
        contracts[index].option_right = static_cast<std::uint8_t>(index + 1);
        contracts[index].raw_symbol_offset = index * 10;
        contracts[index].raw_symbol_length = 10;
    }
    require(dbf_feed_subscribe_option_chain(
        feed,
        &subscription,
        contracts.data(),
        static_cast<std::uint32_t>(contracts.size()),
        reinterpret_cast<const std::uint8_t*>(symbols.data()),
        static_cast<std::uint32_t>(symbols.size()),
        1'000));

    dbf_market_record64* buffer{};
    require(dbf_feed_allocate_read_buffer64(feed, 16, &buffer));
    require(dbf_feed_start(feed, 2'000));
    std::uint32_t mapping_count{};
    std::uint32_t mapping_bytes{};
    require(dbf_feed_get_ticker_mapping_counts(feed, &mapping_count, &mapping_bytes));
    assert(mapping_count == 2);
    std::vector<dbf_ticker_instrument_mapping_v1> mappings(mapping_count);
    std::vector<std::uint8_t> strings(mapping_bytes);
    require(dbf_feed_copy_ticker_mappings(
        feed, mappings.data(), mapping_count, strings.data(), mapping_bytes));
    assert(mappings[0].instrument_id == 101);
    assert(mappings[1].instrument_id == 102);
    assert(mappings[0].publisher_id == 1);
    assert(mappings[1].publisher_id == 1);
    require(dbf_feed_set_consumer_ready(feed, 2'000));
    require(dbf_feed_stop(feed, 2'000));
    require(dbf_feed_destroy(feed));
}

void test_lifecycle_and_order() {
    constexpr std::uint32_t expected_records = 10'000;
    auto* feed = create_subscribed_feed(expected_records);
    dbf_market_record64* buffer{};
    require(dbf_feed_allocate_read_buffer64(feed, 512, &buffer));
    require(dbf_feed_start(feed, 2'000));

    std::uint32_t mapping_count{};
    std::uint32_t mapping_bytes{};
    require(dbf_feed_get_ticker_mapping_counts(feed, &mapping_count, &mapping_bytes));
    assert(mapping_count == 2);
    std::vector<dbf_ticker_instrument_mapping_v1> mappings(mapping_count);
    std::vector<std::uint8_t> strings(mapping_bytes);
    require(dbf_feed_copy_ticker_mappings(
        feed, mappings.data(), mapping_count, strings.data(), mapping_bytes));
    assert(mappings[0].instrument_id == 1);
    assert(mappings[1].instrument_id == 2);

    require(dbf_feed_set_consumer_ready(feed, 2'000));
    std::uint64_t consumed = 0;
    std::uint32_t last_sequence = 0;
    bool terminal = false;
    while (!terminal || consumed < expected_records) {
        dbf_wait_result_v1 wait{};
        wait.struct_size = sizeof(wait);
        wait.abi_version = DBF_ABI_VERSION;
        require(dbf_feed_wait(feed, 5'000, &wait));
        if ((wait.flags & DBF_WAIT_DATA) != 0) {
            do {
                dbf_batch_result_v1 batch{};
                batch.struct_size = sizeof(batch);
                batch.abi_version = DBF_ABI_VERSION;
                require(dbf_feed_read_batch64(feed, buffer, 512, &batch));
                for (std::uint32_t index = 0; index < batch.records_read; ++index) {
                    const auto sequence = buffer[index].header.sequence;
                    assert(sequence == last_sequence + 1);
                    last_sequence = sequence;
                    assert(buffer[index].header.instrument_id == ((sequence - 1) % 2) + 1);
                }
                consumed += batch.records_read;
                if (batch.more_available == 0) {
                    break;
                }
            } while (true);
        }
        terminal = (wait.flags & DBF_WAIT_TERMINAL) != 0;
    }
    assert(consumed == expected_records);

    dbf_stats_v1 stats{};
    stats.struct_size = sizeof(stats);
    stats.abi_version = DBF_ABI_VERSION;
    require(dbf_feed_get_stats(feed, &stats));
    assert(stats.records_produced == expected_records);
    assert(stats.records_consumed == expected_records);
    assert(stats.ring_capacity_records == 16'384);

    require(dbf_feed_stop(feed, 2'000));
    require(dbf_feed_free_read_buffer64(feed, buffer));
    require(dbf_feed_destroy(feed));
}

void test_registered_buffer_ownership() {
    auto* feed = create_subscribed_feed(1);
    dbf_market_record64* buffer{};
    require(dbf_feed_allocate_read_buffer64(feed, 8, &buffer));
    dbf_market_record64* second{};
    require(dbf_feed_allocate_read_buffer64(feed, 8, &second), DBF_INVALID_STATE);

    dbf_batch_result_v1 batch{};
    batch.struct_size = sizeof(batch);
    batch.abi_version = DBF_ABI_VERSION;
    std::array<dbf_market_record64, 8> wrong_buffer{};
    require(dbf_feed_read_batch64(feed, wrong_buffer.data(), 8, &batch), DBF_INVALID_ARGUMENT);

    require(dbf_feed_free_read_buffer64(feed, buffer));
    require(dbf_feed_free_read_buffer64(feed, buffer), DBF_INVALID_ARGUMENT);
    require(dbf_feed_destroy(feed));
}

void test_ring_overrun_faults_without_overwrite() {
    auto* feed = create_subscribed_feed(10'000, sizeof(dbf_market_record64) * 8);
    dbf_market_record64* buffer{};
    require(dbf_feed_allocate_read_buffer64(feed, 8, &buffer));
    require(dbf_feed_start(feed, 2'000));
    require(dbf_feed_set_consumer_ready(feed, 2'000));

    std::this_thread::sleep_for(std::chrono::milliseconds(20));

    dbf_wait_result_v1 wait{};
    wait.struct_size = sizeof(wait);
    wait.abi_version = DBF_ABI_VERSION;
    require(dbf_feed_wait(feed, 100, &wait));
    assert((wait.flags & DBF_WAIT_FAULT) != 0);
    assert(wait.terminal_status == DBF_RING_OVERRUN);

    dbf_stats_v1 stats{};
    stats.struct_size = sizeof(stats);
    stats.abi_version = DBF_ABI_VERSION;
    require(dbf_feed_get_stats(feed, &stats));
    assert(stats.records_produced == 8);
    assert(stats.ring_overruns == 1);

    require(dbf_feed_stop(feed, 2'000));
    require(dbf_feed_free_read_buffer64(feed, buffer));
    require(dbf_feed_destroy(feed));
}

#if defined(DBF_ENABLE_LIVE)

databento::RecordHeader make_dbn_header(databento::RType type,
                                        std::size_t size) {
    databento::RecordHeader header{};
    header.length = static_cast<std::uint8_t>(
        size / databento::RecordHeader::kLengthMultiplier);
    header.rtype = type;
    header.publisher_id = 17;
    header.instrument_id = 42;
    header.ts_event = databento::UnixNanos{std::chrono::nanoseconds{123456789}};
    return header;
}

void test_live_dbn_normalization() {
    databento::Mbp1Msg quote{};
    quote.hd = make_dbn_header(databento::RType::Mbp1, sizeof(quote));
    quote.ts_recv = databento::UnixNanos{std::chrono::nanoseconds{123456999}};
    quote.sequence = 7;
    quote.levels[0] = {101'000'000'000LL, 102'000'000'000LL, 3, 4, 5, 6};
    databento::Record quote_source{&quote.hd};
    dbf_market_record64 normalized{};
    assert(dbf_live::normalize(quote_source, normalized));
    assert(normalized.header.record_kind == DBF_RECORD_QUOTE);
    assert(normalized.header.instrument_id == 42);
    assert(normalized.header.publisher_id == 17);
    assert(normalized.header.ts_event_ns == 123456789);
    assert(normalized.header.ts_recv_ns == 123456999);
    assert(normalized.header.sequence == 7);
    assert(normalized.header.source_schema
           == static_cast<std::uint16_t>(databento::Schema::Mbp1));
    assert(normalized.quote.bid_price == 101'000'000'000LL);
    assert(normalized.quote.ask_count == 6);

    databento::TradeMsg trade{};
    trade.hd = make_dbn_header(databento::RType::Mbp0, sizeof(trade));
    trade.price = 103'000'000'000LL;
    trade.size = 9;
    trade.action = databento::Action::Trade;
    trade.side = databento::Side::Bid;
    trade.depth = 0;
    trade.ts_recv = databento::UnixNanos{std::chrono::nanoseconds{123457000}};
    trade.ts_in_delta = databento::TimeDeltaNanos{37};
    trade.sequence = 8;
    databento::Record trade_source{&trade.hd};
    assert(dbf_live::normalize(trade_source, normalized));
    assert(normalized.header.record_kind == DBF_RECORD_TRADE);
    assert(normalized.trade.price == 103'000'000'000LL);
    assert(normalized.trade.size == 9);
    assert(normalized.trade.action == 'T');
    assert(normalized.trade.side == 'B');
    assert(normalized.trade.ts_in_delta_ns == 37);

    databento::MboMsg mbo{};
    mbo.hd = make_dbn_header(databento::RType::Mbo, sizeof(mbo));
    mbo.order_id = 999;
    mbo.price = databento::kUndefPrice;
    mbo.size = 11;
    mbo.channel_id = 2;
    mbo.action = databento::Action::Add;
    mbo.side = databento::Side::Ask;
    mbo.ts_recv = databento::UnixNanos{std::chrono::nanoseconds{123457100}};
    mbo.ts_in_delta = databento::TimeDeltaNanos{-12};
    mbo.sequence = 9;
    databento::Record mbo_source{&mbo.hd};
    assert(dbf_live::normalize(mbo_source, normalized));
    assert(normalized.header.record_kind == DBF_RECORD_MBO);
    assert((normalized.header.flags & DBF_RECORD_FLAG_UNDEFINED_PRICE) != 0);
    assert(normalized.mbo.price == 0);
    assert(normalized.mbo.order_id == 999);
    assert(normalized.mbo.channel_id == 2);
}

#endif

} // namespace

int main() {
    std::cout << "test_layouts" << std::endl;
    test_layouts();
    std::cout << "test_latest_price_session_guard_closes_every_path" << std::endl;
    test_latest_price_session_guard_closes_every_path();
#if !defined(DBF_ENABLE_LIVE)
    std::cout << "test_contract_query_reports_missing_historical_support" << std::endl;
    test_contract_query_reports_missing_historical_support();
    std::cout << "test_latest_price_reports_missing_live_support" << std::endl;
    test_latest_price_reports_missing_live_support();
#endif
    std::cout << "test_invalid_config_is_rejected_before_allocation" << std::endl;
    test_invalid_config_is_rejected_before_allocation();
    std::cout << "test_option_chain_subscription_preserves_resolved_mappings" << std::endl;
    test_option_chain_subscription_preserves_resolved_mappings();
    std::cout << "test_lifecycle_and_order" << std::endl;
    test_lifecycle_and_order();
    std::cout << "test_native_producer_affinity_is_verified" << std::endl;
    test_native_producer_affinity_is_verified();
    std::cout << "test_registered_buffer_ownership" << std::endl;
    test_registered_buffer_ownership();
    std::cout << "test_ring_overrun_faults_without_overwrite" << std::endl;
    test_ring_overrun_faults_without_overwrite();
#if defined(DBF_ENABLE_LIVE)
    std::cout << "test_live_dbn_normalization" << std::endl;
    test_live_dbn_normalization();
#endif
    std::cout << "All native synthetic feed tests passed\n";
    return 0;
}
