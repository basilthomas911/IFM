//! Shared bounded cold-result ownership for references and historical controls.
//! A pending reservation removes itself on cancellation, panic or failed submit.
use super::runtime::{Instance, Status};
use std::{
    collections::BTreeMap,
    io::Write,
    sync::{Arc, Mutex},
};
pub(super) const CAP: usize = 16 * 1024 * 1024;
type Results = Arc<Mutex<BTreeMap<u64, Option<Vec<u8>>>>>;
pub(super) struct Pending {
    results: Results,
    id: u64,
    committed: bool,
}
impl Pending {
    pub(super) fn reserve(instance: &Instance, id: u64) -> Result<Self, Status> {
        let mut results = instance
            .reference_results
            .lock()
            .map_err(|_| Status::InternalFailure)?;
        if results.len() >= 32 || results.contains_key(&id) {
            return Err(Status::Capacity);
        }
        results.insert(id, None);
        Ok(Self {
            results: instance.reference_results.clone(),
            id,
            committed: false,
        })
    }
    pub(super) fn commit(&mut self, bytes: Vec<u8>, maximum: usize) -> Result<(), String> {
        if bytes.len() > maximum.min(CAP) {
            return Err("Cold result exceeded its byte capacity".into());
        }
        let mut results = self
            .results
            .lock()
            .map_err(|_| "Cold result owner interrupted".to_owned())?;
        if results.values().flatten().map(Vec::len).sum::<usize>() + bytes.len() > CAP {
            return Err("Shared cold-result byte capacity exceeded".into());
        }
        let slot = results
            .get_mut(&self.id)
            .ok_or_else(|| "Cold result was discarded".to_owned())?;
        *slot = Some(bytes);
        self.committed = true;
        Ok(())
    }
}
impl Drop for Pending {
    fn drop(&mut self) {
        if !self.committed
            && let Ok(mut results) = self.results.lock()
        {
            results.remove(&self.id);
        }
    }
}
struct Limited(Vec<u8>);
impl Write for Limited {
    fn write(&mut self, bytes: &[u8]) -> std::io::Result<usize> {
        if self
            .0
            .len()
            .checked_add(bytes.len())
            .is_none_or(|n| n > CAP)
        {
            return Err(std::io::Error::other("Cold result exceeds byte capacity"));
        }
        self.0
            .try_reserve(bytes.len())
            .map_err(|_| std::io::Error::other("Cold result allocation failed"))?;
        self.0.extend_from_slice(bytes);
        Ok(bytes.len())
    }
    fn flush(&mut self) -> std::io::Result<()> {
        Ok(())
    }
}
pub(super) fn encode(value: &impl serde::Serialize) -> Result<Vec<u8>, String> {
    let mut output = Limited(Vec::new());
    serde_json::to_writer(&mut output, value)
        .map_err(|_| "Cold result encoding capacity exceeded".to_owned())?;
    Ok(output.0)
}
