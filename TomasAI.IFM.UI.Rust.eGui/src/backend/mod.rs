//! Native backend boundary. No managed runtime or database access is used here.
pub mod codec;
pub mod configuration;
pub mod contract_view;
pub mod contracts;
pub mod date_display;
pub mod date_input;
pub mod health_http;
pub mod identity;
pub mod schema;
pub mod time_zone;
pub mod transport;
