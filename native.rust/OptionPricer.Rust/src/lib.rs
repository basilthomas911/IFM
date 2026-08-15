#![cfg_attr(not(windows), allow(dead_code))]
#![allow(clippy::missing_safety_doc)]

mod abi;
mod black76;

pub use abi::*;

#[cfg(not(windows))]
compile_error!("The current OptionPricer.Rust implementation targets Windows only");

use std::panic::{AssertUnwindSafe, catch_unwind};

fn ffi_status(operation: impl FnOnce() -> Status) -> Status {
    catch_unwind(AssertUnwindSafe(operation)).unwrap_or(PANIC)
}

#[unsafe(no_mangle)]
pub extern "C" fn ifm_option_pricer_get_abi_version() -> u32 {
    ABI_VERSION
}

#[unsafe(no_mangle)]
pub unsafe extern "C" fn ifm_black76_price_v1(
    forward_price: f64,
    strike_price: f64,
    risk_free_rate: f64,
    volatility: f64,
    time_to_expiry: f64,
    option_type: i32,
    result: *mut f64,
) -> Status {
    ffi_status(|| {
        if result.is_null() {
            return NULL_POINTER;
        }

        let option_price = black76::price(
            forward_price,
            strike_price,
            risk_free_rate,
            volatility,
            time_to_expiry,
            option_type,
        );
        unsafe {
            result.write(option_price);
        }
        OK
    })
}

#[unsafe(no_mangle)]
pub unsafe extern "C" fn ifm_black76_price_with_greeks_v1(
    forward_price: f64,
    strike_price: f64,
    risk_free_rate: f64,
    volatility: f64,
    time_to_expiry: f64,
    option_type: i32,
    result: *mut Black76ResultV1,
) -> Status {
    ffi_status(|| {
        if result.is_null() {
            return NULL_POINTER;
        }

        match black76::price_with_greeks(
            forward_price,
            strike_price,
            risk_free_rate,
            volatility,
            time_to_expiry,
            option_type,
        ) {
            Ok(greeks) => {
                unsafe {
                    result.write(greeks);
                }
                OK
            }
            Err(status) => status,
        }
    })
}

#[unsafe(no_mangle)]
#[allow(clippy::too_many_arguments)]
pub unsafe extern "C" fn ifm_black76_implied_volatility_v1(
    forward_price: f64,
    strike_price: f64,
    risk_free_rate: f64,
    market_price: f64,
    time_to_expiry: f64,
    option_type: i32,
    tolerance: f64,
    max_iterations: i32,
    has_initial_guess: i32,
    initial_guess: f64,
    result: *mut f64,
) -> Status {
    ffi_status(|| {
        if result.is_null() {
            return NULL_POINTER;
        }

        match black76::implied_volatility(
            forward_price,
            strike_price,
            risk_free_rate,
            market_price,
            time_to_expiry,
            option_type,
            tolerance,
            max_iterations,
            (has_initial_guess != 0).then_some(initial_guess),
        ) {
            Ok(volatility) => {
                unsafe {
                    result.write(volatility);
                }
                OK
            }
            Err(status) => status,
        }
    })
}

#[unsafe(no_mangle)]
#[allow(clippy::too_many_arguments)]
pub unsafe extern "C" fn ifm_black76_implied_volatility_with_greeks_v1(
    forward_price: f64,
    strike_price: f64,
    risk_free_rate: f64,
    market_price: f64,
    time_to_expiry: f64,
    option_type: i32,
    tolerance: f64,
    max_iterations: i32,
    has_initial_guess: i32,
    initial_guess: f64,
    result: *mut Black76ImpliedGreeksResultV1,
) -> Status {
    ffi_status(|| {
        if result.is_null() {
            return NULL_POINTER;
        }

        match black76::implied_volatility_with_greeks(
            forward_price,
            strike_price,
            risk_free_rate,
            market_price,
            time_to_expiry,
            option_type,
            tolerance,
            max_iterations,
            (has_initial_guess != 0).then_some(initial_guess),
        ) {
            Ok(greeks) => {
                unsafe {
                    result.write(greeks);
                }
                OK
            }
            Err(status) => status,
        }
    })
}

#[unsafe(no_mangle)]
#[allow(clippy::too_many_arguments)]
pub unsafe extern "C" fn ifm_black76_price_batch_v1(
    forward_prices: *const f64,
    strike_prices: *const f64,
    risk_free_rates: *const f64,
    volatilities: *const f64,
    times_to_expiry: *const f64,
    option_types: *const i32,
    count: u32,
    results: *mut f64,
    error_index: *mut u32,
) -> Status {
    ffi_status(|| {
        if error_index.is_null() {
            return NULL_POINTER;
        }
        unsafe {
            error_index.write(u32::MAX);
        }
        if count == 0 {
            return OK;
        }
        if forward_prices.is_null()
            || strike_prices.is_null()
            || risk_free_rates.is_null()
            || volatilities.is_null()
            || times_to_expiry.is_null()
            || option_types.is_null()
            || results.is_null()
        {
            return NULL_POINTER;
        }

        for index in 0..count as usize {
            let option_price = black76::price(
                unsafe { forward_prices.add(index).read() },
                unsafe { strike_prices.add(index).read() },
                unsafe { risk_free_rates.add(index).read() },
                unsafe { volatilities.add(index).read() },
                unsafe { times_to_expiry.add(index).read() },
                unsafe { option_types.add(index).read() },
            );
            unsafe {
                results.add(index).write(option_price);
            }
        }
        OK
    })
}

#[unsafe(no_mangle)]
#[allow(clippy::too_many_arguments)]
pub unsafe extern "C" fn ifm_black76_price_with_greeks_batch_v1(
    forward_prices: *const f64,
    strike_prices: *const f64,
    risk_free_rates: *const f64,
    volatilities: *const f64,
    times_to_expiry: *const f64,
    option_types: *const i32,
    count: u32,
    results: *mut Black76ResultV1,
    error_index: *mut u32,
) -> Status {
    ffi_status(|| {
        if error_index.is_null() {
            return NULL_POINTER;
        }
        unsafe {
            error_index.write(u32::MAX);
        }
        if count == 0 {
            return OK;
        }
        if forward_prices.is_null()
            || strike_prices.is_null()
            || risk_free_rates.is_null()
            || volatilities.is_null()
            || times_to_expiry.is_null()
            || option_types.is_null()
            || results.is_null()
        {
            return NULL_POINTER;
        }

        for index in 0..count as usize {
            match black76::price_with_greeks(
                unsafe { forward_prices.add(index).read() },
                unsafe { strike_prices.add(index).read() },
                unsafe { risk_free_rates.add(index).read() },
                unsafe { volatilities.add(index).read() },
                unsafe { times_to_expiry.add(index).read() },
                unsafe { option_types.add(index).read() },
            ) {
                Ok(greeks) => unsafe {
                    results.add(index).write(greeks);
                },
                Err(status) => {
                    unsafe {
                        error_index.write(index as u32);
                    }
                    return status;
                }
            }
        }
        OK
    })
}
