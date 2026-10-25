//! Identity adapters are separate from transport compression and display formatting.
use serde_json::Value;
use sha2::{Digest, Sha256};

/// Match .NET Decimal's legacy double conversion, including its intermediate
/// rounding. Parsing decimal text as f64 is not bit-identical for every scale.
/// Source: dotnet/runtime v10.0.10 Decimal.DecCalc.VarR8FromDec (MIT):
/// https://github.com/dotnet/runtime/blob/v10.0.10/src/libraries/System.Private.CoreLib/src/System/Decimal.DecCalc.cs
/// This adapter serves compatibility fields only; exact decimal values remain
/// authoritative for identities, monetary amounts and canonical hashes.
pub fn decimal_to_legacy_double(text: &str) -> Result<f64, String> {
    super::contracts::ExactDecimal::parse(text)?;
    const POWERS: [f64; 29] = [
        1.0, 1e1, 1e2, 1e3, 1e4, 1e5, 1e6, 1e7, 1e8, 1e9, 1e10, 1e11, 1e12, 1e13, 1e14, 1e15, 1e16,
        1e17, 1e18, 1e19, 1e20, 1e21, 1e22, 1e23, 1e24, 1e25, 1e26, 1e27, 1e28,
    ];
    let digits = text.trim_start_matches('-');
    let scale = digits.split_once('.').map(|(_, f)| f.len()).unwrap_or(0);
    let coefficient = digits
        .bytes()
        .filter(|b| *b != b'.')
        .fold(0u128, |n, b| n * 10 + (b - b'0') as u128);
    let low = coefficient as u64;
    let high = (coefficient >> 64) as u32;
    let magnitude = (low as f64 + high as f64 * 18_446_744_073_709_552_000.0) / POWERS[scale];
    Ok(if text.starts_with('-') {
        -magnitude
    } else {
        magnitude
    })
}

/// Exact positive strike parsing used by contract maintenance, not a valuation.
/// IDs use canonical plain digits; the payload retains the representable input
/// scale, matching Decimal.TryParse without accepting rounded nonzero digits.
pub fn option_strike(text: &str) -> Result<(String, super::contracts::ExactDecimal), String> {
    if text.is_empty()
        || text.starts_with('.')
        || text.ends_with('.')
        || text.bytes().any(|c| !c.is_ascii_digit() && c != b'.')
        || text.bytes().filter(|c| *c == b'.').count() > 1
    {
        return Err("Enter an exact positive strike".into());
    }
    let (whole, fraction) = text.split_once('.').unwrap_or((text, ""));
    let whole = whole.trim_start_matches('0');
    let whole = if whole.is_empty() { "0" } else { whole };
    let trimmed = fraction.trim_end_matches('0');
    let canonical = if trimmed.is_empty() {
        whole.to_string()
    } else {
        format!("{whole}.{trimmed}")
    };
    let exact = super::contracts::ExactDecimal::parse(&canonical)?;
    let mut coefficient = canonical
        .bytes()
        .filter(|b| *b != b'.')
        .fold(0u128, |n, b| n * 10 + (b - b'0') as u128);
    if coefficient == 0 {
        return Err("Enter an exact positive strike".into());
    }
    let mut scale = trimmed.len();
    let target = fraction.len().min(28);
    while scale < target {
        let Some(next) = coefficient.checked_mul(10).filter(|n| *n < 1u128 << 96) else {
            break;
        };
        coefficient = next;
        scale += 1;
    }
    let divisor = 10u128.pow(scale as u32);
    let payload = if scale == 0 {
        coefficient.to_string()
    } else {
        format!(
            "{}.{:0width$}",
            coefficient / divisor,
            coefficient % divisor,
            width = scale
        )
    };
    Ok((
        exact.as_str().into(),
        super::contracts::ExactDecimal::parse(&payload)?,
    ))
}
pub fn option_contract_id(
    symbol: &str,
    expiry: chrono::NaiveDate,
    right: &str,
    strike: &str,
) -> Result<String, String> {
    use chrono::Datelike;
    if symbol.is_empty()
        || !symbol.bytes().all(|c| c.is_ascii_alphanumeric())
        || !(1..=9999).contains(&expiry.year())
        || expiry.to_string() == "0001-01-01"
        || expiry.to_string() == "9999-12-31"
    {
        return Err("A symbol, expiry, right and positive decimal strike are required".into());
    }
    let right = match right.as_bytes().first() {
        Some(b'C' | b'c') => 'C',
        Some(b'P' | b'p') => 'P',
        _ => return Err("Select Call or Put".into()),
    };
    let (strike, _) = option_strike(strike)?;
    Ok(format!(
        "{symbol}{}{right}{strike}",
        expiry.format("%Y%m%d")
    ))
}

/// SHA256(seed)[0..16] interpreted using .NET Guid(byte[]) byte order.
pub fn lifecycle_id(seed: &str) -> uuid::Uuid {
    guid_from_hash(seed.as_bytes())
}
/// Match IdempotentCommandId.Create using the caller's *uncompressed default*
/// MessagePack body. Do not pass LZ4 wire bytes here.
pub fn idempotent_command_id(
    key: uuid::Uuid,
    uncompressed_body: &[u8],
) -> Result<uuid::Uuid, String> {
    if key.is_nil() {
        return Err("IdempotencyKey is required".into());
    }
    let mut bytes = key.to_bytes_le().to_vec();
    bytes.extend_from_slice(uncompressed_body);
    Ok(guid_from_hash(&bytes))
}
fn guid_from_hash(bytes: &[u8]) -> uuid::Uuid {
    let hash = Sha256::digest(bytes);
    uuid::Uuid::from_bytes_le(hash[..16].try_into().unwrap())
}

