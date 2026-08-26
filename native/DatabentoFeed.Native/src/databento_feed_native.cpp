#include "databento_feed_native.h"
#include "latest_price_session_guard.hpp"

#include <algorithm>
#include <array>
#include <atomic>
#include <chrono>
#include <condition_variable>
#include <charconv>
#include <cstdio>
#include <cstdlib>
#include <cstring>
#include <filesystem>
#include <fstream>
#include <limits>
#include <memory>
#include <mutex>
#include <new>
#include <numeric>
#include <optional>
#include <stdexcept>
#include <string>
#include <string_view>
#include <system_error>
#include <thread>
#include <unordered_map>
#include <utility>
#include <vector>

#if defined(DBF_ENABLE_LIVE)
#include "databento_live_normalization.hpp"

#include <databento/constants.hpp>
#include <databento/exceptions.hpp>
#include <databento/historical.hpp>
#include <databento/live.hpp>
#endif

#if defined(_WIN32)
#define NOMINMAX
#include <Windows.h>
#else
#include <cerrno>
#include <linux/mempolicy.h>
#include <poll.h>
#include <pthread.h>
#include <sched.h>
#include <sys/resource.h>
#include <sys/eventfd.h>
#include <sys/mman.h>
#include <sys/syscall.h>
#include <unistd.h>
#endif

namespace {

using monotonic_clock = std::chrono::steady_clock;

struct mapping_entry {
    std::uint32_t subscription_index{};
    std::uint32_t instrument_id{};
    std::uint16_t publisher_id{};
    std::uint32_t data_kinds{};
    std::uint32_t input_symbology{};
    std::string requested_symbol;
    std::string raw_symbol;
    bool resolved{};
};

struct alignas(64) ring_cursor {
    std::atomic<std::uint64_t> value{0};
};

constexpr std::uint16_t unpinned_processor = 0xffffu;

std::uint64_t monotonic_nanoseconds() noexcept {
    return static_cast<std::uint64_t>(
        std::chrono::duration_cast<std::chrono::nanoseconds>(
            monotonic_clock::now().time_since_epoch())
            .count());
}

bool valid_struct(std::uint32_t struct_size,
                  std::uint32_t expected_size,
                  std::uint32_t abi_version) noexcept {
    return struct_size >= expected_size && abi_version == DBF_ABI_VERSION;
}

bool valid_blob_range(std::uint32_t offset,
                      std::uint32_t length,
                      std::uint32_t blob_bytes) noexcept {
    return offset <= blob_bytes && length <= blob_bytes - offset;
}

bool is_power_of_two(std::uint64_t value) noexcept {
    return value != 0 && (value & (value - 1)) == 0;
}

void* allocate_pages(std::size_t bytes,
                     bool require_base_pages,
                     std::uint16_t numa_node,
                     bool require_numa,
                     dbf_status& status) noexcept {
#if defined(_WIN32)
    (void)require_base_pages;
    void* memory = numa_node == 0xffffu
                       ? VirtualAlloc(nullptr, bytes, MEM_RESERVE | MEM_COMMIT, PAGE_READWRITE)
                       : VirtualAllocExNuma(
                             GetCurrentProcess(), nullptr, bytes,
                             MEM_RESERVE | MEM_COMMIT, PAGE_READWRITE, numa_node);
    if (memory == nullptr && numa_node != 0xffffu && !require_numa) {
        memory = VirtualAlloc(nullptr, bytes, MEM_RESERVE | MEM_COMMIT, PAGE_READWRITE);
    }
    status = memory == nullptr && numa_node != 0xffffu && require_numa
                 ? DBF_NUMA_CONFIGURATION_FAILED
                 : (memory == nullptr ? DBF_NO_MEMORY : DBF_OK);
    return memory;
#else
    void* memory = mmap(nullptr, bytes, PROT_READ | PROT_WRITE,
                        MAP_PRIVATE | MAP_ANONYMOUS, -1, 0);
    if (memory == MAP_FAILED) {
        status = DBF_NO_MEMORY;
        return nullptr;
    }
#if defined(MADV_NOHUGEPAGE)
    if (madvise(memory, bytes, MADV_NOHUGEPAGE) != 0 && require_base_pages) {
        munmap(memory, bytes);
        status = DBF_PAGE_CONFIGURATION_FAILED;
        return nullptr;
    }
#else
    if (require_base_pages) {
        munmap(memory, bytes);
        status = DBF_PAGE_CONFIGURATION_FAILED;
        return nullptr;
    }
#endif
    if (numa_node != 0xffffu) {
        if (numa_node >= sizeof(unsigned long) * 8u) {
            munmap(memory, bytes);
            status = DBF_NUMA_CONFIGURATION_FAILED;
            return nullptr;
        }
        const unsigned long node_mask = 1ul << numa_node;
        if (syscall(SYS_mbind, memory, bytes, MPOL_BIND,
                    &node_mask, sizeof(node_mask) * 8u, 0) != 0) {
            if (require_numa) {
                munmap(memory, bytes);
                status = DBF_NUMA_CONFIGURATION_FAILED;
                return nullptr;
            }
        }
    }
    status = DBF_OK;
    return memory;
#endif
}

void release_pages(void* memory, std::size_t bytes) noexcept {
    if (memory == nullptr) {
        return;
    }
#if defined(_WIN32)
    (void)bytes;
    VirtualFree(memory, 0, MEM_RELEASE);
#else
    munmap(memory, bytes);
#endif
}

bool lock_pages(void* memory, std::size_t bytes) noexcept {
#if defined(_WIN32)
    return VirtualLock(memory, bytes) != FALSE;
#else
    return mlock(memory, bytes) == 0;
#endif
}

void unlock_pages(void* memory, std::size_t bytes) noexcept {
#if defined(_WIN32)
    VirtualUnlock(memory, bytes);
#else
    munlock(memory, bytes);
#endif
}

} // namespace

struct dbf_feed {
    dbf_feed_config_v1 config{};
    std::string dataset;
    dbf_market_record64* ring{};
    std::size_t ring_bytes{};
    std::uint64_t ring_capacity{};
    std::uint64_t ring_mask{};
    bool ring_locked{};
    ring_cursor head;
    ring_cursor tail;

#if defined(_WIN32)
    HANDLE signal{};
#else
    int signal{-1};
#endif

    std::mutex control_mutex;
    std::mutex join_mutex;
    std::condition_variable control_cv;
    std::thread producer;
    std::atomic<bool> stop_requested{false};
    std::atomic<bool> producer_done{false};
    std::atomic<std::uint32_t> state{DBF_STATE_CREATED};
    std::atomic<std::int32_t> terminal_status{DBF_OK};
    monotonic_clock::time_point start_deadline{};

    std::vector<mapping_entry> mappings;
    dbf_market_record64* read_buffer{};
    std::uint32_t read_buffer_capacity{};
    std::size_t read_buffer_bytes{};

    std::atomic<std::uint64_t> ring_high_water{};
    std::atomic<std::uint64_t> records_produced{};
    std::atomic<std::uint64_t> records_consumed{};
    std::atomic<std::uint64_t> signal_count{};
    std::atomic<std::uint64_t> wait_count{};
    std::atomic<std::uint64_t> ring_full_episodes{};
    std::atomic<std::uint64_t> ring_overruns{};
    std::atomic<std::uint64_t> subscription_acknowledgements{};
    std::atomic<std::uint64_t> heartbeat_messages{};
    std::atomic<std::uint64_t> slow_reader_warnings{};
    std::atomic<std::uint64_t> last_message_monotonic_ns{};
    std::atomic<std::uint32_t> observed_producer_location{0xffffffffu};
    std::atomic<std::uint32_t> producer_affinity_verified{};
    std::atomic<std::uint32_t> last_producer_location{0xffffffffu};
    std::atomic<std::uint64_t> producer_processor_sample_count{};
    std::atomic<std::uint64_t> producer_processor_migration_count{};
    std::atomic<std::uint32_t> producer_off_assignment_count{};
    std::atomic<std::uint32_t> producer_unique_processor_count{};
    std::array<std::atomic<std::uint64_t>, 64> producer_observed_processors{};
    bool producer_using_alternate{};

    std::mutex error_mutex;
    std::string last_error;
};

std::uint32_t current_processor_location() noexcept {
#if defined(_WIN32)
    PROCESSOR_NUMBER processor{};
    GetCurrentProcessorNumberEx(&processor);
    return (static_cast<std::uint32_t>(processor.Group) << 16u)
           | processor.Number;
#else
    const auto processor = sched_getcpu();
    return processor < 0 || processor > std::numeric_limits<std::uint16_t>::max()
               ? 0xffffffffu
               : static_cast<std::uint32_t>(processor);
#endif
}

void record_producer_processor_residency(dbf_feed* feed) noexcept {
    if ((feed->config.flags & DBF_CONFIG_TRACK_PROCESSOR_RESIDENCY) == 0) {
        return;
    }
    const auto location = current_processor_location();
    if (location == 0xffffffffu) {
        return;
    }
    auto first = 0xffffffffu;
    feed->observed_producer_location.compare_exchange_strong(
        first, location, std::memory_order_release, std::memory_order_relaxed);
    const auto previous = feed->last_producer_location.exchange(
        location, std::memory_order_relaxed);
    if (previous != 0xffffffffu && previous != location) {
        feed->producer_processor_migration_count.fetch_add(
            1, std::memory_order_relaxed);
    }
    const auto group = location >> 16u;
    const auto logical_processor = location & 0xffffu;
    const auto processor_id = group * 64u + logical_processor;
    const auto word_index = processor_id / 64u;
    if (word_index < feed->producer_observed_processors.size()) {
        const auto mask = 1ull << (processor_id % 64u);
        const auto previous_mask = feed->producer_observed_processors[word_index].fetch_or(
            mask, std::memory_order_relaxed);
        if ((previous_mask & mask) == 0) {
            feed->producer_unique_processor_count.fetch_add(1, std::memory_order_relaxed);
        }
    }
    if (feed->config.producer_logical_processor != unpinned_processor) {
        const auto assigned =
            (static_cast<std::uint32_t>(feed->config.producer_processor_group) << 16u)
            | feed->config.producer_logical_processor;
        if (location != assigned) {
            feed->producer_off_assignment_count.fetch_add(1, std::memory_order_relaxed);
        }
    }
    feed->producer_processor_sample_count.fetch_add(1, std::memory_order_relaxed);
}

