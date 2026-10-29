//! Dataset-owned access to raw background strategy policies and definitions.
//! External storage returns copied facts only. Rust determines the expiry range,
//! validates the complete source, and owns cancellation and the original deadline.
use super::strategy::{self, Parameters};
use crate::{
    messaging::{publisher::BrokerSettings, raw_io},
    reference::option_contract::OptionContract,
    runtime::{Deadline, owner::Child},
};
use std::{
    collections::{BTreeMap, BTreeSet},
    sync::Arc,
};
use tokio_util::sync::CancellationToken;

/// Immutable source configuration, containing no managed callback or lease.
pub struct Discovery {
    /// Native broker configuration for bounded raw database requests.
    pub broker: BrokerSettings,
    /// Existing deployment scope.
    pub scope: String,
    /// Explicit API-selected environment; never inferred from the OS.
    pub environment: String,
}
/// Complete detached strategy source for native universe planning.
pub struct Universe {
    /// Complete reviewed global policy, owned by Rust.
    pub parameters: Arc<Parameters>,
    /// Full raw contracts; no filtered/truncated subset is treated as complete.
    pub contracts: Vec<OptionContract>,
}
/// Parses a bounded complete published-policy source with unique identities.
///
/// # Errors
/// Malformed, oversized, duplicate or wrong-environment published facts.
pub fn policies(bytes: &[u8], environment: &str) -> Result<Vec<Arc<Parameters>>, String> {
    if bytes.len() > 4 * 1024 * 1024
        || !matches!(environment, "Development" | "Paper" | "Production")
    {
        return Err("Invalid native strategy source capacity/environment".into());
    }
    let value: serde_json::Value =
        serde_json::from_slice(bytes).map_err(|_| "Malformed native strategy facts")?;
    let rows = value
        .as_array()
        .filter(|v| v.len() <= 64)
        .ok_or("Native strategy policy capacity exceeded")?;
    let mut seen = BTreeSet::new();
    rows.iter()
        .map(|row| {
            let policy = strategy::from_command(row)?;
            if row[6].as_str() != Some(environment) || !seen.insert(policy.id().to_owned()) {
                return Err("Conflicting native published strategy identity".into());
            }
            Ok(Arc::new(policy))
        })
        .collect()
}
/// Computes the requested raw definition range from validated native bias rules.
///
/// # Errors
/// Invalid API ValueDate or dates outside the representable exchange calendar.
pub fn range(
    parameters: &Parameters,
    date: time::Date,
) -> Result<Option<(time::Date, time::Date)>, String> {
    if !parameters.enabled {
        return Ok(None);
    }
    let mut enabled = parameters.biases.iter().filter(|b| b.enabled);
    let Some(first) = enabled.next() else {
        return Ok(None);
    };
    let (minimum, maximum) = enabled.fold((first.minimum, first.maximum), |(low, high), b| {
        (low.min(b.minimum), high.max(b.maximum))
    });
    if date.to_julian_day() <= 1721426 {
        return Err("Invalid strategy API ValueDate".into());
    }
    let from = date
        .checked_add(time::Duration::days(i64::from(minimum)))
        .ok_or("Strategy expiry range overflow")?;
    let until = date
        .checked_add(time::Duration::days(i64::from(maximum)))
        .ok_or("Strategy expiry range overflow")?;
    if until.year() > 9999 {
        return Err("Strategy expiry range overflow".into());
    }
    Ok(Some((from, until)))
}
/// Parses all returned raw source models. Review qualification occurs in native
/// universe policy; no C# eligibility, convention or pricing decision is trusted.
///
/// # Errors
/// Invalid structure, bounded capacity or conflicting canonical/physical identity.
pub fn definitions(bytes: &[u8]) -> Result<Vec<OptionContract>, String> {
    if bytes.len() > 4 * 1024 * 1024 {
        return Err("Native raw definition byte capacity exceeded".into());
    }
    let value: serde_json::Value =
        serde_json::from_slice(bytes).map_err(|_| "Malformed native raw definitions")?;
    let rows = value
        .as_array()
        .filter(|v| v.len() <= 2048)
        .ok_or("Native raw definition capacity exceeded")?;
    let mut identities = BTreeSet::new();
    let mut bindings = BTreeMap::new();
    let mut canonical_sources = BTreeMap::new();
    let mut output = Vec::new();
    for row in rows {
        let model = OptionContract::from_command(row)?;
        let id = row[0]
            .as_str()
            .filter(|s| !s.is_empty())
            .ok_or("Missing raw option identity")?;
        if !identities.insert(id.to_owned()) {
            return Err("Conflicting native raw option identity".into());
        }
        // A physical instrument cannot change canonical identity or asset role
        // across two copied rows. Repeated shared underlying bindings are valid.
        for (contract, publisher, instrument, option) in [
            (row[0].as_str(), row[15].as_u64(), row[16].as_u64(), true),
            (row[33].as_str(), row[44].as_u64(), row[43].as_u64(), false),
        ] {
            if let (Some(dataset), Some(contract), Some(publisher), Some(instrument)) =
                (row[14].as_str(), contract, publisher, instrument)
                && publisher != 0
                && instrument != 0
            {
                let physical = (dataset.to_owned(), publisher, instrument);
                let identity = (contract.to_owned(), option);
                if bindings
                    .insert(physical.clone(), identity.clone())
                    .is_some_and(|old| old != identity)
                    || canonical_sources
                        .insert(identity, physical.clone())
                        .is_some_and(|old| old != physical)
                {
                    return Err("Conflicting native raw provider identity".into());
                }
            }
        }
        output.push(model);
    }
    Ok(output)
}
impl Discovery {
    /// Reads one complete root/range once for overlapping native policies.
    /// Source facts remain copied bytes; no external lease or context is imported.
    ///
    /// # Errors
    /// Invalid range/root, incomplete raw facts, cancellation or original deadline.
    pub async fn read_definitions(
        &self,
        root: &str,
        from: time::Date,
        until: time::Date,
        parent: Arc<Child>,
        stop: &CancellationToken,
        deadline: Deadline,
    ) -> Result<Vec<OptionContract>, String> {
        if root.is_empty()
            || root.len() > 60
            || !root.is_ascii()
            || from > until
            || from.year() < 1
            || until.year() > 9999
        {
            return Err("Invalid native raw definition range".into());
        }
        let args=serde_json::to_vec(&serde_json::json!({"root":root,"from":from.to_julian_day()-1721426,"until":until.to_julian_day()-1721426}))
            .map_err(|_|"Invalid raw definition arguments")?;
        let bytes = raw_io::read(
            self.broker.clone(),
            self.scope.clone(),
            parent,
            5,
            &args,
            stop,
            deadline,
        )
        .await?;
        definitions(&bytes)
    }
    /// Captures up to 100,000 compact raw coverage rows, then reads immutable
    /// 256-row pages. Every page has the same original dataset deadline. Full
    /// reviewed option graphs are acquired only for the selected bounded subset.
    ///
    /// # Errors
    /// Incomplete/conflicting snapshot, capacity, cancellation or original deadline.
    pub async fn read_coverage(
        &self,
        root: &str,
        from: time::Date,
        until: time::Date,
        parent: Arc<Child>,
        stop: &CancellationToken,
        deadline: Deadline,
    ) -> Result<Vec<crate::reference::option_contract::CoverageRow>, String> {
        if root.is_empty() || root.len() > 60 || !root.is_ascii() || from > until {
            return Err("Invalid compact definition range".into());
        }
        let args=serde_json::to_vec(&serde_json::json!({"root":root,"from":from.to_julian_day()-1721426,"until":until.to_julian_day()-1721426})).map_err(|_|"Invalid compact definition arguments")?;
        self.read_coverage_arguments(args, parent, stop, deadline)
            .await
    }
    /// Reads one exact underlying/expiry and copied provider-root filter for
    /// browsing. Source acquisition remains below the dataset resource parent;
    /// legacy display fields are required rather than inferred by managed code.
    ///
    /// # Errors
    /// Invalid filters, missing browser projection, incomplete data or cancellation.
    #[allow(clippy::too_many_arguments)]
    pub async fn read_browser_coverage(
        &self,
        root: &str,
        underlying: &str,
        expiry: time::Date,
        provider_roots: &[String],
        parent: Arc<Child>,
        stop: &CancellationToken,
        deadline: Deadline,
    ) -> Result<Vec<crate::reference::option_contract::CoverageRow>, String> {
        if root.is_empty()
            || root.len() > 60
            || !root.is_ascii()
            || underlying.trim().is_empty()
            || underlying.len() > 1024
            || provider_roots.len() > 64
            || provider_roots
                .iter()
                .any(|root| root.trim().is_empty() || root.len() > 128)
            || expiry.year() < 1
            || expiry.year() > 9999
        {
            return Err("Invalid native exact browser source filter".into());
        }
        let date = expiry.to_julian_day() - 1721426;
        let args=serde_json::to_vec(&serde_json::json!({"root":root,"underlying":underlying,"from":date,"until":date,"provider_roots":provider_roots,"browser":true})).map_err(|_|"Invalid native browser source arguments")?;
        let rows = self
            .read_coverage_arguments(args, parent, stop, deadline)
            .await?;
        for row in &rows {
            row.browser_row()?;
        }
        Ok(rows)
    }
    async fn read_coverage_arguments(
        &self,
        args: Vec<u8>,
        parent: Arc<Child>,
        stop: &CancellationToken,
        deadline: Deadline,
    ) -> Result<Vec<crate::reference::option_contract::CoverageRow>, String> {
        use sha2::{Digest, Sha256};
        let bytes = raw_io::read(
            self.broker.clone(),
            self.scope.clone(),
            parent.clone(),
            7,
            &args,
            stop,
            deadline,
        )
        .await?;
        let header: serde_json::Value =
            serde_json::from_slice(&bytes).map_err(|_| "Malformed compact definition header")?;
        let id = header["id"]
            .as_str()
            .filter(|s| s.len() == 32 && s.bytes().all(|b| b.is_ascii_hexdigit()))
            .ok_or("Invalid compact snapshot identity")?;
        let count = header["count"]
            .as_u64()
            .filter(|n| *n <= 100000)
            .ok_or("Invalid compact snapshot row count")? as usize;
        let digest = header["digest"]
            .as_str()
            .filter(|s| s.len() == 64 && s.bytes().all(|b| b.is_ascii_hexdigit()))
            .ok_or("Invalid compact snapshot digest")?;
        let mut rows = Vec::new();
        let mut hash = Sha256::new();
        let mut total = 0usize;
        let mut identities = BTreeSet::new();
        for offset in (0..count).step_by(256) {
            let args = serde_json::to_vec(&serde_json::json!({"snapshot":id,"offset":offset}))
                .map_err(|_| "Invalid compact definition page arguments")?;
            let bytes = raw_io::read(
                self.broker.clone(),
                self.scope.clone(),
                parent.clone(),
                7,
                &args,
                stop,
                deadline,
            )
            .await?;
            total = total
                .checked_add(bytes.len())
                .ok_or("Compact source byte overflow")?;
            if total > 64 * 1024 * 1024 {
                return Err("Compact source byte capacity exceeded".into());
            }
            hash.update(&bytes);
            let page: serde_json::Value =
                serde_json::from_slice(&bytes).map_err(|_| "Malformed compact definition page")?;
            let page = page
                .as_array()
                .filter(|p| p.len() == (count - offset).min(256))
                .ok_or("Incomplete compact definition page")?;
            for value in page {
                let row = crate::reference::option_contract::CoverageRow::from_command(value)?;
                if !identities.insert(row.coverage_definition()?.contract.to_owned()) {
                    return Err("Duplicate compact source identity".into());
                }
                rows.push(row);
            }
        }
        if !format!("{:X}", hash.finalize()).eq_ignore_ascii_case(digest) {
            return Err("Compact source snapshot digest mismatch".into());
        }
        Ok(rows)
    }
    /// Acquires copied global policy facts inside the dataset's cancellable IO.
    ///
    /// # Errors
    /// Source/storage failure, malformed complete data or original deadline expiry.
    pub async fn policies(
        &self,
        parent: Arc<Child>,
        stop: &CancellationToken,
        deadline: Deadline,
    ) -> Result<Vec<Arc<Parameters>>, String> {
        let args = serde_json::to_vec(&serde_json::json!({"environment":self.environment}))
            .map_err(|_| "Invalid strategy source arguments")?;
        let bytes = raw_io::read(
            self.broker.clone(),
            self.scope.clone(),
            parent,
            4,
            &args,
            stop,
            deadline,
        )
        .await?;
        policies(&bytes, &self.environment)
    }
    /// Acquires the full native-computed expiry range, never a live quote/lease
    /// from C#. All pages share the caller's original dataset recovery deadline.
    ///
    /// # Errors
    /// Invalid range, incomplete source, cancellation or storage/provider failure.
    pub async fn universe(
        &self,
        parameters: Arc<Parameters>,
        date: time::Date,
        parent: Arc<Child>,
        stop: &CancellationToken,
        deadline: Deadline,
    ) -> Result<Universe, String> {
        let Some((from, until)) = range(&parameters, date)? else {
            return Ok(Universe {
                parameters,
                contracts: vec![],
            });
        };
        let contracts = self
            .read_definitions(parameters.root(), from, until, parent, stop, deadline)
            .await?;
        Ok(Universe {
            parameters,
            contracts,
        })
    }
}

impl Universe {
    /// Plans the complete copied source with native exact-decimal coverage rules.
    /// The prices and observed IV must come from native query views. This method
    /// creates no resource, background task or managed renewal dependency.
    ///
    /// # Errors
    /// Invalid exact metadata, cancellation, original deadline or planner failure.
    #[allow(clippy::too_many_arguments)]
    pub fn plan(
        &self,
        planner: &super::coverage::Planner,
        date: time::Date,
        forward: super::qualification::Decimal,
        volatility: f64,
        zone: crate::reference::calendar::Zone,
        underlying_prices: &std::collections::BTreeMap<String, super::qualification::Decimal>,
        stop: &CancellationToken,
        deadline: Deadline,
    ) -> Result<super::coverage::ResultSet, String> {
        let definitions = self
            .contracts
            .iter()
            .map(OptionContract::coverage_definition)
            .collect::<Result<Vec<_>, _>>()?;
        planner
            .plan(
                &self.parameters,
                date,
                &definitions,
                true,
                forward,
                volatility,
                zone,
                Some(underlying_prices),
                stop,
                deadline,
            )
            .map_err(str::to_owned)
    }
}
