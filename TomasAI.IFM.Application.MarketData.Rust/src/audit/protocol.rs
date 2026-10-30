//! Existing Function actor MessagePack contract, with bounded receipt decoding.
use crate::messaging::{codec, wire::guid};
use rmpv::Value;
use sha2::{Digest, Sha256};
/// Immutable outbox entry; retries preserve these exact JSON bytes and digest.
#[derive(Clone, Debug)]
pub struct Record {
    /// Native immutable JSON payload, bounded before durable write.
    pub payload: String,
    /// Exact UTF-8 content digest, lower-case hexadecimal.
    pub digest: String,
    /// Stable record identity.
    pub id: String,
    /// Incident identity.
    pub operation: String,
    /// Ordered fact number.
    pub sequence: u64,
}
impl Record {
    /// Validates retained files as rigorously as newly captured facts.
    /// # Errors
    /// Invalid JSON, identity, sequence, length or content digest.
    pub fn new(payload: String) -> Result<Self, String> {
        if payload.len() > 256 * 1024 {
            return Err("Recovery audit record exceeds capacity".into());
        }
        let value: serde_json::Value =
            serde_json::from_str(&payload).map_err(|_| "Invalid recovery audit JSON")?;
        let text = |name: &str| {
            value[name]
                .as_str()
                .map(str::to_owned)
                .ok_or_else(|| format!("Missing audit {name}"))
        };
        let id = text("RecordId")?;
        let operation = text("OperationId")?;
        fn valid(s: &str) -> bool {
            s.len() == 36
                && s.bytes().enumerate().all(|(i, b)| {
                    if [8, 13, 18, 23].contains(&i) {
                        b == b'-'
                    } else {
                        b.is_ascii_hexdigit()
                    }
                })
                && s != "00000000-0000-0000-0000-000000000000"
        }
        if !valid(&id) || !valid(&operation) || value["Version"].as_u64() != Some(1) {
            return Err("Invalid audit identity/version".into());
        }
        let sequence = value["Sequence"]
            .as_u64()
            .filter(|s| *s > 0)
            .ok_or("Invalid audit sequence")?;
        let digest = format!("{:x}", Sha256::digest(payload.as_bytes()));
        Ok(Self {
            payload,
            digest,
            id,
            operation,
            sequence,
        })
    }
    /// Fresh attempt envelope. Occurrence time is never substituted for attempt time.
    /// # Errors
    /// Invalid attempt clock or encoding failure.
    pub fn request(&self, at: i64) -> Result<(String, Vec<u8>), String> {
        let attempt = guid(rand::random::<u128>().max(1).to_le_bytes());
        let subject = format!(
            "Function.MarketDataFeedRecoveryRecordFunction.Record.{}",
            self.id
        );
        let command = Value::Array(vec![
            attempt.clone().into(),
            Value::Array(vec![
                8.into(),
                "MarketDataFeedRecoveryRecordFunction".into(),
                "Record".into(),
                self.id.clone().into(),
            ]),
            false.into(),
            Value::Array(vec![self.id.clone().into()]),
            5090.into(),
            17.into(),
            self.payload.clone().into(),
            self.digest.clone().into(),
            attempt.into(),
            codec::timestamp(at)?,
            codec::timestamp(at.saturating_add(1_200_000_000))?,
        ]);
        Ok((subject, codec::compress(codec::encode(&command)?)?))
    }
    /// Accepts only a successful Function completion matching all immutable identities.
    /// # Errors
    /// No responders, failed/partial/wrong receipt or malformed/bomb-sized framing.
    pub fn acknowledge(&self, bytes: &[u8]) -> Result<(), String> {
        let value = decode(bytes)?;
        let service = value
            .as_array()
            .filter(|v| v.len() == 4)
            .ok_or("Invalid audit service reply")?;
        if service[0] != Value::Boolean(true) {
            let failed=service[3].as_array().and_then(|result|result.get(1)).and_then(Value::as_array);
            let message=failed.and_then(|event|event.get(10)).and_then(Value::as_str)
                .or_else(||service[2].as_str()).unwrap_or("No failure detail");
            let detail=failed.and_then(|event|event.get(12)).and_then(Value::as_str).unwrap_or("");
            return Err(format!("Recovery audit storage rejected: {message}; {detail}"));
        }
        let result = service[3]
            .as_array()
            .filter(|v| v.len() == 2)
            .ok_or("Invalid audit function result")?;
        if !result[1].is_nil() {
            let failure = result[1].as_array();
            let message = failure
                .and_then(|v| v.get(10))
                .and_then(Value::as_str)
                .unwrap_or("No failure detail");
            let detail = failure
                .and_then(|v| v.get(12))
                .and_then(Value::as_str)
                .unwrap_or("");
            return Err(format!(
                "Recovery audit function failed: {message}; {detail}"
            ));
        }
        let stored = result[0]
            .as_array()
            .filter(|v| v.len() == 12)
            .ok_or("Invalid audit stored acknowledgement")?;
        if stored[3].as_i64().is_none_or(|event| event <= 0)
            || stored[1]
                .as_array()
                .is_none_or(|entity| entity.len() != 1 || entity[0].as_str() != Some(&self.id))
        {
            return Err("Audit acknowledgement lacks committed event/entity identity".into());
        }
        if stored[8].as_str() != Some(&self.operation)
            || stored[9].as_str() != Some(&self.id)
            || stored[10].as_str() != Some(&self.digest)
            || stored[11].as_u64() != Some(self.sequence)
        {
            return Err("Recovery audit receipt identity conflict".into());
        }
        Ok(())
    }
}
fn decode(bytes: &[u8]) -> Result<Value, String> {
    if bytes.len() > 8192 {
        return Err("Audit receipt exceeds capacity".into());
    }
    // Validate lengths before rmpv can allocate arrays/strings from hostile prefixes.
    fn scan(input: &mut &[u8], depth: u8) -> Result<(), String> {
        if depth > 12 || input.is_empty() {
            return Err("Invalid audit receipt nesting".into());
        }
        let marker = input[0];
        *input = &input[1..];
        fn take(input: &mut &[u8], n: usize) -> Result<(), String> {
            if n > input.len() {
                return Err("Truncated audit receipt".into());
            }
            *input = &input[n..];
            Ok(())
        }
        fn number(input: &mut &[u8], n: usize) -> Result<usize, String> {
            if n > input.len() {
                return Err("Truncated audit receipt length".into());
            }
            let mut value = 0usize;
            for b in &input[..n] {
                value = (value << 8) | usize::from(*b);
            }
            *input = &input[n..];
            Ok(value)
        }
        match marker {
            0x00..=0x7f | 0xc0 | 0xc2 | 0xc3 | 0xe0..=0xff => Ok(()),
            0xa0..=0xbf => take(input, usize::from(marker & 31)),
            0x90..=0x9f => {
                for _ in 0..marker & 15 {
                    scan(input, depth + 1)?;
                }
                Ok(())
            }
            0xdc | 0xdd => {
                let n = number(input, if marker == 0xdc { 2 } else { 4 })?;
                if n > 32 {
                    return Err("Audit receipt array capacity".into());
                }
                for _ in 0..n {
                    scan(input, depth + 1)?;
                }
                Ok(())
            }
            0xc4 | 0xd9 | 0xc5 | 0xda | 0xc6 | 0xdb => {
                let n = number(
                    input,
                    match marker {
                        0xc4 | 0xd9 => 1,
                        0xc5 | 0xda => 2,
                        _ => 4,
                    },
                )?;
                take(input, n)
            }
            0xc7..=0xc9 => {
                let n = number(
                    input,
                    match marker {
                        0xc7 => 1,
                        0xc8 => 2,
                        _ => 4,
                    },
                )?;
                take(input, n.checked_add(1).ok_or("Audit size overflow")?)
            }
            0xcc | 0xd0 => take(input, 1),
            0xcd | 0xd1 => take(input, 2),
            0xce | 0xd2 | 0xca => take(input, 4),
            0xcf | 0xd3 | 0xcb => take(input, 8),
            0xd4 => take(input, 2),
            0xd5 => take(input, 3),
            0xd6 => take(input, 5),
            0xd7 => take(input, 9),
            0xd8 => take(input, 17),
            _ => Err("Unsupported audit receipt marker".into()),
        }
    }
    let mut remaining = bytes;
    scan(&mut remaining, 0)?;
    if !remaining.is_empty() {
        return Err("Audit receipt trailing data".into());
    }
    let value = rmpv::decode::read_value(&mut std::io::Cursor::new(bytes))
        .map_err(|_| "Invalid audit receipt")?;
    if let Some(array) = value.as_array()
        && let Some(Value::Ext(98, lengths)) = array.first()
    {
        // MessagePack-CSharp may split even a small receipt across pooled
        // writer segments. Extension 98 contains one decoded length per block.
        // Bound both the block count and total output before allocating.
        if !(2..=32).contains(&array.len()) {
            return Err("Invalid audit compression".into());
        }
        let mut sizes = std::io::Cursor::new(lengths);
        let mut decoded_sizes = Vec::with_capacity(array.len() - 1);
        let mut total = 0usize;
        for block in &array[1..] {
            let length = rmp::decode::read_int::<u64, _>(&mut sizes)
                .ok().filter(|n| (1..=8192).contains(n))
                .ok_or("Audit decompression capacity")? as usize;
            total = total.checked_add(length).filter(|n| *n <= 8192)
                .ok_or("Audit decompression capacity")?;
            if block.as_slice().is_none() {return Err("Invalid audit compression bytes".into());}
            decoded_sizes.push(length);
        }
        if sizes.position() != lengths.len() as u64 {
            return Err("Audit compression length count mismatch".into());
        }
        let mut output = Vec::with_capacity(total);
        for (block, length) in array[1..].iter().zip(decoded_sizes) {
            let compressed = block.as_slice().ok_or("Invalid audit compression bytes")?;
            let decoded = lz4_flex::block::decompress(compressed, length)
                .map_err(|_| "Invalid audit compression body")?;
            if decoded.len() != length {return Err("Invalid audit compression length".into());}
            output.extend_from_slice(&decoded);
        }
        let mut remaining = output.as_slice();
        scan(&mut remaining, 0)?;
        if !remaining.is_empty() {
            return Err("Audit decompression trailing data".into());
        }
        return rmpv::decode::read_value(&mut std::io::Cursor::new(output))
            .map_err(|_| "Invalid audit decompressed receipt".into());
    }
    Ok(value)
}
