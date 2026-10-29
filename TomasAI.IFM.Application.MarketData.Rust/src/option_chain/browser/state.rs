//! Bounded explicit view ownership. Idle UI time never removes live demand;
//! release or runtime/ValueDate teardown ends it without a renewal heartbeat.
use super::{Command, Family};
use std::collections::{BTreeMap, BTreeSet};
use tokio::time::Instant;
#[derive(Clone)]
struct View {
    required: BTreeSet<String>,
}
#[derive(Clone)]
struct Entry {
    command: Command,
    views: BTreeMap<String, View>,
}
/// Copied exact-family demand, independent of all dataset generations.
#[derive(Clone, Default)]
pub struct Directory {
    families: BTreeMap<Family, Entry>,
}
impl Directory {
    /// Accepts or releases a view atomically. Validation/capacity failure leaves
    /// the previous directory intact. Repeated observation replaces only this
    /// view's demand, never a dataset recovery deadline.
    ///
    /// # Errors
    /// More than 64 families/total views, 8,192 retained leg references, conflicting source identity or
    /// more than 2,048 distinct required contracts across all owners.
    pub fn apply(&mut self, command: Command, _now: Instant) -> Result<bool, String> {
        let before = self.demands();
        let mut next = self.clone();
        if command.release {
            if let Some(entry) = next.families.get_mut(&command.family) {
                entry.views.remove(&command.owner);
            }
            next.families.retain(|_, entry| !entry.views.is_empty());
        } else {
            if next.families.len() == 64 && !next.families.contains_key(&command.family) {
                return Err("Native browser family capacity exceeded".into());
            }
            let entry = next
                .families
                .entry(command.family.clone())
                .or_insert_with(|| Entry {
                    command: command.clone(),
                    views: BTreeMap::new(),
                });
            if entry.command.root != command.root || entry.command.providers != command.providers {
                return Err("Native browser family source conflicts".into());
            }
            if entry.views.len() == 64 && !entry.views.contains_key(&command.owner) {
                return Err("Native browser view capacity exceeded".into());
            }
            entry.views.insert(
                command.owner.clone(),
                View {
                    required: command.required.clone(),
                },
            );
            entry.command = command;
        }
        let views = next
            .families
            .values()
            .flat_map(|entry| entry.views.values())
            .collect::<Vec<_>>();
        if views.len() > 64 || views.iter().map(|view| view.required.len()).sum::<usize>() > 8192 {
            return Err("Native browser copied-view capacity exceeded".into());
        }
        let union = next
            .families
            .values()
            .flat_map(|entry| entry.views.values().flat_map(|view| view.required.iter()))
            .collect::<BTreeSet<_>>();
        if union.len() > 2048 {
            return Err("Native browser required union capacity exceeded".into());
        }
        let after = next.demands();
        let changed = !same(&before, &after);
        *self = next;
        Ok(changed)
    }
    /// Whether explicit browser demand remains, regardless of UI inactivity.
    pub fn has_work(&self) -> bool {
        !self.families.is_empty()
    }
    /// Detached latest query with every surviving view's required legs united.
    pub fn demands(&self) -> Vec<Command> {
        self.families
            .values()
            .map(|entry| {
                let mut command = entry.command.clone();
                command.required = entry
                    .views
                    .values()
                    .flat_map(|view| view.required.iter().cloned())
                    .collect();
                command
            })
            .collect()
    }
}
fn same(a: &[Command], b: &[Command]) -> bool {
    a.len() == b.len()
        && a.iter().zip(b).all(|(a, b)| {
            a.family == b.family
                && a.root == b.root
                && a.providers == b.providers
                && a.required == b.required
                && a.multiplier.to_bits() == b.multiplier.to_bits()
                && a.wing.bits == b.wing.bits
        })
}
