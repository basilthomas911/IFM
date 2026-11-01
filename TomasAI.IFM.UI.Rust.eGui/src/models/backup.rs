//! Concrete backup display documents shared by native controllers and egui.
use super::cells::{Cell, Cells, ScalarCell};
pub use super::labels::Label;
use serde::{Deserialize, Serialize};
use std::{borrow::Cow, collections::BTreeMap};

/// Operation used by the backup component.
#[derive(Clone, Debug, Default, Serialize, Deserialize, PartialEq)]
#[serde(default, rename_all = "camelCase")]
pub struct Operation {
    /// Optional operation id; `None` represents an absent value.
    pub operation_id: Option<String>,
    /// Optional protection set; `None` represents an absent value.
    pub protection_set: Option<String>,
    /// Optional source; `None` represents an absent value.
    pub source: Option<Label>,
    /// Optional phase; `None` represents an absent value.
    pub phase: Option<Label>,
    /// Optional outcome; `None` represents an absent value.
    pub outcome: Option<Label>,
    /// Optional progress percent; `None` represents an absent value.
    pub progress_percent: Option<i32>,
    /// Optional safe diagnostic reference; `None` represents an absent value.
    pub safe_diagnostic_reference: Option<String>,
    /// Optional requested mode; `None` represents an absent value.
    pub requested_mode: Option<Label>,
    /// Optional resolved mode; `None` represents an absent value.
    pub resolved_mode: Option<Label>,
    /// Optional created utc; `None` represents an absent value.
    pub created_utc: Option<String>,
    /// Optional completed utc; `None` represents an absent value.
    pub completed_utc: Option<String>,
    /// Optional backup set id; `None` represents an absent value.
    pub backup_set_id: Option<String>,
    /// Optional kind; `None` represents an absent value.
    pub kind: Option<Label>,
    /// Optional engine; `None` represents an absent value.
    pub engine: Option<Label>,
}
impl Cells for Operation {
    /// Resolves a named display cell; unsupported columns remain missing.
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

/// Protection Set used by the backup component.
#[derive(Clone, Debug, Default, Serialize, Deserialize, PartialEq)]
#[serde(default, rename_all = "camelCase")]
pub struct ProtectionSet {
    /// Optional id; `None` represents an absent value.
    pub id: Option<String>,
    /// Optional source; `None` represents an absent value.
    pub source: Option<Label>,
    /// Optional engines; `None` represents an absent value.
    pub engines: Option<Vec<Label>>,
    /// Optional enabled; `None` represents an absent value.
    pub enabled: Option<bool>,
    /// Optional policy revision; `None` represents an absent value.
    pub policy_revision: Option<i64>,
}
impl Cells for ProtectionSet {
    /// Resolves a named display cell; unsupported columns remain missing.
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

/// Restore Point used by the backup component.
#[derive(Clone, Debug, Default, Serialize, Deserialize, PartialEq)]
#[serde(default, rename_all = "camelCase")]
pub struct RestorePoint {
    /// Optional restore point id; `None` represents an absent value.
    pub restore_point_id: Option<String>,
    /// Optional recovery point utc; `None` represents an absent value.
    pub recovery_point_utc: Option<String>,
    /// Optional verification level; `None` represents an absent value.
    pub verification_level: Option<Label>,
    /// Optional verified utc; `None` represents an absent value.
    pub verified_utc: Option<String>,
    /// Optional restore tested utc; `None` represents an absent value.
    pub restore_tested_utc: Option<String>,
    /// Optional eligible; `None` represents an absent value.
    pub eligible: Option<bool>,
}
impl Cells for RestorePoint {
    /// Resolves a named display cell; unsupported columns remain missing.
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

/// Health used by the backup component.
#[derive(Clone, Debug, Default, Serialize, Deserialize, PartialEq)]
#[serde(default, rename_all = "camelCase")]
pub struct Health {
    /// Optional host id; `None` represents an absent value.
    pub host_id: Option<String>,
    /// Optional ready; `None` represents an absent value.
    pub ready: Option<bool>,
    /// Optional capability; `None` represents an absent value.
    pub capability: Option<Label>,
    /// Optional observed utc; `None` represents an absent value.
    pub observed_utc: Option<String>,
    /// Optional diagnostic; `None` represents an absent value.
    pub diagnostic: Option<String>,
}
impl Cells for Health {
    /// Resolves a named display cell; unsupported columns remain missing.
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

/// Setup used by the backup component.
#[derive(Clone, Debug, Default, Serialize, Deserialize, PartialEq)]
#[serde(default, rename_all = "camelCase")]
pub struct Setup {
    /// Keyed backup host settings retained by Setup.
    pub backup_host_settings: BTreeMap<String, String>,
    /// Optional available; `None` represents an absent value.
    pub available: Option<bool>,
}

/// Dashboard used by the backup component.
#[derive(Clone, Debug, Default, Serialize, Deserialize, PartialEq)]
#[serde(default, rename_all = "camelCase")]
pub struct Dashboard {
    /// Source associated with this dashboard.
    pub source: String,
    /// Ordered protection sets retained by Dashboard.
    pub protection_sets: Vec<ProtectionSet>,
    /// Ordered recent operations retained by Dashboard.
    pub recent_operations: Vec<Operation>,
    /// Optional latest verified; `None` represents an absent value.
    pub latest_verified: Option<RestorePoint>,
    /// Optional latest restore tested; `None` represents an absent value.
    pub latest_restore_tested: Option<RestorePoint>,
}

/// Policy used by the backup component.
#[derive(Clone, Debug, Default, Serialize, Deserialize, PartialEq)]
#[serde(default, rename_all = "camelCase")]
pub struct Policy {
    /// Optional id; `None` represents an absent value.
    pub id: Option<String>,
    /// Optional revision; `None` represents an absent value.
    pub revision: Option<i64>,
    /// Optional enforced; `None` represents an absent value.
    pub enforced: Option<bool>,
    /// Optional enabled sources; `None` represents an absent value.
    pub enabled_sources: Option<Vec<Label>>,
    /// Ordered protection sets retained by Policy.
    pub protection_sets: Vec<Option<String>>,
    /// Rpo associated with this policy.
    pub rpo: String,
    /// Rto associated with this policy.
    pub rto: String,
    /// Optional daily; `None` represents an absent value.
    pub daily: Option<i32>,
    /// Optional weekly; `None` represents an absent value.
    pub weekly: Option<i32>,
    /// Optional monthly; `None` represents an absent value.
    pub monthly: Option<i32>,
    /// Optional verification levels; `None` represents an absent value.
    pub verification_levels: Option<Vec<Label>>,
    /// Maximum verification age associated with this policy.
    pub maximum_verification_age: String,
}
impl Cells for Policy {
    /// Resolves a named display cell; unsupported columns remain missing.
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

/// Policy Editor used by the backup component.
#[derive(Clone, Debug, Default, Serialize, Deserialize, PartialEq)]
#[serde(default, rename_all = "camelCase")]
pub struct PolicyEditor {
    /// Optional rpo minutes; `None` represents an absent value.
    #[serde(skip_serializing_if = "Option::is_none")]
    pub rpo_minutes: Option<String>,
    /// Optional rto minutes; `None` represents an absent value.
    #[serde(skip_serializing_if = "Option::is_none")]
    pub rto_minutes: Option<String>,
    /// Optional maximum verification hours; `None` represents an absent value.
    #[serde(skip_serializing_if = "Option::is_none")]
    pub maximum_verification_hours: Option<String>,
}
impl Cells for PolicyEditor {
    /// Resolves a named display cell; unsupported columns remain missing.
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
    /// Reads the text representation expected by this component.
    fn string(&self, key: &str) -> &str;


