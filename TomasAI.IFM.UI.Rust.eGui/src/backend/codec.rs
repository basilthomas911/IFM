//! MessagePack-CSharp 3.1.9 Lz4BlockArray (98) and Lz4Block (99) framing.
//! Bounds apply before decompression and values have a finite nesting limit.
use rmpv::Value;
use std::io::Cursor;

pub const MAX_MESSAGE: usize = 16 * 1024 * 1024;
const MAX_DEPTH: usize = 64;

fn read(bytes: &[u8]) -> Result<Value, String> {
    if bytes.len() > MAX_MESSAGE {
        return Err("Message exceeds 16 MiB".into());
    }
    let mut cursor = Cursor::new(bytes);
    let value = rmpv::decode::read_value_with_max_depth(&mut cursor, MAX_DEPTH)
        .map_err(|e| e.to_string())?;
    if cursor.position() != bytes.len() as u64 {
        return Err("Trailing MessagePack bytes".into());
    }
    Ok(value)
}

pub fn raw(value: &Value) -> Result<Vec<u8>, String> {
    let mut bytes = Vec::new();
    rmpv::encode::write_value(&mut bytes, value).map_err(|e| e.to_string())?;
    if bytes.len() > MAX_MESSAGE {
        return Err("Message exceeds 16 MiB".into());
    }
    Ok(bytes)
}

/// Same framing as C#, with one LZ4 block. Block boundaries need not match C#.
pub fn encode(value: &Value) -> Result<Vec<u8>, String> {
    let bytes = raw(value)?;
    if bytes.len() < 64 {
        return Ok(bytes);
    }
    let length = raw(&Value::from(bytes.len() as u64))?;
    raw(&Value::Array(vec![
        Value::Ext(98, length),
        Value::Binary(lz4_flex::block::compress(&bytes)),
    ]))
}

pub fn decode(bytes: &[u8]) -> Result<Value, String> {
    let value = read(bytes)?;
    let expanded = match &value {
        Value::Ext(99, data) => {
            let mut cursor = Cursor::new(data);
            let length = rmpv::decode::read_value(&mut cursor).map_err(|e| e.to_string())?;
            let length = checked_length(&length, MAX_MESSAGE)?;
            expand(&data[cursor.position() as usize..], length)?
        }
        Value::Array(parts) if matches!(parts.first(), Some(Value::Ext(98, _))) => {
            let Value::Ext(_, lengths) = &parts[0] else {
                unreachable!()
            };
            if parts.len() < 2 {
                return Err("Empty LZ4 block array".into());
            }
            let mut cursor = Cursor::new(lengths);
            let mut result = Vec::new();
            for part in &parts[1..] {
                let length = rmpv::decode::read_value(&mut cursor).map_err(|e| e.to_string())?;
                let length = checked_length(&length, MAX_MESSAGE - result.len())?;
                let Value::Binary(block) = part else {
                    return Err("LZ4 block must be binary".into());
                };
                result.extend_from_slice(&expand(block, length)?);
            }
            if cursor.position() != lengths.len() as u64 {
                return Err("LZ4 length count mismatch".into());
            }
            result
        }
        _ => return Ok(value),
    };
    // Compression applies once at the message boundary, not recursively to user extensions.
    read(&expanded)
}
fn checked_length(value: &Value, remaining: usize) -> Result<usize, String> {
    let length = value.as_u64().ok_or("Invalid decompressed length")?;
    if length > remaining as u64 {
        return Err("Decompressed message exceeds 16 MiB".into());
    }
    Ok(length as usize)
}
fn expand(bytes: &[u8], length: usize) -> Result<Vec<u8>, String> {
    let value = lz4_flex::block::decompress(bytes, length)
        .map_err(|e| format!("Invalid LZ4 block: {e}"))?;
    if value.len() != length {
        return Err("LZ4 expanded length mismatch".into());
    }
    Ok(value)
}
