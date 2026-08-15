use std::ptr;

use ifm_option_pricer_native::*;

fn assert_close(actual: f64, expected: f64, tolerance: f64) {
    let scale = expected.abs().max(1.0);
    assert!(
        (actual - expected).abs() <= tolerance * scale,
        "actual {actual:.17} differs from expected {expected:.17}"
    );
}

#[test]
fn abi_version_and_null_pointer_status_match_the_frozen_contract() {
    assert_eq!(ifm_option_pricer_get_abi_version(), 1);
    unsafe {
        assert_eq!(
            ifm_black76_price_v1(100.0, 100.0, 0.04, 0.2, 1.0, 1, ptr::null_mut()),
            NULL_POINTER
        );
        assert_eq!(
            ifm_black76_price_with_greeks_v1(100.0, 100.0, 0.04, 0.2, 1.0, 1, ptr::null_mut()),
            NULL_POINTER
        );
    }
}

#[test]
fn scalar_exports_write_results_on_success() {
    let mut price = f64::NAN;
    let mut greeks = Black76ResultV1::default();

    unsafe {
        assert_eq!(
            ifm_black76_price_v1(5_300.0, 5_300.0, 0.045, 0.18, 0.25, 1, &mut price),
            OK
        );
        assert_eq!(
            ifm_black76_price_with_greeks_v1(5_300.0, 5_300.0, 0.045, 0.18, 0.25, 1, &mut greeks),
            OK
        );
    }

    assert_close(price, 188.10357386, 1e-10);
    assert_close(greeks.price, price, 1e-14);
    assert_close(greeks.delta, 0.51215214, 1e-8);
}

#[test]
fn invalid_greeks_input_does_not_overwrite_the_output() {
    let sentinel = Black76ResultV1 {
        price: 1.0,
        delta: 2.0,
        gamma: 3.0,
        vega: 4.0,
        theta: 5.0,
        rho: 6.0,
    };
    let mut result = sentinel;

    let status =
        unsafe { ifm_black76_price_with_greeks_v1(0.0, 100.0, 0.04, 0.2, 1.0, 1, &mut result) };

    assert_eq!(status, INVALID_ARGUMENT);
    assert_eq!(result, sentinel);
}
