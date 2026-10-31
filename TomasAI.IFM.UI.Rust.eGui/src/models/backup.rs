//! Concrete backup display documents shared by native controllers and egui.
use super::cells::{Cell, Cells, ScalarCell};
pub use super::labels::Label;
use serde::{Deserialize, Serialize};
use std::{borrow::Cow, collections::BTreeMap};
#[derive(Clone, Debug, Default, Serialize, Deserialize, PartialEq)]
#[serde(default, rename_all = "camelCase")]
pub struct Operation {
    pub operation_id: Option<String>,
    pub protection_set: Option<String>,
    pub source: Option<Label>,
    pub phase: Option<Label>,
    pub outcome: Option<Label>,
    pub progress_percent: Option<i32>,
    pub safe_diagnostic_reference: Option<String>,
    pub requested_mode: Option<Label>,
    pub resolved_mode: Option<Label>,
    pub created_utc: Option<String>,
    pub completed_utc: Option<String>,
    pub backup_set_id: Option<String>,
    pub kind: Option<Label>,
    pub engine: Option<Label>,
}
impl Cells for Operation {
    fn cell(&self, key: &str) -> Cell<'_> {
        match key {
            "operationId" => self.operation_id.scalar_cell(),
            "protectionSet" => self.protection_set.scalar_cell(),
            "source" => self.source.scalar_cell(),
            "phase" => self.phase.scalar_cell(),
            "outcome" => self.outcome.scalar_cell(),
            "progressPercent" => self.progress_percent.scalar_cell(),
            "safeDiagnosticReference" => self.safe_diagnostic_reference.scalar_cell(),
            "requestedMode" => self.requested_mode.scalar_cell(),
            "resolvedMode" => self.resolved_mode.scalar_cell(),
            "createdUtc" => self.created_utc.scalar_cell(),
            "completedUtc" => self.completed_utc.scalar_cell(),
            "backupSetId" => self.backup_set_id.scalar_cell(),
            "kind" => self.kind.scalar_cell(),
            "engine" => self.engine.scalar_cell(),
            _ => Cell::Missing,
        }
    }
}
#[derive(Clone, Debug, Default, Serialize, Deserialize, PartialEq)]
#[serde(default, rename_all = "camelCase")]
pub struct ProtectionSet {
    pub id: Option<String>,
    pub source: Option<Label>,
    pub engines: Option<Vec<Label>>,
    pub enabled: Option<bool>,
    pub policy_revision: Option<i64>,
}
impl Cells for ProtectionSet {
    fn cell(&self, key: &str) -> Cell<'_> {
        match key {
            "id" => self.id.scalar_cell(),
            "source" => self.source.scalar_cell(),
            "engines" => {
                if self.engines.is_some() {
                    Cell::Text(Cow::Owned(
                        serde_json::to_string(&self.engines).unwrap_or_default(),
                    ))
                } else {
                    Cell::Missing
                }
            }
            "enabled" => self.enabled.scalar_cell(),
            "policyRevision" => self.policy_revision.scalar_cell(),
            _ => Cell::Missing,
        }
    }
}
#[derive(Clone, Debug, Default, Serialize, Deserialize, PartialEq)]
#[serde(default, rename_all = "camelCase")]
pub struct RestorePoint {
    pub restore_point_id: Option<String>,
    pub recovery_point_utc: Option<String>,
    pub verification_level: Option<Label>,
    pub verified_utc: Option<String>,
    pub restore_tested_utc: Option<String>,
    pub eligible: Option<bool>,
}
impl Cells for RestorePoint {
    fn cell(&self, key: &str) -> Cell<'_> {
        match key {
            "restorePointId" => self.restore_point_id.scalar_cell(),
            "recoveryPointUtc" => self.recovery_point_utc.scalar_cell(),
            "verificationLevel" => self.verification_level.scalar_cell(),
            "verifiedUtc" => self.verified_utc.scalar_cell(),
            "restoreTestedUtc" => self.restore_tested_utc.scalar_cell(),
            "eligible" => self.eligible.scalar_cell(),
            _ => Cell::Missing,
        }
    }
}
#[derive(Clone, Debug, Default, Serialize, Deserialize, PartialEq)]
#[serde(default, rename_all = "camelCase")]
pub struct Health {
    pub host_id: Option<String>,
    pub ready: Option<bool>,
    pub capability: Option<Label>,
    pub observed_utc: Option<String>,
    pub diagnostic: Option<String>,
}
impl Cells for Health {
    fn cell(&self, key: &str) -> Cell<'_> {
        match key {
            "hostId" => self.host_id.scalar_cell(),
            "ready" => self.ready.scalar_cell(),
            "capability" => self.capability.scalar_cell(),
            "observedUtc" => self.observed_utc.scalar_cell(),
            "diagnostic" => self.diagnostic.scalar_cell(),
            _ => Cell::Missing,
        }
    }
}
#[derive(Clone, Debug, Default, Serialize, Deserialize, PartialEq)]
#[serde(default, rename_all = "camelCase")]
pub struct Setup {
    pub backup_host_settings: BTreeMap<String, String>,
    pub available: Option<bool>,
}
#[derive(Clone, Debug, Default, Serialize, Deserialize, PartialEq)]
#[serde(default, rename_all = "camelCase")]
pub struct Dashboard {
    pub source: String,
    pub protection_sets: Vec<ProtectionSet>,
    pub recent_operations: Vec<Operation>,
    pub latest_verified: Option<RestorePoint>,
    pub latest_restore_tested: Option<RestorePoint>,
}
#[derive(Clone, Debug, Default, Serialize, Deserialize, PartialEq)]
#[serde(default, rename_all = "camelCase")]
pub struct Policy {
    pub id: Option<String>,
    pub revision: Option<i64>,
    pub enforced: Option<bool>,
    pub enabled_sources: Option<Vec<Label>>,
    pub protection_sets: Vec<Option<String>>,
    pub rpo: String,
    pub rto: String,
    pub daily: Option<i32>,
    pub weekly: Option<i32>,
    pub monthly: Option<i32>,
    pub verification_levels: Option<Vec<Label>>,
    pub maximum_verification_age: String,
}
impl Cells for Policy {
    fn cell(&self, key: &str) -> Cell<'_> {
        match key {
            "id" => self.id.scalar_cell(),
            "revision" => self.revision.scalar_cell(),
            "enforced" => self.enforced.scalar_cell(),
            "enabledSources" => {
                if self.enabled_sources.is_some() {
                    Cell::Text(Cow::Owned(
                        serde_json::to_string(&self.enabled_sources).unwrap_or_default(),
                    ))
                } else {
                    Cell::Missing
                }
            }
            "protectionSets" => Cell::Text(Cow::Owned(
                serde_json::to_string(&self.protection_sets).unwrap_or_default(),
            )),
            "rpo" => self.rpo.scalar_cell(),
            "rto" => self.rto.scalar_cell(),
            "daily" => self.daily.scalar_cell(),
            "weekly" => self.weekly.scalar_cell(),
            "monthly" => self.monthly.scalar_cell(),
            "verificationLevels" => {
                if self.verification_levels.is_some() {
                    Cell::Text(Cow::Owned(
                        serde_json::to_string(&self.verification_levels).unwrap_or_default(),
                    ))
                } else {
                    Cell::Missing
                }
            }
            "maximumVerificationAge" => self.maximum_verification_age.scalar_cell(),
            _ => Cell::Missing,
        }
    }
}
#[derive(Clone, Debug, Default, Serialize, Deserialize, PartialEq)]
#[serde(default, rename_all = "camelCase")]
pub struct PolicyEditor {
    #[serde(skip_serializing_if = "Option::is_none")]
    pub rpo_minutes: Option<String>,
    #[serde(skip_serializing_if = "Option::is_none")]
    pub rto_minutes: Option<String>,
    #[serde(skip_serializing_if = "Option::is_none")]
    pub maximum_verification_hours: Option<String>,
}
impl Cells for PolicyEditor {
    fn cell(&self, key: &str) -> Cell<'_> {
        match key {
            "rpoMinutes" => self.rpo_minutes.scalar_cell(),
            "rtoMinutes" => self.rto_minutes.scalar_cell(),
            "maximumVerificationHours" => self.maximum_verification_hours.scalar_cell(),
            _ => Cell::Missing,
        }
    }
}

