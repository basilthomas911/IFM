#include "databento_feed_native.h"

#include <array>
#if defined(NDEBUG)
#undef NDEBUG
#endif
#include <cassert>
#include <chrono>
#include <cstdint>
#include <cstring>
#include <iostream>
#include <string_view>
#include <thread>
#include <vector>

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
                                   std::uint64_t ring_bytes = 1u << 20) {
    constexpr std::string_view dataset = "SYNTHETIC";
    auto config = make_config(record_count, ring_bytes);
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

void test_layouts() {
    static_assert(sizeof(dbf_record_header32) == 32);
    static_assert(sizeof(dbf_quote_record64) == 64);
    static_assert(sizeof(dbf_trade_record64) == 64);
    static_assert(sizeof(dbf_mbo_record64) == 64);
    static_assert(sizeof(dbf_market_record64) == 64);
    assert(dbf_get_abi_version() == DBF_ABI_VERSION);
}

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

} // namespace

int main() {
    std::cout << "test_layouts" << std::endl;
    test_layouts();
    std::cout << "test_invalid_config_is_rejected_before_allocation" << std::endl;
    test_invalid_config_is_rejected_before_allocation();
    std::cout << "test_lifecycle_and_order" << std::endl;
    test_lifecycle_and_order();
    std::cout << "test_registered_buffer_ownership" << std::endl;
    test_registered_buffer_ownership();
    std::cout << "test_ring_overrun_faults_without_overwrite" << std::endl;
    test_ring_overrun_faults_without_overwrite();
    std::cout << "All native synthetic feed tests passed\n";
    return 0;
}
