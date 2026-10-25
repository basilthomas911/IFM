//! Exact C# wire adapters. Display coordinates may use floats; financial inputs may not.
use chrono::{Datelike, NaiveDate};
use rmpv::Value;

#[derive(Clone, Debug, Eq, PartialEq)]
pub struct ActorSubject {
    pub kind: ActorKind,
    pub name: String,
    pub verb: String,
    pub entity: String,
}
#[derive(Clone, Copy, Debug, Eq, PartialEq)]
pub enum ActorKind {
    Command = 2,
    Event = 3,
    Query = 4,
    Notify = 5,
    Realtime = 7,
    Function = 8,
}
impl ActorKind {
    pub fn name(self) -> &'static str {
        match self {
            Self::Command => "Command",
            Self::Event => "Event",
            Self::Query => "Query",
            Self::Notify => "Notify",
            Self::Realtime => "Realtime",
            Self::Function => "Function",
        }
    }
}
impl ActorSubject {
    pub fn query(name: &str, verb: &str, entity: &str) -> Self {
        Self {
            kind: ActorKind::Query,
            name: name.into(),
            verb: verb.into(),
            entity: entity.into(),
        }
    }
    pub fn route(&self) -> Result<String, String> {
        for (index, part) in [&self.name, &self.verb, &self.entity].iter().enumerate() {
            if part.is_empty()
                || part
                    .chars()
                    .any(|c| c.is_whitespace() || c == '*' || c == '>')
                || (index < 2 && part.contains('.'))
                || part.split('.').any(str::is_empty)
            {
                return Err("Invalid actor subject token".into());
            }
        }
        Ok(format!(
            "{}.{}.{}.{}",
            self.kind.name(),
            self.name,
            self.verb,
            self.entity
        ))
    }
    pub fn wire(&self) -> Value {
        Value::Array(vec![
            (self.kind as u8).into(),
            self.name.clone().into(),
            self.verb.clone().into(),
            self.entity.clone().into(),
        ])
    }
}

pub fn service_result(value: &Value) -> Result<&Value, String> {
    let parts = value.as_array().ok_or("ServiceResult must be an array")?;
    if parts.len() < 4 {
        return Err("Incomplete ServiceResult".into());
    }
    match parts[0].as_bool() {
        Some(true) => Ok(&parts[3]),
        Some(false) => Err(format!(
            "Backend error {}: {}",
            parts[1],
            parts[2].as_str().unwrap_or("Unspecified error")
        )),
        None => Err("Invalid ServiceResult.Success".into()),
    }
}

/// .NET decimal is encoded as an invariant string by MessagePack-CSharp.
/// Validate its 96-bit coefficient and 0..28 scale without floating conversion.
#[derive(Clone, Debug, Eq, PartialEq)]
pub struct ExactDecimal(String);
impl ExactDecimal {
    pub fn parse(text: &str) -> Result<Self, String> {
        let digits = text.strip_prefix('-').unwrap_or(text);
        if digits.is_empty() {
            return Err("Empty decimal".into());
        }
        let mut coefficient = 0_u128;
        let mut scale = None;
        for byte in digits.bytes() {
            if byte == b'.' && scale.is_none() {
                scale = Some(0);
                continue;
            }
            if !byte.is_ascii_digit() {
                return Err("Decimal must use invariant digits".into());
            }
            coefficient = coefficient
                .checked_mul(10)
                .and_then(|n| n.checked_add((byte - b'0') as u128))
                .ok_or("Decimal coefficient overflow")?;
            if let Some(s) = &mut scale {
                *s += 1;
            }
        }
        if digits.starts_with('.')
            || digits.ends_with('.')
            || scale.unwrap_or(0) > 28
            || coefficient > (1_u128 << 96) - 1
        {
            return Err("Value is outside .NET decimal range".into());
        }
        Ok(Self(text.into()))
    }
    pub fn as_str(&self) -> &str {
        &self.0
    }
    pub fn wire(&self) -> Value {
        self.0.clone().into()
    }
}

