#pragma once

#include <cstdint>

namespace dbf_live {

/// <summary>
/// Selects a mapping by the instrument ID resolved for this dataset and
/// definition-date feed epoch. Publisher identifies the record source only.
/// </summary>
class instrument_mapping_selector final {
public:
    constexpr explicit instrument_mapping_selector(
        std::uint32_t instrument_id) noexcept
        : instrument_id_{instrument_id} {
    }

    [[nodiscard]] constexpr bool selects(
        std::uint32_t instrument_id) const noexcept {
        return instrument_id != 0 && instrument_id == instrument_id_;
    }

private:
    std::uint32_t instrument_id_{};
};

} // namespace dbf_live
