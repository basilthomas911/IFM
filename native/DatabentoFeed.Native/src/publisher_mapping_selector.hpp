#pragma once

#include <cstdint>

namespace dbf_live {

enum class publisher_match_status : std::uint8_t {
    unrelated,
    unresolved,
    exact,
    conflict
};

/// <summary>
/// Selects a live mapping by Databento's publisher-scoped instrument identity.
/// Instrument IDs alone are not globally unique across publishers.
/// </summary>
class publisher_mapping_selector final {
public:
    constexpr publisher_mapping_selector(
        std::uint32_t instrument_id,
        std::uint16_t publisher_id) noexcept
        : instrument_id_{instrument_id}, publisher_id_{publisher_id} {
    }

    constexpr void observe(
        std::uint32_t instrument_id,
        std::uint16_t publisher_id) noexcept {
        if (instrument_id != instrument_id_) {
            return;
        }
        saw_instrument_ = true;
        if (publisher_id == publisher_id_) {
            has_exact_ = true;
        } else if (publisher_id == 0) {
            has_unresolved_ = true;
        }
    }

    [[nodiscard]] constexpr publisher_match_status status() const noexcept {
        if (has_exact_) {
            return publisher_match_status::exact;
        }
        if (has_unresolved_) {
            return publisher_match_status::unresolved;
        }
        return saw_instrument_
                   ? publisher_match_status::conflict
                   : publisher_match_status::unrelated;
    }

    [[nodiscard]] constexpr bool selects(
        std::uint32_t instrument_id,
        std::uint16_t publisher_id) const noexcept {
        if (instrument_id != instrument_id_) {
            return false;
        }
        if (has_exact_) {
            return publisher_id == publisher_id_;
        }
        return has_unresolved_ && publisher_id == 0;
    }

private:
    std::uint32_t instrument_id_{};
    std::uint16_t publisher_id_{};
    bool saw_instrument_{};
    bool has_exact_{};
    bool has_unresolved_{};
};

} // namespace dbf_live
