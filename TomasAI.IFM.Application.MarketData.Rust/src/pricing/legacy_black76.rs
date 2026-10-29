//! Exact operation order of the current selectable legacy Rust pricing kernel.
//! Kept separately because managed FMA sites change IEEE results.
use super::{Failure, black76::Black76ResultV1};
type Status = Failure;
const INVALID_ARGUMENT: Failure = Failure::InvalidInput;
// Keep the managed constant literal exactly; numerical parity takes precedence over replacing it.
#[allow(clippy::approx_constant)]
const INV_SQRT_2: f64 = 0.70710678118654752440084436210485;
const INV_SQRT_2_PI: f64 = 0.39894228040143267793994605993438;

#[inline(always)]
fn dotnet_max(left: f64, right: f64) -> f64 {
    if left.is_nan() || right.is_nan() {
        f64::NAN
    } else if left > right {
        left
    } else {
        right
    }
}

#[inline(always)]
fn intrinsic(forward_price: f64, strike_price: f64, option_type: i32) -> f64 {
    if option_type > 0 {
        dotnet_max(forward_price - strike_price, 0.0)
    } else {
        dotnet_max(strike_price - forward_price, 0.0)
    }
}

#[inline(always)]
fn erfc(value: f64) -> f64 {
    let z = value.abs();
    let t = 1.0 / (0.5 * z + 1.0);

    let mut polynomial = t * 0.17087277 - 0.82215223;
    polynomial = t * polynomial + 1.48851587;
    polynomial = t * polynomial - 1.13520398;
    polynomial = t * polynomial + 0.27886807;
    polynomial = t * polynomial - 0.18628806;
    polynomial = t * polynomial + 0.09678418;
    polynomial = t * polynomial + 0.37409196;
    polynomial = t * polynomial + 1.00002368;

    let exponent = -z * z + (-1.26551223 + t * polynomial);
    let answer = t * exponent.exp();
    if value >= 0.0 { answer } else { 2.0 - answer }
}

#[inline(always)]
fn norm_cdf(value: f64) -> f64 {
    0.5 * erfc(-value * INV_SQRT_2)
}

#[inline(always)]
pub(super) fn price(
    forward_price: f64,
    strike_price: f64,
    risk_free_rate: f64,
    volatility: f64,
    time_to_expiry: f64,
    option_type: i32,
) -> f64 {
    if time_to_expiry <= 0.0 {
        return intrinsic(forward_price, strike_price, option_type);
    }

    if volatility <= 0.0 {
        return (-risk_free_rate * time_to_expiry).exp()
            * intrinsic(forward_price, strike_price, option_type);
    }

    let sqrt_t = time_to_expiry.sqrt();
    let volatility_sqrt_t = volatility * sqrt_t;
    let inverse_volatility_sqrt_t = 1.0 / volatility_sqrt_t;
    let d1 =
        0.5 * volatility_sqrt_t + (forward_price / strike_price).ln() * inverse_volatility_sqrt_t;
    let d2 = d1 - volatility_sqrt_t;
    let discount_factor = (-risk_free_rate * time_to_expiry).exp();
    let call_price = discount_factor * (forward_price * norm_cdf(d1) - strike_price * norm_cdf(d2));

    if option_type > 0 {
        call_price
    } else {
        call_price - discount_factor * (forward_price - strike_price)
    }
}

#[inline]
pub(super) fn price_with_greeks(
    forward_price: f64,
    strike_price: f64,
    risk_free_rate: f64,
    volatility: f64,
    time_to_expiry: f64,
    option_type: i32,
) -> Result<Black76ResultV1, Status> {
    if forward_price <= 0.0 || strike_price <= 0.0 {
        return Err(INVALID_ARGUMENT);
    }

    if time_to_expiry <= 0.0 {
        let option_price = intrinsic(forward_price, strike_price, option_type);
        let delta = if option_type > 0 {
            if forward_price > strike_price {
                1.0
            } else {
                0.0
            }
        } else if forward_price < strike_price {
            -1.0
        } else {
            0.0
        };
        return Ok(Black76ResultV1 {
            price: option_price,
            delta,
            ..Black76ResultV1::default()
        });
    }

    if volatility <= 0.0 {
        let option_price = (-risk_free_rate * time_to_expiry).exp()
            * intrinsic(forward_price, strike_price, option_type);
        return Ok(Black76ResultV1 {
            price: option_price,
            rho: -time_to_expiry * option_price,
            ..Black76ResultV1::default()
        });
    }

    let sqrt_t = time_to_expiry.sqrt();
    let volatility_sqrt_t = volatility * sqrt_t;
    let inverse_volatility_sqrt_t = 1.0 / volatility_sqrt_t;
    let d1 =
        0.5 * volatility_sqrt_t + (forward_price / strike_price).ln() * inverse_volatility_sqrt_t;
    let d2 = d1 - volatility_sqrt_t;
    let rate_time = risk_free_rate * time_to_expiry;
    let discounted_density_d1 = INV_SQRT_2_PI * (-0.5 * d1 * d1 - rate_time).exp();
    let discount_factor = (-rate_time).exp();

    let (option_price, delta) = if option_type > 0 {
        let normal_d1 = norm_cdf(d1);
        let normal_d2 = norm_cdf(d2);
        (
            discount_factor * (forward_price * normal_d1 - strike_price * normal_d2),
            discount_factor * normal_d1,
        )
    } else {
        let negative_normal_d1 = norm_cdf(-d1);
        let negative_normal_d2 = norm_cdf(-d2);
        (
            discount_factor
                * (strike_price * negative_normal_d2 - forward_price * negative_normal_d1),
            -discount_factor * negative_normal_d1,
        )
    };

    let gamma = discounted_density_d1 * inverse_volatility_sqrt_t / forward_price;
    let vega = discounted_density_d1 * forward_price * sqrt_t;
    let theta = risk_free_rate * option_price - 0.5 * vega * volatility / time_to_expiry;
    let rho = -time_to_expiry * option_price;

    Ok(Black76ResultV1 {
        price: option_price,
        delta,
        gamma,
        vega,
        theta,
        rho,
    })
}
