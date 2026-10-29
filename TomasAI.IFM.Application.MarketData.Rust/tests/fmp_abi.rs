//! Native FMP result ownership and rejection atomicity without network access.
use ifm_market_data_native::ffi::{fmp::*, live::*, reference::*, runtime::*};
unsafe fn finish(handle: u64, id: u64) {
    let mut count = 0;
    let mut completion = NativeCompletion::default();
    while count == 0 {
        let mut available = 0;
        // SAFETY: Independent aligned exclusive destinations and a live runtime.
        unsafe {
            assert_eq!(ifm_market_data_runtime_wait_v1(handle, &mut available), 0);
            let mut signals = 0;
            assert_eq!(ifm_market_data_live_signals_v1(handle, &mut signals), 0);
            assert_eq!(
                ifm_market_data_runtime_drain_v1(handle, &mut completion, 1, &mut count),
                0
            );
        }
    }
    assert_eq!(completion.operation, id);
    assert_eq!(completion.outcome, 0);
    let mut result = NativeControlResult::default();
    // SAFETY: Correct size, matching retained control operation, exclusive DTO.
    unsafe {
        assert_eq!(
            ifm_market_data_live_take_v1(handle, id, 4112, &mut result),
            0
        );
    }
    assert_eq!(
        result.failure,
        0,
        "{}",
        String::from_utf8_lossy(&result.message[..result.length as usize])
    );
}

#[test]
fn fmp_slots_are_bounded_and_failed_admission_does_not_leak_control() {
    let config=serde_json::to_vec(&serde_json::json!({"baseAddress":"http://127.0.0.1:9/","treasuryRatesEndpoint":"treasury-rates","economicCalendarEndpoint":"economic-calendar","latestTreasuryLookbackDays":14,"maximumProviderWindowDays":90,"maximumRequestRangeDays":3660,"maximumResponseBytes":4194304,"maximumNormalizedRows":10000,"maximumConcurrentRequests":2,"maximumRetryAttempts":0,"initialRetryDelayMs":0,"maximumRetryDelayMs":0,"circuitBreakerFailureThreshold":5,"circuitBreakerBreakDurationMs":30000,"totalOperationTimeoutMs":120000,"requestTimeoutMs":20000,"key":"fixture-secret","dns":["127.0.0.1:9"]})).unwrap();
    let query = br#"{"from":20261027,"to":20261026,"countries":[]}"#;
    let mut handle = 0;
    let mut bytes = [0xaau8; 1024];
    let mut length = 77;
    // SAFETY: All independent buffers/DTOs are bounded and valid for the call.
    // Rejected pointers/lengths are never dereferenced by the implementation.
    unsafe {
        assert_eq!(ifm_market_data_runtime_create_v1(1, 32, 2, &mut handle), 0);
        assert_eq!(
            ifm_market_data_fmp_configure_v1(handle, config.as_ptr(), config.len() as u32),
            1
        );
        let clock = NativeLiveConfiguration {
            size: 40,
            date: 20261026,
            identity_low: 1,
            capacity: 8,
            ..Default::default()
        };
        assert_eq!(ifm_market_data_live_configure_v1(handle, &clock), 0);
        assert_eq!(
            ifm_market_data_fmp_configure_v1(handle, std::ptr::null(), 16),
            1
        );
        assert_eq!(
            ifm_market_data_fmp_configure_v1(handle, config.as_ptr(), 16385),
            1
        );
        assert_eq!(
            ifm_market_data_fmp_configure_v1(handle, config.as_ptr(), config.len() as u32),
            0
        );
        assert_eq!(
            ifm_market_data_fmp_configure_v1(handle, config.as_ptr(), config.len() as u32),
            7
        );
        assert_eq!(
            ifm_market_data_fmp_submit_v1(handle, 1, 3, query.as_ptr(), query.len() as u32, 1000),
            1
        );
        assert_eq!(
            ifm_market_data_fmp_submit_v1(handle, 1, 0, std::ptr::null(), 10, 1000),
            1
        );
        for id in 1..=32 {
            assert_eq!(
                ifm_market_data_fmp_submit_v1(
                    handle,
                    id,
                    0,
                    query.as_ptr(),
                    query.len() as u32,
                    1000
                ),
                0
            );
            finish(handle, id);
        }
        assert_eq!(
            ifm_market_data_fmp_submit_v1(handle, 33, 0, query.as_ptr(), query.len() as u32, 1000),
            4
        );
        assert_eq!(
            ifm_market_data_reference_take_v1(handle, 1, bytes.as_mut_ptr(), 1, &mut length),
            4
        );
        assert_eq!(length, 77);
        assert!(bytes.iter().all(|b| *b == 0xaa));
        assert_eq!(
            ifm_market_data_reference_take_v1(handle, 1, bytes.as_mut_ptr(), 1024, &mut length),
            0
        );
        let result: serde_json::Value = serde_json::from_slice(&bytes[..length as usize]).unwrap();
        assert_eq!(result["failure"]["code"], "Validation");
        assert_eq!(
            ifm_market_data_fmp_submit_v1(handle, 33, 0, query.as_ptr(), query.len() as u32, 1000),
            0,
            "capacity rejection leaked the operation reservation"
        );
        finish(handle, 33);
        assert_eq!(ifm_market_data_runtime_stop_v1(handle), 0);
        let mut available = 99;
        assert_eq!(ifm_market_data_runtime_wait_v1(handle, &mut available), 0);
        assert_eq!(available, 0);
        assert_eq!(
            ifm_market_data_runtime_release_v1(handle),
            7,
            "retained reference results must prevent owner release"
        );
        for id in 1..=33 {
            assert_eq!(ifm_market_data_reference_discard_v1(handle, id), 0);
        }
        assert_eq!(ifm_market_data_runtime_release_v1(handle), 0);
    }
}
