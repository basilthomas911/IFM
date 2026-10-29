//! Approved Treasury tenor selection and the continuous-rate numerical core.
//!
//! No clock or HTTP request occurs in these functions. Full source policy, curve
//! validation and decimal-to-ABI conversion remain separate required operations;
//! this core does not make an unqualified curve suitable for pricing.

/// Selects tenor months using the current C# trading-day boundaries.
pub fn select_tenor(remaining_trading_days: i32) -> Option<u16> {
    match remaining_trading_days {
        0..30 => Some(1),
        30..60 => Some(2),
        60..90 => Some(3),
        _ => None,
    }
}

/// Converts the already-qualified decimal-to-double half-yield into an annual rate.
///
/// `half_yield` is the annual CMT percentage divided by 200 using the application
/// decimal convention, then converted to binary64. A neighboring tenor or zero
/// must never be substituted for a missing value.
///
/// # Errors
/// Returns the existing C# failure code for a non-finite or out-of-domain result.
pub fn continuous_rate(half_yield: f64) -> Result<f64, &'static str> {
    if !half_yield.is_finite() || half_yield <= -1.0 {
        return Err("RateConversionDomainInvalid");
    }
    // The baseline .NET Double.LogP1 path rounds 1+x before its logarithm.
    // ln_1p is more stable for tiny x but does not reproduce those exact bits.
    let value = 2.0 * (1.0 + half_yield).ln();
    if value.is_finite() {
        Ok(value)
    } else {
        Err("RateConversionDomainInvalid")
    }
}
