//! Shared existing MessagePack-CSharp scalar and compression conventions.
//! Numerical values remain exact decimal strings; timestamps preserve CLR's
//! 100 ns resolution. No wall clock or local ValueDate is consulted here.

use rmpv::Value;

/// Encodes the inner numeric-key event contract. The current Host uses ordinary
/// MessagePack here; only the enclosing NATS envelope uses Lz4BlockArray.
///
/// # Errors
/// Returns encoding failure rather than publishing a truncated contract.
pub fn encode(value: &Value) -> Result<Vec<u8>, &'static str> {
    let mut content = Vec::new();
    rmpv::encode::write_value(&mut content, value).map_err(|_| "Event encoding failed")?;
    Ok(content)
}

/// Wraps complete MessagePack content in MessagePack-CSharp extension 98.
///
/// # Errors
/// Rejects content beyond the existing signed length representation.
pub fn compress(content: Vec<u8>) -> Result<Vec<u8>, &'static str> {
    if content.len() < 64 {
        return Ok(content);
    }
    let length = i32::try_from(content.len()).map_err(|_| "Event length exceeded")?;
    let mut size = Vec::new();
    rmpv::encode::write_value(&mut size, &Value::from(length))
        .map_err(|_| "Event length encoding failed")?;
    let value = Value::Array(vec![
        Value::Ext(98, size),
        Value::Binary(lz4_flex::block::compress(&content)),
    ]);
    let mut output = Vec::new();
    rmpv::encode::write_value(&mut output, &value)
        .map_err(|_| "Event compression framing failed")?;
    Ok(output)
}

/// Exact invariant decimal string, retaining the explicitly supplied scale.
///
/// # Errors
/// Rejects coefficients/scales outside CLR Decimal's representation.
pub fn decimal(coefficient: i128, scale: u32) -> Result<Value, &'static str> {
    if scale > 28 || coefficient.unsigned_abs() >= 1u128 << 96 {
        return Err("Event decimal outside CLR range");
    }
    let negative = coefficient < 0;
    let mut digits = coefficient.unsigned_abs().to_string();
    if scale != 0 {
        let count = scale as usize;
        if digits.len() <= count {
            digits = format!("{}{}", "0".repeat(count + 1 - digits.len()), digits);
        }
        digits.insert(digits.len() - count, '.');
    }
    if negative {
        digits.insert(0, '-');
    }
    Ok(digits.into())
}

/// Provider nanounits divided by one billion, as CLR Decimal division formats it.
pub fn price(value: i64) -> Value {
    let mut coefficient = i128::from(value);
    let mut scale = 9;
    while scale != 0 && coefficient % 10 == 0 {
        coefficient /= 10;
        scale -= 1;
    }
    // i64 / 1e9 always fits a 96-bit coefficient and scale 0..9.
    decimal(coefficient, scale).unwrap_or(Value::Nil)
}

/// Existing Decimal formatter string for exact imported CLR coefficient/scale.
///
/// # Errors
/// Rejects malformed flags or scale instead of silently changing financial data.
pub fn decimal_bits(
    value: crate::option_chain::qualification::Decimal,
) -> Result<Value, &'static str> {
    let [lo, mid, hi, flags] = value.bits;
    let scale = (flags >> 16) & 255;
    if flags & 0x7f00ffff != 0 || scale > 28 {
        return Err("Invalid event decimal flags");
    }
    let coefficient = i128::from(lo) | (i128::from(mid) << 32) | (i128::from(hi) << 64);
    decimal(
        if flags >> 31 != 0 {
            -coefficient
        } else {
            coefficient
        },
        scale,
    )
}

/// MessagePack timestamp extension for UTC ticks since the Unix epoch.
///
/// # Errors
/// Rejects times outside CLR DateTime's supported years 1..9999.
pub fn timestamp(ticks: i64) -> Result<Value, &'static str> {
    if !(-621_355_968_000_000_000..=2_534_023_007_999_999_999).contains(&ticks) {
        return Err("Event timestamp outside CLR range");
    }
    let seconds = ticks.div_euclid(10_000_000);
    let nanos = (ticks.rem_euclid(10_000_000) * 100) as u32;
    let bytes = if (0..1i64 << 34).contains(&seconds) {
        if nanos == 0 && seconds <= i64::from(u32::MAX) {
            (seconds as u32).to_be_bytes().to_vec()
        } else {
            ((u64::from(nanos) << 34) | seconds as u64)
                .to_be_bytes()
                .to_vec()
        }
    } else {
        let mut bytes = nanos.to_be_bytes().to_vec();
        bytes.extend_from_slice(&seconds.to_be_bytes());
        bytes
    };
    Ok(Value::Ext(-1, bytes))
}

/// Existing DateTimeOffset formatter's [UTC timestamp, zero offset] contract.
///
/// # Errors
/// Rejects invalid CLR timestamps.
pub fn utc_offset(ticks: i64) -> Result<Value, &'static str> {
    Ok(Value::Array(vec![timestamp(ticks)?, 0.into()]))
}
