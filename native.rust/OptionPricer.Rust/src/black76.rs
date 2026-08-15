use crate::abi::{
    Black76ImpliedGreeksResultV1, Black76ResultV1, INVALID_ARGUMENT, NO_CONVERGENCE, Status,
};

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
pub fn price(
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
pub fn price_with_greeks(
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

#[inline]
#[allow(clippy::too_many_arguments)]
pub fn implied_volatility(
    forward_price: f64,
    strike_price: f64,
    risk_free_rate: f64,
    market_price: f64,
    time_to_expiry: f64,
    option_type: i32,
    tolerance: f64,
    max_iterations: i32,
    initial_guess: Option<f64>,
) -> Result<f64, Status> {
    if forward_price <= 0.0 || strike_price <= 0.0 || market_price <= 0.0 || time_to_expiry <= 0.0 {
        return Err(INVALID_ARGUMENT);
    }

    if max_iterations <= 0 {
        return Err(NO_CONVERGENCE);
    }

    let mut sigma = initial_guess.unwrap_or(0.20);
    const VEGA_FLOOR: f64 = 1e-12;

    let sqrt_t = time_to_expiry.sqrt();
    let log_forward_strike = (forward_price / strike_price).ln();
    let rate_time = risk_free_rate * time_to_expiry;
    let discount_factor = (-rate_time).exp();
    let discounted_forward = discount_factor * forward_price;
    let discounted_strike = discount_factor * strike_price;
    let discounted_forward_minus_strike = discounted_forward - discounted_strike;
    let density_coefficient = INV_SQRT_2_PI * discount_factor;
    let forward_sqrt_t = forward_price * sqrt_t;

    for _ in 0..max_iterations {
        let volatility_sqrt_t = sigma * sqrt_t;
        let inverse_volatility_sqrt_t = 1.0 / volatility_sqrt_t;
        let d1 = 0.5 * volatility_sqrt_t + log_forward_strike * inverse_volatility_sqrt_t;
        let d2 = d1 - volatility_sqrt_t;

        let discounted_density_d1 = density_coefficient * (-0.5 * d1 * d1).exp();
        let call_price = discounted_forward * norm_cdf(d1) - discounted_strike * norm_cdf(d2);
        let option_price = if option_type > 0 {
            call_price
        } else {
            call_price - discounted_forward_minus_strike
        };
        let vega = discounted_density_d1 * forward_sqrt_t;
        let difference = option_price - market_price;

        if difference.abs() < tolerance {
            return Ok(sigma);
        }

        sigma -= difference / dotnet_max(vega.abs(), VEGA_FLOOR);
        if sigma <= 0.0 {
            sigma = 1e-6;
        }
    }

    Err(NO_CONVERGENCE)
}

#[inline]
#[allow(clippy::too_many_arguments)]
pub fn implied_volatility_with_greeks(
    forward_price: f64,
    strike_price: f64,
    risk_free_rate: f64,
    market_price: f64,
    time_to_expiry: f64,
    option_type: i32,
    tolerance: f64,
    max_iterations: i32,
    initial_guess: Option<f64>,
) -> Result<Black76ImpliedGreeksResultV1, Status> {
    let volatility = implied_volatility(
        forward_price,
        strike_price,
        risk_free_rate,
        market_price,
        time_to_expiry,
        option_type,
        tolerance,
        max_iterations,
        initial_guess,
    )?;
    let result = price_with_greeks(
        forward_price,
        strike_price,
        risk_free_rate,
        volatility,
        time_to_expiry,
        option_type,
    )?;

    Ok(Black76ImpliedGreeksResultV1 {
        implied_volatility: volatility,
        price: result.price,
        delta: result.delta,
        gamma: result.gamma,
        vega: result.vega,
        theta: result.theta,
        rho: result.rho,
    })
}

#[cfg(test)]
mod tests {
    use super::{implied_volatility, implied_volatility_with_greeks, price, price_with_greeks};
    use crate::abi::{INVALID_ARGUMENT, NO_CONVERGENCE};

    fn assert_close(actual: f64, expected: f64, tolerance: f64) {
        let scale = expected.abs().max(1.0);
        assert!(
            (actual - expected).abs() <= tolerance * scale,
            "actual {actual:.17} differs from expected {expected:.17}"
        );
    }

    #[test]
    fn scalar_price_matches_the_managed_at_the_money_reference() {
        let call = price(5_300.0, 5_300.0, 0.045, 0.18, 0.25, 1);
        let put = price(5_300.0, 5_300.0, 0.045, 0.18, 0.25, -1);

        assert_close(call, 188.10357386, 1e-10);
        assert_close(put, 188.10357386, 1e-10);
        assert_close(call, put, 1e-14);
    }

    #[test]
    fn scalar_greeks_match_the_managed_at_the_money_reference() {
        let call = price_with_greeks(5_300.0, 5_300.0, 0.045, 0.18, 0.25, 1).unwrap();
        let put = price_with_greeks(5_300.0, 5_300.0, 0.045, 0.18, 0.25, -1).unwrap();

        assert_close(call.price, 188.10357386, 1e-10);
        assert_close(call.delta, 0.51215214, 1e-8);
        assert_close(call.gamma, 0.00082616, 1e-8);
        assert_close(call.vega, 1_044.31232520, 1e-10);
        assert_close(call.theta, -367.48777625, 1e-10);
        assert_close(call.rho, -47.02589346, 1e-10);

        assert_close(put.price, 188.10357386, 1e-10);
        assert_close(put.delta, -0.47666090, 1e-8);
        assert_close(put.gamma, call.gamma, 1e-14);
        assert_close(put.vega, call.vega, 1e-14);
        assert_close(put.theta, call.theta, 1e-14);
        assert_close(put.rho, call.rho, 1e-14);
    }

    #[test]
    fn option_type_and_put_call_parity_match_the_managed_contract() {
        let call = price(100.0, 105.0, 0.04, 0.25, 1.0, 2);
        let put = price(100.0, 105.0, 0.04, 0.25, 1.0, 0);
        let parity = (-0.04_f64).exp() * (100.0 - 105.0);

        assert_eq!(call, price(100.0, 105.0, 0.04, 0.25, 1.0, 1));
        assert_eq!(put, price(100.0, 105.0, 0.04, 0.25, 1.0, -1));
        assert_close(call - put, parity, 1e-14);
    }

    #[test]
    fn expiry_and_non_positive_volatility_match_the_managed_edges() {
        assert_eq!(price(110.0, 100.0, 0.04, 0.25, 0.0, 1), 10.0);
        assert_eq!(price(90.0, 100.0, 0.04, 0.25, -1.0, -1), 10.0);

        let expected = (-0.08_f64).exp() * 10.0;
        let result = price_with_greeks(110.0, 100.0, 0.04, 0.0, 2.0, 1).unwrap();
        assert_eq!(result.price, expected);
        assert_eq!(result.delta, 0.0);
        assert_eq!(result.gamma, 0.0);
        assert_eq!(result.vega, 0.0);
        assert_eq!(result.theta, 0.0);
        assert_eq!(result.rho, -2.0 * expected);
    }

    #[test]
    fn price_with_greeks_rejects_only_non_positive_forward_or_strike() {
        assert_eq!(
            price_with_greeks(0.0, 100.0, 0.04, 0.25, 1.0, 1),
            Err(INVALID_ARGUMENT)
        );
        assert_eq!(
            price_with_greeks(100.0, -1.0, 0.04, 0.25, 1.0, 1),
            Err(INVALID_ARGUMENT)
        );

        let nan_result = price_with_greeks(f64::NAN, 100.0, 0.04, 0.25, 1.0, 1).unwrap();
        assert!(nan_result.price.is_nan());
    }

    #[test]
    fn scalar_price_preserves_unvalidated_ieee_754_behavior() {
        assert!(price(f64::NAN, 100.0, 0.04, 0.25, 1.0, 1).is_nan());
        assert_eq!(price(0.0, 100.0, 0.04, 0.25, 1.0, 1), 0.0);
        assert_close(
            price(0.0, 100.0, 0.04, 0.25, 1.0, -1),
            (-0.04_f64).exp() * 100.0,
            1e-14,
        );
    }

    #[test]
    fn implied_volatility_recovers_the_managed_call_and_put_inputs() {
        for (option_type, volatility) in [(1, 0.18), (-1, 0.24)] {
            let market_price = price(5_300.0, 5_250.0, 0.045, volatility, 0.75, option_type);
            let actual = implied_volatility(
                5_300.0,
                5_250.0,
                0.045,
                market_price,
                0.75,
                option_type,
                1e-10,
                100,
                None,
            )
            .unwrap();

            assert_close(actual, volatility, 1e-10);
        }
    }

    #[test]
    fn implied_volatility_preserves_validation_and_non_convergence() {
        assert_eq!(
            implied_volatility(0.0, 100.0, 0.04, 10.0, 1.0, 1, 1e-10, 100, None),
            Err(INVALID_ARGUMENT)
        );
        assert_eq!(
            implied_volatility(100.0, 100.0, 0.04, 10.0, 1.0, 1, 1e-10, 0, None),
            Err(NO_CONVERGENCE)
        );
    }

    #[test]
    fn fused_implied_volatility_and_greeks_matches_separate_operations() {
        let market_price = price(5_300.0, 5_350.0, 0.045, 0.21, 0.5, 1);
        let fused = implied_volatility_with_greeks(
            5_300.0,
            5_350.0,
            0.045,
            market_price,
            0.5,
            1,
            1e-10,
            100,
            None,
        )
        .unwrap();
        let separate =
            price_with_greeks(5_300.0, 5_350.0, 0.045, fused.implied_volatility, 0.5, 1).unwrap();

        assert_close(fused.implied_volatility, 0.21, 1e-10);
        assert_eq!(fused.price, separate.price);
        assert_eq!(fused.delta, separate.delta);
        assert_eq!(fused.gamma, separate.gamma);
        assert_eq!(fused.vega, separate.vega);
        assert_eq!(fused.theta, separate.theta);
        assert_eq!(fused.rho, separate.rho);
    }
}
