//! Scoped control admission, generation fencing and output atomicity without
//! provider I/O. The managed bridge separately qualifies actual SDK connections.
use ifm_market_data_native::ffi::{
    live::*, publication::NativePublication, runtime::*, scope_pricing::*, scopes::*,
};

#[test]
fn source_time_trade_layout_and_rejected_calls_preserve_all_outputs() {
    use ifm_market_data_native::ffi::scope_trades::*;
    assert_eq!(std::mem::size_of::<NativeOptionPublication>(), 816);
    assert_eq!(
        std::mem::offset_of!(NativeOptionPublication, underlying),
        696
    );
    assert_eq!(
        std::mem::offset_of!(NativeOptionPublication, manifest_offset),
        808
    );
    let scope = b"absent";
    let mut row = NativeOptionPublication {
        size: 777,
        flags: 123,
        ..Default::default()
    };
    let mut bytes = [99u8; 32];
    let mut count = 77;
    let mut written = 88;
    for (size, capacity, manifest_capacity) in [
        (815, 1, 32),
        (817, 1, 32),
        (816, 17, 32),
        (816, 1, 4 * 1024 * 1024 + 1),
        (816, 1, 32),
    ] {
        // SAFETY: Every invalid range is rejected before its pointer is read;
        // the last case uses an invalid handle with otherwise valid storage.
        unsafe {
            assert_ne!(
                ifm_market_data_scope_publications_v2(
                    0,
                    scope.as_ptr(),
                    6,
                    1,
                    0,
                    size,
                    &mut row,
                    capacity,
                    bytes.as_mut_ptr(),
                    manifest_capacity,
                    &mut count,
                    &mut written
                ),
                0
            );
        }
        assert_eq!((row.size, row.flags, count, written), (777, 123, 77, 88));
        assert_eq!(bytes, [99u8; 32]);
    }
    // SAFETY: Null output is explicitly rejected for a positive capacity.
    unsafe {
        assert_ne!(
            ifm_market_data_scope_publications_v2(
                0,
                scope.as_ptr(),
                6,
                1,
                0,
                816,
                std::ptr::null_mut(),
                1,
                bytes.as_mut_ptr(),
                32,
                &mut count,
                &mut written
            ),
            0
        );
    }
    assert_eq!((count, written), (77, 88));
}

