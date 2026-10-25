//! Explicit desktop broker route, matching DesktopTradeExecutionContext.
//! Production cannot silently fall back to an emulator or another account.
#[derive(Clone, Debug, Eq, PartialEq)]
pub struct Context {
    pub account_alias: String,
    pub environment: String,
}
impl Context {
    pub fn resolve(
        app_environment: &str,
        alias: Option<&str>,
        mode: Option<&str>,
    ) -> Result<Self, String> {
        let development = app_environment == "Development";
        let alias = alias
            .or(if development {
                Some("IFM-EMULATOR-PAPER")
            } else {
                None
            })
            .filter(|v| !v.trim().is_empty())
            .ok_or("Configure the desktop broker account alias")?;
        let mode = mode
            .or(if development { Some("Emulator") } else { None })
            .ok_or("Configure the desktop broker environment")?;
        let environment = match mode {
            "Emulator" | "1" => "Emulator",
            "Paper" | "2" => "Paper",
            "Live" | "3" => "Live",
            _ => return Err("Invalid desktop broker environment".into()),
        };
        if development && environment != "Emulator" {
            return Err("Development desktop orders require the emulator".into());
        }
        Ok(Self {
            account_alias: alias.into(),
            environment: environment.into(),
        })
    }
    pub fn from_env() -> Result<Self, String> {
        let app = std::env::var("DOTNET_ENVIRONMENT").unwrap_or("Development".into());
        let mut alias = crate::backend::configuration::value("IFM_UI_BROKER_ACCOUNT_ALIAS")?;
        if alias.is_none() && app == "Development" {
            alias=crate::backend::configuration::value("TradeBroker__Emulator__AccountAlias")?;
        }
        let mode = crate::backend::configuration::value("IFM_UI_BROKER_ENVIRONMENT")?;
        Self::resolve(&app, alias.as_deref(), mode.as_deref())
    }
}
