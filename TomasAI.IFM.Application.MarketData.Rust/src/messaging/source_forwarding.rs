//! Development snapshot boundary: retain/capture input while forwarding is held.
//! This never stops provider ownership, changes a generation, or acknowledges a source.
use std::sync::{Condvar, Mutex};
use std::time::{Duration, Instant};

#[derive(Default)]
struct State { lease: u64, active: u64 }
static STATE: Mutex<State> = Mutex::new(State { lease: 0, active: 0 });
static CHANGED: Condvar = Condvar::new();

pub(crate) struct ForwardingLease;
impl Drop for ForwardingLease {
    fn drop(&mut self) {
        let mut state = STATE.lock().unwrap_or_else(|e| e.into_inner());
        state.active -= 1;
        CHANGED.notify_all();
    }
}

/// A relay must hold this guard through its entire request/reply source send.
pub(crate) fn try_begin() -> Option<ForwardingLease> {
    let mut state = STATE.lock().ok()?;
    if state.lease != 0 { return None; }
    state.active = state.active.checked_add(1)?;
    Some(ForwardingLease)
}

fn hold(lease: u64, budget: Duration, capture_enabled: bool) -> i32 {
    if lease == 0 || budget.is_zero() || budget > Duration::from_secs(30) { return 1; }
    if !capture_enabled { return 2; } // Core-only forwarding has no retained-source pause guarantee.
    let deadline = Instant::now() + budget;
    let mut state = match STATE.lock() { Ok(value) => value, Err(_) => return 3 };
    if state.lease != 0 && state.lease != lease { return 4; }
    state.lease = lease;
    while state.active != 0 {
        let left = deadline.saturating_duration_since(Instant::now());
        if left.is_zero() {
            state.lease = 0; CHANGED.notify_all(); return 5;
        }
        let result = CHANGED.wait_timeout(state, left);
        state = match result { Ok((value, _)) => value, Err(_) => return 3 };
    }
    0
}

fn release(lease: u64) -> i32 {
    let mut state = match STATE.lock() { Ok(value) => value, Err(_) => return 3 };
    if lease == 0 || (state.lease != 0 && state.lease != lease) { return 4; }
    state.lease = 0; CHANGED.notify_all(); 0
}

/// Only an explicit development opt-in can request a forwarding hold.
#[unsafe(no_mangle)]
pub extern "C" fn ifm_market_data_source_forwarding_hold_v1(lease: u64, budget_ms: u64) -> i32 {
    if std::env::var("IFM_SESSION_CAPTURE_BOUNDARY_ENABLED").ok().as_deref() != Some("1") { return 6; }
    hold(lease, Duration::from_millis(budget_ms), super::source_outbox::enabled())
}
/// Releases only the matching owner's forwarding hold; an absent hold is idempotent.
#[unsafe(no_mangle)]
pub extern "C" fn ifm_market_data_source_forwarding_release_v1(lease: u64) -> i32 { release(lease) }
/// Returns the current hold identity, zero if open, or the maximum value on poisoned state.
#[unsafe(no_mangle)]
pub extern "C" fn ifm_market_data_source_forwarding_lease_v1() -> u64 {
    STATE.lock().map(|value| value.lease).unwrap_or(u64::MAX)
}
/// Returns the number of admitted source sends still awaiting completion.
#[unsafe(no_mangle)]
pub extern "C" fn ifm_market_data_source_forwarding_active_v1() -> u64 {
    STATE.lock().map(|value| value.active).unwrap_or(u64::MAX)
}

/// Copies immutable source-prefix evidence while the exact forwarding lease is held.
/// # Safety
/// All non-null pointers must address the declared readable/writable lengths for this call.
#[unsafe(no_mangle)]
pub unsafe extern "C" fn ifm_market_data_source_prefix_v1(
    scope: *const u8, scope_length: u32, lease: u64, output: *mut u8, capacity: u32, required: *mut u32,
) -> i32 {
    if scope.is_null() || required.is_null() || scope_length == 0 || scope_length > 256 || capacity > 1024 * 1024 { return 1; }
    let scope = match std::str::from_utf8(unsafe { std::slice::from_raw_parts(scope, scope_length as usize) }) { Ok(value) => value, Err(_) => return 1 };
    let manifest = match crate::checkpoint::source_prefix_manifest(scope, lease) { Ok(value) => value, Err(_) => return 3 };
    if manifest.len() > 1024 * 1024 { return 4; }
    unsafe { *required = manifest.len() as u32; }
    if capacity < manifest.len() as u32 || output.is_null() { return 2; }
    unsafe { std::ptr::copy_nonoverlapping(manifest.as_ptr(), output, manifest.len()); }
    0
}

#[cfg(test)]
mod tests {
    use super::*;
    static SERIAL: Mutex<()> = Mutex::new(());
    #[test]
    fn hold_waits_for_actual_send_then_prevents_new_sends_without_stopping_capture() {
        let _serial = SERIAL.lock().unwrap(); let _ = release(701);
        let send = try_begin().unwrap();
        let worker = std::thread::spawn(|| hold(701, Duration::from_secs(1), true));
        for _ in 0..100 { if ifm_market_data_source_forwarding_lease_v1() == 701 { break; } std::thread::sleep(Duration::from_millis(1)); }
        assert!(try_begin().is_none()); assert!(!worker.is_finished());
        drop(send); assert_eq!(worker.join().unwrap(), 0);
        assert_eq!(ifm_market_data_source_forwarding_active_v1(), 0);
        assert_eq!(release(702), 4); assert!(try_begin().is_none());
        assert_eq!(release(701), 0); assert!(try_begin().is_some());
    }
    #[test]
    fn core_only_refuses_and_timed_out_hold_reopens_original_forwarder() {
        let _serial = SERIAL.lock().unwrap(); let _ = release(703);
        assert_eq!(hold(703, Duration::from_secs(1), false), 2);
        assert_eq!(ifm_market_data_source_forwarding_lease_v1(), 0);
        let send = try_begin().unwrap();
        assert_eq!(hold(703, Duration::from_millis(1), true), 5);
        assert_eq!(ifm_market_data_source_forwarding_lease_v1(), 0);
        drop(send); assert!(try_begin().is_some());
    }
}
