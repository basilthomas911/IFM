#pragma once

namespace dbf_latest {

template <typename TClient>
class session_guard {
public:
    explicit session_guard(TClient& client) noexcept
        : client_{client} {
    }

    session_guard(const session_guard&) = delete;
    session_guard& operator=(const session_guard&) = delete;

    ~session_guard() {
        stop_noexcept();
    }

    void stop() {
        if (active_) {
            client_.Stop();
            active_ = false;
        }
    }

private:
    void stop_noexcept() noexcept {
        if (!active_) {
            return;
        }
        try {
            client_.Stop();
        } catch (...) {
        }
        active_ = false;
    }

    TClient& client_;
    bool active_{true};
};

} // namespace dbf_latest
