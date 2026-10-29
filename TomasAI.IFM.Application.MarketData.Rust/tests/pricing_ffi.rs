//! Actual asynchronous ABI result ownership, cancellation and capacity checks.
use ifm_market_data_native::ffi::{pricing::*, runtime::*};
fn request() -> NativePricingRequest {
    NativePricingRequest {
        size: 80,
        action: 1,
        flags: 1,
        profile: 0,
        forward: 6500.,
        strike: 6500.,
        time: 0.1,
        rate: 0.04,
        input: 0.2,
        steps: 64,
        iterations: 100,
        maximum_volatility: 4.,
        tolerance: 1e-8,
    }
}
fn create() -> u64 {
    let mut id = 0;
    assert_eq!(
        // SAFETY: Output is exclusive aligned stack storage for the full duration of this call.
        unsafe { ifm_market_data_runtime_create_v1(1, 32, 1, &mut id) },
        0
    );
    id
}
fn drain(id: u64, count: usize) -> Vec<NativeCompletion> {
    let mut result = Vec::new();
    while result.len() < count {
        let mut available = 0;
        assert_eq!(
            // SAFETY: Output is exclusive aligned stack storage for the full duration of this call.
            unsafe { ifm_market_data_runtime_wait_v1(id, &mut available) },
            0
        );
        assert_eq!(available, 1);
        let mut records = [NativeCompletion::default(); 256];
        let mut copied = 0;
        assert_eq!(
            // SAFETY: The 256-element output array and separate count are exclusive aligned stack storage.
            unsafe { ifm_market_data_runtime_drain_v1(id, records.as_mut_ptr(), 256, &mut copied) },
            0
        );
        result.extend_from_slice(&records[..copied as usize]);
    }
    result
}
#[test]
fn pricing_abi_layout_bounded_results_and_cancelled_unpolled_ownership() {
    assert_eq!(std::mem::size_of::<NativePricingRequest>(), 80);
    assert_eq!(std::mem::size_of::<NativePricingResult>(), 64);
    let id = create();
    let mut input = request();
    let mut result = NativePricingResult {
        size: 777,
        ..Default::default()
    };
    input.size = 79;
    assert_eq!(
        // SAFETY: Input is an aligned stack DTO, copied during this call, with no retained borrow.
        unsafe { ifm_market_data_submit_pricing_v1(id, 1, &input, 1000) },
        Status::AbiMismatch as i32
    );
    input.size = 80;
    for operation in 1..=256 {
        assert_eq!(
            // SAFETY: Input is an aligned stack DTO, copied during this call, with no retained borrow.
            unsafe { ifm_market_data_submit_pricing_v1(id, operation, &input, 0) },
            0
        );
    }
    assert_eq!(
        // SAFETY: Input is an aligned stack DTO, copied during this call, with no retained borrow.
        unsafe { ifm_market_data_submit_pricing_v1(id, 257, &input, 1000) },
        Status::Capacity as i32
    );
    let events = drain(id, 256);
    assert!(events.iter().all(|e| e.outcome == 2));
    // Draining notifications does not consume their separately retained payloads.
    assert_eq!(
        // SAFETY: Input is an aligned stack DTO, copied during this call, with no retained borrow.
        unsafe { ifm_market_data_submit_pricing_v1(id, 257, &input, 1000) },
        Status::Capacity as i32
    );
    assert_eq!(
        ifm_market_data_submit_treasury_v1(id, 1, 0.01, 1000),
        Status::InvalidInput as i32
    );
    assert_eq!(ifm_market_data_runtime_stop_v1(id), 0);
    assert_eq!(ifm_market_data_runtime_release_v1(id), Status::Busy as i32);
    assert_eq!(
        // SAFETY: Output is exclusive aligned stack storage; the deliberately wrong size must consume nothing.
        unsafe { ifm_market_data_take_pricing_v1(id, 1, 63, &mut result) },
        Status::AbiMismatch as i32
    );
    assert_eq!(result.size, 777);
    for operation in 1..=256 {
        assert_eq!(
            // SAFETY: Output is exclusive aligned stack storage; the deliberately wrong size must consume nothing.
            unsafe { ifm_market_data_take_pricing_v1(id, operation, 64, &mut result) },
            0
        );
        assert_eq!(result.failure, 9);
        assert_eq!(result.size, 64);
    }
    assert_eq!(
        // SAFETY: Output is exclusive aligned stack storage; the deliberately wrong size must consume nothing.
        unsafe { ifm_market_data_take_pricing_v1(id, 1, 64, &mut result) },
        Status::InvalidInput as i32
    );
    assert_eq!(ifm_market_data_runtime_release_v1(id), 0);
    for cycle in 0..40 {
        let id = create();
        let mut input = request();
        input.flags = 3;
        input.steps = 4096;
        input.action = 2;
        input.input = 80.;
        for op in 1..=8 {
            assert_eq!(
                // SAFETY: Input is an aligned stack DTO, copied during this call, with no retained borrow.
                unsafe { ifm_market_data_submit_pricing_v1(id, op, &input, 1000) },
                0
            );
        }
        assert_eq!(ifm_market_data_runtime_stop_v1(id), 0);
        for e in drain(id, 8) {
            assert!([0, 1, 2].contains(&e.outcome));
            assert_eq!(
                // SAFETY: Output is exclusive aligned stack storage; the deliberately wrong size must consume nothing.
                unsafe { ifm_market_data_take_pricing_v1(id, e.operation, 64, &mut result) },
                0
            );
        }
        assert_eq!(ifm_market_data_runtime_release_v1(id), 0, "cycle {cycle}");
    }
}
