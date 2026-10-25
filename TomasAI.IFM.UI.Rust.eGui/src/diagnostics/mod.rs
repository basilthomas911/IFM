//! Optional native diagnostics. Measurements support UI performance investigation and do not drive business state.

/// Opt-in bounded publication-to-render timing.
pub mod latency;

/// Correlated first-load and already-open trade timings.
pub mod trade_load;