/// FinancialCanonicalHash.Compute on a correctly shaped .NET JSON contract.
/// Wire decimal strings must become exact JSON numbers before calling this.
/// Type-specific default omission and DateTime normalization belong to adapters.
pub fn financial_hash(value: &Value) -> Result<String, String> {
    let text = canonical_json(value)?;
    Ok(format!("{:X}", Sha256::digest(text.as_bytes())))
}
pub fn canonical_json(value: &Value) -> Result<String, String> {
    let mut output = String::new();
    write_canonical(value, &mut output)?;
    Ok(output)
}
fn write_canonical(value: &Value, output: &mut String) -> Result<(), String> {
    match value {
        Value::Object(map) => {
            output.push('{');
            let mut properties: Vec<_> = map.iter().collect();
            properties.sort_by(|(a, _), (b, _)| a.encode_utf16().cmp(b.encode_utf16()));
            for (index, (name, value)) in properties.into_iter().enumerate() {
                if index != 0 {
                    output.push(',');
                }
                write_dotnet_string(name, output);
                output.push(':');
                write_canonical(value, output)?;
            }
            output.push('}');
        }
        Value::Array(values) => {
            output.push('[');
            for (index, value) in values.iter().enumerate() {
                if index != 0 {
                    output.push(',');
                }
                write_canonical(value, output)?;
            }
            output.push(']');
        }
        Value::Number(number) => output.push_str(&decimal_g29(&number.to_string())?),
        Value::String(text) => write_dotnet_string(text, output),
        Value::Bool(value) => output.push_str(if *value { "true" } else { "false" }),
        Value::Null => output.push_str("null"),
    }
    Ok(())
}
fn write_dotnet_string(text: &str, output: &mut String) {
    output.push('"');
    for unit in text.encode_utf16() {
        match unit {
            8 => output.push_str("\\b"),
            9 => output.push_str("\\t"),
            10 => output.push_str("\\n"),
            12 => output.push_str("\\f"),
            13 => output.push_str("\\r"),
            92 => output.push_str("\\\\"),
            0x20..=0x7e if ![34, 38, 39, 43, 60, 62, 96].contains(&unit) => {
                output.push(char::from_u32(unit as u32).unwrap())
            }
            _ => output.push_str(&format!("\\u{unit:04X}")),
        }
    }
    output.push('"');
}
/// Invariant .NET decimal G29: strip trailing zeroes and use E-05 notation below
/// 1e-4. Integer coefficients are limited to 96 bits; no f64 conversion occurs.
fn decimal_g29(text: &str) -> Result<String, String> {
    let negative = text.starts_with('-');
    let unsigned = text.strip_prefix('-').unwrap_or(text);
    let mut split = unsigned.split(['e', 'E']);
    let mantissa = split.next().unwrap();
    let exponent = split
        .next()
        .map(|s| s.parse::<i32>().map_err(|_| "Invalid decimal exponent"))
        .transpose()?
        .unwrap_or(0);
    if split.next().is_some() {
        return Err("Invalid decimal number".into());
    }
    let fraction = mantissa.split('.').nth(1).map_or(0, str::len) as i32;
    let mut digits = mantissa.replace('.', "");
    if digits.is_empty() || !digits.bytes().all(|b| b.is_ascii_digit()) {
        return Err("Invalid decimal digits".into());
    }
    let mut scale = fraction
        .checked_sub(exponent)
        .ok_or("Decimal scale overflow")?;
    while digits.starts_with('0') && digits.len() > 1 {
        digits.remove(0);
    }
    if digits.bytes().all(|b| b == b'0') {
        return Ok("0".into());
    }
    while scale > 0 && digits.ends_with('0') {
        digits.pop();
        scale -= 1;
    }
    if scale < 0 {
        if -scale > 29 {
            return Err("Decimal outside .NET range".into());
        }
        digits.extend(std::iter::repeat_n('0', -scale as usize));
        scale = 0;
    }
    let coefficient = digits
        .parse::<u128>()
        .map_err(|_| "Decimal coefficient overflow")?;
    if scale > 28 || coefficient > (1_u128 << 96) - 1 {
        return Err("Decimal outside .NET range".into());
    }
    let scientific_exponent = digits.len() as i32 - scale - 1;
    let sign = if negative { "-" } else { "" };
    if scientific_exponent < -4 {
        let tail = if digits.len() > 1 {
            format!(".{}", &digits[1..])
        } else {
            String::new()
        };
        return Ok(format!(
            "{sign}{}{tail}E-{:02}",
            &digits[..1],
            -scientific_exponent
        ));
    }
    if scale == 0 {
        return Ok(format!("{sign}{digits}"));
    }
    let point = digits.len() as i32 - scale;
    if point > 0 {
        digits.insert(point as usize, '.');
        Ok(format!("{sign}{digits}"))
    } else {
        Ok(format!("{sign}0.{}{digits}", "0".repeat(-point as usize)))
    }
}

/// Lossless compatibility translation of the typed option strike members.
pub fn exact_option_strike(value: &Value) -> Result<String, String> {
    let legacy = value["StrikePrice"]
        .as_f64()
        .filter(|v| v.is_finite() && *v > 0.0)
        .ok_or("Invalid legacy strike")?;
    let source = value["StrikePriceDecimal"]
        .as_str()
        .map(str::to_string)
        .unwrap_or_else(|| legacy.to_string());
    let (_, exact) = option_strike(&source)?;
    if decimal_to_legacy_double(exact.as_str())? != legacy {
        return Err("Decimal and legacy strike conflict".into());
    }
    Ok(exact.as_str().into())
}
