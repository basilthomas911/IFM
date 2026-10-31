//! Shared concrete portfolio publication records used by native producers and renderers.
use crate::contract_editor::ContractRow;
use serde::Deserialize;
use std::collections::BTreeMap;
/// Managed field-choice value, display label, and selection eligibility.
#[derive(Default, Clone, Deserialize, serde::Serialize)]
#[serde(default, rename_all = "camelCase")]
pub(crate) struct Choice {
    /// Stable choice value submitted to the managed field contract when selected.
    pub(crate) value: String,
    /// Human-readable caption for this identity/value.
    pub(crate) label: String,
    /// Published control/action eligibility; local pending state can further restrict it.
    pub(crate) enabled: bool,
}
/// Managed property editor definition. Kind chooses the widget; numeric bounds/decimals and choices come from the managed form contract.
#[derive(Default, Clone, Deserialize, serde::Serialize)]
#[serde(default, rename_all = "camelCase")]
pub(crate) struct Field {
    /// Stable managed property key used to namespace the local draft and submit edits.
    pub(crate) key: String,
    /// Managed field caption displayed beside its value editor.
    pub(crate) caption: String,
    /// Published or edited display value; formatting/validation belongs to its managed field contract.
    pub(crate) value: String,
    /// Managed field-kind discriminator selecting the editor widget/representation.
    pub(crate) kind: String,
    /// Published control/action eligibility; local pending state can further restrict it.
    pub(crate) enabled: bool,
    /// Published permitted values for the field/editor.
    pub(crate) choices: Option<Vec<Choice>>,
    /// Smallest value accepted by this managed numeric editor definition.
    pub(crate) minimum: f64,
    /// Largest value accepted by this managed numeric editor definition.
    pub(crate) maximum: f64,
    /// Configured number of decimal places for a numeric editor.
    pub(crate) decimals: usize,
}
/// Managed grid definition: ordered columns, stable rows, selection, and editability.
#[derive(Default, Clone, Deserialize, serde::Serialize)]
#[serde(default, rename_all = "camelCase")]
pub(crate) struct Grid {
    /// Stable managed grid key used for widget IDs, selection, and grid actions.
    pub(crate) key: String,
    /// Ordered column definitions or captured column-rule positions for this grid.
    pub(crate) columns: Vec<String>,
    /// Rows supplied by the enclosing publication/model; they are not fetched during drawing.
    pub(crate) rows: Vec<ContractRow>,
    /// Stable selected identity/index; interpretation follows the enclosing view's row contract.
    pub(crate) selected: Option<String>,
    /// Whether the managed contract allows local cell editing.
    pub(crate) editable: bool,
}
/// Managed form definition with reference dimensions, fields, grids, buttons, and eligibility.
#[derive(Default, Clone, Deserialize, serde::Serialize)]
#[serde(default, rename_all = "camelCase")]
pub(crate) struct Dialog {
    /// Stable managed dialog key used for local draft namespaces and action routing.
    pub(crate) key: String,
    /// Published form/window/section title.
    pub(crate) title: String,
    /// Reference control width in egui logical points unless the platform structure specifies pixels.
    pub(crate) width: f32,
    /// Reference control height in egui logical points.
    pub(crate) height: f32,
    /// Reference label width used to align managed form fields.
    pub(crate) caption_width: f32,
    /// Ordered typed property editors supplied by the managed form definition.
    pub(crate) fields: Vec<Field>,
    /// Ordered managed action captions exposed by the dialog.
    pub(crate) buttons: Vec<String>,
    /// Display-ready status supplied by the managed projection or current view state.
    pub(crate) status: String,
    /// Managed grid definitions displayed by this form.
    pub(crate) grids: Option<Vec<Grid>>,
    /// Managed action eligibility keyed by the original action caption.
    pub(crate) button_enabled: Option<BTreeMap<String, bool>>,
}
/// Portfolio administration projection, including optional embedded policy and child dialogs.
#[derive(Default, Clone, Deserialize, serde::Serialize)]
#[serde(default, rename_all = "camelCase")]
pub(crate) struct Document {
    /// Managed grid definitions displayed by this form.
    pub(crate) grids: Vec<Grid>,
    /// Backend action eligibility indexed by the original administration action captions.
    pub(crate) enabled: BTreeMap<String, bool>,
    /// Managed view context/eligibility values indexed by stable keys.
    pub(crate) context: BTreeMap<String, String>,
    /// Ordered managed child dialogs to render above administration.
    pub(crate) dialogs: Vec<Dialog>,
    /// Optional embedded Risk Policy dialog definition.
    pub(crate) policy: Option<Dialog>,
    /// Selected Funds subtab content, rendered inside administration rather than as a modal.
    pub(crate) fund_view: Option<Dialog>,
}

