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
    let mut size = std::io::Cursor::new([0u8; 5]);
    rmp::encode::write_sint(&mut size, i64::from(length))
        .map_err(|_| "Event length encoding failed")?;
    let size = &size.get_ref()[..size.position() as usize];
    let compressed = lz4_flex::block::compress(&content);
    // The input is owned and no longer needed. Reuse its allocation for the wire frame.
    let mut output = content;
    output.clear();
    let encoded = (|| {
        rmp::encode::write_array_len(&mut output, 2)?;
        rmp::encode::write_ext_meta(&mut output, size.len() as u32, 98)?;
        output.extend_from_slice(size);
        rmp::encode::write_bin(&mut output, &compressed)
    })();
    encoded.map_err(|_| "Event compression framing failed")?;
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

/// Checks encoded expansion before source groups allocate any decompressed event graphs.
pub(crate) fn expanded_length(bytes:&[u8])->Result<usize,&'static str>{
    if bytes.len()>2*1024*1024{return Err("Source wire exceeds bound");}
    let value=rmpv::decode::read_value(&mut std::io::Cursor::new(bytes)).map_err(|_|"Invalid source MessagePack")?;
    if let Some(parts)=value.as_array()&&let Some(Value::Ext(98,sizes))=parts.first(){
        let mut reader=std::io::Cursor::new(sizes.as_slice());let mut total=0usize;let mut blocks=0;
        while reader.position()<sizes.len()as u64{let length=rmp::decode::read_int::<usize,_>(&mut reader).map_err(|_|"Invalid source compression length")?;total=total.checked_add(length).filter(|total|*total<=2*1024*1024).ok_or("Source expansion exceeds bound")?;blocks+=1;}
        if blocks+1!=parts.len(){return Err("Source compression count differs");}return Ok(total);
    }
    Ok(bytes.len())
}

/// Decodes bounded MessagePack-CSharp Lz4BlockArray framing, retaining original bytes elsewhere.
pub(crate) fn decode_compressed(bytes: &[u8]) -> Result<Value, &'static str> {
    if bytes.len() > 2 * 1024 * 1024 {
        return Err("Source wire exceeds bound");
    }
    let value = rmpv::decode::read_value(&mut std::io::Cursor::new(bytes))
        .map_err(|_| "Invalid source MessagePack")?;
    if let Some(parts) = value.as_array() {
        if let Some(Value::Ext(98, sizes)) = parts.first() {
            let mut lengths = std::io::Cursor::new(sizes.as_slice());
            let mut out = Vec::new();
            for block in &parts[1..] {
                let len = rmp::decode::read_int::<usize, _>(&mut lengths)
                    .map_err(|_| "Invalid source compression length")?;
                if len > 2 * 1024 * 1024 - out.len() {
                    return Err("Source decompression exceeds bound");
                }
                let compressed = block.as_slice().ok_or("Invalid source compressed block")?;
                let decoded = lz4_flex::block::decompress(compressed, len)
                    .map_err(|_| "Invalid source compressed bytes")?;
                out.extend_from_slice(&decoded);
            }
            if lengths.position() != sizes.len() as u64 {
                return Err("Unexpected source compression sizes");
            }
            return rmpv::decode::read_value(&mut std::io::Cursor::new(out))
                .map_err(|_| "Invalid source MessagePack content");
        }
    }
    Ok(value)
}