bool apply_producer_forced_migration_if_required(dbf_feed* feed) noexcept {
    const auto interval = feed->config.forced_migration_interval_records;
    const auto produced = feed->records_produced.load(std::memory_order_relaxed);
    if (interval == 0 || produced == 0 || produced % interval != 0) {
        return true;
    }
    feed->producer_using_alternate = !feed->producer_using_alternate;
    const auto group = feed->producer_using_alternate
                           ? feed->config.producer_alternate_processor_group
                           : feed->config.producer_processor_group;
    const auto logical_processor = feed->producer_using_alternate
                                       ? feed->config.producer_alternate_logical_processor
                                       : feed->config.producer_logical_processor;
#if defined(_WIN32)
    GROUP_AFFINITY affinity{};
    affinity.Group = group;
    affinity.Mask = 1ull << (logical_processor % 64u);
    GROUP_AFFINITY observed{};
    if (SetThreadGroupAffinity(GetCurrentThread(), &affinity, nullptr) == FALSE
        || GetThreadGroupAffinity(GetCurrentThread(), &observed) == FALSE
        || observed.Group != affinity.Group
        || observed.Mask != affinity.Mask) {
#else
    if (group != 0) {
        return false;
    }
    cpu_set_t affinity;
    CPU_ZERO(&affinity);
    CPU_SET(logical_processor, &affinity);
    cpu_set_t observed;
    CPU_ZERO(&observed);
    if (pthread_setaffinity_np(pthread_self(), sizeof(affinity), &affinity) != 0
        || pthread_getaffinity_np(pthread_self(), sizeof(observed), &observed) != 0
        || CPU_COUNT(&observed) != 1
        || CPU_ISSET(logical_processor, &observed) == 0) {
#endif
        return false;
    }
    return true;
}

struct contract_result_entry {
    dbf_contract_detail_v1 detail{};
    std::string raw_symbol;
    std::string asset;
    std::string underlying;
    std::string currency;
    std::string settlement_currency;
    std::string exchange;
    std::string security_type;
    std::string cfi;
    std::string unit_of_measure;
};

struct dbf_contract_details_result {
    std::vector<contract_result_entry> entries;
    std::string error;
};

struct dbf_historical_result {
    std::string payload;
    std::string error;
    std::vector<dbf_historical_record120> records;
    std::size_t cursor{};
    std::uint64_t batch_ordinal{};
};

namespace {

bool valid_historical_request(
    const dbf_historical_request_v1* request,
    const dbf_utf8_slice_v1* symbols,
    const std::uint8_t* blob,
    std::uint32_t blob_bytes) noexcept {
    if (request == nullptr
        || !valid_struct(request->struct_size, sizeof(*request), request->abi_version)
        || request->reserved32 != 0
        || request->schema < DBF_HISTORICAL_DEFINITION
        || request->schema > DBF_HISTORICAL_STATISTICS
        || request->symbol_count == 0 || symbols == nullptr || blob == nullptr
        || request->dataset.length == 0
        || !valid_blob_range(request->dataset.offset, request->dataset.length, blob_bytes)
        || request->start_ts_ns >= request->end_ts_ns
        || request->timeout_ms == 0 || request->timeout_ms == DBF_WAIT_INFINITE) {
        return false;
    }
    for (std::uint32_t index = 0; index < request->symbol_count; ++index) {
        if (symbols[index].length == 0
            || !valid_blob_range(symbols[index].offset, symbols[index].length, blob_bytes)) {
            return false;
        }
    }
    return true;
}

std::string historical_text(
    const std::uint8_t* value,
    std::uint32_t bytes) {
    if (value == nullptr || bytes == 0) {
        return {};
    }
    return {reinterpret_cast<const char*>(value), bytes};
}

dbf_status copy_historical_text(
    const std::string& value,
    std::uint8_t* buffer,
    std::uint32_t capacity,
    std::uint32_t* required) noexcept {
    if (required == nullptr || value.size() + 1 > std::numeric_limits<std::uint32_t>::max()) {
        return DBF_INVALID_ARGUMENT;
    }
    *required = static_cast<std::uint32_t>(value.size() + 1);
    if (buffer == nullptr || capacity < *required) {
        return DBF_BUFFER_TOO_SMALL;
    }
    std::memcpy(buffer, value.c_str(), *required);
    return DBF_OK;
}

dbf_historical_record120 make_historical_synthetic_record(
    const dbf_historical_request_v1& request,
    std::uint64_t ordinal) noexcept {
    dbf_historical_record120 record{};
    record.struct_size = sizeof(record);
    record.abi_version = DBF_ABI_VERSION;
    record.schema = request.schema;
    record.record_kind = request.schema == DBF_HISTORICAL_TRADES
                             ? DBF_HISTORICAL_RECORD_TRADE
                             : request.schema == DBF_HISTORICAL_DEFINITION
                                   ? DBF_HISTORICAL_RECORD_DEFINITION
                                   : request.schema == DBF_HISTORICAL_STATISTICS
                                         ? DBF_HISTORICAL_RECORD_STATISTIC
                                         : DBF_HISTORICAL_RECORD_OHLCV;
    record.instrument_id = 1000u + static_cast<std::uint32_t>(ordinal);
    record.publisher_id = 7;
    record.event_ts_ns = request.start_ts_ns
                         + static_cast<std::int64_t>(ordinal) * 60'000'000'000LL;
    record.source_sequence = static_cast<std::int64_t>(ordinal + 1);
    const auto price = 5'000'000'000LL + static_cast<std::int64_t>(ordinal) * 1'000'000LL;
    record.open_price = price;
    record.high_price = price + 2'000'000LL;
    record.low_price = price - 2'000'000LL;
    record.close_or_trade_price = price + 500'000LL;
    record.volume_or_size = 10 + ordinal;
    record.action = 'T';
    record.side = ordinal % 2 == 0 ? 'B' : 'A';
    constexpr char symbol[] = "SYNTH";
    std::memcpy(record.symbol, symbol, sizeof(symbol));
    return record;
}

#if defined(DBF_ENABLE_LIVE)

std::string environment_value(const char* name) {
#if defined(_WIN32)
    char* buffer{};
    std::size_t length{};
    if (_dupenv_s(&buffer, &length, name) != 0 || buffer == nullptr) {
        return {};
    }
    std::string value{buffer};
    std::free(buffer);
    return value;
#else
    const auto* value = std::getenv(name);
    return value == nullptr ? "" : value;
#endif
}

databento::Schema historical_schema(std::uint32_t schema) {
    return schema == DBF_HISTORICAL_DEFINITION
               ? databento::Schema::Definition
               : schema == DBF_HISTORICAL_OHLCV_1M
                     ? databento::Schema::Ohlcv1M
                     : schema == DBF_HISTORICAL_TRADES
                           ? databento::Schema::Trades
                           : databento::Schema::Statistics;
}

databento::SType historical_stype(std::uint32_t value) {
    switch (value) {
        case 1u: return databento::SType::RawSymbol;
        case 2u: return databento::SType::Continuous;
        case 3u: return databento::SType::InstrumentId;
        default: throw std::invalid_argument("Unsupported historical input symbology");
    }
}

std::uint32_t historical_schema_id(databento::Schema schema) noexcept {
    switch (schema) {
        case databento::Schema::Definition: return DBF_HISTORICAL_DEFINITION;
        case databento::Schema::Ohlcv1M: return DBF_HISTORICAL_OHLCV_1M;
        case databento::Schema::Trades: return DBF_HISTORICAL_TRADES;
        case databento::Schema::Statistics: return DBF_HISTORICAL_STATISTICS;
        default: return 0;
    }
}

std::string json_escape(std::string_view value) {
    std::string escaped;
    escaped.reserve(value.size());
    for (const auto character : value) {
        switch (character) {
            case '\\': escaped += "\\\\"; break;
            case '"': escaped += "\\\""; break;
            case '\n': escaped += "\\n"; break;
            case '\r': escaped += "\\r"; break;
            case '\t': escaped += "\\t"; break;
            default: escaped += character; break;
        }
    }
    return escaped;
}

std::string historical_job_state(databento::JobState state) {
    switch (state) {
        case databento::JobState::Queued: return "Queued";
        case databento::JobState::Processing: return "Processing";
        case databento::JobState::Done: return "Completed";
        case databento::JobState::Expired: return "Expired";
        default: return "Failed";
    }
}

std::string historical_job_payload(const databento::BatchJob& job) {
    return "{\"providerJobId\":\"" + json_escape(job.id)
           + "\",\"state\":\"" + historical_job_state(job.state)
           + "\",\"costUsd\":" + std::to_string(job.cost_usd)
           + ",\"recordCount\":" + std::to_string(job.record_count)
           + ",\"billedBytes\":" + std::to_string(job.billed_size)
           + ",\"progressPercent\":"
           + std::to_string(job.progress.value_or(job.state == databento::JobState::Done ? 100 : 0))
           + "}";
}

std::vector<std::string> historical_symbols(
    const dbf_historical_request_v1& request,
    const dbf_utf8_slice_v1* symbols,
    const std::uint8_t* utf8_blob) {
    std::vector<std::string> values;
    values.reserve(request.symbol_count);
    for (std::uint32_t index = 0; index < request.symbol_count; ++index) {
        values.emplace_back(
            reinterpret_cast<const char*>(utf8_blob + symbols[index].offset),
            symbols[index].length);
    }
    return values;
}

std::string historical_record_symbol(
    const databento::Metadata& metadata,
    databento::UnixNanos event_timestamp,
    std::string_view fallback) {
    const date::year_month_day event_date{
        date::floor<date::days>(event_timestamp)};
    for (const auto& mapping : metadata.mappings) {
        for (const auto& interval : mapping.intervals) {
            if (event_date >= interval.start_date && event_date < interval.end_date) {
                return interval.symbol;
            }
        }
    }
    return std::string{fallback};
}

void append_historical_records(
    databento::DbnStore& store,
    std::uint32_t schema,
    std::string_view fallback_symbol,
    dbf_historical_result& result) {
    const auto& metadata = store.GetMetadata();
    std::uint64_t ordinal{};
    while (const auto* source = store.NextRecord()) {
        dbf_historical_record120 record{};
        record.struct_size = sizeof(record);
        record.abi_version = DBF_ABI_VERSION;
        record.schema = schema;
        databento::UnixNanos event_timestamp{};
        if (const auto* ohlcv = source->GetIf<databento::OhlcvMsg>()) {
            record.record_kind = DBF_HISTORICAL_RECORD_OHLCV;
            record.instrument_id = ohlcv->hd.instrument_id;
            record.publisher_id = ohlcv->hd.publisher_id;
            event_timestamp = ohlcv->hd.ts_event;
            record.open_price = ohlcv->open;
            record.high_price = ohlcv->high;
            record.low_price = ohlcv->low;
            record.close_or_trade_price = ohlcv->close;
            record.volume_or_size = ohlcv->volume;
            record.source_sequence = static_cast<std::int64_t>(++ordinal);
        } else if (const auto* trade = source->GetIf<databento::TradeMsg>()) {
            record.record_kind = DBF_HISTORICAL_RECORD_TRADE;
            record.instrument_id = trade->hd.instrument_id;
            record.publisher_id = trade->hd.publisher_id;
            event_timestamp = trade->hd.ts_event;
            record.close_or_trade_price = trade->price;
            record.volume_or_size = trade->size;
            record.source_sequence = trade->sequence;
            record.action = static_cast<std::uint8_t>(trade->action);
            record.side = static_cast<std::uint8_t>(trade->side);
            ++ordinal;
        } else {
            continue;
        }
        record.event_ts_ns = static_cast<std::int64_t>(
            event_timestamp.time_since_epoch().count());
        const auto symbol = historical_record_symbol(
            metadata, event_timestamp, fallback_symbol);
        std::memcpy(record.symbol, symbol.data(),
                    std::min(symbol.size(), sizeof(record.symbol) - 1));
        result.records.push_back(record);
    }
}

std::uint8_t contract_kind(char instrument_class) noexcept {
    switch (instrument_class) {
    case 'F':
        return DBF_CONTRACT_FUTURE;
    case 'C':
        return DBF_CONTRACT_CALL_OPTION;
    case 'P':
        return DBF_CONTRACT_PUT_OPTION;
    default:
        return 0;
    }
}

std::string previous_iso_date(std::string_view timestamp) {
    if (timestamp.size() < 10) {
        throw std::runtime_error("Databento returned an invalid definition range");
    }
    int year{};
    unsigned month{};
    unsigned day{};
    const auto parse = [](std::string_view value, auto& destination) {
        const auto result = std::from_chars(
            value.data(), value.data() + value.size(), destination);
        return result.ec == std::errc{} && result.ptr == value.data() + value.size();
    };
    if (timestamp[4] != '-' || timestamp[7] != '-'
        || !parse(timestamp.substr(0, 4), year)
        || !parse(timestamp.substr(5, 2), month)
        || !parse(timestamp.substr(8, 2), day)) {
        throw std::runtime_error("Databento returned an invalid definition range");
    }
    const std::chrono::year_month_day parsed{
        std::chrono::year{year}, std::chrono::month{month}, std::chrono::day{day}};
    if (!parsed.ok()) {
        throw std::runtime_error("Databento returned an invalid definition range");
    }
    const std::chrono::year_month_day previous{
        std::chrono::sys_days{parsed} - std::chrono::days{1}};
    char buffer[11]{};
    const int written = std::snprintf(
        buffer, sizeof(buffer), "%04d-%02u-%02u",
        static_cast<int>(previous.year()),
        static_cast<unsigned>(previous.month()),
        static_cast<unsigned>(previous.day()));
    if (written != 10) {
        throw std::runtime_error("Unable to format Databento definition range");
    }
    return buffer;
}

contract_result_entry make_contract_entry(const databento::InstrumentDefMsg& source) {
    contract_result_entry destination{};
    auto& detail = destination.detail;
    detail.struct_size = sizeof(detail);
    detail.abi_version = DBF_ABI_VERSION;
    detail.flags = DBF_CONTRACT_FOUND;
    detail.instrument_id = source.hd.instrument_id;
    detail.publisher_id = source.hd.publisher_id;
    detail.contract_kind = contract_kind(static_cast<char>(source.instrument_class));
    detail.maturity_year = source.maturity_year;
    detail.maturity_month = source.maturity_month;
    detail.maturity_day = source.maturity_day;
    detail.maturity_week = source.maturity_week;
    detail.underlying_id = source.underlying_id;
    if (source.contract_multiplier != std::numeric_limits<std::int32_t>::max()) {
        detail.flags |= DBF_CONTRACT_HAS_MULTIPLIER;
        detail.contract_multiplier = source.contract_multiplier;
    }
    detail.raw_instrument_id = source.raw_instrument_id;
    if (source.strike_price != databento::kUndefPrice) {
        detail.flags |= DBF_CONTRACT_HAS_STRIKE_PRICE;
        detail.strike_price = source.strike_price;
    }
    if (source.min_price_increment != databento::kUndefPrice) {
        detail.flags |= DBF_CONTRACT_HAS_MIN_PRICE_INCREMENT;
        detail.min_price_increment = source.min_price_increment;
    }
    if (source.min_price_increment_amount != databento::kUndefPrice) {
        detail.flags |= DBF_CONTRACT_HAS_MIN_PRICE_INCREMENT_AMOUNT;
        detail.min_price_increment_amount = source.min_price_increment_amount;
    }
    const auto expiration = source.expiration.time_since_epoch().count();
    if (expiration != databento::kUndefTimestamp) {
        detail.flags |= DBF_CONTRACT_HAS_EXPIRATION;
        detail.expiration_ts_ns = expiration;
    }
    const auto activation = source.activation.time_since_epoch().count();
    if (activation != databento::kUndefTimestamp) {
        detail.flags |= DBF_CONTRACT_HAS_ACTIVATION;
        detail.activation_ts_ns = activation;
    }
    const std::chrono::year_month_day maturity{
        std::chrono::year{source.maturity_year},
        std::chrono::month{source.maturity_month},
        std::chrono::day{source.maturity_day}};
    if (maturity.ok()) {
        detail.flags |= DBF_CONTRACT_HAS_MATURITY_DATE;
    }
    if (source.maturity_week != std::numeric_limits<std::uint8_t>::max()) {
        detail.flags |= DBF_CONTRACT_HAS_MATURITY_WEEK;
    }
    destination.raw_symbol = source.RawSymbol();
    destination.asset = source.Asset();
    destination.underlying = source.Underlying();
    destination.currency = source.Currency();
    destination.settlement_currency = source.SettlCurrency();
    destination.exchange = source.Exchange();
    destination.security_type = source.SecurityType();
    destination.cfi = source.Cfi();
    destination.unit_of_measure = source.UnitOfMeasure();
    return destination;
}

std::vector<contract_result_entry> fetch_definitions(
    const std::string& dataset,
    const std::vector<std::string>& symbols,
    databento::SType input_symbology,
    std::uint32_t timeout_ms) {
    const auto timeout = std::chrono::milliseconds{timeout_ms};
    const auto ca_file = environment_value("SSL_CERT_FILE");
    auto client = databento::Historical::Builder()
                      .SetKeyFromEnv()
                      .SetHttpClientConfig([timeout, ca_file](httplib::Client& http) {
                          http.set_connection_timeout(timeout);
                          http.set_read_timeout(timeout);
                          http.set_write_timeout(timeout);
                          if (!ca_file.empty()) {
                              http.set_ca_cert_path(ca_file);
                              http.enable_system_ca(false);
                          }
#if defined(_WIN32)
                          else {
                              http.enable_system_ca(true);
                              http.enable_windows_certificate_verification(true);
                              // OpenSSL cannot always build chains that require Windows AIA
                              // retrieval. Let the TLS handshake continue so cpp-httplib's
                              // mandatory post-handshake Schannel policy check can build and
                              // validate the chain, including the requested host name.
                              http.set_server_certificate_verifier(
                                  [](const httplib::tls::VerifyContext&) { return true; });
                          }
#endif
                      })
                      .Build();
    const auto dataset_range = client.MetadataGetDatasetRange(dataset);
    const auto schema_range = dataset_range.range_by_schema.find(
        databento::Schema::Definition);
    const std::string definition_end = schema_range == dataset_range.range_by_schema.end()
                                           ? dataset_range.end
                                           : schema_range->second.end;
    if (definition_end.empty()) {
        throw std::runtime_error("Databento returned no definition range for the dataset");
    }
    auto store = client.TimeseriesGetRange(
        dataset,
        databento::DateTimeRange<std::string>{
            previous_iso_date(definition_end), definition_end},
        symbols,
        databento::Schema::Definition,
        input_symbology,
        databento::SType::InstrumentId,
        0);

    std::vector<contract_result_entry> entries;
    std::unordered_map<std::string, std::size_t> by_symbol;
    while (const auto* record = store.NextRecord()) {
        const auto* definition = record->GetIf<databento::InstrumentDefMsg>();
        if (definition == nullptr || contract_kind(
                static_cast<char>(definition->instrument_class)) == 0) {
            continue;
        }
        auto entry = make_contract_entry(*definition);
        const auto [position, inserted] = by_symbol.emplace(
            entry.raw_symbol, entries.size());
        if (inserted) {
            entries.push_back(std::move(entry));
        } else {
            entries[position->second] = std::move(entry);
        }
    }
    return entries;
}

#endif

std::uint64_t contract_result_blob_bytes(
    const dbf_contract_details_result& result) noexcept {
    std::uint64_t bytes{};
    for (const auto& entry : result.entries) {
        bytes += entry.raw_symbol.size();
        bytes += entry.asset.size();
        bytes += entry.underlying.size();
        bytes += entry.currency.size();
        bytes += entry.settlement_currency.size();
        bytes += entry.exchange.size();
        bytes += entry.security_type.size();
        bytes += entry.cfi.size();
        bytes += entry.unit_of_measure.size();
    }
    return bytes;
}

void set_contract_result_error(
    dbf_contract_details_result* result,
    const char* message) noexcept {
    if (result == nullptr) {
        return;
    }
    try {
        result->error = message == nullptr ? "" : message;
    } catch (...) {
    }
}

void set_error(dbf_feed* feed, dbf_status status, const char* message) noexcept {
    if (feed == nullptr) {
        return;
    }
    feed->terminal_status.store(status, std::memory_order_release);
    try {
        std::lock_guard lock(feed->error_mutex);
        feed->last_error = message == nullptr ? "" : message;
    } catch (...) {
    }
}

void notify_signal(dbf_feed* feed) noexcept {
    feed->signal_count.fetch_add(1, std::memory_order_relaxed);
#if defined(_WIN32)
    SetEvent(feed->signal);
#else
    const std::uint64_t value = 1;
    const auto ignored = write(feed->signal, &value, sizeof(value));
    (void)ignored;
#endif
}

dbf_status wait_signal(dbf_feed* feed, std::uint32_t timeout_ms) noexcept {
    feed->wait_count.fetch_add(1, std::memory_order_relaxed);
#if defined(_WIN32)
    const DWORD timeout = timeout_ms == DBF_WAIT_INFINITE ? INFINITE : timeout_ms;
    const DWORD result = WaitForSingleObject(feed->signal, timeout);
    if (result == WAIT_OBJECT_0) {
        return DBF_OK;
    }
    return result == WAIT_TIMEOUT ? DBF_TIMEOUT : DBF_OS_ERROR;
#else
    pollfd descriptor{};
    descriptor.fd = feed->signal;
    descriptor.events = POLLIN;
    const int timeout = timeout_ms == DBF_WAIT_INFINITE
                            ? -1
                            : static_cast<int>(std::min<std::uint32_t>(
                                  timeout_ms,
                                  static_cast<std::uint32_t>(std::numeric_limits<int>::max())));
    int result;
    do {
        result = poll(&descriptor, 1, timeout);
    } while (result < 0 && errno == EINTR);
    if (result == 0) {
        return DBF_TIMEOUT;
    }
    if (result < 0) {
        return DBF_OS_ERROR;
    }
    std::uint64_t value{};
    const auto ignored = read(feed->signal, &value, sizeof(value));
    (void)ignored;
    return DBF_OK;
#endif
}

void update_high_water(dbf_feed* feed, std::uint64_t used) noexcept {
    auto current = feed->ring_high_water.load(std::memory_order_relaxed);
    while (current < used
           && !feed->ring_high_water.compare_exchange_weak(
               current, used, std::memory_order_relaxed, std::memory_order_relaxed)) {
    }
}

std::uint32_t select_record_kind(std::uint32_t kinds, std::uint64_t sequence) noexcept {
    std::uint32_t enabled[4]{};
    std::uint32_t count = 0;
    if ((kinds & DBF_MARKET_DATA_QUOTE) != 0) {
        enabled[count++] = DBF_RECORD_QUOTE;
    }
    if ((kinds & DBF_MARKET_DATA_TRADE) != 0) {
        enabled[count++] = DBF_RECORD_TRADE;
    }
    if ((kinds & DBF_MARKET_DATA_MBO) != 0) {
        enabled[count++] = DBF_RECORD_MBO;
    }
    if ((kinds & DBF_MARKET_DATA_STATISTICS) != 0) {
        enabled[count++] = DBF_RECORD_STATISTICS;
    }
    return count == 0
               ? static_cast<std::uint32_t>(DBF_RECORD_QUOTE)
               : enabled[sequence % count];
}

dbf_market_record64 make_synthetic_record(const mapping_entry& mapping,
                                          std::uint64_t sequence) noexcept {
    dbf_market_record64 record{};
    const auto timestamp = static_cast<std::int64_t>(monotonic_nanoseconds());
    const auto kind = select_record_kind(mapping.data_kinds, sequence);
    record.header.instrument_id = mapping.instrument_id;
    record.header.publisher_id = mapping.publisher_id;
    record.header.record_kind = static_cast<std::uint8_t>(kind);
    record.header.ts_event_ns = timestamp;
    record.header.ts_recv_ns = timestamp;
    record.header.sequence = static_cast<std::uint32_t>(sequence);
    record.header.source_schema = static_cast<std::uint16_t>(kind);

    const auto price = static_cast<std::int64_t>(100'000'000'000LL
                                                 + static_cast<std::int64_t>(sequence) * 1'000'000LL);
    if (kind == DBF_RECORD_QUOTE) {
        record.quote.bid_price = price - 500'000LL;
        record.quote.ask_price = price + 500'000LL;
        record.quote.bid_size = 10 + static_cast<std::uint32_t>(sequence % 100);
        record.quote.ask_size = 12 + static_cast<std::uint32_t>(sequence % 100);
        record.quote.bid_count = 1;
        record.quote.ask_count = 1;
    } else if (kind == DBF_RECORD_TRADE) {
        record.trade.price = price;
        record.trade.size = 1 + static_cast<std::uint32_t>(sequence % 50);
        record.trade.action = 'T';
        record.trade.side = (sequence & 1u) == 0 ? 'B' : 'A';
        record.trade.ts_out_ns = timestamp;
    } else if (kind == DBF_RECORD_MBO) {
        record.mbo.order_id = sequence + 1;
        record.mbo.price = price;
        record.mbo.size = 1 + static_cast<std::uint32_t>(sequence % 25);
        record.mbo.action = 'A';
        record.mbo.side = (sequence & 1u) == 0 ? 'B' : 'A';
    } else {
        // Statistics is normally the third record in a quote/trade/statistics
        // synthetic cycle. Divide by the cycle width so open/high/low rotate
        // instead of always selecting the same statistic type.
        const auto statistic_index = (sequence / 3u) % 3u;
        record.statistics.price = statistic_index == 1u
                                      ? price - 1'000'000'000LL
                                      : statistic_index == 2u
                                            ? price + 1'000'000'000LL
                                            : price;
        record.statistics.ts_ref_ns = timestamp;
        record.statistics.stat_type = statistic_index == 1u ? 4u
                                                : statistic_index == 2u ? 5u : 1u;
        record.statistics.update_action = 1u;
    }
    return record;
}

bool publish_record(dbf_feed* feed, const dbf_market_record64& record) noexcept {
    if (!apply_producer_forced_migration_if_required(feed)) {
        set_error(feed, DBF_AFFINITY_CONFIGURATION_FAILED,
                  "Native producer forced migration failed");
        feed->state.store(DBF_STATE_FAULTED, std::memory_order_release);
        notify_signal(feed);
        return false;
    }
    auto head = feed->head.value.load(std::memory_order_relaxed);
    auto tail = feed->tail.value.load(std::memory_order_acquire);
    if (head - tail == feed->ring_capacity) {
        feed->ring_full_episodes.fetch_add(1, std::memory_order_relaxed);
        const auto deadline = monotonic_clock::now()
                              + std::chrono::microseconds(feed->config.ring_full_timeout_us);
        std::uint32_t spins = 0;
        do {
            if (feed->stop_requested.load(std::memory_order_acquire)) {
                return false;
            }
            if (spins++ < feed->config.spin_iterations) {
                std::atomic_signal_fence(std::memory_order_seq_cst);
            } else {
                std::this_thread::yield();
            }
            tail = feed->tail.value.load(std::memory_order_acquire);
            if (head - tail < feed->ring_capacity) {
                break;
            }
        } while (monotonic_clock::now() < deadline);

        if (head - tail == feed->ring_capacity) {
            feed->ring_overruns.fetch_add(1, std::memory_order_relaxed);
            set_error(feed, DBF_RING_OVERRUN, "Synthetic producer exhausted the native ring deadline");
            feed->state.store(DBF_STATE_FAULTED, std::memory_order_release);
            notify_signal(feed);
            return false;
        }
    }

    const bool was_empty = head == tail;
    feed->ring[head & feed->ring_mask] = record;
    feed->head.value.store(head + 1, std::memory_order_release);
    const auto used = head + 1 - tail;
    update_high_water(feed, used);
    feed->records_produced.fetch_add(1, std::memory_order_relaxed);
    record_producer_processor_residency(feed);
    if (was_empty) {
        notify_signal(feed);
    }
    return true;
}

void apply_producer_thread_settings(dbf_feed* feed) noexcept {
#if defined(_WIN32)
    if (feed->config.producer_logical_processor != unpinned_processor) {
        GROUP_AFFINITY affinity{};
        affinity.Group = feed->config.producer_processor_group;
        affinity.Mask = 1ull << (feed->config.producer_logical_processor % 64u);
        if (SetThreadGroupAffinity(GetCurrentThread(), &affinity, nullptr) == FALSE) {
            set_error(feed, DBF_AFFINITY_CONFIGURATION_FAILED,
                      "Unable to apply native producer affinity");
            return;
        }
        GROUP_AFFINITY observed{};
        if (GetThreadGroupAffinity(GetCurrentThread(), &observed) == FALSE
            || observed.Group != affinity.Group
            || observed.Mask != affinity.Mask) {
            set_error(feed, DBF_AFFINITY_CONFIGURATION_FAILED,
                      "Native producer affinity verification failed");
            return;
        }
        feed->observed_producer_location.store(
            (static_cast<std::uint32_t>(observed.Group) << 16u)
                | feed->config.producer_logical_processor,
            std::memory_order_release);
        feed->producer_affinity_verified.store(1u, std::memory_order_release);
    }
    int priority = THREAD_PRIORITY_NORMAL;
    if (feed->config.producer_priority == 1) {
        priority = THREAD_PRIORITY_ABOVE_NORMAL;
    } else if (feed->config.producer_priority >= 2) {
        priority = THREAD_PRIORITY_HIGHEST;
    }
    if (SetThreadPriority(GetCurrentThread(), priority) == FALSE
        && (feed->config.flags & DBF_CONFIG_REQUIRE_PRIORITY) != 0) {
        set_error(feed, DBF_PRIORITY_CONFIGURATION_FAILED,
                  "Unable to apply native producer priority");
    }
#else
    if (feed->config.producer_logical_processor != unpinned_processor) {
        if (feed->config.producer_processor_group != 0) {
            set_error(feed, DBF_AFFINITY_CONFIGURATION_FAILED,
                      "Linux does not support Windows processor groups");
            return;
        }
        cpu_set_t cpu_set;
        CPU_ZERO(&cpu_set);
        CPU_SET(feed->config.producer_logical_processor, &cpu_set);
        if (pthread_setaffinity_np(pthread_self(), sizeof(cpu_set), &cpu_set) != 0) {
            set_error(feed, DBF_AFFINITY_CONFIGURATION_FAILED,
                      "Unable to apply Linux native producer affinity");
            return;
        }
        cpu_set_t observed;
        CPU_ZERO(&observed);
        if (pthread_getaffinity_np(pthread_self(), sizeof(observed), &observed) != 0
            || CPU_COUNT(&observed) != 1
            || CPU_ISSET(feed->config.producer_logical_processor, &observed) == 0) {
            set_error(feed, DBF_AFFINITY_CONFIGURATION_FAILED,
                      "Linux native producer affinity verification failed");
            return;
        }
        feed->observed_producer_location.store(
            feed->config.producer_logical_processor,
            std::memory_order_release);
        feed->producer_affinity_verified.store(1u, std::memory_order_release);
    }
    int nice_value = 0;
    if (feed->config.producer_priority == 1) {
        nice_value = -5;
    } else if (feed->config.producer_priority >= 2) {
        nice_value = -10;
    }
    if (nice_value != 0
        && setpriority(PRIO_PROCESS, static_cast<id_t>(syscall(SYS_gettid)), nice_value) != 0
        && (feed->config.flags & DBF_CONFIG_REQUIRE_PRIORITY) != 0) {
        set_error(feed, DBF_PRIORITY_CONFIGURATION_FAILED,
                  "Unable to apply Linux native producer nice value");
    }
#endif
}

void synthetic_producer_main(dbf_feed* feed) noexcept {
    {
        std::lock_guard lock(feed->control_mutex);
        if (feed->terminal_status.load(std::memory_order_acquire) != DBF_OK) {
            feed->state.store(DBF_STATE_FAULTED, std::memory_order_release);
            feed->producer_done.store(true, std::memory_order_release);
            feed->control_cv.notify_all();
            notify_signal(feed);
            return;
        }
        feed->state.store(DBF_STATE_CONSUMER_SETUP, std::memory_order_release);
    }
    feed->control_cv.notify_all();

    {
        std::unique_lock lock(feed->control_mutex);
        feed->control_cv.wait(lock, [feed] {
            const auto state = feed->state.load(std::memory_order_acquire);
            return state == DBF_STATE_RUNNING
                   || feed->stop_requested.load(std::memory_order_acquire);
        });
    }

    const auto record_count = feed->config.synthetic_record_count == 0
                                  ? 100'000u
                                  : feed->config.synthetic_record_count;
    const auto start_sequence = feed->config.synthetic_start_sequence == 0
                                    ? 1u
                                    : feed->config.synthetic_start_sequence;
    auto next_due = monotonic_clock::now();
    for (std::uint64_t index = 0;
         index < record_count && !feed->stop_requested.load(std::memory_order_acquire);
         ++index) {
        const auto sequence = start_sequence + index;
        const auto& mapping = feed->mappings[index % feed->mappings.size()];
        if (!publish_record(feed, make_synthetic_record(mapping, sequence))) {
            break;
        }
        if (feed->config.synthetic_records_per_second != 0) {
            next_due += std::chrono::nanoseconds(
                1'000'000'000ull / feed->config.synthetic_records_per_second);
            std::this_thread::sleep_until(next_due);
        }
    }

    if (feed->state.load(std::memory_order_acquire) != DBF_STATE_FAULTED) {
        feed->state.store(DBF_STATE_STOPPED, std::memory_order_release);
    }
    feed->producer_done.store(true, std::memory_order_release);
    feed->control_cv.notify_all();
    notify_signal(feed);
}

#if defined(DBF_ENABLE_LIVE)

std::uint32_t remaining_start_milliseconds(const dbf_feed* feed) noexcept {
    const auto now = monotonic_clock::now();
    if (now >= feed->start_deadline) {
        return 0;
    }
    const auto remaining = std::chrono::duration_cast<std::chrono::milliseconds>(
        feed->start_deadline - now);
    return static_cast<std::uint32_t>(std::min<std::int64_t>(
        std::max<std::int64_t>(remaining.count(), 1),
        std::numeric_limits<std::uint32_t>::max()));
}

void finish_producer(dbf_feed* feed) noexcept {
    if (feed->state.load(std::memory_order_acquire) != DBF_STATE_FAULTED) {
        feed->state.store(DBF_STATE_STOPPED, std::memory_order_release);
    }
    feed->producer_done.store(true, std::memory_order_release);
    feed->control_cv.notify_all();
    notify_signal(feed);
}

dbf_status classify_gateway_error(databento::ErrorCode code) noexcept {
    switch (code) {
    case databento::ErrorCode::ConnectionLimitExceeded:
        return DBF_CONNECTION_LIMIT;
    case databento::ErrorCode::SymbolResolutionFailed:
        return DBF_SYMBOL_RESOLUTION_FAILED;
    default:
        return DBF_DATABENTO_ERROR;
    }
}

bool fail_live(dbf_feed* feed, dbf_status status, const std::string& message) noexcept {
    if (feed->terminal_status.load(std::memory_order_acquire) == DBF_OK) {
        set_error(feed, status, message.c_str());
    }
    feed->state.store(DBF_STATE_FAULTED, std::memory_order_release);
    feed->control_cv.notify_all();
    notify_signal(feed);
    return false;
}

databento::SType to_stype(std::uint32_t value) {
    return value == 1u ? databento::SType::RawSymbol
                       : databento::SType::InstrumentId;
}

void subscribe_group(databento::LiveBlocking& client,
                     const std::vector<mapping_entry>& mappings,
                     std::uint32_t input_symbology,
                     std::uint32_t data_kind,
                     databento::Schema schema,
                     std::uint32_t& subscription_count,
                     std::uint64_t replay_start_ns = 0,
                     std::uint32_t excluded_data_kind = 0) {
    std::vector<std::string> symbols;
    symbols.reserve(mappings.size());
    for (const auto& mapping : mappings) {
        if (mapping.input_symbology == input_symbology
            && (mapping.data_kinds & data_kind) != 0
            && (mapping.data_kinds & excluded_data_kind) == 0) {
            symbols.push_back(mapping.requested_symbol);
        }
    }
    if (!symbols.empty()) {
        if (replay_start_ns == 0) {
            client.Subscribe(symbols, schema, to_stype(input_symbology));
        } else {
            client.Subscribe(
                symbols,
                schema,
                to_stype(input_symbology),
                databento::UnixNanos{databento::UnixNanos::duration{replay_start_ns}});
        }
        ++subscription_count;
    }
}

bool publish_statistics_replay_complete(dbf_feed* feed) noexcept {
    for (const auto& mapping : feed->mappings) {
        if ((mapping.data_kinds & DBF_MARKET_DATA_STATISTICS) == 0
            || mapping.instrument_id == 0 || mapping.publisher_id == 0) {
            continue;
        }
        dbf_market_record64 record{};
        record.header.instrument_id = mapping.instrument_id;
        record.header.publisher_id = mapping.publisher_id;
        record.header.record_kind = DBF_RECORD_STATISTICS_REPLAY_COMPLETE;
        record.header.source_schema =
            static_cast<std::uint16_t>(databento::Schema::Statistics);
        if (!publish_record(feed, record)) {
            return false;
        }
    }
    return true;
}

bool publish_trade_replay_complete(dbf_feed* feed) noexcept {
    for (const auto& mapping : feed->mappings) {
        if ((mapping.data_kinds & DBF_MARKET_DATA_SESSION_VOLUME) == 0
            || mapping.instrument_id == 0 || mapping.publisher_id == 0) {
            continue;
        }
        dbf_market_record64 record{};
        record.header.instrument_id = mapping.instrument_id;
        record.header.publisher_id = mapping.publisher_id;
        record.header.record_kind = DBF_RECORD_TRADE_REPLAY_COMPLETE;
        record.header.source_schema =
            static_cast<std::uint16_t>(databento::Schema::Trades);
        if (!publish_record(feed, record)) {
            return false;
        }
    }
    return true;
}

bool resolve_mapping(dbf_feed* feed,
                     const databento::SymbolMappingMsg& message,
                     bool allow_new) {
    const std::string requested{message.STypeInSymbol()};
    const auto instrument_id = message.hd.instrument_id;
    if (instrument_id == 0) {
        return fail_live(feed, DBF_SYMBOL_RESOLUTION_FAILED,
                         "Databento returned a symbol mapping without an instrument ID");
    }

    auto found = false;
    for (auto& mapping : feed->mappings) {
        if (mapping.requested_symbol != requested) {
            continue;
        }
        found = true;
        if (mapping.instrument_id != 0 && mapping.instrument_id != instrument_id) {
            return fail_live(feed, DBF_SYMBOL_RESOLUTION_FAILED,
                             "A resolved symbol remapped to a different instrument");
        }
        if (mapping.publisher_id != 0 && message.hd.publisher_id != 0
            && mapping.publisher_id != message.hd.publisher_id) {
            return fail_live(feed, DBF_SYMBOL_RESOLUTION_FAILED,
                             "A resolved symbol remapped to a different publisher");
        }
        mapping.instrument_id = instrument_id;
        if (message.hd.publisher_id != 0) {
            mapping.publisher_id = message.hd.publisher_id;
        }
        mapping.raw_symbol = mapping.input_symbology == 1u
                                 ? requested
                                 : mapping.requested_symbol;
        mapping.resolved = mapping.publisher_id != 0;
    }
    if (!found && !allow_new) {
        return fail_live(feed, DBF_SYMBOL_RESOLUTION_FAILED,
                         "Databento returned an unexpected ticker mapping");
    }
    return true;
}

bool resolve_mapping_publisher(dbf_feed* feed,
                               const databento::RecordHeader& header) {
    if (header.instrument_id == 0 || header.publisher_id == 0) {
        return true;
    }
    for (auto& mapping : feed->mappings) {
        if (mapping.instrument_id != header.instrument_id) {
            continue;
        }
        if (mapping.publisher_id != 0
            && mapping.publisher_id != header.publisher_id) {
            return fail_live(feed, DBF_SYMBOL_RESOLUTION_FAILED,
                             "A resolved instrument produced data from a different publisher");
        }
        mapping.publisher_id = header.publisher_id;
        mapping.resolved = true;
    }
    return true;
}

bool all_mappings_resolved(const dbf_feed* feed) noexcept {
    return std::all_of(feed->mappings.begin(), feed->mappings.end(),
                       [](const mapping_entry& mapping) { return mapping.resolved; });
}

bool process_live_record(dbf_feed* feed,
                         const databento::Record& source,
                         bool initial_mapping,
                         bool& statistics_replay_pending,
                         bool& trade_replay_pending) {
    feed->last_message_monotonic_ns.store(
        monotonic_nanoseconds(), std::memory_order_relaxed);
    if (const auto* error = source.GetIf<databento::ErrorMsg>()) {
        return fail_live(feed, classify_gateway_error(error->code), error->Err());
    }
    if (const auto* system = source.GetIf<databento::SystemMsg>()) {
        switch (system->code) {
        case databento::SystemCode::Heartbeat:
            feed->heartbeat_messages.fetch_add(1, std::memory_order_relaxed);
            break;
        case databento::SystemCode::SubscriptionAck:
            feed->subscription_acknowledgements.fetch_add(1, std::memory_order_relaxed);
            break;
        case databento::SystemCode::ReplayCompleted:
            switch (dbf_live::classify_replay_schema(system->Msg())) {
            case dbf_live::replay_schema::statistics:
                if (!statistics_replay_pending) {
                    break;
                }
                statistics_replay_pending = false;
                if (!publish_statistics_replay_complete(feed)) {
                    return false;
                }
                break;
            case dbf_live::replay_schema::trades:
                if (!trade_replay_pending) {
                    break;
                }
                trade_replay_pending = false;
                if (!publish_trade_replay_complete(feed)) {
                    return false;
                }
                break;
            case dbf_live::replay_schema::unknown:
                break;
            }
            break;
        case databento::SystemCode::SlowReaderWarning:
            feed->slow_reader_warnings.fetch_add(1, std::memory_order_relaxed);
            return fail_live(feed, DBF_DATABENTO_ERROR,
                             "Databento reported a slow-reader warning");
        default:
            break;
        }
        return true;
    }
    if (const auto* mapping = source.GetIf<databento::SymbolMappingMsg>()) {
        return resolve_mapping(feed, *mapping, initial_mapping);
    }
    if (initial_mapping && !resolve_mapping_publisher(feed, source.Header())) {
        return false;
    }
    const auto trade_replay = trade_replay_pending
        && std::any_of(feed->mappings.begin(), feed->mappings.end(),
                       [&source](const mapping_entry& mapping) {
                           return mapping.instrument_id == source.Header().instrument_id
                               && (mapping.data_kinds
                                   & DBF_MARKET_DATA_SESSION_VOLUME) != 0;
                       });
    dbf_market_record64 normalized{};
    return !dbf_live::normalize(
               source, normalized, statistics_replay_pending, trade_replay)
           || publish_record(feed, normalized);
}

void live_producer_main(dbf_feed* feed) noexcept {
    try {
        auto remaining = remaining_start_milliseconds(feed);
        if (remaining == 0) {
            fail_live(feed, DBF_TIMEOUT, "Databento start deadline expired before connect");
            finish_producer(feed);
            return;
        }
        const auto rounded_seconds = remaining / 1'000u
                                     + (remaining % 1'000u == 0 ? 0u : 1u);
        const auto timeout_seconds = std::chrono::seconds{
            std::max<std::uint32_t>(1, rounded_seconds)};
        auto client = databento::LiveBlocking::Builder()
                          .SetKeyFromEnv()
                          .SetDataset(feed->dataset)
                          .SetHeartbeatInterval(std::chrono::seconds{
                              feed->config.heartbeat_interval_ms / 1000u})
                          .SetSlowReaderBehavior(databento::SlowReaderBehavior::Warn)
                          .SetTimeoutConf({timeout_seconds, timeout_seconds})
                          .BuildBlocking();

        std::uint32_t expected_acknowledgements{};
        auto statistics_replay_pending =
            feed->config.statistics_replay_start_ns != 0
            && std::any_of(feed->mappings.begin(), feed->mappings.end(),
                           [](const mapping_entry& mapping) {
                               return (mapping.data_kinds
                                       & DBF_MARKET_DATA_STATISTICS) != 0;
                           });
        auto trade_replay_pending =
            feed->config.trade_replay_start_ns != 0
            && std::any_of(feed->mappings.begin(), feed->mappings.end(),
                           [](const mapping_entry& mapping) {
                               return (mapping.data_kinds
                                       & DBF_MARKET_DATA_SESSION_VOLUME) != 0;
                           });
        for (const auto input_symbology : {1u, 2u}) {
            subscribe_group(client, feed->mappings, input_symbology,
                            DBF_MARKET_DATA_QUOTE, databento::Schema::Mbp1,
                            expected_acknowledgements);
            subscribe_group(client, feed->mappings, input_symbology,
                            DBF_MARKET_DATA_TRADE, databento::Schema::Trades,
                            expected_acknowledgements, 0,
                            DBF_MARKET_DATA_SESSION_VOLUME);
            subscribe_group(client, feed->mappings, input_symbology,
                            DBF_MARKET_DATA_SESSION_VOLUME,
                            databento::Schema::Trades,
                            expected_acknowledgements,
                            feed->config.trade_replay_start_ns);
            subscribe_group(client, feed->mappings, input_symbology,
                            DBF_MARKET_DATA_MBO, databento::Schema::Mbo,
                            expected_acknowledgements);
            subscribe_group(client, feed->mappings, input_symbology,
                            DBF_MARKET_DATA_STATISTICS,
                            databento::Schema::Statistics,
                            expected_acknowledgements,
                            feed->config.statistics_replay_start_ns);
        }
        const auto metadata = client.Start();
        if (!metadata.not_found.empty() || !metadata.partial.empty()) {
            fail_live(feed, DBF_SYMBOL_RESOLUTION_FAILED,
                      "Databento could not resolve one or more ticker symbols");
            client.Stop();
            finish_producer(feed);
            return;
        }

        while (!feed->stop_requested.load(std::memory_order_acquire)
               && (!all_mappings_resolved(feed)
                   || feed->subscription_acknowledgements.load(
                          std::memory_order_relaxed) < expected_acknowledgements)) {
            remaining = remaining_start_milliseconds(feed);
            if (remaining == 0) {
                fail_live(feed, DBF_TIMEOUT,
                          "Databento ticker mappings or acknowledgements timed out");
                client.Stop();
                finish_producer(feed);
                return;
            }
            const auto* record = client.NextRecord(std::chrono::milliseconds{
                std::min<std::uint32_t>(remaining, 250u)});
            if (record != nullptr && !process_live_record(
                    feed, *record, true, statistics_replay_pending,
                    trade_replay_pending)) {
                client.Stop();
                finish_producer(feed);
                return;
            }
        }

        {
            std::lock_guard lock(feed->control_mutex);
            if (!feed->stop_requested.load(std::memory_order_acquire)) {
                feed->state.store(DBF_STATE_CONSUMER_SETUP, std::memory_order_release);
            }
        }
        feed->control_cv.notify_all();
        {
            std::unique_lock lock(feed->control_mutex);
            feed->control_cv.wait(lock, [feed] {
                return feed->state.load(std::memory_order_acquire) == DBF_STATE_RUNNING
                       || feed->stop_requested.load(std::memory_order_acquire);
            });
        }

        while (!feed->stop_requested.load(std::memory_order_acquire)
               && feed->state.load(std::memory_order_acquire) != DBF_STATE_FAULTED) {
            const auto* record = client.NextRecord(std::chrono::milliseconds{250});
            if (record != nullptr && !process_live_record(
                    feed, *record, false, statistics_replay_pending,
                    trade_replay_pending)) {
                break;
            }
        }
        client.Stop();
    } catch (const databento::HeartbeatTimeoutError& exception) {
        fail_live(feed, DBF_CONNECTION_HUNG, exception.what());
    } catch (const databento::Exception& exception) {
        fail_live(feed, DBF_DATABENTO_ERROR, exception.what());
    } catch (const std::bad_alloc&) {
        fail_live(feed, DBF_NO_MEMORY, "Unable to allocate Databento live-session state");
    } catch (const std::exception& exception) {
        fail_live(feed, DBF_DATABENTO_ERROR, exception.what());
    } catch (...) {
        fail_live(feed, DBF_INTERNAL_ERROR, "Unknown Databento live-session failure");
    }
    finish_producer(feed);
}

std::uint32_t remaining_milliseconds(
    monotonic_clock::time_point deadline) noexcept {
    const auto now = monotonic_clock::now();
    if (now >= deadline) {
        return 0;
    }
    const auto remaining = std::chrono::duration_cast<std::chrono::milliseconds>(
        deadline - now);
    return static_cast<std::uint32_t>(std::min<std::int64_t>(
        std::max<std::int64_t>(remaining.count(), 1),
        std::numeric_limits<std::uint32_t>::max()));
}

void set_latest_header(const databento::RecordHeader& header,
                       databento::UnixNanos receive_timestamp,
                       dbf_latest_price_result64& result) noexcept {
    result.instrument_id = header.instrument_id;
    result.publisher_id = header.publisher_id;
    result.ts_event_ns = static_cast<std::int64_t>(
        header.ts_event.time_since_epoch().count());
    result.ts_recv_ns = static_cast<std::int64_t>(
        receive_timestamp.time_since_epoch().count());
}

bool select_latest_trade(const databento::TradeMsg& trade,
                         dbf_latest_price_result64& result) noexcept {
    if (trade.price == databento::kUndefPrice || trade.size == 0) {
        return false;
    }
    set_latest_header(trade.hd, trade.ts_recv, result);
    result.flags = DBF_LATEST_PRICE_TRADE_VALID;
    result.selected_price = trade.price;
    result.last_trade_price = trade.price;
    return true;
}

template <typename TQuote>
bool select_latest_quote(const TQuote& quote,
                         std::uint32_t selected_policy,
                         dbf_latest_price_result64& result) noexcept {
    const auto& level = quote.levels[0];
    const bool bid_valid = level.bid_px != databento::kUndefPrice
                           && level.bid_sz != 0;
    const bool ask_valid = level.ask_px != databento::kUndefPrice
                           && level.ask_sz != 0;
    const bool midpoint_valid = bid_valid && ask_valid
                                && level.bid_px <= level.ask_px;
    const bool selected_valid =
        selected_policy == DBF_LATEST_PRICE_BID
            ? bid_valid
            : (selected_policy == DBF_LATEST_PRICE_ASK
                   ? ask_valid
                   : midpoint_valid);
    if (!selected_valid) {
        return false;
    }

    set_latest_header(quote.hd, quote.ts_recv, result);
    result.flags = static_cast<std::uint8_t>(
        (bid_valid ? DBF_LATEST_PRICE_BID_VALID : 0)
        | (ask_valid ? DBF_LATEST_PRICE_ASK_VALID : 0));
    result.bid_price = bid_valid ? level.bid_px : 0;
    result.ask_price = ask_valid ? level.ask_px : 0;
    result.bid_size = bid_valid ? level.bid_sz : 0;
    result.ask_size = ask_valid ? level.ask_sz : 0;
    if (selected_policy == DBF_LATEST_PRICE_BID) {
        result.selected_price = level.bid_px;
    } else if (selected_policy == DBF_LATEST_PRICE_ASK) {
        result.selected_price = level.ask_px;
    } else {
        result.selected_price = std::midpoint(level.bid_px, level.ask_px);
    }
    return true;
}

dbf_status get_latest_price_live(
    const dbf_latest_price_request_v1& request,
    const std::string& dataset,
    const std::string& symbol,
    std::uint32_t timeout_ms,
    dbf_latest_price_result64& result) {
    const auto deadline = monotonic_clock::now()
                          + std::chrono::milliseconds{timeout_ms};
    const auto rounded_seconds = timeout_ms / 1'000u
                                 + (timeout_ms % 1'000u == 0 ? 0u : 1u);
    const auto timeout_seconds = std::chrono::seconds{
        std::max<std::uint32_t>(1, rounded_seconds)};
    auto client = databento::LiveBlocking::Builder()
                      .SetKeyFromEnv()
                      .SetDataset(dataset)
                      .SetHeartbeatInterval(std::chrono::seconds{5})
                      .SetSlowReaderBehavior(databento::SlowReaderBehavior::Warn)
                      .SetTimeoutConf({timeout_seconds, timeout_seconds})
                      .BuildBlocking();
    dbf_latest::session_guard session{client};

    const auto schema = request.selected_policy == DBF_LATEST_PRICE_LAST_TRADE
                            ? databento::Schema::Trades
                            : databento::Schema::Bbo1S;
    const std::vector<std::string> symbols{symbol};
    if (request.freshness_policy == DBF_LATEST_PRICE_REPLAY_LOOKBACK_THEN_LIVE) {
        const auto now_ns = std::chrono::duration_cast<std::chrono::nanoseconds>(
            std::chrono::system_clock::now().time_since_epoch()).count();
        const auto lookback_ns = static_cast<std::int64_t>(
            request.replay_lookback_ms) * 1'000'000LL;
        const auto start_ns = static_cast<std::uint64_t>(
            std::max<std::int64_t>(0, now_ns - lookback_ns));
        client.Subscribe(
            symbols, schema, to_stype(request.input_symbology),
            databento::UnixNanos{
                std::chrono::duration<std::uint64_t, std::nano>{start_ns}});
    } else {
        client.Subscribe(symbols, schema, to_stype(request.input_symbology));
    }

    const auto metadata = client.Start();
    if (!metadata.not_found.empty() || !metadata.partial.empty()) {
        return DBF_SYMBOL_RESOLUTION_FAILED;
    }

    const bool replay_requested = request.freshness_policy
                                  == DBF_LATEST_PRICE_REPLAY_LOOKBACK_THEN_LIVE;
    bool replay_complete = !replay_requested;
    std::optional<dbf_latest_price_result64> replay_candidate;
    while (true) {
        const auto remaining = remaining_milliseconds(deadline);
        if (remaining == 0) {
            return DBF_TIMEOUT;
        }
        const auto* record = client.NextRecord(
            std::chrono::milliseconds{remaining});
        if (record == nullptr) {
            return DBF_TIMEOUT;
        }
        if (const auto* error = record->GetIf<databento::ErrorMsg>()) {
            return classify_gateway_error(error->code);
        }
        if (const auto* system = record->GetIf<databento::SystemMsg>()) {
            if (system->code == databento::SystemCode::SlowReaderWarning) {
                return DBF_DATABENTO_ERROR;
            }
            if (system->code == databento::SystemCode::ReplayCompleted) {
                replay_complete = true;
                if (replay_candidate.has_value()) {
                    result = *replay_candidate;
                    result.flags = static_cast<std::uint8_t>(
                        result.flags | DBF_LATEST_PRICE_REPLAY_CONTRIBUTED);
                    session.stop();
                    return DBF_OK;
                }
            }
            continue;
        }

        dbf_latest_price_result64 candidate{};
        candidate.selected_policy = static_cast<std::uint8_t>(
            request.selected_policy);
        bool selected{};
        if (request.selected_policy == DBF_LATEST_PRICE_LAST_TRADE) {
            if (const auto* trade = record->GetIf<databento::TradeMsg>()) {
                selected = select_latest_trade(*trade, candidate);
            }
        } else if (const auto* quote = record->GetIf<databento::BboMsg>()) {
            selected = select_latest_quote(
                *quote, request.selected_policy, candidate);
        }
        if (!selected) {
            continue;
        }
        if (!replay_complete) {
            replay_candidate = candidate;
            continue;
        }
        candidate.flags = static_cast<std::uint8_t>(
            candidate.flags | DBF_LATEST_PRICE_FINAL_RECORD_LIVE);
        result = candidate;
        session.stop();
        return DBF_OK;
    }
}

#endif

void producer_main(dbf_feed* feed) noexcept {
    apply_producer_thread_settings(feed);
    if (feed->terminal_status.load(std::memory_order_acquire) != DBF_OK) {
        feed->state.store(DBF_STATE_FAULTED, std::memory_order_release);
        feed->producer_done.store(true, std::memory_order_release);
        feed->control_cv.notify_all();
        notify_signal(feed);
        return;
    }
    if (feed->config.data_source == DBF_DATA_SOURCE_SYNTHETIC) {
        synthetic_producer_main(feed);
        return;
    }
#if defined(DBF_ENABLE_LIVE)
    live_producer_main(feed);
#else
    set_error(feed, DBF_NOT_SUPPORTED,
              "The native library was built without Databento live support");
    feed->state.store(DBF_STATE_FAULTED, std::memory_order_release);
    feed->producer_done.store(true, std::memory_order_release);
    feed->control_cv.notify_all();
    notify_signal(feed);
#endif
}

dbf_status initialize_ring(dbf_feed* feed) noexcept {
    if (feed->config.ring_memory_bytes % sizeof(dbf_market_record64) != 0) {
        return DBF_INVALID_ARGUMENT;
    }
    const auto requested_slots = feed->config.ring_memory_bytes / sizeof(dbf_market_record64);
    if (requested_slots < 2 || !is_power_of_two(requested_slots)
        || feed->config.ring_memory_bytes > std::numeric_limits<std::size_t>::max()) {
        return DBF_INVALID_ARGUMENT;
    }
    const auto capacity = requested_slots;
    feed->ring_capacity = capacity;
    feed->ring_mask = capacity - 1;
    feed->ring_bytes = static_cast<std::size_t>(capacity * sizeof(dbf_market_record64));
    dbf_status status{};
    feed->ring = static_cast<dbf_market_record64*>(allocate_pages(
        feed->ring_bytes,
        (feed->config.flags & DBF_CONFIG_REQUIRE_BASE_PAGE_POLICY) != 0,
        feed->config.numa_node,
        (feed->config.flags & DBF_CONFIG_REQUIRE_NUMA_LOCALITY) != 0,
        status));
    if (feed->ring == nullptr) {
        return status;
    }
    std::memset(feed->ring, 0, feed->ring_bytes);
    if ((feed->config.flags & DBF_CONFIG_LOCK_RING_MEMORY) != 0) {
        feed->ring_locked = lock_pages(feed->ring, feed->ring_bytes);
        if (!feed->ring_locked
            && (feed->config.flags & DBF_CONFIG_REQUIRE_LOCKED_MEMORY) != 0) {
            release_pages(feed->ring, feed->ring_bytes);
            feed->ring = nullptr;
            return DBF_MEMORY_LOCK_FAILED;
        }
    }
    return DBF_OK;
}

void close_signal(dbf_feed* feed) noexcept {
#if defined(_WIN32)
    if (feed->signal != nullptr) {
        CloseHandle(feed->signal);
        feed->signal = nullptr;
    }
#else
    if (feed->signal >= 0) {
        close(feed->signal);
        feed->signal = -1;
    }
#endif
}

dbf_status initialize_signal(dbf_feed* feed) noexcept {
#if defined(_WIN32)
    feed->signal = CreateEventW(nullptr, FALSE, FALSE, nullptr);
    return feed->signal == nullptr ? DBF_OS_ERROR : DBF_OK;
#else
    feed->signal = eventfd(0, EFD_NONBLOCK | EFD_CLOEXEC);
    return feed->signal < 0 ? DBF_OS_ERROR : DBF_OK;
#endif
}

void release_feed_memory(dbf_feed* feed) noexcept {
    if (feed->read_buffer != nullptr) {
        release_pages(feed->read_buffer, feed->read_buffer_bytes);
        feed->read_buffer = nullptr;
        feed->read_buffer_capacity = 0;
        feed->read_buffer_bytes = 0;
    }
    if (feed->ring != nullptr) {
        if (feed->ring_locked) {
            unlock_pages(feed->ring, feed->ring_bytes);
        }
        release_pages(feed->ring, feed->ring_bytes);
        feed->ring = nullptr;
    }
    close_signal(feed);
}

dbf_status validate_feed_and_result(dbf_feed* feed,
                                    std::uint32_t struct_size,
                                    std::uint32_t expected_size,
                                    std::uint32_t abi_version) noexcept {
    if (feed == nullptr) {
        return DBF_INVALID_ARGUMENT;
    }
    return valid_struct(struct_size, expected_size, abi_version)
               ? DBF_OK
               : DBF_ABI_MISMATCH;
}

} // namespace

extern "C" {

std::uint32_t DBF_CALL dbf_get_abi_version(void) {
    return DBF_ABI_VERSION;
}

dbf_status DBF_CALL dbf_feed_create(const dbf_feed_config_v1* config,
                                    const std::uint8_t* utf8_blob,
                                    std::uint32_t utf8_blob_bytes,
                                    dbf_feed_t** result) {
    if (result == nullptr) {
        return DBF_INVALID_ARGUMENT;
    }
    *result = nullptr;
    if (config == nullptr
        || !valid_struct(config->struct_size, sizeof(dbf_feed_config_v1), config->abi_version)) {
        return DBF_ABI_MISMATCH;
    }
    if (config->reserved16 != 0 || config->reserved32 != 0
        || std::any_of(std::begin(config->reserved), std::end(config->reserved),
                       [](std::uint64_t value) { return value != 0; })
        || config->flags > (DBF_CONFIG_LOCK_RING_MEMORY
                            | DBF_CONFIG_REQUIRE_LOCKED_MEMORY
                             | DBF_CONFIG_REQUIRE_BASE_PAGE_POLICY
                             | DBF_CONFIG_REQUIRE_PRIORITY
                             | DBF_CONFIG_REQUIRE_NUMA_LOCALITY
                             | DBF_CONFIG_TRACK_PROCESSOR_RESIDENCY)
        || config->producer_priority < 0 || config->producer_priority > 2
        || config->drain_priority < 0 || config->drain_priority > 2
        || config->ring_full_timeout_us == 0) {
        return DBF_INVALID_ARGUMENT;
    }
    if (config->forced_migration_interval_records != 0
        && ((config->flags & DBF_CONFIG_TRACK_PROCESSOR_RESIDENCY) == 0
            || config->data_source != DBF_DATA_SOURCE_SYNTHETIC
            || config->producer_logical_processor == unpinned_processor
            || config->producer_alternate_logical_processor == unpinned_processor
            || config->drain_logical_processor == unpinned_processor
            || config->drain_alternate_logical_processor == unpinned_processor)) {
        return DBF_INVALID_ARGUMENT;
    }
    if (config->data_source != DBF_DATA_SOURCE_SYNTHETIC
        && config->data_source != DBF_DATA_SOURCE_DATABENTO_LIVE) {
        return DBF_INVALID_ARGUMENT;
    }
#if !defined(DBF_ENABLE_LIVE)
    if (config->data_source == DBF_DATA_SOURCE_DATABENTO_LIVE) {
        return DBF_NOT_SUPPORTED;
    }
#endif
    if (config->data_source == DBF_DATA_SOURCE_DATABENTO_LIVE
        && (config->heartbeat_interval_ms < 5'000u
            || config->heartbeat_interval_ms % 1'000u != 0)) {
        return DBF_INVALID_ARGUMENT;
    }
    if (config->feed_kind != DBF_FEED_TICKER
        && config->feed_kind != DBF_FEED_OPTION_CHAIN) {
        return DBF_INVALID_ARGUMENT;
    }
    if (!valid_blob_range(config->dataset_offset, config->dataset_length, utf8_blob_bytes)
        || (config->dataset_length != 0 && utf8_blob == nullptr)) {
        return DBF_INVALID_ARGUMENT;
    }

    try {
        auto* feed = new dbf_feed{};
        feed->config = *config;
        if (config->dataset_length != 0) {
            feed->dataset.assign(
                reinterpret_cast<const char*>(utf8_blob + config->dataset_offset),
                config->dataset_length);
        }
        auto status = initialize_ring(feed);
        if (status == DBF_OK) {
            status = initialize_signal(feed);
        }
        if (status != DBF_OK) {
            release_feed_memory(feed);
            delete feed;
            return status;
        }
        *result = feed;
        return DBF_OK;
    } catch (const std::bad_alloc&) {
        return DBF_NO_MEMORY;
    } catch (...) {
        return DBF_INTERNAL_ERROR;
    }
}

dbf_status DBF_CALL dbf_feed_subscribe_tickers(
    dbf_feed_t* feed,
    const dbf_ticker_subscription_v1* subscriptions,
    std::uint32_t subscription_count,
    const std::uint8_t* utf8_blob,
    std::uint32_t utf8_blob_bytes,
    std::uint32_t timeout_ms) {
    (void)timeout_ms;
    if (feed == nullptr || subscriptions == nullptr || subscription_count == 0) {
        return DBF_INVALID_ARGUMENT;
    }
    if (feed->config.feed_kind != DBF_FEED_TICKER
        || feed->state.load(std::memory_order_acquire) != DBF_STATE_CREATED) {
        return DBF_INVALID_STATE;
    }
    try {
        std::vector<mapping_entry> mappings;
        mappings.reserve(subscription_count);
        for (std::uint32_t index = 0; index < subscription_count; ++index) {
            const auto& item = subscriptions[index];
            if (!valid_struct(item.struct_size, sizeof(item), item.abi_version)
                || item.reserved != 0) {
                return DBF_ABI_MISMATCH;
            }
            if (!valid_blob_range(item.symbol_offset, item.symbol_length, utf8_blob_bytes)
                || item.symbol_length == 0 || item.symbol_length > 0xffffu
                || utf8_blob == nullptr
                || (item.input_symbology != 1u && item.input_symbology != 2u)
                || (item.data_kinds & 15u) == 0 || (item.data_kinds & ~31u) != 0
                || ((item.data_kinds & DBF_MARKET_DATA_SESSION_VOLUME) != 0
                    && (item.data_kinds & DBF_MARKET_DATA_TRADE) == 0)) {
                return DBF_INVALID_ARGUMENT;
            }
            mapping_entry mapping{};
            mapping.subscription_index = index;
            mapping.input_symbology = item.input_symbology;
            mapping.data_kinds = item.data_kinds & 31u;
            mapping.requested_symbol.assign(
                reinterpret_cast<const char*>(utf8_blob + item.symbol_offset),
                item.symbol_length);
            mapping.raw_symbol = mapping.requested_symbol;
            if (feed->config.data_source == DBF_DATA_SOURCE_SYNTHETIC) {
                mapping.instrument_id = index + 1;
                mapping.publisher_id = 1;
                mapping.resolved = true;
            }
            mappings.push_back(std::move(mapping));
        }
        feed->mappings = std::move(mappings);
        feed->state.store(DBF_STATE_SUBSCRIBED, std::memory_order_release);
        return DBF_OK;
    } catch (const std::bad_alloc&) {
        return DBF_NO_MEMORY;
    } catch (...) {
        return DBF_INTERNAL_ERROR;
    }
}

dbf_status DBF_CALL dbf_feed_subscribe_option_chain(
    dbf_feed_t* feed,
    const dbf_option_chain_subscription_v1* subscription,
    const dbf_option_contract_selection_v1* contracts,
    std::uint32_t contract_count,
    const std::uint8_t* utf8_blob,
    std::uint32_t utf8_blob_bytes,
    std::uint32_t timeout_ms) {
    (void)timeout_ms;
    if (feed == nullptr || subscription == nullptr || contracts == nullptr
        || contract_count == 0) {
        return DBF_INVALID_ARGUMENT;
    }
    if (feed->config.feed_kind != DBF_FEED_OPTION_CHAIN
        || feed->state.load(std::memory_order_acquire) != DBF_STATE_CREATED) {
        return DBF_INVALID_STATE;
    }
    if (!valid_struct(subscription->struct_size, sizeof(*subscription), subscription->abi_version)
        || subscription->contract_count != contract_count
        || (subscription->data_kinds & 7u) == 0
        || (subscription->data_kinds & ~7u) != 0
        || subscription->reserved[0] != 0 || subscription->reserved[1] != 0) {
        return DBF_INVALID_ARGUMENT;
    }
    try {
        std::vector<mapping_entry> mappings;
        mappings.reserve(contract_count);
        for (std::uint32_t index = 0; index < contract_count; ++index) {
            const auto& contract = contracts[index];
            if (!valid_struct(contract.struct_size, sizeof(contract), contract.abi_version)
                || contract.reserved != 0
                || (contract.option_right != 1u && contract.option_right != 2u)
                || contract.reserved8 != 0
                || contract.instrument_id == 0
                || contract.publisher_id == 0
                || !valid_blob_range(contract.raw_symbol_offset,
                                     contract.raw_symbol_length,
                                     utf8_blob_bytes)
                || contract.raw_symbol_length == 0
                || contract.raw_symbol_length > 0xffffu
                || utf8_blob == nullptr) {
                return DBF_INVALID_ARGUMENT;
            }
            mapping_entry mapping{};
            mapping.subscription_index = index;
            mapping.instrument_id = contract.instrument_id;
            mapping.publisher_id = contract.publisher_id;
            mapping.input_symbology = 1u;
            mapping.data_kinds = subscription->data_kinds & 7u;
            mapping.raw_symbol.assign(
                reinterpret_cast<const char*>(utf8_blob + contract.raw_symbol_offset),
                contract.raw_symbol_length);
            mapping.requested_symbol = mapping.raw_symbol;
            mapping.resolved = feed->config.data_source == DBF_DATA_SOURCE_SYNTHETIC;
            mappings.push_back(std::move(mapping));
        }
        feed->mappings = std::move(mappings);
        feed->state.store(DBF_STATE_SUBSCRIBED, std::memory_order_release);
        return DBF_OK;
    } catch (const std::bad_alloc&) {
        return DBF_NO_MEMORY;
    } catch (...) {
        return DBF_INTERNAL_ERROR;
    }
}

dbf_status DBF_CALL dbf_feed_allocate_read_buffer64(dbf_feed_t* feed,
                                                     std::uint32_t record_capacity,
                                                     dbf_market_record64** buffer) {
    if (feed == nullptr || buffer == nullptr || record_capacity == 0) {
        return DBF_INVALID_ARGUMENT;
    }
    *buffer = nullptr;
    if (feed->read_buffer != nullptr || feed->producer.joinable()) {
        return DBF_INVALID_STATE;
    }
    const auto bytes = static_cast<std::size_t>(record_capacity)
                       * sizeof(dbf_market_record64);
    dbf_status status{};
    auto* memory = static_cast<dbf_market_record64*>(allocate_pages(
        bytes,
        (feed->config.flags & DBF_CONFIG_REQUIRE_BASE_PAGE_POLICY) != 0,
        feed->config.numa_node,
        (feed->config.flags & DBF_CONFIG_REQUIRE_NUMA_LOCALITY) != 0,
        status));
    if (memory == nullptr) {
        return status;
    }
    std::memset(memory, 0, bytes);
    feed->read_buffer = memory;
    feed->read_buffer_capacity = record_capacity;
    feed->read_buffer_bytes = bytes;
    *buffer = memory;
    return DBF_OK;
}

dbf_status DBF_CALL dbf_feed_start(dbf_feed_t* feed, std::uint32_t timeout_ms) {
    if (feed == nullptr || feed->read_buffer == nullptr) {
        return DBF_INVALID_ARGUMENT;
    }
    if (feed->state.load(std::memory_order_acquire) != DBF_STATE_SUBSCRIBED
        || feed->mappings.empty()) {
        return DBF_INVALID_STATE;
    }
    if (feed->config.data_source == DBF_DATA_SOURCE_DATABENTO_LIVE
        && timeout_ms == DBF_WAIT_INFINITE) {
        return DBF_INVALID_ARGUMENT;
    }
    try {
        feed->start_deadline = timeout_ms == DBF_WAIT_INFINITE
                                   ? monotonic_clock::time_point::max()
                                   : monotonic_clock::now()
                                         + std::chrono::milliseconds(timeout_ms);
        feed->state.store(DBF_STATE_STARTING, std::memory_order_release);
        feed->producer = std::thread(producer_main, feed);
        std::unique_lock lock(feed->control_mutex);
        const auto predicate = [feed] {
            const auto state = feed->state.load(std::memory_order_acquire);
            return state == DBF_STATE_CONSUMER_SETUP || state == DBF_STATE_FAULTED;
        };
        const bool ready = timeout_ms == DBF_WAIT_INFINITE
                               ? (feed->control_cv.wait(lock, predicate), true)
                               : feed->control_cv.wait_for(
                                     lock, std::chrono::milliseconds(timeout_ms), predicate);
        if (!ready) {
            return DBF_TIMEOUT;
        }
        return feed->state.load(std::memory_order_acquire) == DBF_STATE_FAULTED
                   ? static_cast<dbf_status>(feed->terminal_status.load(std::memory_order_acquire))
                   : DBF_OK;
    } catch (const std::system_error&) {
        set_error(feed, DBF_OS_ERROR, "Unable to start synthetic producer thread");
        feed->state.store(DBF_STATE_FAULTED, std::memory_order_release);
        return DBF_OS_ERROR;
    } catch (...) {
        return DBF_INTERNAL_ERROR;
    }
}

dbf_status DBF_CALL dbf_feed_get_ticker_mapping_counts(dbf_feed_t* feed,
                                                        std::uint32_t* mapping_count,
                                                        std::uint32_t* utf8_blob_bytes) {
    if (feed == nullptr || mapping_count == nullptr || utf8_blob_bytes == nullptr) {
        return DBF_INVALID_ARGUMENT;
    }
    if (feed->state.load(std::memory_order_acquire) != DBF_STATE_CONSUMER_SETUP) {
        return DBF_INVALID_STATE;
    }
    std::uint64_t bytes = 0;
    for (const auto& mapping : feed->mappings) {
        bytes += mapping.requested_symbol.size() + mapping.raw_symbol.size();
    }
    if (bytes > std::numeric_limits<std::uint32_t>::max()) {
        return DBF_BUFFER_TOO_SMALL;
    }
    *mapping_count = static_cast<std::uint32_t>(feed->mappings.size());
    *utf8_blob_bytes = static_cast<std::uint32_t>(bytes);
    return DBF_OK;
}

dbf_status DBF_CALL dbf_feed_copy_ticker_mappings(
    dbf_feed_t* feed,
    dbf_ticker_instrument_mapping_v1* mappings,
    std::uint32_t mapping_capacity,
    std::uint8_t* utf8_blob,
    std::uint32_t utf8_blob_capacity) {
    if (feed == nullptr || mappings == nullptr || utf8_blob == nullptr) {
        return DBF_INVALID_ARGUMENT;
    }
    if (feed->state.load(std::memory_order_acquire) != DBF_STATE_CONSUMER_SETUP) {
        return DBF_INVALID_STATE;
    }
    std::uint32_t required_count{};
    std::uint32_t required_bytes{};
    const auto count_status = dbf_feed_get_ticker_mapping_counts(
        feed, &required_count, &required_bytes);
    if (count_status != DBF_OK) {
        return count_status;
    }
    if (mapping_capacity < required_count || utf8_blob_capacity < required_bytes) {
        return DBF_BUFFER_TOO_SMALL;
    }
    std::uint32_t offset = 0;
    for (std::uint32_t index = 0; index < required_count; ++index) {
        const auto& source = feed->mappings[index];
        auto& destination = mappings[index];
        destination = {};
        destination.struct_size = sizeof(destination);
        destination.abi_version = DBF_ABI_VERSION;
        destination.subscription_index = source.subscription_index;
        destination.instrument_id = source.instrument_id;
        destination.publisher_id = source.publisher_id;
        destination.requested_symbol_offset = offset;
        destination.requested_symbol_length = static_cast<std::uint16_t>(
            source.requested_symbol.size());
        std::memcpy(utf8_blob + offset,
                    source.requested_symbol.data(), source.requested_symbol.size());
        offset += static_cast<std::uint32_t>(source.requested_symbol.size());
        destination.raw_symbol_offset = offset;
        destination.raw_symbol_length = static_cast<std::uint16_t>(
            source.raw_symbol.size());
        std::memcpy(utf8_blob + offset, source.raw_symbol.data(), source.raw_symbol.size());
        offset += static_cast<std::uint32_t>(source.raw_symbol.size());
    }
    return DBF_OK;
}

dbf_status DBF_CALL dbf_feed_set_consumer_ready(dbf_feed_t* feed,
                                                std::uint32_t timeout_ms) {
    (void)timeout_ms;
    if (feed == nullptr) {
        return DBF_INVALID_ARGUMENT;
    }
    {
        std::lock_guard lock(feed->control_mutex);
        if (feed->state.load(std::memory_order_acquire) != DBF_STATE_CONSUMER_SETUP) {
            return DBF_INVALID_STATE;
        }
        feed->state.store(DBF_STATE_RUNNING, std::memory_order_release);
    }
    feed->control_cv.notify_all();
    return DBF_OK;
}

dbf_status DBF_CALL dbf_feed_wait(dbf_feed_t* feed,
                                  std::uint32_t timeout_ms,
                                  dbf_wait_result_v1* result) {
    const auto validation = result == nullptr
                                ? DBF_INVALID_ARGUMENT
                                : validate_feed_and_result(
                                      feed, result->struct_size, sizeof(*result), result->abi_version);
    if (validation != DBF_OK) {
        return validation;
    }
    auto available = feed->head.value.load(std::memory_order_acquire)
                     - feed->tail.value.load(std::memory_order_acquire);
    auto state = feed->state.load(std::memory_order_acquire);
    if (available == 0 && state != DBF_STATE_STOPPED && state != DBF_STATE_FAULTED) {
        const auto status = wait_signal(feed, timeout_ms);
        if (status != DBF_OK) {
            return status;
        }
        available = feed->head.value.load(std::memory_order_acquire)
                    - feed->tail.value.load(std::memory_order_acquire);
        state = feed->state.load(std::memory_order_acquire);
    }
    result->flags = 0;
    if (available != 0) {
        result->flags |= DBF_WAIT_DATA;
    }
    if (state == DBF_STATE_STOPPED || state == DBF_STATE_FAULTED) {
        result->flags |= DBF_WAIT_TERMINAL;
    }
    if (state == DBF_STATE_FAULTED) {
        result->flags |= DBF_WAIT_FAULT;
    }
    result->state = state;
    result->available_records = available;
    result->terminal_status = feed->terminal_status.load(std::memory_order_acquire);
    result->reserved = 0;
    return DBF_OK;
}

dbf_status DBF_CALL dbf_feed_read_batch64(dbf_feed_t* feed,
                                          dbf_market_record64* destination,
                                          std::uint32_t destination_record_capacity,
                                          dbf_batch_result_v1* result) {
    const auto validation = result == nullptr
                                ? DBF_INVALID_ARGUMENT
                                : validate_feed_and_result(
                                      feed, result->struct_size, sizeof(*result), result->abi_version);
    if (validation != DBF_OK || destination == nullptr || destination_record_capacity == 0) {
        return validation == DBF_OK ? DBF_INVALID_ARGUMENT : validation;
    }
    if (destination != feed->read_buffer
        || destination_record_capacity > feed->read_buffer_capacity) {
        return DBF_INVALID_ARGUMENT;
    }
    auto tail = feed->tail.value.load(std::memory_order_relaxed);
    const auto head = feed->head.value.load(std::memory_order_acquire);
    const auto available = head - tail;
    const auto count = static_cast<std::uint32_t>(
        std::min<std::uint64_t>(available, destination_record_capacity));
    result->records_read = count;
    result->more_available = available > count ? 1u : 0u;
    result->first_sequence = count == 0 ? 0 : feed->ring[tail & feed->ring_mask].header.sequence;
    for (std::uint32_t index = 0; index < count; ++index) {
        destination[index] = feed->ring[(tail + index) & feed->ring_mask];
    }
    result->last_sequence = count == 0 ? 0 : destination[count - 1].header.sequence;
    if (count != 0) {
        tail += count;
        feed->tail.value.store(tail, std::memory_order_release);
        feed->records_consumed.fetch_add(count, std::memory_order_relaxed);
    }
    return DBF_OK;
}

dbf_status DBF_CALL dbf_feed_stop(dbf_feed_t* feed, std::uint32_t timeout_ms) {
    if (feed == nullptr) {
        return DBF_INVALID_ARGUMENT;
    }
    feed->stop_requested.store(true, std::memory_order_release);
    const auto prior_state = feed->state.load(std::memory_order_acquire);
    if (prior_state != DBF_STATE_STOPPED && prior_state != DBF_STATE_FAULTED) {
        feed->state.store(DBF_STATE_STOPPING, std::memory_order_release);
    }
    feed->control_cv.notify_all();
    notify_signal(feed);
    std::lock_guard join_lock(feed->join_mutex);
    if (feed->producer.joinable()) {
        std::unique_lock lock(feed->control_mutex);
        const auto predicate = [feed] {
            return feed->producer_done.load(std::memory_order_acquire);
        };
        const bool stopped = timeout_ms == DBF_WAIT_INFINITE
                                 ? (feed->control_cv.wait(lock, predicate), true)
                                 : feed->control_cv.wait_for(
                                       lock, std::chrono::milliseconds(timeout_ms), predicate);
        if (!stopped) {
            return DBF_TIMEOUT;
        }
        lock.unlock();
        feed->producer.join();
    }
    if (feed->state.load(std::memory_order_acquire) != DBF_STATE_FAULTED) {
        feed->state.store(DBF_STATE_STOPPED, std::memory_order_release);
    }
    return DBF_OK;
}

dbf_status DBF_CALL dbf_feed_free_read_buffer64(dbf_feed_t* feed,
                                                dbf_market_record64* buffer) {
    if (feed == nullptr || buffer == nullptr || buffer != feed->read_buffer) {
        return DBF_INVALID_ARGUMENT;
    }
    {
        std::lock_guard join_lock(feed->join_mutex);
        if (feed->producer.joinable()) {
            if (!feed->producer_done.load(std::memory_order_acquire)) {
                return DBF_INVALID_STATE;
            }
            feed->producer.join();
        }
    }
    release_pages(feed->read_buffer, feed->read_buffer_bytes);
    feed->read_buffer = nullptr;
    feed->read_buffer_capacity = 0;
    feed->read_buffer_bytes = 0;
    return DBF_OK;
}

dbf_status DBF_CALL dbf_feed_get_stats(dbf_feed_t* feed, dbf_stats_v1* stats) {
    const auto validation = stats == nullptr
                                ? DBF_INVALID_ARGUMENT
                                : validate_feed_and_result(
                                      feed, stats->struct_size, sizeof(*stats), stats->abi_version);
    if (validation != DBF_OK) {
        return validation;
    }
    stats->state = feed->state.load(std::memory_order_acquire);
    stats->terminal_status = feed->terminal_status.load(std::memory_order_acquire);
    stats->ring_capacity_records = feed->ring_capacity;
    stats->ring_used_records = feed->head.value.load(std::memory_order_acquire)
                               - feed->tail.value.load(std::memory_order_acquire);
    stats->ring_high_water_records = feed->ring_high_water.load(std::memory_order_relaxed);
    stats->records_produced = feed->records_produced.load(std::memory_order_relaxed);
    stats->records_consumed = feed->records_consumed.load(std::memory_order_relaxed);
    stats->signal_count = feed->signal_count.load(std::memory_order_relaxed);
    stats->wait_count = feed->wait_count.load(std::memory_order_relaxed);
    stats->ring_full_episodes = feed->ring_full_episodes.load(std::memory_order_relaxed);
    stats->ring_overruns = feed->ring_overruns.load(std::memory_order_relaxed);
    stats->allocated_read_buffer_records = feed->read_buffer_capacity;
    const auto producer_location = feed->observed_producer_location.load(
        std::memory_order_acquire);
    stats->observed_producer_processor_group = static_cast<std::uint16_t>(
        producer_location >> 16u);
    stats->observed_producer_logical_processor = static_cast<std::uint16_t>(
        producer_location & 0xffffu);
    stats->producer_affinity_verified = feed->producer_affinity_verified.load(
        std::memory_order_acquire);
    stats->producer_processor_sample_count =
        feed->producer_processor_sample_count.load(std::memory_order_relaxed);
    stats->producer_processor_migration_count =
        feed->producer_processor_migration_count.load(std::memory_order_relaxed);
    stats->producer_off_assignment_count =
        feed->producer_off_assignment_count.load(std::memory_order_relaxed);
    stats->producer_unique_processor_count =
        feed->producer_unique_processor_count.load(std::memory_order_relaxed);
    return DBF_OK;
}

dbf_status DBF_CALL dbf_feed_get_last_error(dbf_feed_t* feed,
                                            std::uint8_t* utf8_buffer,
                                            std::uint32_t utf8_buffer_capacity,
                                            std::uint32_t* required_bytes) {
    if (feed == nullptr || required_bytes == nullptr) {
        return DBF_INVALID_ARGUMENT;
    }
    std::lock_guard lock(feed->error_mutex);
    const auto bytes = feed->last_error.size() + 1;
    if (bytes > std::numeric_limits<std::uint32_t>::max()) {
        return DBF_BUFFER_TOO_SMALL;
    }
    *required_bytes = static_cast<std::uint32_t>(bytes);
    if (utf8_buffer == nullptr || utf8_buffer_capacity < bytes) {
        return DBF_BUFFER_TOO_SMALL;
    }
    std::memcpy(utf8_buffer, feed->last_error.c_str(), bytes);
    return DBF_OK;
}

dbf_status DBF_CALL dbf_feed_destroy(dbf_feed_t* feed) {
    if (feed == nullptr) {
        return DBF_INVALID_ARGUMENT;
    }
    {
        std::lock_guard join_lock(feed->join_mutex);
        if (feed->producer.joinable()) {
            if (!feed->producer_done.load(std::memory_order_acquire)) {
                return DBF_INVALID_STATE;
            }
            feed->producer.join();
        }
    }
    release_feed_memory(feed);
    delete feed;
    return DBF_OK;
}

dbf_status DBF_CALL dbf_contract_details_query(
    const dbf_contract_query_v1* query,
    const dbf_utf8_slice_v1* symbols,
    const std::uint8_t* utf8_blob,
    std::uint32_t utf8_blob_bytes,
    dbf_contract_details_result_t** output) {
    if (output == nullptr) {
        return DBF_INVALID_ARGUMENT;
    }
    *output = nullptr;
    if (query == nullptr
        || !valid_struct(query->struct_size, sizeof(*query), query->abi_version)) {
        return DBF_ABI_MISMATCH;
    }
    if (query->reserved32 != 0
        || std::any_of(std::begin(query->reserved), std::end(query->reserved),
                       [](std::uint64_t value) { return value != 0; })
        || (query->query_kind != DBF_CONTRACT_QUERY_EXACT
            && query->query_kind != DBF_CONTRACT_QUERY_TICKER
            && query->query_kind != DBF_CONTRACT_QUERY_INSTRUMENT_ID)
        || query->timeout_ms == 0 || query->timeout_ms == DBF_WAIT_INFINITE
        || query->symbol_count == 0 || symbols == nullptr || utf8_blob == nullptr
        || !valid_blob_range(query->dataset_offset, query->dataset_length, utf8_blob_bytes)
        || query->dataset_length == 0
        || ((query->query_kind == DBF_CONTRACT_QUERY_TICKER
             || query->query_kind == DBF_CONTRACT_QUERY_INSTRUMENT_ID)
            && query->symbol_count != 1)) {
        return DBF_INVALID_ARGUMENT;
    }
    try {
        auto* result = new dbf_contract_details_result{};
        *output = result;
        std::string dataset{
            reinterpret_cast<const char*>(utf8_blob + query->dataset_offset),
            query->dataset_length};
        std::vector<std::string> requested;
        requested.reserve(query->symbol_count);
        for (std::uint32_t index = 0; index < query->symbol_count; ++index) {
            const auto& symbol = symbols[index];
            if (symbol.length == 0
                || !valid_blob_range(symbol.offset, symbol.length, utf8_blob_bytes)) {
                result->error = "A contract symbol was empty or outside the UTF-8 input buffer";
                return DBF_INVALID_ARGUMENT;
            }
            requested.emplace_back(
                reinterpret_cast<const char*>(utf8_blob + symbol.offset),
                symbol.length);
        }
#if defined(DBF_ENABLE_LIVE)
        if (query->query_kind == DBF_CONTRACT_QUERY_EXACT) {
            auto fetched = fetch_definitions(
                dataset, requested, databento::SType::RawSymbol, query->timeout_ms);
            std::unordered_map<std::string, contract_result_entry> by_symbol;
            by_symbol.reserve(fetched.size());
            for (auto& entry : fetched) {
                by_symbol.insert_or_assign(entry.raw_symbol, std::move(entry));
            }
            result->entries.reserve(requested.size());
            for (const auto& requested_symbol : requested) {
                const auto found = by_symbol.find(requested_symbol);
                if (found == by_symbol.end()) {
                    contract_result_entry missing{};
                    missing.detail.struct_size = sizeof(missing.detail);
                    missing.detail.abi_version = DBF_ABI_VERSION;
                    result->entries.push_back(std::move(missing));
                } else {
                    result->entries.push_back(found->second);
                }
            }
        } else if (query->query_kind == DBF_CONTRACT_QUERY_TICKER) {
            const auto& ticker = requested.front();
            const auto is_parent = ticker.ends_with(".FUT")
                                   || ticker.ends_with(".OPT");
            result->entries = fetch_definitions(
                dataset,
                is_parent
                    ? std::vector<std::string>{ticker}
                    : std::vector<std::string>{ticker + ".FUT", ticker + ".OPT"},
                databento::SType::Parent,
                query->timeout_ms);
            std::sort(result->entries.begin(), result->entries.end(),
                      [](const contract_result_entry& left,
                         const contract_result_entry& right) {
                          const auto left_expiration =
                              (left.detail.flags & DBF_CONTRACT_HAS_EXPIRATION) != 0
                                  ? left.detail.expiration_ts_ns
                                  : std::numeric_limits<std::uint64_t>::max();
                          const auto right_expiration =
                              (right.detail.flags & DBF_CONTRACT_HAS_EXPIRATION) != 0
                                  ? right.detail.expiration_ts_ns
                                  : std::numeric_limits<std::uint64_t>::max();
                          if (left_expiration != right_expiration) {
                              return left_expiration < right_expiration;
                          }
                          if (left.detail.contract_kind != right.detail.contract_kind) {
                              return left.detail.contract_kind < right.detail.contract_kind;
                          }
                          if (left.detail.strike_price != right.detail.strike_price) {
                              return left.detail.strike_price < right.detail.strike_price;
                          }
                          return left.raw_symbol < right.raw_symbol;
                      });
        } else {
            result->entries = fetch_definitions(
                dataset,
                requested,
                databento::SType::InstrumentId,
                query->timeout_ms);
        }
        return DBF_OK;
#else
        result->error =
            "The native library was built without Databento historical API support";
        return DBF_NOT_SUPPORTED;
#endif
    } catch (const std::bad_alloc&) {
        set_contract_result_error(
            *output, "Unable to allocate contract-detail query state");
        return DBF_NO_MEMORY;
#if defined(DBF_ENABLE_LIVE)
    } catch (const databento::Exception& exception) {
        set_contract_result_error(*output, exception.what());
        return DBF_DATABENTO_ERROR;
#endif
    } catch (const std::exception& exception) {
        set_contract_result_error(*output, exception.what());
        return DBF_DATABENTO_ERROR;
    } catch (...) {
        set_contract_result_error(*output, "Unknown contract-detail query failure");
        return DBF_INTERNAL_ERROR;
    }
}

dbf_status DBF_CALL dbf_contract_details_result_get_counts(
    const dbf_contract_details_result_t* result,
    std::uint32_t* detail_count,
    std::uint32_t* utf8_blob_bytes) {
    if (result == nullptr || detail_count == nullptr || utf8_blob_bytes == nullptr) {
        return DBF_INVALID_ARGUMENT;
    }
    const auto bytes = contract_result_blob_bytes(*result);
    if (result->entries.size() > std::numeric_limits<std::uint32_t>::max()
        || bytes > std::numeric_limits<std::uint32_t>::max()) {
        return DBF_BUFFER_TOO_SMALL;
    }
    *detail_count = static_cast<std::uint32_t>(result->entries.size());
    *utf8_blob_bytes = static_cast<std::uint32_t>(bytes);
    return DBF_OK;
}

dbf_status DBF_CALL dbf_contract_details_result_copy(
    const dbf_contract_details_result_t* result,
    dbf_contract_detail_v1* details,
    std::uint32_t detail_capacity,
    std::uint8_t* utf8_blob,
    std::uint32_t utf8_blob_capacity) {
    if (result == nullptr) {
        return DBF_INVALID_ARGUMENT;
    }
    std::uint32_t required_count{};
    std::uint32_t required_bytes{};
    const auto status = dbf_contract_details_result_get_counts(
        result, &required_count, &required_bytes);
    if (status != DBF_OK) {
        return status;
    }
    if (detail_capacity < required_count || utf8_blob_capacity < required_bytes
        || (required_count != 0 && details == nullptr)
        || (required_bytes != 0 && utf8_blob == nullptr)) {
        return DBF_BUFFER_TOO_SMALL;
    }
    std::uint32_t offset{};
    const auto copy_string = [&](const std::string& source,
                                 dbf_utf8_slice_v1& destination) {
        destination.offset = offset;
        destination.length = static_cast<std::uint32_t>(source.size());
        if (!source.empty()) {
            std::memcpy(utf8_blob + offset, source.data(), source.size());
            offset += static_cast<std::uint32_t>(source.size());
        }
    };
    for (std::uint32_t index = 0; index < required_count; ++index) {
        details[index] = result->entries[index].detail;
        copy_string(result->entries[index].raw_symbol, details[index].raw_symbol);
        copy_string(result->entries[index].asset, details[index].asset);
        copy_string(result->entries[index].underlying, details[index].underlying);
        copy_string(result->entries[index].currency, details[index].currency);
        copy_string(result->entries[index].settlement_currency,
                    details[index].settlement_currency);
        copy_string(result->entries[index].exchange, details[index].exchange);
        copy_string(result->entries[index].security_type,
                    details[index].security_type);
        copy_string(result->entries[index].cfi, details[index].cfi);
        copy_string(result->entries[index].unit_of_measure,
                    details[index].unit_of_measure);
    }
    return DBF_OK;
}

dbf_status DBF_CALL dbf_contract_details_result_get_error(
    const dbf_contract_details_result_t* result,
    std::uint8_t* utf8_buffer,
    std::uint32_t utf8_buffer_capacity,
    std::uint32_t* required_bytes) {
    if (result == nullptr || required_bytes == nullptr) {
        return DBF_INVALID_ARGUMENT;
    }
    const auto bytes = result->error.size() + 1;
    if (bytes > std::numeric_limits<std::uint32_t>::max()) {
        return DBF_BUFFER_TOO_SMALL;
    }
    *required_bytes = static_cast<std::uint32_t>(bytes);
    if (utf8_buffer == nullptr || utf8_buffer_capacity < bytes) {
        return DBF_BUFFER_TOO_SMALL;
    }
    std::memcpy(utf8_buffer, result->error.c_str(), bytes);
    return DBF_OK;
}

dbf_status DBF_CALL dbf_contract_details_result_destroy(
    dbf_contract_details_result_t* result) {
    if (result == nullptr) {
        return DBF_INVALID_ARGUMENT;
    }
    delete result;
    return DBF_OK;
}

dbf_status DBF_CALL dbf_get_latest_price(
    const dbf_latest_price_request_v1* request,
    std::uint32_t timeout_ms,
    dbf_latest_price_result64* result) {
    if (request == nullptr || result == nullptr) {
        return DBF_INVALID_ARGUMENT;
    }
    *result = {};
    if (!valid_struct(
            request->struct_size, sizeof(*request), request->abi_version)) {
        return DBF_ABI_MISMATCH;
    }
    const bool valid_price_policy =
        request->selected_policy == DBF_LATEST_PRICE_LAST_TRADE
        || request->selected_policy == DBF_LATEST_PRICE_QUOTE_MIDPOINT
        || request->selected_policy == DBF_LATEST_PRICE_BID
        || request->selected_policy == DBF_LATEST_PRICE_ASK;
    const bool valid_freshness =
        request->freshness_policy == DBF_LATEST_PRICE_NEXT_OBSERVED
        || request->freshness_policy
               == DBF_LATEST_PRICE_REPLAY_LOOKBACK_THEN_LIVE;
    const bool valid_lookback =
        (request->freshness_policy == DBF_LATEST_PRICE_NEXT_OBSERVED
         && request->replay_lookback_ms == 0)
        || (request->freshness_policy
                == DBF_LATEST_PRICE_REPLAY_LOOKBACK_THEN_LIVE
            && request->replay_lookback_ms != 0);
    if (!valid_price_policy || !valid_freshness || !valid_lookback
        || (request->input_symbology != 1 && request->input_symbology != 2)
        || timeout_ms == 0 || timeout_ms == DBF_WAIT_INFINITE
        || request->utf8_blob == nullptr || request->utf8_blob_bytes == 0
        || request->dataset.length == 0 || request->symbol.length == 0
        || !valid_blob_range(
            request->dataset.offset, request->dataset.length,
            request->utf8_blob_bytes)
        || !valid_blob_range(
            request->symbol.offset, request->symbol.length,
            request->utf8_blob_bytes)
        || request->reserved32 != 0
        || std::any_of(
            std::begin(request->reserved), std::end(request->reserved),
            [](std::uint64_t value) { return value != 0; })) {
        return DBF_INVALID_ARGUMENT;
    }

    const auto deadline = monotonic_clock::now()
                          + std::chrono::milliseconds{timeout_ms};
    try {
        const std::string dataset{
            reinterpret_cast<const char*>(
                request->utf8_blob + request->dataset.offset),
            request->dataset.length};
        const std::string symbol{
            reinterpret_cast<const char*>(
                request->utf8_blob + request->symbol.offset),
            request->symbol.length};
        if (dataset.find('\0') != std::string::npos
            || symbol.find('\0') != std::string::npos) {
            return DBF_INVALID_ARGUMENT;
        }
#if defined(DBF_ENABLE_LIVE)
        const auto status = get_latest_price_live(
            *request, dataset, symbol, timeout_ms, *result);
        if (status != DBF_OK) {
            *result = {};
        }
        return status;
#else
        (void)dataset;
        (void)symbol;
        return DBF_NOT_SUPPORTED;
#endif
    } catch (const std::bad_alloc&) {
        *result = {};
        return DBF_NO_MEMORY;
#if defined(DBF_ENABLE_LIVE)
    } catch (const databento::HeartbeatTimeoutError&) {
        *result = {};
        return monotonic_clock::now() >= deadline
                   ? DBF_TIMEOUT
                   : DBF_CONNECTION_HUNG;
    } catch (const databento::Exception&) {
        *result = {};
        return monotonic_clock::now() >= deadline
                   ? DBF_TIMEOUT
                   : DBF_DATABENTO_ERROR;
#endif
    } catch (const std::exception&) {
        *result = {};
        return monotonic_clock::now() >= deadline
                   ? DBF_TIMEOUT
                   : DBF_DATABENTO_ERROR;
    } catch (...) {
        *result = {};
        return DBF_INTERNAL_ERROR;
    }
}

dbf_status DBF_CALL dbf_historical_estimate(
    const dbf_historical_request_v1* request,
    const dbf_utf8_slice_v1* symbols,
    const std::uint8_t* utf8_blob,
    std::uint32_t utf8_blob_bytes,
    dbf_historical_estimate_v1* estimate) {
    if (estimate == nullptr
        || !valid_struct(estimate->struct_size, sizeof(*estimate), estimate->abi_version)) {
        return DBF_ABI_MISMATCH;
    }
    if (!valid_historical_request(request, symbols, utf8_blob, utf8_blob_bytes)) {
        return DBF_INVALID_ARGUMENT;
    }
    const auto duration_minutes = static_cast<std::uint64_t>(
        (request->end_ts_ns - request->start_ts_ns) / 60'000'000'000LL);
    if ((request->flags & DBF_HISTORICAL_SYNTHETIC) != 0) {
        const auto records = std::max<std::uint64_t>(
            1, std::min<std::uint64_t>(duration_minutes, 10'000));
        estimate->estimated_records = records * request->symbol_count;
        estimate->estimated_bytes = estimate->estimated_records * sizeof(dbf_historical_record120);
        estimate->estimated_cost_usd = 0.0;
        return DBF_OK;
    }
#if defined(DBF_ENABLE_LIVE)
    try {
        const std::string dataset{
            reinterpret_cast<const char*>(utf8_blob + request->dataset.offset),
            request->dataset.length};
        std::vector<std::string> requested_symbols;
        requested_symbols.reserve(request->symbol_count);
        for (std::uint32_t index = 0; index < request->symbol_count; ++index) {
            requested_symbols.emplace_back(
                reinterpret_cast<const char*>(utf8_blob + symbols[index].offset),
                symbols[index].length);
        }
        const auto schema = historical_schema(request->schema);
        auto client = databento::Historical::Builder().SetKeyFromEnv().Build();
        const databento::DateTimeRange<databento::UnixNanos> range{
            databento::UnixNanos{databento::UnixNanos::duration{request->start_ts_ns}},
            databento::UnixNanos{databento::UnixNanos::duration{request->end_ts_ns}}};
        estimate->estimated_cost_usd = client.MetadataGetCost(
            dataset, range, requested_symbols, schema,
            historical_stype(request->input_symbology), request->record_limit);
        estimate->estimated_bytes = client.MetadataGetBillableSize(
            dataset, range, requested_symbols, schema,
            historical_stype(request->input_symbology), request->record_limit);
        estimate->estimated_records = client.MetadataGetRecordCount(
            dataset, range, requested_symbols, schema,
            historical_stype(request->input_symbology), request->record_limit);
        return DBF_OK;
    } catch (const databento::Exception&) {
        return DBF_DATABENTO_ERROR;
    } catch (...) {
        return DBF_INTERNAL_ERROR;
    }
#else
    return DBF_NOT_SUPPORTED;
#endif
}

dbf_status DBF_CALL dbf_historical_batch_submit(
    const dbf_historical_request_v1* request,
    const dbf_utf8_slice_v1* symbols,
    const std::uint8_t* utf8_blob,
    std::uint32_t utf8_blob_bytes,
    dbf_historical_result_t** output) {
    if (output == nullptr) return DBF_INVALID_ARGUMENT;
    *output = nullptr;
    if (!valid_historical_request(request, symbols, utf8_blob, utf8_blob_bytes)) {
        return DBF_INVALID_ARGUMENT;
    }
    try {
        auto result = std::make_unique<dbf_historical_result>();
        if ((request->flags & DBF_HISTORICAL_SYNTHETIC) != 0) {
            result->payload =
                "{\"providerJobId\":\"synthetic-job\",\"state\":\"Completed\","
                "\"costUsd\":0,\"recordCount\":2,\"billedBytes\":240,\"progressPercent\":100}";
        } else {
#if defined(DBF_ENABLE_LIVE)
            const std::string dataset{
                reinterpret_cast<const char*>(utf8_blob + request->dataset.offset),
                request->dataset.length};
            const databento::DateTimeRange<databento::UnixNanos> range{
                databento::UnixNanos{databento::UnixNanos::duration{request->start_ts_ns}},
                databento::UnixNanos{databento::UnixNanos::duration{request->end_ts_ns}}};
            auto client = databento::Historical::Builder().SetKeyFromEnv().Build();
            const auto job = client.BatchSubmitJob(
                dataset, historical_symbols(*request, symbols, utf8_blob),
                historical_schema(request->schema), range,
                databento::Encoding::Dbn, databento::Compression::Zstd,
                false, false, true, false,
                databento::SplitDuration::Month, 0,
                databento::Delivery::Download,
                historical_stype(request->input_symbology),
                databento::SType::RawSymbol,
                request->record_limit);
            result->payload = historical_job_payload(job);
#else
            return DBF_NOT_SUPPORTED;
#endif
        }
        *output = result.release();
        return DBF_OK;
    } catch (const std::bad_alloc&) {
        return DBF_NO_MEMORY;
#if defined(DBF_ENABLE_LIVE)
    } catch (const databento::Exception&) {
        return DBF_DATABENTO_ERROR;
#endif
    } catch (...) {
        return DBF_INTERNAL_ERROR;
    }
}

dbf_status DBF_CALL dbf_historical_batch_get_status(
    const std::uint8_t* provider_job_id,
    std::uint32_t provider_job_id_bytes,
    dbf_historical_result_t** output) {
    if (output == nullptr || provider_job_id == nullptr || provider_job_id_bytes == 0) {
        return DBF_INVALID_ARGUMENT;
    }
    *output = nullptr;
    try {
        auto result = std::make_unique<dbf_historical_result>();
        const auto job_id = historical_text(provider_job_id, provider_job_id_bytes);
        if (job_id == "synthetic-job") {
            result->payload = "{\"providerJobId\":\"" + job_id
                              + "\",\"state\":\"Completed\",\"costUsd\":0,"
                                "\"recordCount\":2,\"billedBytes\":240,\"progressPercent\":100}";
        } else {
#if defined(DBF_ENABLE_LIVE)
            auto client = databento::Historical::Builder().SetKeyFromEnv().Build();
            result->payload = historical_job_payload(client.BatchGetJobDetails(job_id));
#else
            return DBF_NOT_SUPPORTED;
#endif
        }
        *output = result.release();
        return DBF_OK;
    } catch (const std::bad_alloc&) {
        return DBF_NO_MEMORY;
#if defined(DBF_ENABLE_LIVE)
    } catch (const databento::Exception&) {
        return DBF_DATABENTO_ERROR;
#endif
    } catch (...) {
        return DBF_INTERNAL_ERROR;
    }
}

dbf_status DBF_CALL dbf_historical_batch_list_files(
    const std::uint8_t* provider_job_id,
    std::uint32_t provider_job_id_bytes,
    dbf_historical_result_t** output) {
    if (output == nullptr || provider_job_id == nullptr || provider_job_id_bytes == 0) {
        return DBF_INVALID_ARGUMENT;
    }
    *output = nullptr;
    try {
        auto result = std::make_unique<dbf_historical_result>();
        const auto job_id = historical_text(provider_job_id, provider_job_id_bytes);
        if (job_id == "synthetic-job") {
            result->payload =
                "{\"files\":[{\"providerFileId\":\"synthetic.csv\","
                "\"fileName\":\"synthetic.csv\",\"schema\":2}]}";
        } else {
#if defined(DBF_ENABLE_LIVE)
            auto client = databento::Historical::Builder().SetKeyFromEnv().Build();
            const auto job = client.BatchGetJobDetails(job_id);
            const auto schema = historical_schema_id(job.schema);
            const auto files = client.BatchListFiles(job_id);
            std::string payload{"{\"files\":["};
            for (std::size_t index = 0; index < files.size(); ++index) {
                if (index != 0) payload += ',';
                const auto& file = files[index];
                payload += "{\"providerFileId\":\"" + json_escape(file.filename)
                           + "\",\"fileName\":\"" + json_escape(file.filename)
                           + "\",\"schema\":" + std::to_string(schema)
                           + ",\"sizeBytes\":" + std::to_string(file.size)
                           + ",\"sha256\":\"" + json_escape(file.hash) + "\"}";
            }
            result->payload = payload + "]}";
#else
            return DBF_NOT_SUPPORTED;
#endif
        }
        *output = result.release();
        return DBF_OK;
    } catch (const std::bad_alloc&) {
        return DBF_NO_MEMORY;
#if defined(DBF_ENABLE_LIVE)
    } catch (const databento::Exception&) {
        return DBF_DATABENTO_ERROR;
#endif
    } catch (...) {
        return DBF_INTERNAL_ERROR;
    }
}

dbf_status DBF_CALL dbf_historical_batch_download_file(
    const std::uint8_t* provider_job_id,
    std::uint32_t provider_job_id_bytes,
    const std::uint8_t* file_name,
    std::uint32_t file_name_bytes,
    const std::uint8_t* destination_path,
    std::uint32_t destination_path_bytes) {
    if (provider_job_id == nullptr || provider_job_id_bytes == 0
        || file_name == nullptr || file_name_bytes == 0
        || destination_path == nullptr || destination_path_bytes == 0) {
        return DBF_INVALID_ARGUMENT;
    }
    try {
        const auto job_id = historical_text(provider_job_id, provider_job_id_bytes);
        const auto requested_file = historical_text(file_name, file_name_bytes);
        const std::filesystem::path path{
            historical_text(destination_path, destination_path_bytes)};
        std::filesystem::create_directories(path.parent_path());
        if (job_id == "synthetic-job") {
            std::ofstream output{path, std::ios::binary | std::ios::trunc};
            if (!output) return DBF_OS_ERROR;
            output << "2,SYNTH,1000,7,1770000000000000000,1,5000000000,5002000000,"
                      "4998000000,5000500000,10,T,B,0\n";
            output << "2,SYNTH,1001,7,1770000060000000000,2,5001000000,5003000000,"
                      "4999000000,5001500000,11,T,A,0\n";
            return output.good() ? DBF_OK : DBF_OS_ERROR;
        }
#if defined(DBF_ENABLE_LIVE)
        auto client = databento::Historical::Builder().SetKeyFromEnv().Build();
        const auto downloaded = client.BatchDownload(
            path.parent_path(), job_id, requested_file);
        if (downloaded != path) {
            std::filesystem::rename(downloaded, path);
        }
        return DBF_OK;
#else
        return DBF_NOT_SUPPORTED;
#endif
    } catch (const std::filesystem::filesystem_error&) {
        return DBF_OS_ERROR;
#if defined(DBF_ENABLE_LIVE)
    } catch (const databento::Exception&) {
        return DBF_DATABENTO_ERROR;
#endif
    } catch (...) {
        return DBF_INTERNAL_ERROR;
    }
}

dbf_status DBF_CALL dbf_historical_range_open(
    const dbf_historical_request_v1* request,
    const dbf_utf8_slice_v1* symbols,
    const std::uint8_t* utf8_blob,
    std::uint32_t utf8_blob_bytes,
    dbf_historical_result_t** output) {
    if (output == nullptr) return DBF_INVALID_ARGUMENT;
    *output = nullptr;
    if (!valid_historical_request(request, symbols, utf8_blob, utf8_blob_bytes)) {
        return DBF_INVALID_ARGUMENT;
    }
    try {
        auto result = std::make_unique<dbf_historical_result>();
        if ((request->flags & DBF_HISTORICAL_SYNTHETIC) != 0) {
            result->records.push_back(make_historical_synthetic_record(*request, 0));
            result->records.push_back(make_historical_synthetic_record(*request, 1));
        } else {
#if defined(DBF_ENABLE_LIVE)
            const std::string dataset{
                reinterpret_cast<const char*>(utf8_blob + request->dataset.offset),
                request->dataset.length};
            const auto requested = historical_symbols(*request, symbols, utf8_blob);
            const databento::DateTimeRange<databento::UnixNanos> range{
                databento::UnixNanos{databento::UnixNanos::duration{request->start_ts_ns}},
                databento::UnixNanos{databento::UnixNanos::duration{request->end_ts_ns}}};
            auto client = databento::Historical::Builder().SetKeyFromEnv().Build();
            auto store = client.TimeseriesGetRange(
                dataset, range, requested, historical_schema(request->schema),
                historical_stype(request->input_symbology), databento::SType::RawSymbol,
                request->record_limit);
            append_historical_records(
                store, request->schema, requested.front(), *result);
#else
            return DBF_NOT_SUPPORTED;
#endif
        }
        *output = result.release();
        return DBF_OK;
    } catch (const std::bad_alloc&) {
        return DBF_NO_MEMORY;
#if defined(DBF_ENABLE_LIVE)
    } catch (const databento::Exception&) {
        return DBF_DATABENTO_ERROR;
#endif
    } catch (...) {
        return DBF_INTERNAL_ERROR;
    }
}

dbf_status DBF_CALL dbf_historical_file_open(
    const std::uint8_t* file_path,
    std::uint32_t file_path_bytes,
    std::uint32_t schema,
    dbf_historical_result_t** output) {
    if (output == nullptr || file_path == nullptr || file_path_bytes == 0
        || schema < DBF_HISTORICAL_DEFINITION
        || schema > DBF_HISTORICAL_STATISTICS) {
        return DBF_INVALID_ARGUMENT;
    }
    *output = nullptr;
#if defined(DBF_ENABLE_LIVE)
    try {
        auto result = std::make_unique<dbf_historical_result>();
        const std::filesystem::path path{
            historical_text(file_path, file_path_bytes)};
        if (!std::filesystem::is_regular_file(path)) return DBF_INVALID_ARGUMENT;
        databento::DbnStore store{path};
        const auto& metadata = store.GetMetadata();
        const auto fallback = metadata.symbols.empty()
                                  ? path.stem().string()
                                  : metadata.symbols.front();
        append_historical_records(store, schema, fallback, *result);
        *output = result.release();
        return DBF_OK;
    } catch (const std::bad_alloc&) {
        return DBF_NO_MEMORY;
    } catch (const databento::Exception&) {
        return DBF_DATABENTO_ERROR;
    } catch (const std::filesystem::filesystem_error&) {
        return DBF_OS_ERROR;
    } catch (...) {
        return DBF_INTERNAL_ERROR;
    }
#else
    (void)schema;
    return DBF_NOT_SUPPORTED;
#endif
}

dbf_status DBF_CALL dbf_historical_result_get_payload(
    const dbf_historical_result_t* result,
    std::uint8_t* utf8_buffer,
    std::uint32_t utf8_buffer_capacity,
    std::uint32_t* required_bytes) {
    if (result == nullptr) return DBF_INVALID_ARGUMENT;
    return copy_historical_text(result->payload, utf8_buffer, utf8_buffer_capacity, required_bytes);
}

dbf_status DBF_CALL dbf_historical_result_get_next_batch(
    dbf_historical_result_t* result,
    dbf_historical_record120* records,
    std::uint32_t record_capacity,
    dbf_historical_batch_v1* batch) {
    if (result == nullptr || records == nullptr || record_capacity == 0 || batch == nullptr
        || !valid_struct(batch->struct_size, sizeof(*batch), batch->abi_version)) {
        return DBF_INVALID_ARGUMENT;
    }
    const auto remaining = result->records.size() - result->cursor;
    const auto count = std::min<std::size_t>(remaining, record_capacity);
    std::copy_n(result->records.begin() + static_cast<std::ptrdiff_t>(result->cursor),
                count, records);
    result->cursor += count;
    batch->records_read = static_cast<std::uint32_t>(count);
    batch->more_available = result->cursor < result->records.size() ? 1u : 0u;
    batch->batch_ordinal = result->batch_ordinal++;
    return DBF_OK;
}

dbf_status DBF_CALL dbf_historical_result_get_error(
    const dbf_historical_result_t* result,
    std::uint8_t* utf8_buffer,
    std::uint32_t utf8_buffer_capacity,
    std::uint32_t* required_bytes) {
    if (result == nullptr) return DBF_INVALID_ARGUMENT;
    return copy_historical_text(result->error, utf8_buffer, utf8_buffer_capacity, required_bytes);
}

dbf_status DBF_CALL dbf_historical_result_destroy(dbf_historical_result_t* result) {
    if (result == nullptr) return DBF_INVALID_ARGUMENT;
    delete result;
    return DBF_OK;
}

} // extern "C"
