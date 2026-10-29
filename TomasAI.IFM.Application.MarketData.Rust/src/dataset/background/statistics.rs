//! Native saved-statistics bootstrap. Passive storage copies raw values only;
//! exact source membership, date policy, cache lifetime and recovery stay native.
use super::*;
use crate::option_chain::statistics::Seed;
#[derive(Default)]
pub(super) struct State {
    values: BTreeMap<(i32, String), Option<Seed>>,
}
fn day(value: &serde_json::Value) -> Result<Option<time::Date>, String> {
    if value.is_null() {
        return Ok(None);
    }
    let day = value
        .as_i64()
        .and_then(|day| i32::try_from(day).ok())
        .filter(|day| (0..=3652058).contains(day))
        .ok_or("Invalid saved option statistics date")?;
    Ok(Some(
        time::Date::from_julian_day(day + 1721426)
            .map_err(|_| "Invalid saved option statistics date")?,
    ))
}
fn count(value: &serde_json::Value) -> Result<Option<i64>, String> {
    if value.is_null() {
        Ok(None)
    } else {
        value
            .as_i64()
            .map(Some)
            .ok_or("Invalid saved option statistic".into())
    }
}
/// Validates a complete raw response before selecting native desired contracts.
/// Negative/future observations remain raw; the existing native Seed policy
/// decides their eligibility rather than a managed helper changing values.
///
/// # Errors
/// Oversized/malformed source, duplicate identities, invalid dates or integers.
pub fn parse(bytes: &[u8]) -> Result<BTreeMap<String, Seed>, String> {
    if bytes.len() > crate::messaging::raw_io::MAXIMUM {
        return Err("Saved statistics byte capacity exceeded".into());
    }
    let source: serde_json::Value =
        serde_json::from_slice(bytes).map_err(|_| "Malformed saved option statistics")?;
    let rows = source
        .as_array()
        .filter(|rows| rows.len() <= 100000)
        .ok_or("Saved option statistics row capacity exceeded")?;
    let mut result = BTreeMap::new();
    for row in rows {
        let row = row
            .as_array()
            .filter(|row| row.len() == 5)
            .ok_or("Malformed saved option statistic")?;
        let contract = row[0]
            .as_str()
            .filter(|id| !id.trim().is_empty() && id.len() <= 1024)
            .ok_or("Invalid saved option statistics identity")?;
        let seed = Seed {
            volume: count(&row[1])?,
            volume_date: day(&row[2])?,
            open_interest: count(&row[3])?,
            open_interest_date: day(&row[4])?,
        };
        if result.insert(contract.into(), seed).is_some() {
            return Err("Duplicate saved option statistics identity".into());
        }
    }
    Ok(result)
}
impl Configuration {
    pub(in crate::dataset) async fn saved_statistics(
        &self,
        intent: &Intent,
        parent: Arc<Child>,
        stop: &CancellationToken,
        deadline: Deadline,
    ) -> Result<Vec<(u32, Seed)>, String> {
        let date = intent.date.to_julian_day() - 1721426;
        let mut groups = BTreeMap::<(String, i32), Vec<(String, u32)>>::new();
        let mut wanted = BTreeSet::new();
        for definition in &intent.reviewed.definitions {
            let mapping = &definition.blueprint.contract;
            let expiry = mapping.expiration().div_euclid(864_000_000_000) + 719162;
            let expiry = i32::try_from(expiry).map_err(|_| "Invalid statistics source expiry")?;
            let id = mapping.contract().to_owned();
            wanted.insert((date, id.clone()));
            groups
                .entry((mapping.underlying().into(), expiry))
                .or_default()
                .push((id, definition.instrument));
        }
        if wanted.len() > 2048 {
            return Err("Native saved statistics desired capacity exceeded".into());
        }
        self.statistics
            .lock()
            .map_err(|_| "Native saved statistics directory interrupted")?
            .values
            .retain(|key, _| wanted.contains(key));
        for ((underlying, expiry), members) in &groups {
            let missing = {
                let state = self
                    .statistics
                    .lock()
                    .map_err(|_| "Native saved statistics directory interrupted")?;
                members
                    .iter()
                    .any(|(id, _)| !state.values.contains_key(&(date, id.clone())))
            };
            if !missing {
                continue;
            }
            let args = serde_json::to_vec(
                &serde_json::json!({"contract":underlying,"expiry":expiry,"date":date}),
            )
            .map_err(|_| "Invalid saved statistics request")?;
            let bytes = crate::messaging::raw_io::read(
                self.discovery.broker.clone(),
                self.discovery.scope.clone(),
                parent.clone(),
                10,
                &args,
                stop,
                deadline,
            )
            .await?;
            let saved = parse(&bytes)?;
            let mut state = self
                .statistics
                .lock()
                .map_err(|_| "Native saved statistics directory interrupted")?;
            for (contract, _) in members {
                state
                    .values
                    .insert((date, contract.clone()), saved.get(contract).copied());
            }
        }
        let state = self
            .statistics
            .lock()
            .map_err(|_| "Native saved statistics directory interrupted")?;
        Ok(groups
            .values()
            .flatten()
            .filter_map(|(id, instrument)| {
                state
                    .values
                    .get(&(date, id.clone()))
                    .copied()
                    .flatten()
                    .map(|seed| (*instrument, seed))
            })
            .collect())
    }
}
