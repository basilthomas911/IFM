//! Strategy workflow observations, published decision details, catalog definitions, and parameter-set administration.

/// Strategy parameter-set editing, comparison, and activation previews. Field paths retain identity across nested component editors; numeric text buffers preserve incomplete edits until managed validation.
pub mod parameter_sets;
/// Versioned strategy catalog administration. Schema-specific sections and templates edit a local JSON-shaped draft; published reference choices and managed commands control validation, lifecycle, and persistence.
pub mod strategy_catalog;
/// Risk/workflow inspection with paged results, decision evidence, and the shared details tree. Queries and refreshes are correlated through publication outcomes; the UI does not re-evaluate risk.
pub mod strategy_inspection;
/// ITI signal chart and Strategy Updates/Details/Summary tabs.
pub mod strategy_workspace;
