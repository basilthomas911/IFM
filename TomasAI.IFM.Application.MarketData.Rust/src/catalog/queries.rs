//! Bounded immutable cold queries. Query ownership is independent of live
//! lifecycle locks, so slow catalog consumers cannot prevent emergency reset.
use super::{Definition, Failure, selection};
use std::{collections::BTreeMap, sync::Arc};

/// Opaque snapshot identity or configured bounded retention was exhausted.
#[derive(Debug, Clone, Copy, PartialEq, Eq)]
pub enum Error {
    /// Invalid/missing query or offset.
    Invalid,
    /// Four queries or their total retained source budget is exhausted.
    Capacity,
}
/// One query's selection; filter predicates retain the qualified source ordering.
pub enum Query<'a> {
    /// All persisted rows, including unsupported/deleted source definitions.
    All,
    /// Existing chain filtering and duplicate/order semantics.
    Chain(selection::Chain<'a>),
}
/// Cold query manager. Source bytes are conservatively charged for every query,
/// including shared Arcs, to bound retained old universes during refresh.
#[derive(Default)]
pub struct Queries {
    next: u64,
    bytes: usize,
    active: BTreeMap<u64, (Vec<Arc<Definition>>, usize)>,
}
impl Queries {
    /// Pins a query after checking all limits; no partial identity is published.
    pub fn open(
        &mut self,
        rows: Vec<Arc<Definition>>,
        query: Query<'_>,
    ) -> Result<(u64, usize), Error> {
        if self.active.len() == 4 || rows.len() > 100_000 {
            return Err(Error::Capacity);
        }
        let rows = match query {
            Query::All => rows,
            Query::Chain(request) => selection::chain(&request, rows),
        };
        let bytes = rows
            .iter()
            .try_fold(0usize, |sum, row| sum.checked_add(row.json.len()))
            .ok_or(Error::Capacity)?;
        let total = self
            .bytes
            .checked_add(bytes)
            .filter(|n| *n <= 128 * 1024 * 1024)
            .ok_or(Error::Capacity)?;
        let token = self.next.checked_add(1).ok_or(Error::Capacity)?;
        let count = rows.len();
        self.active.insert(token, (rows, bytes));
        self.bytes = total;
        self.next = token;
        Ok((token, count))
    }
    /// Reads one immutable row. No pointer is exposed or mutable cache consulted.
    pub fn row(&self, token: u64, index: usize) -> Result<Arc<Definition>, Error> {
        self.active
            .get(&token)
            .and_then(|(rows, _)| rows.get(index))
            .cloned()
            .ok_or(Error::Invalid)
    }
    /// Clones a bounded contiguous page of immutable row handles.
    pub fn page(
        &self,
        token: u64,
        start: usize,
        count: usize,
    ) -> Result<Vec<Arc<Definition>>, Error> {
        let (rows, _) = self.active.get(&token).ok_or(Error::Invalid)?;
        if !(1..=64).contains(&count) || start >= rows.len() {
            return Err(Error::Invalid);
        }
        Ok(rows[start..(start + count).min(rows.len())].to_vec())
    }
    /// Clones immutable row handles for a further native selection, without
    /// copying provider JSON or resolving against a refreshed mutable universe.
    pub fn rows(&self, token: u64) -> Result<Vec<Arc<Definition>>, Error> {
        self.active
            .get(&token)
            .map(|(rows, _)| rows.clone())
            .ok_or(Error::Invalid)
    }
    /// Releases only this query's pinned universe. Delayed closes cannot affect
    /// a replacement because tokens are never reused within the runtime.
    pub fn close(&mut self, token: u64) -> Result<(), Error> {
        let (_, bytes) = self.active.remove(&token).ok_or(Error::Invalid)?;
        self.bytes -= bytes;
        Ok(())
    }
}

/// Serializes the already-parsed native projection for the cold managed mapper.
/// All source timestamps/prices remain integers; original JSON remains separately
/// accessible for definition persistence and digest identity.
pub fn project(row: &Definition) -> Result<Vec<u8>, Failure> {
    let bytes = serde_json::to_vec(&Projection(row)).map_err(|_| Failure::InvalidDefinition)?;
    if bytes.len() > 262_144 {
        return Err(Failure::Capacity);
    }
    Ok(bytes)
}

struct Projection<'a>(&'a Definition);

impl serde::Serialize for Projection<'_> {
    fn serialize<S: serde::Serializer>(&self, serializer: S) -> Result<S::Ok, S::Error> {
        use serde::ser::SerializeStruct;

        // Preserve the exact alphabetical property order from the former Value map.
        let row = self.0;
        let mut fields = serializer.serialize_struct("NativeContractDefinition", 29)?;
        fields.serialize_field("Activation", &row.activation)?;
        fields.serialize_field("Asset", &row.asset)?;
        fields.serialize_field("CanonicalId", &selection::format(row).ok())?;
        fields.serialize_field("Cfi", &row.cfi)?;
        fields.serialize_field("Class", &row.class)?;
        fields.serialize_field("Currency", &row.currency)?;
        fields.serialize_field("Dataset", &row.dataset)?;
        fields.serialize_field("Deleted", &row.deleted)?;
        fields.serialize_field("EventNanoseconds", &row.event_ns)?;
        fields.serialize_field("Exchange", &row.exchange)?;
        fields.serialize_field("Expiration", &row.expiration)?;
        fields.serialize_field("Instrument", &row.instrument)?;
        fields.serialize_field("Kind", &row.kind)?;
        fields.serialize_field("Maturity", &row.maturity.map(|value| value.to_string()))?;
        fields.serialize_field("MaturityWeek", &row.maturity_week)?;
        fields.serialize_field("Multiplier", &row.multiplier)?;
        fields.serialize_field("Publisher", &row.publisher)?;
        fields.serialize_field(
            "QueryMaturity",
            &row.query_maturity.map(|value| value.to_string()),
        )?;
        fields.serialize_field("QueryMaturityInvalid", &row.query_maturity_invalid)?;
        fields.serialize_field("ReceivedNanoseconds", &row.received_ns)?;
        fields.serialize_field("SecurityType", &row.security_type)?;
        fields.serialize_field("SettlementCurrency", &row.settlement_currency)?;
        fields.serialize_field("Strike", &row.strike)?;
        fields.serialize_field("Symbol", &row.symbol)?;
        fields.serialize_field("Tick", &row.tick)?;
        fields.serialize_field("TickAmount", &row.tick_amount)?;
        fields.serialize_field("Underlying", &row.underlying)?;
        fields.serialize_field("UnderlyingInstrument", &row.underlying_instrument)?;
        fields.serialize_field("UnitOfMeasure", &row.unit)?;
        fields.end()
    }
}

/// Matches the preserved bridge's non-exact contract-detail ordering. Stored
/// source universes retain their independent dataset/instrument persistence order.
pub fn provider_details(rows: impl IntoIterator<Item = Arc<Definition>>) -> Vec<Arc<Definition>> {
    let mut rows: Vec<_> = rows
        .into_iter()
        .filter(|row| (1..=3).contains(&row.kind))
        .collect();
    rows.sort_by(|a, b| {
        a.expiration
            .unwrap_or(u64::MAX)
            .cmp(&b.expiration.unwrap_or(u64::MAX))
            .then(a.kind.cmp(&b.kind))
            .then(
                a.strike
                    .unwrap_or(i64::MAX)
                    .cmp(&b.strike.unwrap_or(i64::MAX)),
            )
            .then_with(|| a.symbol.as_bytes().cmp(b.symbol.as_bytes()))
    });
    rows
}
