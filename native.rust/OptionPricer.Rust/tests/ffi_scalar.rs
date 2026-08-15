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

#[test]
fn implied_volatility_and_fused_exports_recover_the_input() {
    let mut market_price = 0.0;
    let mut volatility = f64::NAN;
    let mut fused = Black76ImpliedGreeksResultV1::default();

    unsafe {
        assert_eq!(
            ifm_black76_price_v1(5_300.0, 5_250.0, 0.045, 0.22, 0.75, 1, &mut market_price),
            OK
        );
        assert_eq!(
            ifm_black76_implied_volatility_v1(
                5_300.0,
                5_250.0,
                0.045,
                market_price,
                0.75,
                1,
                1e-10,
                100,
                0,
                0.0,
                &mut volatility
            ),
            OK
        );
        assert_eq!(
            ifm_black76_implied_volatility_with_greeks_v1(
                5_300.0,
                5_250.0,
                0.045,
                market_price,
                0.75,
                1,
                1e-10,
                100,
                1,
                0.30,
                &mut fused
            ),
            OK
        );
    }

    assert_close(volatility, 0.22, 1e-10);
    assert_close(fused.implied_volatility, 0.22, 1e-10);
    assert_close(fused.price, market_price, 1e-10);
}

#[test]
fn implied_volatility_non_convergence_leaves_output_unchanged() {
    let mut result = 123.0;
    let status = unsafe {
        ifm_black76_implied_volatility_v1(
            100.0,
            100.0,
            0.04,
            10.0,
            1.0,
            1,
            1e-10,
            0,
            0,
            0.0,
            &mut result,
        )
    };

    assert_eq!(status, NO_CONVERGENCE);
    assert_eq!(result, 123.0);
}

#[test]
fn batch_exports_match_scalar_exports_without_allocation_contract_changes() {
    let forwards = [5_300.0, 5_350.0, 5_250.0];
    let strikes = [5_300.0, 5_300.0, 5_300.0];
    let rates = [0.045, 0.045, 0.045];
    let volatilities = [0.18, 0.20, 0.22];
    let expiries = [0.25, 0.50, 0.75];
    let option_types = [1, -1, 0];
    let mut prices = [0.0; 3];
    let mut greeks = [Black76ResultV1::default(); 3];
    let mut error_index = 0;

    unsafe {
        assert_eq!(
            ifm_black76_price_batch_v1(
                forwards.as_ptr(),
                strikes.as_ptr(),
                rates.as_ptr(),
                volatilities.as_ptr(),
                expiries.as_ptr(),
                option_types.as_ptr(),
                forwards.len() as u32,
                prices.as_mut_ptr(),
                &mut error_index,
            ),
            OK
        );
        assert_eq!(error_index, u32::MAX);
        assert_eq!(
            ifm_black76_price_with_greeks_batch_v1(
                forwards.as_ptr(),
                strikes.as_ptr(),
                rates.as_ptr(),
                volatilities.as_ptr(),
                expiries.as_ptr(),
                option_types.as_ptr(),
                forwards.len() as u32,
                greeks.as_mut_ptr(),
                &mut error_index,
            ),
            OK
        );
    }

    assert_eq!(error_index, u32::MAX);
    for index in 0..forwards.len() {
        let mut scalar_price = 0.0;
        let mut scalar_greeks = Black76ResultV1::default();
        unsafe {
            assert_eq!(
                ifm_black76_price_v1(
                    forwards[index],
                    strikes[index],
                    rates[index],
                    volatilities[index],
                    expiries[index],
                    option_types[index],
                    &mut scalar_price,
                ),
                OK
            );
            assert_eq!(
                ifm_black76_price_with_greeks_v1(
                    forwards[index],
                    strikes[index],
                    rates[index],
                    volatilities[index],
                    expiries[index],
                    option_types[index],
                    &mut scalar_greeks,
                ),
                OK
            );
        }
        assert_eq!(prices[index], scalar_price);
        assert_eq!(greeks[index], scalar_greeks);
    }
}

#[test]
fn empty_batch_accepts_null_data_pointers_and_greeks_batch_stops_at_invalid_input() {
    let mut error_index = 0;
    unsafe {
        assert_eq!(
            ifm_black76_price_batch_v1(
                ptr::null(),
                ptr::null(),
                ptr::null(),
                ptr::null(),
                ptr::null(),
                ptr::null(),
                0,
                ptr::null_mut(),
                &mut error_index,
            ),
            OK
        );
    }
    assert_eq!(error_index, u32::MAX);

    let forwards = [100.0, 0.0, 110.0];
    let values = [100.0; 3];
    let option_types = [1; 3];
    let sentinel = Black76ResultV1 {
        price: 1.0,
        delta: 2.0,
        gamma: 3.0,
        vega: 4.0,
        theta: 5.0,
        rho: 6.0,
    };
    let mut results = [sentinel; 3];
    let status = unsafe {
        ifm_black76_price_with_greeks_batch_v1(
            forwards.as_ptr(),
            values.as_ptr(),
            values.as_ptr(),
            values.as_ptr(),
            values.as_ptr(),
            option_types.as_ptr(),
            forwards.len() as u32,
            results.as_mut_ptr(),
            &mut error_index,
        )
    };

    assert_eq!(status, INVALID_ARGUMENT);
    assert_eq!(error_index, 1);
    assert_ne!(results[0], sentinel);
    assert_eq!(results[1], sentinel);
    assert_eq!(results[2], sentinel);
}
