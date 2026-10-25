//! Market Data views: futures/option contracts, yield curves, provider instruments, price charts, and the published market outlook.

/// Published market-price series with Eastern-time labels and symbol-specific display formatting. Charts consume backend points and bounds; they do not query providers or construct missing prices.
pub mod market_charts;
/// Market-outlook caption/value matrix. Published color roles are mapped to the native palette; numeric signals and outlook decisions remain backend-owned.
pub mod market_outlook;
/// Provider instrument browser and draft mapping workflow. Filters, selected instruments, mapping edits, preview evidence, and confirmation are local state; the managed bridge owns discovery and mapping persistence.
pub mod provider_browser;

/// Combined Futures Contracts, Futures Option Contracts, and Yield Curve Rates window.
pub mod market_data_manager;
