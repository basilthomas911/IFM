//! ABI v5 incremental publication transport. Binary row documents retain their MessagePack representation.
use rmpv::Value;
use serde::Deserialize;
use std::io::Cursor;

#[derive(Clone, Debug, Deserialize, PartialEq, Eq)]
struct Path {
    kind: u8,
    #[serde(default)]
    key: String,
    #[serde(default)]
    index: usize,
}
#[derive(Debug, Deserialize)]
struct Operation {
    path: Vec<Path>,
    action: u8,
    #[serde(default)]
    value: Option<serde_bytes::ByteBuf>,
    #[serde(default)]
    index: usize,
    #[serde(rename = "delete", default)]
    delete_count: usize,
    #[serde(default)]
    items: Option<Vec<serde_bytes::ByteBuf>>,
}
#[derive(Debug, Deserialize)]
struct Patch {
    #[serde(rename = "baseSequence")]
    base_sequence: u64,
    sequence: u64,
    operations: Vec<Operation>,
}
#[derive(Clone, Copy, Default, Debug)]
pub struct Stamp {
    pub kind: u32,
    pub sequence: u64,
    pub source_ticks: i64,
    pub produced_ticks: i64,
    pub received_ticks: i64,
}
#[derive(Default)]
struct Slot {
    sequence: u64,
    bytes: Vec<u8>,
    tree: Option<Value>,
    pending: bool,
    stamp: Stamp,
}
#[derive(Default)]
pub struct TransferCache {
    slots: [Slot; 9],
    pub scratch: Vec<u8>,
}
impl TransferCache {
    pub fn sequence(&self, kind: u32) -> u64 {
        self.slots[kind as usize].sequence
    }
    pub fn reset(&mut self, kind: u32) {
        self.slots[kind as usize].sequence = 0;
    }
    pub fn pending(&self, kind: u32) -> bool {
        self.slots[kind as usize].pending
    }
    pub fn bytes(&self, kind: u32) -> &[u8] {
        &self.slots[kind as usize].bytes
    }
    pub fn stamp(&self, kind: u32) -> Stamp {
        self.slots[kind as usize].stamp
    }
    pub fn acknowledge(&mut self, kind: u32) {
        self.slots[kind as usize].pending = false;
    }
    pub fn legacy(&mut self, kind: u32, bytes: Vec<u8>) -> bool {
        let slot = &mut self.slots[kind as usize];
        if slot.bytes != bytes {
            slot.bytes = bytes;
            slot.sequence += 1;
            slot.pending = true;
            slot.tree = None;
        }
        slot.pending
    }
    pub fn complete(
        &mut self,
        kind: u32,
        mode: i32,
        sequence: u64,
        produced_ticks: i64,
        source_ticks: i64,
    ) -> Result<(), String> {
        let payload = std::mem::take(&mut self.scratch);
        let slot = &mut self.slots[kind as usize];
        let result = (|| {
            if mode == 3 {
                let patch: Patch = crate::wire::decode(&payload)?;
                if patch.base_sequence != slot.sequence || patch.sequence != sequence {
                    return Err("Publication patch base mismatch".into());
                }
                let mut next = match slot.tree.take() {
                    Some(value) => value,
                    None => decode(&slot.bytes)?,
                };
                apply(&mut next, &patch.operations, 0)?;
                slot.bytes.clear();
                rmpv::encode::write_value(&mut slot.bytes, &next).map_err(|e| e.to_string())?;
                slot.tree = Some(next);
            } else if mode == 0 {
                slot.bytes.clear();
                slot.bytes.extend_from_slice(&payload);
                slot.tree = None;
            } else {
                return Err("Invalid publication transfer mode".into());
            }
            slot.sequence = sequence;
            slot.pending = true;
            slot.stamp = Stamp {
                kind,
                sequence,
                source_ticks,
                produced_ticks,
                received_ticks: utc_ticks(),
            };
            Ok(())
        })();
        self.scratch = payload;
        if result.is_err() {
            slot.sequence = 0;
            slot.tree = None;
        }
        result
    }
}
pub fn utc_ticks() -> i64 {
    621355968000000000
        + (std::time::SystemTime::now()
            .duration_since(std::time::UNIX_EPOCH)
            .unwrap_or_default()
            .as_nanos()
            / 100) as i64
}
fn decode(bytes: &[u8]) -> Result<Value, String> {
    let mut cursor = Cursor::new(bytes);
    let value = rmpv::decode::read_value(&mut cursor).map_err(|e| e.to_string())?;
    if cursor.position() != bytes.len() as u64 {
        return Err("Trailing MessagePack bytes".into());
    }
    Ok(value)
}
fn target<'a>(value: &'a mut Value, path: &[Path]) -> Result<&'a mut Value, String> {
    let mut current = value;
    for segment in path {
        current = match (segment.kind, current) {
            (0, Value::Map(fields)) => fields
                .iter_mut()
                .find(|(key, _)| key.as_str() == Some(&segment.key))
                .map(|(_, value)| value)
                .ok_or("Missing patch map key")?,
            (1, Value::Array(items)) => items
                .get_mut(segment.index)
                .ok_or("Invalid patch array index")?,
            _ => return Err("Invalid patch path".into()),
        };
    }
    Ok(current)
}
fn apply(value: &mut Value, operations: &[Operation], skip: usize) -> Result<(), String> {
    let mut index = 0;
    while index < operations.len() {
        let operation = &operations[index];
        let path = operation.path.get(skip..).ok_or("Invalid patch prefix")?;
        if let Some(binary) = path.iter().position(|segment| segment.kind == 2) {
            let prefix = &operation.path[..skip + binary];
            let mut end = index + 1;
            while end < operations.len()
                && operations[end].path.len() > skip + binary
                && operations[end].path[..skip + binary] == *prefix
                && operations[end].path[skip + binary].kind == 2
            {
                end += 1;
            }
            let Value::Binary(bytes) = target(value, &path[..binary])? else {
                return Err("Patch document is not binary".into());
            };
            let mut inner = decode(bytes)?;
            apply(&mut inner, &operations[index..end], skip + binary + 1)?;
            bytes.clear();
            rmpv::encode::write_value(bytes, &inner).map_err(|e| e.to_string())?;
            index = end;
            continue;
        }
        match operation.action {
            0 => {
                let replacement =
                    decode(operation.value.as_ref().ok_or("Patch has no replacement")?)?;
                if path.is_empty() {
                    *value = replacement;
                } else {
                    let (last, parent) = path.split_last().unwrap();
                    match (last.kind, target(value, parent)?) {
                        (0, Value::Map(fields)) => {
                            if let Some((_, old)) = fields
                                .iter_mut()
                                .find(|(key, _)| key.as_str() == Some(&last.key))
                            {
                                *old = replacement;
                            } else {
                                fields.push((Value::from(last.key.clone()), replacement));
                            }
                        }
                        (1, Value::Array(items)) => {
                            *items
                                .get_mut(last.index)
                                .ok_or("Invalid replacement index")? = replacement
                        }
                        _ => return Err("Invalid replacement target".into()),
                    }
                }
            }
            1 => {
                let (last, parent) = path.split_last().ok_or("Cannot remove publication root")?;
                let Value::Map(fields) = target(value, parent)? else {
                    return Err("Removal needs a map".into());
                };
                let position = fields
                    .iter()
                    .position(|(key, _)| key.as_str() == Some(&last.key))
                    .ok_or("Missing removal key")?;
                fields.remove(position);
            }
            2 => {
                let Value::Array(items) = target(value, path)? else {
                    return Err("Splice needs an array".into());
                };
                let end = operation
                    .index
                    .checked_add(operation.delete_count)
                    .ok_or("Splice overflow")?;
                if operation.index > items.len() || end > items.len() {
                    return Err("Invalid splice range".into());
                }
                let inserted = operation
                    .items
                    .as_deref()
                    .unwrap_or_default()
                    .iter()
                    .map(|item| decode(item))
                    .collect::<Result<Vec<_>, _>>()?;
                items.splice(operation.index..end, inserted);
            }
            _ => return Err("Unknown patch action".into()),
        }
        index += 1;
    }
    Ok(())
}
