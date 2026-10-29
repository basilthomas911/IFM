//! Exact managed decimal midpoint preparation for provider nanounit prices.
//! Avoids converting each side to binary64 before decimal half/add operations.
//! This mirrors (double)(bid/2m + ask/2m), including the resulting decimal scale.
fn half(raw: i64) -> (u128, u32) {
    let mut coefficient = raw as u128;
    let mut scale = 9;
    while scale > 0 && coefficient.is_multiple_of(10) {
        coefficient /= 10;
        scale -= 1;
    }
    if coefficient.is_multiple_of(2) {
        coefficient /= 2;
    } else {
        coefficient *= 5;
        scale += 1;
    }
    (coefficient, scale)
}
/// Prepares a positive noncrossed quote midpoint with the current C# decimal-to-
/// double operation order. Invalid/undefined sides return None, never zero risk.
pub fn midpoint(bid: i64, ask: i64) -> Option<f64> {
    if bid <= 0 || ask < bid {
        return None;
    }
    let (b, bs) = half(bid);
    let (a, as_) = half(ask);
    let scale = bs.max(as_);
    let coefficient = b * 10_u128.pow(scale - bs) + a * 10_u128.pow(scale - as_);
    // Decimal.ToDouble combines low64/high32 before scaling, rather than a
    // direct i128 float cast or separately rounded floating-point half-prices.
    let low = coefficient as u64;
    let high = (coefficient >> 64) as u32;
    Some((low as f64 + f64::from(high) * 18446744073709551616.0) / 10_u64.pow(scale) as f64)
}