/// DateOnly is a .NET day number, not a machine-local date or UTC instant.
pub fn date_only(value: &Value) -> Result<NaiveDate, String> {
    let day = value
        .as_i64()
        .ok_or("DateOnly must be an integer day number")?;
    if !(0..=3652058).contains(&day) {
        return Err("Invalid .NET DateOnly day number".into());
    }
    NaiveDate::from_num_days_from_ce_opt(day as i32 + 1).ok_or_else(|| "Invalid date".into())
}
pub fn date_only_wire(value: NaiveDate) -> Value {
    (value.num_days_from_ce() - 1).into()
}

/// .NET round-trip display uses exactly seven fractional digits. Chrono's
/// strftime precision accepts 3/6/9, so do not use an unsupported %.7f pattern.
pub fn utc_roundtrip(time: chrono::DateTime<chrono::Utc>) -> String {
    format!(
        "{}.{:07}Z",
        time.format("%Y-%m-%dT%H:%M:%S"),
        time.timestamp_subsec_nanos() / 100
    )
}
/// Convert MessagePack timestamp extensions without losing subsecond precision.
pub fn timestamp(value: &Value) -> Result<chrono::DateTime<chrono::Utc>, String> {
    let Value::Ext(-1, data) = value else {
        return Err("Expected MessagePack timestamp".into());
    };
    let (seconds, nanos) = match data.len() {
        4 => (u32::from_be_bytes(data[..].try_into().unwrap()) as i64, 0),
        8 => {
            let packed = u64::from_be_bytes(data[..].try_into().unwrap());
            ((packed & 0x3ffffffff) as i64, (packed >> 34) as u32)
        }
        12 => (
            i64::from_be_bytes(data[4..].try_into().unwrap()),
            u32::from_be_bytes(data[..4].try_into().unwrap()),
        ),
        _ => return Err("Invalid timestamp length".into()),
    };
    if nanos >= 1_000_000_000 {
        return Err("Invalid timestamp nanoseconds".into());
    }
    chrono::DateTime::from_timestamp(seconds, nanos)
        .ok_or_else(|| "Timestamp outside supported range".into())
}

/// C# DateTimeOffset formatter writes [wall-clock timestamp, offset minutes].
pub fn datetime_offset(value: &Value) -> Result<chrono::DateTime<chrono::Utc>, String> {
    let values = value.as_array().ok_or("Expected DateTimeOffset array")?;
    if values.len() != 2 {
        return Err("Invalid DateTimeOffset length".into());
    }
    let minutes = values[1]
        .as_i64()
        .filter(|n| (-840..=840).contains(n))
        .ok_or("Invalid DateTimeOffset offset")?;
    timestamp(&values[0])?
        .checked_sub_signed(chrono::Duration::minutes(minutes))
        .ok_or_else(|| "DateTimeOffset overflow".into())
}

pub fn timestamp_wire(time: chrono::DateTime<chrono::Utc>) -> Value {
    let seconds = time.timestamp();
    let nanos = time.timestamp_subsec_nanos();
    let data = if nanos == 0 && (0..=u32::MAX as i64).contains(&seconds) {
        (seconds as u32).to_be_bytes().to_vec()
    } else if (0..(1_i64 << 34)).contains(&seconds) {
        (((nanos as u64) << 34) | seconds as u64)
            .to_be_bytes()
            .to_vec()
    } else {
        let mut data = nanos.to_be_bytes().to_vec();
        data.extend_from_slice(&seconds.to_be_bytes());
        data
    };
    Value::Ext(-1, data)
}

/// Exact seven-digit offset display for reference/provider and admin evidence.
pub fn reference_offset(value: &Value) -> Result<String, String> {
    if value.is_nil() {
        return Ok(String::new());
    }
    let parts = value.as_array().ok_or("Invalid reference timestamp")?;
    let minutes = parts
        .get(1)
        .and_then(Value::as_i64)
        .filter(|minutes| (-840..=840).contains(minutes))
        .ok_or("Invalid reference timestamp offset")?;
    let offset = chrono::FixedOffset::east_opt(
        i32::try_from(minutes * 60).map_err(|_| "Invalid reference timezone")?,
    )
    .ok_or("Invalid reference timezone")?;
    let time = datetime_offset(value)?.with_timezone(&offset);
    Ok(format!(
        "{}.{:07}{}",
        time.format("%Y-%m-%dT%H:%M:%S"),
        time.timestamp_subsec_nanos() / 100,
        time.format("%:z")
    ))
}