unsafe fn completed(handle: u64, operation: u64) -> NativeControlResult {
    let mut completion = NativeCompletion::default();
    let mut count = 0;
    while count == 0 {
        let mut available = 0;
        // SAFETY: Exclusive aligned scalars/DTOs, live runtime throughout.
        unsafe {
            assert_eq!(ifm_market_data_runtime_wait_v1(handle, &mut available), 0);
            assert_eq!(available, 1);
            let mut signals = 0;
            assert_eq!(ifm_market_data_live_signals_v1(handle, &mut signals), 0);
            assert_eq!(
                ifm_market_data_runtime_drain_v1(handle, &mut completion, 1, &mut count),
                0
            );
        }
    }
    assert_eq!(completion.operation, operation);
    assert_eq!(completion.outcome, 0);
    let mut result = NativeControlResult::default();
    // SAFETY: Aligned exclusive result and matching retained operation.
    unsafe {
        assert_eq!(
            ifm_market_data_live_take_v1(handle, operation, 4112, &mut result),
            0
        );
    }
    result
}
#[test]
fn scopes_fence_old_generations_and_preserve_outputs_on_invalid_arguments() {
    let mut handle = 0;
    let mut low = 99;
    let mut high = 99;
    // SAFETY: Accepted ranges are bounded, aligned, exclusive and independent.
    // Deliberately invalid lengths/pointers are rejected before dereference.
    unsafe {
        assert_eq!(ifm_market_data_runtime_create_v1(1, 32, 2, &mut handle), 0);
        assert_eq!(
            ifm_market_data_scope_generation_v1(handle, &mut low, &mut high),
            0
        );
        assert_eq!((low, high), (0, 0));
        let configuration = NativeLiveConfiguration {
            size: 40,
            date: 20261026,
            identity_low: 1,
            capacity: 8,
            ..Default::default()
        };
        assert_eq!(ifm_market_data_live_configure_v1(handle, &configuration), 0);
        let json = b"{}";
        for length in 0..json.len() {
            assert_ne!(
                ifm_market_data_scope_submit_v1(
                    handle,
                    1,
                    4,
                    0,
                    0,
                    json.as_ptr(),
                    length as u32,
                    1000
                ),
                0
            );
        }
        assert_ne!(
            ifm_market_data_scope_submit_v1(handle, 1, 5, 0, 0, json.as_ptr(), 2, 1000),
            0
        );
        assert_eq!(
            ifm_market_data_scope_submit_v1(handle, 1, 4, 0, 0, json.as_ptr(), 2, 1000),
            0
        );
        assert_eq!(completed(handle, 1).failure, 0);
        assert_eq!(
            ifm_market_data_scope_generation_v1(handle, &mut low, &mut high),
            0
        );
        assert_ne!((low, high), (0, 0));
        let first = (low, high);
        assert_eq!(std::mem::size_of::<NativeOptionCalculation>(), 640);
        let id = b"absent";
        let mut calculation = NativeOptionCalculation {
            size: 999,
            flags: 999,
            values: [99.0; 7],
            ..Default::default()
        };
        for (instrument, risk, size, generation, length) in [
            (0, 0, 640, low, 6),
            (42, 2, 640, low, 6),
            (42, 0, 639, low, 6),
            (42, 0, 641, low, 6),
            (42, 0, 640, 0, 0),
            (42, 0, 640, low, 513),
        ] {
            assert_ne!(
                ifm_market_data_scope_calculation_v1(
                    handle,
                    id.as_ptr(),
                    length,
                    generation,
                    high,
                    instrument,
                    risk,
                    size,
                    &mut calculation
                ),
                0
            );
            assert_eq!(calculation.size, 999);
            assert_eq!(calculation.flags, 999);
            assert_eq!(calculation.values, [99.0; 7]);
        }
        assert_ne!(
            ifm_market_data_scope_calculation_v1(
                handle,
                id.as_ptr(),
                6,
                low,
                high,
                42,
                0,
                640,
                std::ptr::null_mut()
            ),
            0
        );
        assert_ne!(
            ifm_market_data_scope_calculation_v1(
                handle,
                [255u8].as_ptr(),
                1,
                low,
                high,
                42,
                0,
                640,
                &mut calculation
            ),
            0
        );
        assert_eq!(
            ifm_market_data_scope_calculation_v1(
                handle,
                id.as_ptr(),
                6,
                low,
                high,
                42,
                0,
                640,
                &mut calculation
            ),
            0
        );
        assert_eq!(calculation.size, 640);
        assert_eq!(calculation.flags, 0);
        assert_eq!(calculation.values, [0.0; 7]);
        let mut state = NativePublication {
            source: NativeMarketEvent {
                size: 999,
                ..Default::default()
            },
            size: 999,
            flags: 999,
            update: 999,
            reserved: 999,
            fields: [999; 64],
        };
        assert_ne!(
            ifm_market_data_scope_state_v1(handle, b"scope".as_ptr(), 5, 42, 3, 655, &mut state),
            0
        );
        assert_eq!(state.size, 999);
        assert_eq!(state.fields, [999; 64]);
        assert_ne!(
            ifm_market_data_scope_state_v1(handle, b"scope".as_ptr(), 513, 42, 3, 656, &mut state),
            0
        );
        assert_eq!(state.size, 999);
        assert_eq!(
            ifm_market_data_scope_state_v1(handle, b"scope".as_ptr(), 5, 42, 3, 656, &mut state),
            0
        );
        assert_eq!(state.flags, 0);
        assert_eq!(state.size, 656);
        let mut count = 999;
        assert_ne!(
            ifm_market_data_scope_publications_v1(
                handle,
                b"scope".as_ptr(),
                5,
                std::ptr::null_mut(),
                1,
                &mut count
            ),
            0
        );
        assert_eq!(count, 999);
        let mut failure = NativeControlResult::default();
        assert_eq!(
            ifm_market_data_scope_failure_v1(handle, std::ptr::null(), 0, 4112, &mut failure),
            0
        );
        assert_eq!(failure.failure, 0);
        // A late generation-zero stop must not retire the prepared owner.
        assert_eq!(
            ifm_market_data_scope_submit_v1(handle, 2, 3, 0, 0, json.as_ptr(), 2, 1000),
            0
        );
        assert_eq!(completed(handle, 2).failure, 0);
        assert_eq!(
            ifm_market_data_scope_submit_v1(handle, 3, 4, 0, 0, json.as_ptr(), 2, 1000),
            0
        );
        assert_eq!(completed(handle, 3).failure, 1);
        assert_eq!(
            ifm_market_data_scope_submit_v1(handle, 4, 4, low, high, json.as_ptr(), 2, 1000),
            0
        );
        assert_eq!(completed(handle, 4).failure, 0);
        assert_eq!(
            ifm_market_data_scope_generation_v1(handle, &mut low, &mut high),
            0
        );
        assert_ne!((low, high), first);
        // Synchronous signal needs no operation reservation, queue slot or
        // remaining deadline. Retired owners cannot cancel the replacement.
        assert_eq!(
            ifm_market_data_scope_stop_signal_v1(handle, first.0, first.1),
            0
        );
        assert_eq!(ifm_market_data_scope_stop_signal_v1(handle, low, high), 0);
        assert_eq!(ifm_market_data_scope_stop_signal_v1(handle, low, high), 0);
        assert_eq!(
            ifm_market_data_scope_submit_v1(handle, 5, 3, low, high, json.as_ptr(), 2, 1000),
            0
        );
        assert_eq!(completed(handle, 5).failure, 0);
        assert_eq!(ifm_market_data_runtime_stop_v1(handle), 0);
        loop {
            let mut available = 0;
            assert_eq!(ifm_market_data_runtime_wait_v1(handle, &mut available), 0);
            if available == 0 {
                break;
            }
            let mut signals = 0;
            assert_eq!(ifm_market_data_live_signals_v1(handle, &mut signals), 0);
        }
        assert_eq!(ifm_market_data_runtime_release_v1(handle), 0);
        assert_ne!(
            ifm_market_data_scope_generation_v1(handle, &mut low, &mut high),
            0
        );
    }
}
