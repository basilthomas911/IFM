//! Read-only smoke check of the desktop's market-view loaders against an explicit client profile.
use ifm_ui_egui::{
    backend::{schema::Registry, transport::{Backend, Settings}},
    services::{business_context::BusinessContext, market_history, outlook},
};


/// Loads published Outlook and chart data without starting feeds or submitting commands.
async fn verify() -> Result<(), String> {
    let backend = Backend::connect(Settings::from_env()?).await?;
    let registry = Registry::compiled()?;
    for observation in 1..=2 {
        let session = BusinessContext::load(&backend).await?;
        if !session.is_market_open() {
            return Err("The API has no open market session to verify".into());
        }
        let outlook = outlook::load_for_session(&backend, &registry, Some(&session)).await?;
        if outlook.cells.is_empty() {
            return Err(format!("Market Outlook is empty: {}", outlook.status));
        }
        println!("Observation {observation}: {}; Outlook cells={}", outlook.summary, outlook.cells.len());
        let charts = market_history::load_for_session(&backend, &registry, &session).await;
        for name in ["ES", "VX", "ES-BB"] {
            let chart = charts.get(name).ok_or_else(|| format!("Missing {name} chart"))?;
            let points = chart.series.first().map_or(0, |series| series.points.len());
            if !chart.status.is_empty() || points == 0 {
                return Err(format!("{name} chart has {points} points: {}", chart.status));
            }
            println!("{name}: series={}, points={points}", chart.series.len());
            if name == "VX" {
                if chart.series.len() != 2 || chart.series.iter().any(|series| series.points.is_empty()) {
                    return Err("VX must have populated front and back month series".into());
                }
                for series in &chart.series {
                    println!("VX {}: color={}, points={}", series.name, series.color, series.points.len());
                }
            }
        }
        if observation == 1 {
            tokio::time::sleep(std::time::Duration::from_secs(16)).await;
        }
    }
    Ok(())
}


/// Reports verification failure as a nonzero exit code for repeatable Development checks.
#[tokio::main]
async fn main() {
    if let Err(error) = verify().await {
        eprintln!("Market-view verification failed: {error}");
        std::process::exit(1);
    }
}
