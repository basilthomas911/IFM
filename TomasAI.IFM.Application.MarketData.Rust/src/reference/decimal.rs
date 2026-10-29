//! Exact CLR invariant decimal parsing for source reference percentages. This
//! preserves signed zero and round-to-nearest-even at the 96-bit/28-scale limit;
//! generic decimal FromStr commonly rounds halfway away from zero instead.
use crate::option_chain::qualification::Decimal;

/// CLR Decimal-to-Double operation order, including sign on a decimal zero.
/// This is the existing native quote conversion, shared by reference preparation.
pub fn to_double(value: Decimal) -> Option<f64> {
    let [lo, mid, hi, flags] = value.bits;
    let scale = (flags >> 16) & 255;
    if flags & 0x7f00ffff != 0 || scale > 28 {
        return None;
    }
    let low = u64::from(lo) | (u64::from(mid) << 32);
    let magnitude =
        (low as f64 + f64::from(hi) * 18446744073709551616.0) / (10u128.pow(scale) as f64);
    Some(if flags >> 31 != 0 {
        -magnitude
    } else {
        magnitude
    })
}

/// Exact CLR division by the Treasury percent/semiannual denominator, 200.
/// Original discarded digits provide half-even rounding at the 96-bit/scale-28
/// boundary; intermediate results are never rounded twice or converted to float.
pub fn divide_200(value: Decimal) -> Option<Decimal> {
    let [lo, mid, hi, flags] = value.bits;
    let scale = (flags >> 16) & 255;
    if flags & 0x7f00ffff != 0 || scale > 28 {
        return None;
    }
    let coefficient = u128::from(lo) | (u128::from(mid) << 32) | (u128::from(hi) << 64);
    let (coefficient, scale) = if coefficient.is_multiple_of(2) {
        (coefficient / 2, scale + 2)
    } else {
        (coefficient * 5, scale + 3)
    };
    for removed in scale.saturating_sub(28)..=scale {
        let factor = 10u128.pow(removed);
        let quotient = coefficient / factor;
        let remainder = coefficient % factor;
        let rounded = quotient
            + u128::from(
                removed != 0
                    && (remainder > factor / 2
                        || remainder == factor / 2 && !quotient.is_multiple_of(2)),
            );
        if rounded < 1u128 << 96 {
            let mut coefficient = rounded;
            let mut scale = scale - removed;
            while scale > 0 && coefficient.is_multiple_of(10) {
                coefficient /= 10;
                scale -= 1;
            }
            return Some(Decimal {
                bits: [
                    coefficient as u32,
                    (coefficient >> 32) as u32,
                    (coefficient >> 64) as u32,
                    (scale << 16) | (flags & 0x80000000),
                ],
            });
        }
    }
    None
}

/// Parses NumberStyles.AllowLeadingSign | AllowDecimalPoint, bounded to one MiB.
/// No allocation, floating conversion, thousands, exponent or implicit trimming.
/// Original discarded digits supply rounding sticky bits; reducing the scale
/// never re-rounds an already rounded coefficient. Invalid/overflow is None.
pub fn parse(value: &str) -> Option<Decimal> {
    if value.is_empty() || value.len() > 1_048_576 {
        return None;
    }
    let negative = value.starts_with('-');
    let unsigned = value.strip_prefix(['+', '-']).unwrap_or(value);
    if unsigned.is_empty()
        || unsigned.bytes().filter(|b| *b == b'.').count() > 1
        || unsigned.bytes().any(|b| !b.is_ascii_digit() && b != b'.')
        || !unsigned.bytes().any(|b| b.is_ascii_digit())
    {
        return None;
    }
    let fractional = unsigned.find('.').map_or(0, |dot| unsigned.len() - dot - 1);
    let significant = unsigned
        .bytes()
        .filter(|b| *b != b'.')
        .skip_while(|b| *b == b'0');
    let length = significant.clone().count();
    for scale in (0..=fractional.min(28)).rev() {
        let removed = fractional - scale;
        let mut coefficient = 0u128;
        if removed <= length {
            let keep = length - removed;
            if keep > 29 {
                continue;
            }
            for digit in significant.clone().take(keep) {
                coefficient = coefficient * 10 + u128::from(digit - b'0');
            }
            let mut remainder = significant.clone().skip(keep);
            if let Some(next) = remainder.next()
                && (next > b'5'
                    || next == b'5' && (coefficient & 1 != 0 || remainder.any(|b| b != b'0')))
            {
                coefficient += 1;
            }
        }
        if coefficient < (1u128 << 96) {
            return Some(Decimal {
                bits: [
                    coefficient as u32,
                    (coefficient >> 32) as u32,
                    (coefficient >> 64) as u32,
                    ((scale as u32) << 16) | if negative { 1 << 31 } else { 0 },
                ],
            });
        }
    }
    None
}