/// Scalar access for declared display fields and the configuration dictionary boundary.
pub trait TextFields {
    fn string(&self, key: &str) -> &str;
    fn display(&self, key: &str) -> String;
}
fn borrowed(cell: Cell<'_>) -> &str {
    match cell {
        Cell::Text(Cow::Borrowed(text)) => text,
        _ => "",
    }
}
macro_rules! text_fields {($($name:ty),*)=>{$(impl TextFields for $name {
 fn string(&self,key:&str)->&str {borrowed(self.cell(key))}
 fn display(&self,key:&str)->String {self.cell(key).text()}
})*};}
text_fields!(
    Operation,
    ProtectionSet,
    RestorePoint,
    Health,
    Policy,
    PolicyEditor
);
impl<T: TextFields> TextFields for Option<T> {
    fn string(&self, key: &str) -> &str {
        self.as_ref().map(|v| v.string(key)).unwrap_or_default()
    }
    fn display(&self, key: &str) -> String {
        self.as_ref().map(|v| v.display(key)).unwrap_or_default()
    }
}
impl<T: TextFields + ?Sized> TextFields for &T {
    fn string(&self, key: &str) -> &str {
        (**self).string(key)
    }
    fn display(&self, key: &str) -> String {
        (**self).display(key)
    }
}
impl TextFields for BTreeMap<String, String> {
    fn string(&self, key: &str) -> &str {
        self.get(key).map(String::as_str).unwrap_or_default()
    }
    fn display(&self, key: &str) -> String {
        self.string(key).to_string()
    }
}
impl TextFields for serde_json::Value {
    fn string(&self, key: &str) -> &str {
        self[key].as_str().unwrap_or_default()
    }
    fn display(&self, key: &str) -> String {
        self[key].as_str().map(str::to_string).unwrap_or_else(|| {
            if self[key].is_null() {
                String::new()
            } else {
                self[key].to_string()
            }
        })
    }
}
