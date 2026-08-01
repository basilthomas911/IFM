#include "databento_feed_native.h"

#include <algorithm>
#include <atomic>
#include <chrono>
#include <condition_variable>
#include <cstring>
#include <limits>
#include <mutex>
#include <new>
#include <string>
#include <thread>
#include <utility>
#include <vector>

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
    std::string requested_symbol;
    std::string raw_symbol;
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

    std::mutex error_mutex;
    std::string last_error;
};

namespace {

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
    std::uint32_t enabled[3]{};
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
    return count == 0 ? DBF_RECORD_QUOTE : enabled[sequence % count];
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
    } else {
        record.mbo.order_id = sequence + 1;
        record.mbo.price = price;
        record.mbo.size = 1 + static_cast<std::uint32_t>(sequence % 25);
        record.mbo.action = 'A';
        record.mbo.side = (sequence & 1u) == 0 ? 'B' : 'A';
    }
    return record;
}

bool publish_record(dbf_feed* feed, const dbf_market_record64& record) noexcept {
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
        }
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

void producer_main(dbf_feed* feed) noexcept {
    apply_producer_thread_settings(feed);
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
    if (config->reserved16 != 0
        || std::any_of(std::begin(config->reserved), std::end(config->reserved),
                       [](std::uint64_t value) { return value != 0; })
        || config->flags > (DBF_CONFIG_LOCK_RING_MEMORY
                            | DBF_CONFIG_REQUIRE_LOCKED_MEMORY
                            | DBF_CONFIG_REQUIRE_BASE_PAGE_POLICY
                            | DBF_CONFIG_REQUIRE_PRIORITY
                            | DBF_CONFIG_REQUIRE_NUMA_LOCALITY)
        || config->producer_priority < 0 || config->producer_priority > 2
        || config->drain_priority < 0 || config->drain_priority > 2
        || config->ring_full_timeout_us == 0) {
        return DBF_INVALID_ARGUMENT;
    }
    if (config->data_source != DBF_DATA_SOURCE_SYNTHETIC) {
        return DBF_NOT_SUPPORTED;
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
                || (item.data_kinds & 7u) == 0 || (item.data_kinds & ~7u) != 0) {
                return DBF_INVALID_ARGUMENT;
            }
            mapping_entry mapping{};
            mapping.subscription_index = index;
            mapping.instrument_id = index + 1;
            mapping.publisher_id = 1;
            mapping.data_kinds = item.data_kinds & 7u;
            mapping.requested_symbol.assign(
                reinterpret_cast<const char*>(utf8_blob + item.symbol_offset),
                item.symbol_length);
            mapping.raw_symbol = mapping.requested_symbol;
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
            mapping.data_kinds = subscription->data_kinds & 7u;
            mapping.raw_symbol.assign(
                reinterpret_cast<const char*>(utf8_blob + contract.raw_symbol_offset),
                contract.raw_symbol_length);
            mapping.requested_symbol = mapping.raw_symbol;
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
    try {
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
    std::fill(std::begin(stats->reserved), std::end(stats->reserved), 0);
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

} // extern "C"
