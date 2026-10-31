//! Typed presentation models. Backend actors retain all financial calculations;
//! these models decode published values and prepare display coordinates only.
pub mod cells;
pub mod trade_chart;
pub mod trade_contracts;
pub mod trade_presentation;
pub mod wire_types;

pub mod actor_health;
pub mod health_fields;
pub mod operations_health;
pub(crate) mod portfolio;
pub mod strategy;

pub mod backup;

pub mod eod;

pub mod backend_document;

pub mod broker_presentation;

pub mod labels;

pub mod scheduled;