/// Scalar editor conversion; already typed text never passes through a JSON value.
pub(crate) trait FieldText {
    fn field_text(self) -> String;
}
impl FieldText for String {
    fn field_text(self) -> String {
        self
    }
}
impl FieldText for &String {
    fn field_text(self) -> String {
        self.clone()
    }
}
impl FieldText for &str {
    fn field_text(self) -> String {
        self.to_string()
    }
}
impl FieldText for serde_json::Value {
    fn field_text(self) -> String {
        (&self).field_text()
    }
}
impl FieldText for &serde_json::Value {
    fn field_text(self) -> String {
        self.as_str().map(str::to_string).unwrap_or_else(|| {
            if self.is_null() {
                String::new()
            } else {
                self.to_string()
            }
        })
    }
}
impl FieldText for bool {
    fn field_text(self) -> String {
        self.to_string()
    }
}
impl FieldText for i32 {
    fn field_text(self) -> String {
        self.to_string()
    }
}
impl FieldText for i64 {
    fn field_text(self) -> String {
        self.to_string()
    }
}

#[cfg(test)]
mod tests {
    use super::*;
    #[test]
    fn all_managed_portfolio_session_documents_preserve_typed_values() {
        let root = std::path::Path::new(env!("CARGO_MANIFEST_DIR")).join("tests/fixtures/backend");
        let mut checked = 0;
        for entry in std::fs::read_dir(root).unwrap() {
            let path = entry.unwrap().path();
            if !path
                .file_name()
                .unwrap()
                .to_string_lossy()
                .starts_with("portfolio-session-")
                || path.extension().and_then(|value| value.to_str()) != Some("msgpack")
            {
                continue;
            }
            let bytes = std::fs::read(&path).unwrap();
            let document: Document = crate::wire::decode(&bytes).unwrap();
            let mut expected: serde_json::Value = crate::wire::decode(&bytes).unwrap();
            let mut actual = serde_json::to_value(&document).unwrap();
            fn normalize_numbers(value: &mut serde_json::Value) {
                match value {
                    serde_json::Value::Array(values) => {
                        values.iter_mut().for_each(normalize_numbers)
                    }
                    serde_json::Value::Object(values) => {
                        values.values_mut().for_each(normalize_numbers)
                    }
                    serde_json::Value::Number(number) => {
                        if let Some(value) = number.as_f64().filter(|value| {
                            value.fract() == 0. && value.abs() < (1u64 << 53) as f64
                        }) {
                            *number = serde_json::Number::from(value as i64);
                        }
                    }
                    _ => {}
                }
            }
            normalize_numbers(&mut expected);
            normalize_numbers(&mut actual);
            assert_eq!(
                expected,
                actual,
                "{} managed fields changed",
                path.display()
            );
            for dialog in document
                .dialogs
                .iter()
                .chain(document.policy.iter())
                .chain(document.fund_view.iter())
            {
                assert!(!dialog.key.is_empty(), "{}", path.display());
            }
            checked += 1;
        }
        assert!(checked >= 20, "Only checked {checked} managed sessions");
    }
}