    /// Provides display for the text fields implementation.
    fn display(&self, key: &str) -> String;
}


/// Exposes the borrowed display representation without cloning its owner.
fn borrowed(cell: Cell<'_>) -> &str {
    match cell {
        Cell::Text(Cow::Borrowed(text)) => text,
        _ => "",
    }
}
// Keep raw borrowed fields distinct from formatted property-grid text.
macro_rules! text_fields {
    ($($name:ty),*) => {
        $(
            impl TextFields for $name {
                /// Borrows a text cell; non-text cells remain empty.
                fn string(&self, key: &str) -> &str {
                    borrowed(self.cell(key))
                }


                /// Formats the requested cell for display.
                fn display(&self, key: &str) -> String {
                    self.cell(key).text()
                }
            }
        )*
    };
}
text_fields!(
    Operation,
    ProtectionSet,
    RestorePoint,
    Health,
    Policy,
    PolicyEditor
);
impl<T: TextFields> TextFields for Option<T> {
    /// Reads the text representation expected by this component.
    fn string(&self, key: &str) -> &str {
        self.as_ref().map(|v| v.string(key)).unwrap_or_default()
    }


    /// Provides display for the option<t> implementation.
    fn display(&self, key: &str) -> String {
        self.as_ref().map(|v| v.display(key)).unwrap_or_default()
    }
}
impl<T: TextFields + ?Sized> TextFields for &T {
    /// Reads the text representation expected by this component.
    fn string(&self, key: &str) -> &str {
        (**self).string(key)
    }


    /// Provides display for the &t implementation.
    fn display(&self, key: &str) -> String {
        (**self).display(key)
    }
}
impl TextFields for BTreeMap<String, String> {
    /// Reads the text representation expected by this component.
    fn string(&self, key: &str) -> &str {
        self.get(key).map(String::as_str).unwrap_or_default()
    }


    /// Provides display for the b tree map<string, string> implementation.
    fn display(&self, key: &str) -> String {
        self.string(key).to_string()
    }
}
impl TextFields for serde_json::Value {
    /// Reads the text representation expected by this component.
    fn string(&self, key: &str) -> &str {
        self[key].as_str().unwrap_or_default()
    }


    /// Provides display for the serde json::value implementation.
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
