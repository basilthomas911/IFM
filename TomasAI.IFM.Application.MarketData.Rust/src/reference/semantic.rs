//! Existing PricingSemanticHash canonical JSON, streamed directly into SHA-256.
//! CLR decimal/double spelling, UTF-16 property ordering and the default JSON
//! encoder are contract identities. Generic serde serialization is not substituted.

use crate::option_chain::qualification::Decimal;
use sha2::{Digest, Sha256};

/// Typed semantic value; Decimal and String remain distinct even though their
/// MessagePack formatters both use a string representation.
#[derive(Clone)]
pub enum Value {
    /// Null reference or nullable value.
    Null,
    /// Boolean value.
    Bool(bool),
    /// Signed integer/enum.
    Integer(i64),
    /// Unsigned integer.
    Unsigned(u64),
    /// Exact CLR decimal bits.
    Decimal(Decimal),
    /// Exact binary64 value, formatted with the CLR round-trip convention.
    Double(f64),
    /// Text, including preformatted DateOnly/DateTimeOffset/Guid identities.
    String(String),
    /// Ordered collection.
    Array(Vec<Value>),
    /// Object properties, sorted by UTF-16 ordinal name during hashing.
    Object(Vec<(String, Value)>),
    /// Fixed-schema declaration order, used only by the existing Treasury curve
    /// digest v1 contract, which predates the sorted pricing hash convention.
    OrderedObject(Vec<(String, Value)>),
}
fn scientific(digits: &str, exponent: i32, negative: bool) -> String {
    let mut output = String::new();
    if negative {
        output.push('-');
    }
    output.push(char::from(digits.as_bytes()[0]));
    if digits.len() > 1 {
        output.push('.');
        output.push_str(&digits[1..]);
    }
    output.push('E');
    output.push(if exponent < 0 { '-' } else { '+' });
    output.push_str(&format!("{:02}", exponent.unsigned_abs()));
    output
}
fn fixed(digits: &str, point: i32, negative: bool) -> String {
    let mut output = String::new();
    if negative {
        output.push('-');
    }
    if point <= 0 {
        output.push_str("0.");
        output.push_str(&"0".repeat(point.unsigned_abs() as usize));
        output.push_str(digits);
    } else if point as usize >= digits.len() {
        output.push_str(digits);
        output.push_str(&"0".repeat(point as usize - digits.len()));
    } else {
        output.push_str(&digits[..point as usize]);
        output.push('.');
        output.push_str(&digits[point as usize..]);
    }
    output
}
/// Invariant CLR Decimal G29 used by the existing canonical hash converter.
///
/// # Errors
/// Rejects invalid decimal flags/scale before producing any digest identity.
pub fn decimal(value: Decimal) -> Result<String, &'static str> {
    let [lo, mid, hi, flags] = value.bits;
    let mut scale = (flags >> 16) & 255;
    if flags & 0x7f00ffff != 0 || scale > 28 {
        return Err("Invalid canonical decimal");
    }
    let mut coefficient = u128::from(lo) | (u128::from(mid) << 32) | (u128::from(hi) << 64);
    if coefficient == 0 {
        return Ok("0".into());
    }
    while scale > 0 && coefficient.is_multiple_of(10) {
        coefficient /= 10;
        scale -= 1;
    }
    let digits = coefficient.to_string();
    let exponent = digits.len() as i32 - scale as i32 - 1;
    Ok(if exponent < -4 {
        scientific(&digits, exponent, flags >> 31 != 0)
    } else {
        fixed(&digits, exponent + 1, flags >> 31 != 0)
    })
}
/// CLR round-trip binary64 spelling followed by current canonical normalization.
///
/// # Errors
/// Nonfinite values cannot become a valid JSON/hash number.
pub fn double(value: f64) -> Result<String, &'static str> {
    if !value.is_finite() {
        return Err("Nonfinite canonical number");
    }
    if value == 0.0 {
        return Ok("0".into());
    }
    let mut buffer = zmij::Buffer::new();
    let raw = buffer.format_finite(value);
    let negative = raw.starts_with('-');
    let raw = raw.strip_prefix('-').unwrap_or(raw);
    let (mantissa, exponent) = match raw.split_once(['e', 'E']) {
        Some((m, e)) => (
            m,
            e.parse::<i32>().map_err(|_| "Invalid canonical exponent")?,
        ),
        None => (raw, 0),
    };
    let point = mantissa.find('.').unwrap_or(mantissa.len()) as i32;
    let mut digits: String = mantissa.chars().filter(|c| *c != '.').collect();
    let leading = digits.bytes().take_while(|b| *b == b'0').count();
    let exponent = exponent + point - leading as i32 - 1;
    digits.drain(..leading);
    while digits.len() > 1 && digits.ends_with('0') {
        digits.pop();
    }
    Ok(if !(-4..17).contains(&exponent) {
        scientific(&digits, exponent, negative)
    } else {
        fixed(&digits, exponent + 1, negative)
    })
}
struct Writer<F> {
    put: F,
    written: usize,
    nodes: usize,
}
impl<F: FnMut(&[u8])> Writer<F> {
    fn bytes(&mut self, bytes: &[u8]) -> Result<(), &'static str> {
        self.written = self
            .written
            .checked_add(bytes.len())
            .filter(|n| *n <= 16 * 1024 * 1024)
            .ok_or("Canonical byte capacity exceeded")?;
        (self.put)(bytes);
        Ok(())
    }
    fn string(&mut self, text: &str) -> Result<(), &'static str> {
        self.bytes(b"\"")?;
        for ch in text.chars() {
            match ch {
                '\\' => self.bytes(b"\\\\")?,
                '\n' => self.bytes(b"\\n")?,
                '\r' => self.bytes(b"\\r")?,
                '\t' => self.bytes(b"\\t")?,
                '\u{8}' => self.bytes(b"\\b")?,
                '\u{c}' => self.bytes(b"\\f")?,
                ch if (' '..='~').contains(&ch)
                    && !matches!(ch, '"' | '\'' | '&' | '+' | '<' | '>' | '`') =>
                {
                    self.bytes(&[ch as u8])?
                }
                ch => {
                    let mut units = [0; 2];
                    for unit in ch.encode_utf16(&mut units) {
                        self.bytes(format!("\\u{unit:04X}").as_bytes())?;
                    }
                }
            }
        }
        self.bytes(b"\"")
    }
    fn value(&mut self, value: &Value, depth: usize) -> Result<(), &'static str> {
        self.nodes += 1;
        if depth > 64 || self.nodes > 65536 {
            return Err("Canonical structural capacity exceeded");
        }
        match value {
            Value::Null => self.bytes(b"null"),
            Value::Bool(value) => self.bytes(if *value { b"true" } else { b"false" }),
            Value::Integer(value) => self.bytes(value.to_string().as_bytes()),
            Value::Unsigned(value) => self.bytes(value.to_string().as_bytes()),
            Value::Decimal(value) => self.bytes(decimal(*value)?.as_bytes()),
            Value::Double(value) => self.bytes(double(*value)?.as_bytes()),
            Value::String(value) => self.string(value),
            Value::Array(items) => {
                self.bytes(b"[")?;
                for (i, item) in items.iter().enumerate() {
                    if i != 0 {
                        self.bytes(b",")?;
                    }
                    self.value(item, depth + 1)?;
                }
                self.bytes(b"]")
            }
            Value::Object(items) | Value::OrderedObject(items) => {
                if items.len() > 65536 {
                    return Err("Canonical object capacity exceeded");
                }
                let mut sorted: Vec<_> = items.iter().collect();
                if matches!(value, Value::Object(_)) {
                    sorted.sort_by(|a, b| a.0.encode_utf16().cmp(b.0.encode_utf16()));
                }
                let mut seen = std::collections::BTreeSet::new();
                if sorted.iter().any(|entry| !seen.insert(entry.0.as_str())) {
                    return Err("Duplicate canonical property");
                }
                self.bytes(b"{")?;
                for (i, (key, item)) in sorted.into_iter().enumerate() {
                    if i != 0 {
                        self.bytes(b",")?;
                    }
                    self.string(key)?;
                    self.bytes(b":")?;
                    self.value(item, depth + 1)?;
                }
                self.bytes(b"}")
            }
        }
    }
}
/// Streams canonical bytes into SHA-256 without allocating a complete JSON body.
///
/// # Errors
/// Invalid numbers or structural/byte bounds never return a usable digest.
pub fn compute(value: &Value) -> Result<String, &'static str> {
    let mut hash = Sha256::new();
    Writer {
        put: |bytes: &[u8]| hash.update(bytes),
        written: 0,
        nodes: 0,
    }
    .value(value, 0)?;
    Ok(format!("{:x}", hash.finalize()))
}
/// Captures bounded canonical JSON for differential qualification/cold diagnostics.
/// Production digest computation uses the streaming `compute` path instead.
///
/// # Errors
/// Rejects invalid values and output beyond sixteen MiB.
pub fn canonical(value: &Value) -> Result<Vec<u8>, &'static str> {
    let mut output = Vec::new();
    Writer {
        put: |bytes: &[u8]| output.extend_from_slice(bytes),
        written: 0,
        nodes: 0,
    }
    .value(value, 0)?;
    Ok(output)
}

/// Exact canonical UTC DateTimeOffset string used by pricing context hashes.
///
/// # Errors
/// Rejects timestamps outside CLR's supported range.
pub fn utc(ticks: i64) -> Result<Value, &'static str> {
    use chrono::{Datelike, Timelike};
    if !(-621355968000000000..=2534023007999999999).contains(&ticks) {
        return Err("Invalid semantic UTC time");
    }
    let at = chrono::DateTime::from_timestamp(
        ticks.div_euclid(10_000_000),
        (ticks.rem_euclid(10_000_000) * 100) as u32,
    )
    .ok_or("Invalid semantic UTC time")?;
    let mut result = format!(
        "{:04}-{:02}-{:02}T{:02}:{:02}:{:02}",
        at.year(),
        at.month(),
        at.day(),
        at.hour(),
        at.minute(),
        at.second()
    );
    let fraction = ticks.rem_euclid(10_000_000);
    if fraction != 0 {
        result.push('.');
        result.push_str(format!("{fraction:07}").trim_end_matches('0'));
    }
    result.push_str("+00:00");
    Ok(Value::String(result))
}
