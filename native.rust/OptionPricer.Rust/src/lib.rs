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
