//! Native exact-expiry source and displayed/buffered selection. Every await is
//! beneath the dataset parent and consumes its original cancellation/deadline.
//! Full metadata is fetched only for the selected bounded subscription envelope.
use super::Command;
use crate::{
    messaging::raw_io,
    option_chain::{
        browser_inputs,
        browser_window::{self, Method, Window},
        discovery::Discovery,
        qualification::Decimal,
    },
    reference::option_contract::{CoverageRow, OptionContract},
    runtime::{Deadline, owner::Child},
};
use futures_util::{StreamExt, TryStreamExt, stream};
use std::sync::Arc;
use tokio_util::sync::CancellationToken;
/// Detached source facts may be cached by native control; they own no dataset
/// task/socket/reference context, and cannot delay cancellation or retirement.
pub struct Facts {
    /// Complete exact-expiry compact source, never silently truncated.
    pub rows: Vec<CoverageRow>,
    /// Baseline stored EOD/Bollinger fallback facts.
    pub fallback: browser_inputs::Inputs,
}
/// Current displayed membership and larger physical subscription envelope.
#[derive(Clone)]
pub struct Selection {
    /// Existing lower/upper bounds and method label.
    pub window: Window,
    /// Complete source indices for bounded native physical demand.
    pub buffered: Vec<usize>,
    /// Accepted underlying mark or labelled stored fallback.
    pub price: Option<Decimal>,
}
impl Facts {
    /// Applies the unchanged baseline window/buffer selectors to copied native
    /// facts. A current live mark overrides only the stored fallback price.
    ///
    /// # Errors
    /// Invalid window arithmetic, excessive display membership or cancellation.
    pub fn select(
        &self,
        command: &Command,
        live: Option<Decimal>,
        stop: &CancellationToken,
        deadline: Deadline,
    ) -> Result<Selection, String> {
        self.select_with_iv(command, live, None, 0, stop, deadline)
    }
    /// Applies an eligible native ATM observation using the baseline 1.65-score,
    /// 15% buffered IV window; otherwise uses the same Bollinger fallback.
    ///
    /// # Errors
    /// Invalid/oversized membership, arithmetic, cancellation or original expiry.
    pub fn select_with_iv(
        &self,
        command: &Command,
        live: Option<Decimal>,
        iv: Option<f64>,
        at: i64,
        stop: &CancellationToken,
        deadline: Deadline,
    ) -> Result<Selection, String> {
        let price = live
            .filter(|value| value.compare(Decimal::default()) == Some(std::cmp::Ordering::Greater))
            .or(self.fallback.price);
        let rows = self
            .rows
            .iter()
            .map(CoverageRow::browser_row)
            .collect::<Result<Vec<_>, _>>()?;
        let window = browser_window::select(
            &rows,
            price,
            if let Some((volatility, expiry)) = iv
                .filter(|_| {
                    price.is_some_and(|price| {
                        price.compare(Decimal::default()) == Some(std::cmp::Ordering::Greater)
                    })
                })
                .zip(
                    self.rows
                        .iter()
                        .filter_map(|row| row.coverage_definition().ok()?.expiration)
                        .min(),
                )
                .filter(|(iv, expiry)| {
                    iv.is_finite() && *iv > 0.0 && i128::from(*expiry) / 100 > i128::from(at)
                })
            {
                Method::Implied {
                    volatility,
                    seconds: ((i128::from(expiry) / 100 - i128::from(at)) as f64) / 10_000_000.0,
                    z: 1.65,
                    buffer: 0.15,
                }
            } else {
                Method::Bollinger {
                    deviation: self.fallback.deviation,
                    multiplier: command.multiplier,
                }
            },
            &command.required,
            stop,
            deadline,
        )?;
        if window.rows.len() > 2048 {
            return Err("Native browser displayed universe exceeds capacity".into());
        }
        let buffered = browser_window::buffer(&rows, &window, command.wing, stop, deadline)?;
        if buffered.len() > 2048 {
            return Err("Native browser subscription universe exceeds capacity".into());
        }
        Ok(Selection {
            window,
            buffered,
            price,
        })
    }
}
impl Discovery {
    /// Copies complete exact-expiry coverage and all raw fallback candidates in
    /// parallel. A stalled storage response is cancelled with the dataset parent.
    ///
    /// # Errors
    /// Incomplete source, malformed facts, explicit date error or original timeout.
    pub async fn browser_facts(
        &self,
        command: &Command,
        date: time::Date,
        parent: Arc<Child>,
        stop: &CancellationToken,
        deadline: Deadline,
    ) -> Result<Facts, String> {
        let expiry = time::Date::from_julian_day(
            command
                .family
                .expiry
                .checked_add(1721426)
                .ok_or("Invalid native browser expiry")?,
        )
        .map_err(|_| "Invalid native browser expiry")?;
        let day = date.to_julian_day() - 1721426;
        if !(1..=3652058).contains(&day) {
            return Err("Native browser requires explicit API ValueDate".into());
        }
        let args = serde_json::to_vec(
            &serde_json::json!({"contract":command.family.contract,"root":command.root,"date":day}),
        )
        .map_err(|_| "Invalid native browser fallback request")?;
        let rows = self.read_browser_coverage(
            &command.root,
            &command.family.contract,
            expiry,
            &command.providers,
            parent.clone(),
            stop,
            deadline,
        );
        let inputs = raw_io::read(
            self.broker.clone(),
            self.scope.clone(),
            parent,
            8,
            &args,
            stop,
            deadline,
        );
        let (rows, inputs) = tokio::try_join!(rows, inputs)?;
        let fallback = browser_inputs::select(&inputs, day)?;
        Ok(Facts { rows, fallback })
    }
    /// Acquires only selected immutable full metadata with eight bounded native
    /// readers. The exact source digest/version is checked before a model is used.
    /// No latest-version fallback or managed reference preparation is involved.
    ///
    /// # Errors
    /// Out-of-range/repeated indices, missing version, changed source, malformed
    /// metadata, storage failure or original dataset cancellation/deadline.
    pub async fn browser_models(
        &self,
        facts: &Facts,
        selection: &Selection,
        parent: Arc<Child>,
        stop: &CancellationToken,
        deadline: Deadline,
    ) -> Result<Vec<OptionContract>, String> {
        if selection.buffered.len() > 2048 {
            return Err("Native browser full-model capacity exceeded".into());
        }
        let mut seen = std::collections::BTreeSet::new();
        let mut wanted = Vec::new();
        for index in &selection.buffered {
            if !seen.insert(*index) {
                return Err("Native browser repeats a buffered source index".into());
            }
            let row = facts
                .rows
                .get(*index)
                .ok_or("Invalid native browser source index")?;
            let version = row.mapping_version();
            if version.trim().is_empty() {
                return Err("Selected browser definition is not reviewed".into());
            }
            let contract = row.coverage_definition()?.contract;
            let args =
                serde_json::to_vec(&serde_json::json!({"contract":contract,"version":version}))
                    .map_err(|_| "Invalid native exact browser model request")?;
            wanted.push((row.clone(), args));
        }
        stream::iter(wanted)
            .map(|(row, args)| {
                let parent = parent.clone();
                async move {
                    let bytes = raw_io::read(
                        self.broker.clone(),
                        self.scope.clone(),
                        parent,
                        6,
                        &args,
                        stop,
                        deadline,
                    )
                    .await?;
                    let raw: serde_json::Value = serde_json::from_slice(&bytes)
                        .map_err(|_| "Malformed native exact browser model")?;
                    let model = OptionContract::from_command(&raw)?;
                    if !row.matches(&model)? {
                        return Err(
                            "Native browser source changed during exact metadata acquisition"
                                .into(),
                        );
                    }
                    if !model.errors().is_empty() {
                        return Err("Selected browser definition is not reviewed".into());
                    }
                    Ok(model)
                }
            })
            .buffered(8)
            .try_collect()
            .await
    }
}
