//! ABI rejection must be atomic and leave caller storage/results untouched.
use ifm_market_data_native::ffi::{live::*, runtime::*};
#[test]
fn live_layout_validation_truncation_and_duplicate_configuration_are_atomic() {
    assert_eq!(std::mem::size_of::<NativeLiveConfiguration>(), 40);
    assert_eq!(std::mem::size_of::<NativeLaneStatus>(), 64);
    assert_eq!(std::mem::size_of::<NativeLast>(), 104);
    assert_eq!(std::mem::size_of::<NativeMarketEvent>(), 128);
    assert_eq!(std::mem::size_of::<NativeDiagnostic>(), 4128);
    assert_eq!(std::mem::size_of::<NativeControlResult>(), 4112);
    let mut handle = 0;
    let mut config = NativeLiveConfiguration {
        size: 40,
        date: 20261026,
        identity_low: 1,
        capacity: 2048,
        ..Default::default()
    };
    // SAFETY: All output/configuration pointers refer to aligned exclusive locals.
    unsafe {
        assert_eq!(ifm_market_data_runtime_create_v1(1, 32, 1, &mut handle), 0);
        for date in [0, 20260229, 20261301, 20261000, u32::MAX] {
            config.date = date;
            assert_eq!(ifm_market_data_live_configure_v1(handle, &config), 1);
        }
        config.date = 20261026;
        config.advancing = 2;
        assert_eq!(ifm_market_data_live_configure_v1(handle, &config), 1);
        config.advancing = 0;
        assert_eq!(ifm_market_data_live_configure_v1(handle, &config), 0);
        assert_eq!(ifm_market_data_live_configure_v1(handle, &config), 7);
    }
    let valid=serde_json::to_vec(&serde_json::json!({"key":"x".repeat(32),"host":"127.0.0.1","port":1,"dns":["127.0.0.1:1"],"evidence":"observations","subscriptions":[{"schema":"trades","symbology":"raw_symbol","symbols":["ESZ6"]}],"registrations":[{"instrument":42,"contract":"ES20261218C6000","source":"ESZ6","asset":"option","tick":null,"vwap":false,"required":true}]})).unwrap();
    for length in 0..valid.len() {
        // SAFETY: Slice is readable for every prefix length; no asynchronous borrow is retained.
        unsafe {
            assert_eq!(
                ifm_market_data_live_submit_v1(handle, 1, 0, 3, valid.as_ptr(), length as u32, 0),
                1
            );
        }
    }
    let mut output = NativeLast {
        size: 123,
        price: 999,
        ..Default::default()
    };
    let mut count = 33;
    // SAFETY: Deliberately invalid scalars/pointers are rejected before dereference.
    unsafe {
        assert_eq!(
            ifm_market_data_live_last_v1(handle, 3, 1, b"x".as_ptr(), 1, 95, &mut output),
            3
        );
        assert_eq!((output.size, output.price), (123, 999));
        assert_eq!(
            ifm_market_data_live_events_v1(handle, std::ptr::null_mut(), 65, &mut count),
            1
        );
        assert_eq!(count, 33);
        assert_eq!(
            ifm_market_data_live_submit_v1(
                handle,
                1,
                0,
                3,
                std::ptr::dangling(),
                2 * 1024 * 1024 + 1,
                0
            ),
            1
        );
    }
    assert_eq!(ifm_market_data_runtime_stop_v1(handle), 0);
    let mut available = 1;
    // SAFETY: Wait runs on this ordinary test thread with exclusive local output.
    unsafe {
        assert_eq!(ifm_market_data_runtime_wait_v1(handle, &mut available), 0);
    }
    assert_eq!(available, 0);
    assert_eq!(ifm_market_data_runtime_release_v1(handle), 0);
}
