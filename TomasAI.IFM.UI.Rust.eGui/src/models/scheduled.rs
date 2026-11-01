//! Declared scheduler display documents. Execution and cron evaluation remain server operations.
use super::{labels::Label, wire_types::*};
use serde::{Deserialize, Serialize};

/// Preview used by the scheduled component.
#[derive(Clone, Debug, Default, Serialize, Deserialize)]
#[serde(default, rename_all = "camelCase")]
pub struct Preview {
    /// Optional valid; `None` represents an absent value.
    pub valid: Option<bool>,
    /// Optional errors; `None` represents an absent value.
    pub errors: Option<Vec<String>>,
    /// Ordered next fire times utc retained by Preview.
    pub next_fire_times_utc: Vec<String>,
}

/// Project used by the scheduled component.
#[derive(Clone, Debug, Default, Serialize, Deserialize)]
#[serde(default, rename_all = "camelCase")]
pub struct Project {
    /// Optional task key; `None` represents an absent value.
    pub task_key: Option<String>,
    /// Optional name; `None` represents an absent value.
    pub name: Option<String>,
    /// Optional available; `None` represents an absent value.
    pub available: Option<bool>,
    /// Optional platform; `None` represents an absent value.
    pub platform: Option<String>,
    /// Optional manifest version; `None` represents an absent value.
    pub manifest_version: Option<String>,
}

/// Run used by the scheduled component.
#[derive(Clone, Debug, Default, Serialize, Deserialize)]
#[serde(default, rename_all = "camelCase")]
pub struct Run {
    /// Id associated with this run.
    pub id: String,
    /// Current status text or state exposed by this component.
    pub status: Option<Label>,
    /// Optional stage; `None` represents an absent value.
    pub stage: Option<String>,
    /// Optional detail; `None` represents an absent value.
    pub detail: Option<String>,
    /// Optional exit code; `None` represents an absent value.
    pub exit_code: Option<i32>,
    /// Optional value date; `None` represents an absent value.
    pub value_date: Option<DateText>,
    /// Optional revision; `None` represents an absent value.
    pub revision: Option<i64>,
    /// Optional operation command id; `None` represents an absent value.
    pub operation_command_id: Option<String>,
    /// Optional process id; `None` represents an absent value.
    pub process_id: Option<i32>,
    /// Optional standard output tail; `None` represents an absent value.
    pub standard_output_tail: Option<String>,
    /// Optional standard error tail; `None` represents an absent value.
    pub standard_error_tail: Option<String>,
    /// Optional output directory; `None` represents an absent value.
    pub output_directory: Option<String>,
    /// Optional intended fire time utc; `None` represents an absent value.
    pub intended_fire_time_utc: Option<String>,
    /// Optional started at utc; `None` represents an absent value.
    pub started_at_utc: Option<String>,
    /// Optional finished at utc; `None` represents an absent value.
    pub finished_at_utc: Option<String>,
}
