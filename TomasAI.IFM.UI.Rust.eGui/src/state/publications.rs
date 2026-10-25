//! Each native controller owns its publication generation independently.
//! Retain results by view so a slow reply for a hidden screen cannot replace
//! another screen's rows or give its next intent the wrong generation.
use crate::contract_editor::ContractPublication;
use crate::lookup_editor::EditorOutcome;
use std::collections::BTreeMap;

fn merge_outcomes(
    mut outcomes: Vec<EditorOutcome>,
    incoming: Vec<EditorOutcome>,
) -> Result<Vec<EditorOutcome>, String> {
    for outcome in incoming {
        if outcome.request_id.is_empty() {
            return Err("Publication outcome lacks its request identity".into());
        }
        if let Some(previous) = outcomes
            .iter_mut()
            .find(|v| v.request_id == outcome.request_id)
        {
            let terminal = matches!(previous.state.as_str(), "Completed" | "Failed");
            if terminal && matches!(outcome.state.as_str(), "Unknown" | "Pending") {
                continue;
            }
            if terminal && previous.state != outcome.state {
                return Err("Publication contains conflicting terminal outcomes".into());
            }
            *previous = outcome;
        } else {
            if outcomes.len() >= 256 {
                return Err(
                    "Too many unconsumed view outcomes; open the view to receive them".into(),
                );
            }
            outcomes.push(outcome);
        }
    }
    Ok(outcomes)
}

#[derive(Default)]
pub struct Publications {
    views: BTreeMap<String, ContractPublication>,
    empty: ContractPublication,
}
impl Publications {
    pub fn get(&self, view: &str) -> &ContractPublication {
        self.views.get(view).unwrap_or(&self.empty)
    }
    /// Acknowledge UI delivery only after all renderers for this view ran. This
    /// does not acknowledge or change a business command's outcome/journal.
    pub fn acknowledge_rendered(&mut self, view: &str) {
        if let Some(value) = self.views.get_mut(view) {
            value.outcomes.clear();
        }
    }
    pub fn apply(&mut self, mut publication: ContractPublication) -> Result<bool, String> {
        let view = publication.result.view.as_str();
        if !matches!(
            view,
            "strategy-catalog"
                | "parameter-sets"
                | "actor-health"
                | "operations-health"
                | "scheduled-tasks"
                | "provider-definitions"
                | "economic-calendar"
                | "yield-curve"
                | "futures-contract"
                | "option-contract"
                | "portfolio"
                | "database-backup"
        ) {
            return Err(format!("Unknown native publication view: {view}"));
        }
        if publication.schema != 1 {
            return Err("Unsupported native publication schema".into());
        }
        if let Some(old) = self.views.get_mut(view) {
            let outcomes = merge_outcomes(
                old.outcomes.clone(),
                std::mem::take(&mut publication.outcomes),
            )?;
            // A late reply can still carry an undelivered command outcome even
            // when its data generation has already been superseded by a refresh.
            if old.generation >= publication.generation {
                old.outcomes = outcomes;
                return Ok(false);
            }
            publication.outcomes = outcomes;
        } else {
            publication.outcomes =
                merge_outcomes(Vec::new(), std::mem::take(&mut publication.outcomes))?;
        }
        self.views.insert(view.to_owned(), publication);
        Ok(true)
    }
}
