//! Independent native ABI identity for the new runtime.
//!
//! These exports must never replace the legacy `dbf_*` exports or claim support
//! for unfinished application operations. The production backend is not selectable
//! until its complete operation adapter and parity matrix have been qualified.

pub mod catalog;
pub mod catalog_query;
pub mod claims;
pub mod fmp;
pub mod history;
pub mod history_control;
pub mod history_stream;
pub mod live;
pub mod market_owner;
pub mod pricing;
pub mod publication;
pub mod reference;
mod retained;
mod reviewed;
pub mod runtime;
pub mod scope_pricing;
pub mod scope_trades;
pub mod scopes;

/// ABI identity for the new library, distinct from the Databento bridge ABI.
pub const ABI_VERSION: u32 = 1;

/// Returns the ABI version without allocating resources or starting background work.
///
/// This export is safe to call before managed services, credentials or Tokio are
/// initialized. It establishes library identity only, not backend readiness.
#[unsafe(no_mangle)]
pub extern "C" fn ifm_market_data_get_abi_version() -> u32 {
    ABI_VERSION
}

/// Runs the qualified Treasury conversion numerical core through the C ABI.
///
/// Returns zero on success, one for a null/misaligned output, or two for a yield
/// outside the conversion domain. Errors leave the caller's output unchanged.
/// Source/tenor/curve qualification and decimal conversion are prerequisites;
/// this export alone does not implement the application's Treasury operation.
///
/// # Safety
/// On a successful call, `output` must point to one aligned, writable `f64` owned
/// by the caller for the duration of the call. It must not alias memory accessed
/// concurrently. No pointer or caller buffer is retained after return.
#[unsafe(no_mangle)]
pub unsafe extern "C" fn ifm_market_data_treasury_rate_v1(
    half_yield: f64,
    output: *mut f64,
) -> i32 {
    if output.is_null() || !(output as usize).is_multiple_of(std::mem::align_of::<f64>()) {
        return 1;
    }
    match crate::reference::treasury::continuous_rate(half_yield) {
        Ok(value) => {
            // SAFETY: Non-null/alignment were checked; the C ABI contract requires
            // the caller to supply exclusive writable memory for this one value.
            unsafe { output.write(value) };
            0
        }
        Err(_) => 2,
    }
}
