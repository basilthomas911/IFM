//! Reference-data editor state machines. Views edit local drafts and send correlated intents; managed services validate and persist changes.

/// Economic-calendar CRUD view. Filter date/country, row selection, deletion confirmation, and local drafts survive frames; outcomes release the matching pending request.
pub mod calendar_editor;
/// Futures and futures-option contract editors and shared publication DTOs.
pub mod contract_editor;
/// Lookup CRUD editing and shared request outcomes. A request ID identifies the local pending action; rendering never treats submission as proof of successful persistence.
pub mod lookup_editor;
/// Yield-curve rate editing, date filtering, imports, and tenor definitions. Draft strings are forwarded to managed validation; the renderer does not calculate or normalize financial rates.
pub mod yield_curve_editor;
