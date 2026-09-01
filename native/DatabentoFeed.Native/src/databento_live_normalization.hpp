#pragma once

#include "databento_feed_native.h"

#if defined(DBF_ENABLE_LIVE)

#include <databento/constants.hpp>
#include <databento/record.hpp>

#include <cstdint>

namespace dbf_live {

enum class replay_schema {
    unknown,
    trades,
    statistics,
};

// Databento emits this warning when the client temporarily falls behind. It is
// an operational signal for health/telemetry, not a terminal transport error.
// The live session must remain open so the consumer can catch up.
inline bool keeps_live_session_open(
    databento::SystemCode code) noexcept {
    return code == databento::SystemCode::SlowReaderWarning;
}

inline replay_schema classify_replay_schema(
    std::string_view message) noexcept {
    if (message.find("trades") != std::string_view::npos) {
        return replay_schema::trades;
    }
    if (message.find("statistics") != std::string_view::npos) {
        return replay_schema::statistics;
    }
    return replay_schema::unknown;
}

inline std::int64_t nanos(databento::UnixNanos value) noexcept {
    return value.time_since_epoch().count();
}

inline std::int64_t price_or_zero(std::int64_t value, std::uint8_t& flags) noexcept {
    if (value == databento::kUndefPrice) {
        flags |= DBF_RECORD_FLAG_UNDEFINED_PRICE;
        return 0;
    }
    return value;
}

inline void fill_header(dbf_record_header32& destination,
                        const databento::RecordHeader& source,
                        std::uint8_t kind,
                        std::int64_t ts_recv,
                        std::uint32_t sequence,
                        std::uint16_t schema) noexcept {
    destination.instrument_id = source.instrument_id;
    destination.publisher_id = source.publisher_id;
    destination.record_kind = kind;
    destination.ts_event_ns = nanos(source.ts_event);
    destination.ts_recv_ns = ts_recv;
    destination.sequence = sequence;
    destination.source_schema = schema;
}

inline bool normalize(const databento::Record& source,
                      dbf_market_record64& destination,
                      bool statistics_replay = false,
                      bool trade_replay = false) noexcept {
    destination = {};
    if (const auto* message = source.GetIf<databento::Mbp1Msg>()) {
        fill_header(destination.header, message->hd, DBF_RECORD_QUOTE,
                    nanos(message->ts_recv), message->sequence,
                    static_cast<std::uint16_t>(databento::Schema::Mbp1));
        if (message->flags.IsSnapshot()) {
            destination.header.flags |= DBF_RECORD_FLAG_SNAPSHOT;
        }
        destination.quote.bid_price = price_or_zero(
            message->levels[0].bid_px, destination.header.flags);
        destination.quote.ask_price = price_or_zero(
            message->levels[0].ask_px, destination.header.flags);
        destination.quote.bid_size = message->levels[0].bid_sz;
        destination.quote.ask_size = message->levels[0].ask_sz;
        destination.quote.bid_count = message->levels[0].bid_ct;
        destination.quote.ask_count = message->levels[0].ask_ct;
        return true;
    }
    if (const auto* message = source.GetIf<databento::TradeMsg>()) {
        fill_header(destination.header, message->hd, DBF_RECORD_TRADE,
                    nanos(message->ts_recv), message->sequence,
                    static_cast<std::uint16_t>(databento::Schema::Trades));
        if (message->flags.IsSnapshot()) {
            destination.header.flags |= DBF_RECORD_FLAG_SNAPSHOT;
        }
        if (trade_replay) {
            destination.header.flags |= DBF_RECORD_FLAG_REPLAY;
        }
        destination.trade.price = price_or_zero(
            message->price, destination.header.flags);
        destination.trade.size = message->size;
        destination.trade.action = static_cast<std::uint8_t>(message->action);
        destination.trade.side = static_cast<std::uint8_t>(message->side);
        destination.trade.dbn_flags = message->flags.Raw();
        destination.trade.depth = message->depth;
        destination.trade.ts_in_delta_ns = message->ts_in_delta.count();
        return true;
    }
    if (const auto* message = source.GetIf<databento::MboMsg>()) {
        fill_header(destination.header, message->hd, DBF_RECORD_MBO,
                    nanos(message->ts_recv), message->sequence,
                    static_cast<std::uint16_t>(databento::Schema::Mbo));
        if (message->flags.IsSnapshot()) {
            destination.header.flags |= DBF_RECORD_FLAG_SNAPSHOT;
        }
        destination.mbo.order_id = message->order_id;
        destination.mbo.price = price_or_zero(
            message->price, destination.header.flags);
        destination.mbo.size = message->size;
        destination.mbo.ts_in_delta_ns = message->ts_in_delta.count();
        destination.mbo.action = static_cast<std::uint8_t>(message->action);
        destination.mbo.side = static_cast<std::uint8_t>(message->side);
        destination.mbo.dbn_flags = message->flags.Raw();
        destination.mbo.channel_id = message->channel_id;
        return true;
    }
    if (const auto* message = source.GetIf<databento::StatMsg>()) {
        fill_header(destination.header, message->hd, DBF_RECORD_STATISTICS,
                    nanos(message->ts_recv), message->sequence,
                    static_cast<std::uint16_t>(databento::Schema::Statistics));
        if (statistics_replay) {
            destination.header.flags |= DBF_RECORD_FLAG_REPLAY;
        }
        destination.statistics.price = price_or_zero(
            message->price, destination.header.flags);
        destination.statistics.quantity = message->quantity;
        destination.statistics.ts_ref_ns = nanos(message->ts_ref);
        destination.statistics.stat_type =
            static_cast<std::uint16_t>(message->stat_type);
        destination.statistics.channel_id = message->channel_id;
        destination.statistics.update_action =
            static_cast<std::uint8_t>(message->update_action);
        destination.statistics.stat_flags = message->stat_flags;
        return true;
    }
    return false;
}

} // namespace dbf_live

#endif