/// Parses a JSON decimal number, including exponent notation, without allocating
/// storage proportional to a hostile exponent. The input and normalized mantissa
/// remain bounded to one MiB plus 32 bytes. Underflow preserves CLR signed zero.
pub fn parse_json(value: &str) -> Option<Decimal> {
    if value.is_empty() || value.len() > 1_048_576 {
        return None;
    }
    // The JSON lexer rejects leading plus, leading zeroes, incomplete fractions
    // and non-number tokens before any exponent expansion can allocate memory.
    let raw: Box<serde_json::value::RawValue> = serde_json::from_str(value).ok()?;
    if raw.get() != value || !matches!(value.as_bytes()[0], b'-' | b'0'..=b'9') {
        return None;
    }
    let Some(index) = value.find(['e', 'E']) else {
        return parse(value);
    };
    let mantissa = &value[..index];
    let exponent = &value[index + 1..];
    let negative = exponent.starts_with('-');
    let digits = exponent.strip_prefix(['+', '-']).unwrap_or(exponent);
    if digits.is_empty() || !digits.bytes().all(|b| b.is_ascii_digit()) {
        return None;
    }
    let magnitude = digits
        .bytes()
        .fold(0i64, |n, b| (n * 10 + i64::from(b - b'0')).min(2_097_152));
    let exponent = if negative { -magnitude } else { magnitude };
    let sign = mantissa.starts_with('-');
    let unsigned = mantissa.strip_prefix('-').unwrap_or(mantissa);
    let fraction = unsigned.find('.').map_or(0, |i| unsigned.len() - i - 1) as i64;
    let scale = fraction - exponent;
    let coefficient: String = unsigned.chars().filter(|c| *c != '.').collect();
    let coefficient = coefficient.trim_start_matches('0');
    if coefficient.is_empty() || scale - coefficient.len() as i64 > 28 {
        return Some(Decimal {
            bits: [
                0,
                0,
                0,
                ((if coefficient.is_empty() && digits.parse::<i32>().is_err() {
                    0
                } else {
                    scale.clamp(0, 28) as u32
                }) << 16)
                    | if sign { 1 << 31 } else { 0 },
            ],
        });
    }
    if coefficient.len() as i64 - scale > 29 {
        return None;
    }
    let mut normalized = String::with_capacity(coefficient.len() + 32);
    if sign {
        normalized.push('-');
    }
    if scale <= 0 {
        normalized.push_str(coefficient);
        normalized.extend(std::iter::repeat_n('0', (-scale) as usize));
    } else if scale as usize >= coefficient.len() {
        normalized.push_str("0.");
        normalized.extend(std::iter::repeat_n('0', scale as usize - coefficient.len()));
        normalized.push_str(coefficient);
    } else {
        let split = coefficient.len() - scale as usize;
        normalized.push_str(&coefficient[..split]);
        normalized.push('.');
        normalized.push_str(&coefficient[split..]);
    }
    parse(&normalized)
}

/// CLR explicit Double-to-Decimal conversion: fifteen significant digits,
/// nearest-even rounding and the original 96-bit / scale-28 range. This is used
/// by strategy delta ranking; a binary float comparison changes boundary picks.
/// The conversion order follows the .NET runtime Decimal contract, qualified
/// independently against actual CLR conversion fixtures.
pub fn from_double(input: f64) -> Option<Decimal> {
    let zero = Decimal { bits: [0; 4] };
    let exponent = ((input.to_bits() >> 52) & 0x7ff) as i32 - 1022;
    if exponent < -94 {
        return Some(zero);
    }
    if exponent > 96 {
        return None;
    }
    let negative = input < 0.0;
    let mut value = input.abs();
    let mut scale = 14 - ((exponent * 19728) >> 16);
    let power = |n: u32| 10u128.pow(n) as f64;
    if scale >= 0 {
        scale = scale.min(28);
        value *= power(scale as u32);
    } else if scale != -1 || value >= 1e15 {
        value /= power((-scale) as u32);
    } else {
        scale = 0;
    }
    if value < 1e14 && scale < 28 {
        value *= 10.0;
        scale += 1;
    }
    let mut coefficient = value.round_ties_even() as u128;
    if coefficient == 0 {
        return Some(zero);
    }
    if scale < 0 {
        coefficient = coefficient.checked_mul(10u128.pow((-scale) as u32))?;
        scale = 0;
    } else {
        let mut remaining = scale.min(14);
        while remaining > 0 && coefficient.is_multiple_of(10) {
            coefficient /= 10;
            scale -= 1;
            remaining -= 1;
        }
    }
    if coefficient >= 1u128 << 96 {
        return None;
    }
    Some(Decimal {
        bits: [
            coefficient as u32,
            (coefficient >> 32) as u32,
            (coefficient >> 64) as u32,
            ((scale as u32) << 16) | if negative { 0x80000000 } else { 0 },
        ],
    })
}

/// Exact comparison units for provider-bounded strategy price/delta arithmetic.
/// No rounding occurs. Values outside signed 128-bit scale-28 representation
/// return absence; the provider's full signed nanounit price range fits it.
pub fn scaled_28(value: Decimal) -> Option<i128> {
    let [lo, mid, hi, flags] = value.bits;
    let scale = (flags >> 16) & 255;
    if flags & 0x7f00ffff != 0 || scale > 28 {
        return None;
    }
    let coefficient = i128::from(lo) | (i128::from(mid) << 32) | (i128::from(hi) << 64);
    let coefficient = coefficient.checked_mul(10i128.pow(28 - scale))?;
    Some(if flags >> 31 != 0 {
        -coefficient
    } else {
        coefficient
    })
}

/// Lossless legacy option-strike translation, matching the existing read model.
/// This deliberately uses shortest round-trip text rather than the fifteen-digit
/// explicit double-to-decimal cast used by candidate delta ranking.
///
/// # Errors
/// Nonpositive/nonfinite legacy values, conflicting exact values, overflow or a
/// decimal which cannot round-trip to the same source double are rejected.
pub fn exact_strike(legacy: f64, exact: Option<Decimal>) -> Result<Decimal, &'static str> {
    if !legacy.is_finite() || legacy <= 0.0 {
        return Err("Invalid legacy strike");
    }
    let amount = match exact {
        Some(value) => value,
        None => {
            let formatted = super::semantic::double(legacy)?;
            parse_json(&formatted)
                .ok_or("Legacy strike cannot be translated without changing its value")?
        }
    };
    if amount.compare(Decimal::default()) != Some(std::cmp::Ordering::Greater)
        || to_double(amount) != Some(legacy)
    {
        return Err("Decimal and legacy strike conflict");
    }
    Ok(amount)
}
