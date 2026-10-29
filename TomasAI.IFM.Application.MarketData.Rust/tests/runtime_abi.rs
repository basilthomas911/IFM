//! Exercises the exported C ABI contract, including release and stale identities.

use ifm_market_data_native::ffi::{ABI_VERSION, runtime::*};

#[test]
fn layout_handles_capacity_output_validation_and_completion_delivery() {
    assert_eq!(std::mem::size_of::<NativeCompletion>(), 32);
    let mut handle = 123;
    // SAFETY: All output arguments here reference aligned, exclusive local storage.
    unsafe {
        assert_eq!(
            ifm_market_data_runtime_create_v1(99, 32, 1, &mut handle),
            Status::AbiMismatch as i32
        );
        assert_eq!(handle, 123);
        assert_eq!(
            ifm_market_data_runtime_create_v1(ABI_VERSION, 31, 1, &mut handle),
            Status::AbiMismatch as i32
        );
        assert_eq!(
            ifm_market_data_runtime_create_v1(ABI_VERSION, 32, 1, std::ptr::null_mut()),
            Status::InvalidInput as i32
        );
        assert_eq!(
            ifm_market_data_runtime_create_v1(ABI_VERSION, 32, 1, &mut handle),
            0
        );
    }
    assert_eq!(
        ifm_market_data_runtime_release_v1(handle),
        Status::Busy as i32
    );
    assert_eq!(
        ifm_market_data_submit_treasury_v1(handle, 0, 0.01, 1000),
        Status::InvalidInput as i32
    );
    assert_eq!(ifm_market_data_submit_treasury_v1(handle, 1, 0.01, 1000), 0);
    assert_eq!(
        ifm_market_data_submit_treasury_v1(handle, 1, 0.01, 1000),
        Status::InvalidInput as i32
    );
    let mut available = 9;
    let mut count = 77;
    let mut output = [NativeCompletion::default(); 8];
    // SAFETY: Buffers/count do not overlap and have the stated aligned capacities.
    unsafe {
        assert_eq!(ifm_market_data_runtime_wait_v1(handle, &mut available), 0);
        assert_eq!(available, 1);
        assert_eq!(
            ifm_market_data_runtime_drain_v1(handle, std::ptr::null_mut(), 1, &mut count),
            Status::InvalidInput as i32
        );
        assert_eq!(count, 77);
        assert_eq!(
            ifm_market_data_runtime_drain_v1(handle, output.as_mut_ptr(), 257, &mut count),
            Status::InvalidInput as i32
        );
        assert_eq!(
            ifm_market_data_runtime_drain_v1(handle, output.as_mut_ptr(), 8, &mut count),
            0
        );
    }
    assert_eq!(count, 1);
    assert_eq!(output[0].size, 32);
    assert_eq!(output[0].operation, 1);
    assert_eq!(output[0].outcome, 0);
    assert_eq!(output[0].value.to_bits(), (2.0 * 1.01_f64.ln()).to_bits());
    assert_eq!(ifm_market_data_runtime_stop_v1(handle), 0);
    // SAFETY: Exclusive local output, and the library remains loaded throughout.
    unsafe {
        assert_eq!(ifm_market_data_runtime_wait_v1(handle, &mut available), 0);
    }
    assert_eq!(available, 0);
    assert_eq!(ifm_market_data_runtime_release_v1(handle), 0);
    assert_eq!(
        ifm_market_data_runtime_stop_v1(handle),
        Status::InvalidHandle as i32
    );
    assert_eq!(
        ifm_market_data_submit_treasury_v1(handle, 2, 0.01, 1000),
        Status::InvalidHandle as i32
    );
    let mut handles = [0; 4];
    // SAFETY: Each output is aligned/exclusive. Native registry hard limit is four.
    unsafe {
        for id in &mut handles {
            assert_eq!(ifm_market_data_runtime_create_v1(ABI_VERSION, 32, 1, id), 0);
        }
        assert_eq!(
            ifm_market_data_runtime_create_v1(ABI_VERSION, 32, 1, &mut handle),
            Status::Capacity as i32
        );
    }
    for id in handles {
        assert_eq!(ifm_market_data_runtime_stop_v1(id), 0);
        assert_eq!(ifm_market_data_runtime_release_v1(id), 0);
    }
}
