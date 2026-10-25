//! Read-only client configuration compatibility. Files supply connection and
//! presentation settings, never business dates. Explicit environment overrides
//! win; a missing endpoint never selects an implicit backend.
use serde_json::Value;
use std::{io::Read,path::{Path,PathBuf},sync::OnceLock};

#[derive(Default)]
pub struct FileSettings { root: Value }

fn member<'a>(value:&'a Value,key:&str)->Option<&'a Value> {
    value.as_object()?.iter().find(|(name,_)|name.eq_ignore_ascii_case(key)).map(|(_,value)|value)
}
fn merge(target:&mut Value,source:Value) {
    if let (Some(left),Some(right))=(target.as_object_mut(),source.as_object()) {
        for (key,value) in right {
            let prior=left.keys().find(|name|name.eq_ignore_ascii_case(key)).cloned();
            if let Some(prior)=prior {merge(left.get_mut(&prior).unwrap(),value.clone());}
            else {left.insert(key.clone(),value.clone());}
        }
    } else {*target=source;}
}
impl FileSettings {
    /// Use the executable's existing profile, including the hybrid's managed
    /// data directory when present. An explicit path is mandatory when supplied.
    pub fn load(base:&Path,environment:&str,explicit:Option<&Path>)->Result<Self,String> {
        if environment.is_empty()||!environment.bytes().all(|b|b.is_ascii_alphanumeric()||b==b'-'||b==b'_') {
            return Err("Invalid client configuration environment".into());
        }
        let profile=format!("ui-client.{environment}.json");
        let managed=base.join("managed");
        let directory=if !base.join("ui-client.json").is_file()&&!base.join(&profile).is_file()
            && (managed.join("ui-client.json").is_file()||managed.join(&profile).is_file()) {managed.as_path()} else {base};
        let paths=explicit.map(|path|vec![path.to_path_buf()]).unwrap_or_else(||vec![directory.join("ui-client.json"),directory.join(profile)]);
        let mut result=Self{root:serde_json::json!({})};
        for path in paths {
            if !path.exists()&&explicit.is_none(){continue;}
            let metadata=std::fs::metadata(&path).map_err(|_|format!("Cannot read client configuration: {}",path.display()))?;
            if !metadata.is_file()||metadata.len()>2*1024*1024 {return Err("Client configuration must be a JSON file of at most 2 MiB".into());}
            let mut text=String::new();
            std::fs::File::open(&path).and_then(|file|file.take(2*1024*1024+1).read_to_string(&mut text)).map_err(|_|format!("Cannot read client configuration: {}",path.display()))?;
            if text.len()>2*1024*1024{return Err("Client configuration exceeds 2 MiB".into());}
            let value:Value=serde_json::from_str(text.trim_start_matches('\u{feff}')).map_err(|_|format!("Invalid JSON client configuration: {}",path.display()))?;
            if !value.is_object(){return Err("Client configuration must contain a JSON object".into());}
            merge(&mut result.root,value);
        }
        Ok(result)
    }
    pub fn value(&self,name:&str,environment:&impl Fn(&str)->Option<String>)->Result<Option<String>,String> {
        let aliases:&[&str]=match name {
            "IFM_UI_BROKER_ACCOUNT_ALIAS"=>&["IFM_UI_BROKER_ACCOUNT_ALIAS","TradeBroker__AccountAlias"],
            "IFM_UI_BROKER_ENVIRONMENT"=>&["IFM_UI_BROKER_ENVIRONMENT","TradeBroker__Environment"],
            _=>&[],
        };
        for key in std::iter::once(name).chain(aliases.iter().copied()) {
            if let Some(value)=environment(key){return Ok(Some(value));}
        }
        let path:Vec<&str>=match name {
            "IFM_UI_NATS_URL"=>vec!["AppSettings","NatsServerUri"],
            "IFM_UI_MARKETDATA_NATS_URL"=>vec!["MarketDataHost","NatsUrl"],
            "IFM_UI_MARKETDATA_SCOPE"=>vec!["MarketDataHost","Scope"],
            "IFM_UI_OPERATIONS_HEALTH_URL"=>vec!["MarketDataOperationsHealth","Endpoint"],
            "IFM_UI_BROKER_ACCOUNT_ALIAS"=>vec!["TradeBroker","AccountAlias"],
            "IFM_UI_BROKER_ENVIRONMENT"=>vec!["TradeBroker","Environment"],
            _=>name.split("__").collect(),
        };
        let mut value=Some(&self.root);
        for key in path {value=value.and_then(|node|member(node,key));}
        let result=match value {
            None|Some(Value::Null)=>None,
            Some(Value::String(value))=>Some(value.clone()),
            Some(Value::Bool(value))=>Some(value.to_string()),
            Some(Value::Number(value))=>Some(value.to_string()),
            _=>return Err(format!("Invalid scalar client setting: {name}")),
        };
        if matches!(name,"IFM_UI_ACTOR_HEALTH_URL"|"IFM_UI_STATUS_CONSOLE_URL")&&result.is_none() {
            return self.value("IFM_UI_OPERATIONS_HEALTH_URL",environment)?.map(|endpoint|reqwest::Url::parse(&endpoint)
                .and_then(|url|url.join(if name=="IFM_UI_ACTOR_HEALTH_URL"{"/api/actor-health"}else{"/api/status-console"})).map(String::from).map_err(|_|"Invalid operations-health endpoint".into())).transpose();
        }
        Ok(result)
    }
}

static FILES:OnceLock<Result<FileSettings,String>>=OnceLock::new();
pub fn value(name:&str)->Result<Option<String>,String> {
    let settings=FILES.get_or_init(|| {
        let base=std::env::current_exe().ok().and_then(|path|path.parent().map(Path::to_path_buf)).ok_or("Cannot locate the UI executable")?;
        let environment=std::env::var("DOTNET_ENVIRONMENT").unwrap_or("Development".into());
        let explicit=std::env::var_os("IFM_UI_CONFIG_PATH").map(PathBuf::from);
        FileSettings::load(&base,&environment,explicit.as_deref())
    }).as_ref().map_err(Clone::clone)?;
    settings.value(name,&|key|std::env::var(key).ok())
}

#[cfg(test)]
mod tests {
    use super::*;
    use std::collections::BTreeMap;
    fn configured()->FileSettings {FileSettings{root:serde_json::json!({"AppSettings":{"NatsServerUri":"nats://127.0.0.1:12345"},"TradeBroker":{"AccountAlias":"CONFIGURED","Environment":"Paper"},"SupervisorControl":{"Enabled":true,"Timeout":"00:02:00"},"MarketDataOperationsHealth":{"Endpoint":"http://127.0.0.1:12346/api/market-data/operations-health"}})}}
    #[test] fn environment_aliases_override_existing_profile_values() {
        let env=BTreeMap::from([("TradeBroker__AccountAlias","OVERRIDE"),("IFM_UI_BROKER_ENVIRONMENT","Live")]);
        let lookup=|key:&str|env.get(key).map(|value|value.to_string());
        assert_eq!(configured().value("IFM_UI_BROKER_ACCOUNT_ALIAS",&lookup).unwrap().as_deref(),Some("OVERRIDE"));
        assert_eq!(configured().value("IFM_UI_BROKER_ENVIRONMENT",&lookup).unwrap().as_deref(),Some("Live"));
        assert_eq!(configured().value("SupervisorControl__Enabled",&lookup).unwrap().as_deref(),Some("true"));
    }
    #[test] fn actor_health_follows_the_effective_operations_endpoint() {
        let env=|key:&str|(key=="IFM_UI_OPERATIONS_HEALTH_URL").then(||"http://127.0.0.1:9876/custom/health".into());
        assert_eq!(configured().value("IFM_UI_ACTOR_HEALTH_URL",&env).unwrap().as_deref(),Some("http://127.0.0.1:9876/api/actor-health"));
    }
    #[test] fn missing_configuration_never_invents_an_endpoint_or_account() {
        for key in ["IFM_UI_NATS_URL","IFM_UI_BROKER_ACCOUNT_ALIAS","IFM_UI_ACTOR_HEALTH_URL"] {assert_eq!(FileSettings::default().value(key,&|_|None).unwrap(),None);}
    }
    struct Directory(PathBuf);
    impl Directory {fn new()->Self {let path=PathBuf::from(env!("CARGO_MANIFEST_DIR")).join("target/configuration-tests").join(uuid::Uuid::new_v4().to_string());std::fs::create_dir_all(&path).unwrap();Self(path)}}
    impl Drop for Directory {fn drop(&mut self){for folder in [self.0.clone(),self.0.join("managed")] {for name in ["ui-client.json","ui-client.Development.json","explicit.json"]{let _=std::fs::remove_file(folder.join(name));}}let _=std::fs::remove_dir(self.0.join("managed"));let _=std::fs::remove_dir(&self.0);}}
    #[test] fn profiles_merge_case_insensitively_and_accept_utf8_bom() {
        let root=Directory::new();std::fs::write(root.0.join("ui-client.json"),r#"{"AppSettings":{"NatsServerUri":"base"},"TradeBroker":{"AccountAlias":"retained"}}"#).unwrap();
        std::fs::write(root.0.join("ui-client.Development.json"),"\u{feff}{\"appsettings\":{\"natsserveruri\":\"profile\"}}").unwrap();
        let value=FileSettings::load(&root.0,"Development",None).unwrap();
        assert_eq!(value.value("IFM_UI_NATS_URL",&|_|None).unwrap().as_deref(),Some("profile"));
        assert_eq!(value.value("IFM_UI_BROKER_ACCOUNT_ALIAS",&|_|None).unwrap().as_deref(),Some("retained"));
    }
    #[test] fn existing_managed_profile_is_read_as_data_without_loading_a_runtime() {
        let root=Directory::new();std::fs::create_dir(root.0.join("managed")).unwrap();
        std::fs::write(root.0.join("managed/ui-client.Development.json"),r#"{"AppSettings":{"NatsServerUri":"managed-profile"}}"#).unwrap();
        assert_eq!(FileSettings::load(&root.0,"Development",None).unwrap().value("IFM_UI_NATS_URL",&|_|None).unwrap().as_deref(),Some("managed-profile"));
    }
    #[test] fn explicit_missing_malformed_or_oversized_files_fail_without_fallback() {
        let root=Directory::new();let path=root.0.join("explicit.json");
        assert!(FileSettings::load(&root.0,"Development",Some(&path)).is_err());
        std::fs::write(&path,"broken").unwrap();assert!(FileSettings::load(&root.0,"Development",Some(&path)).is_err());
        std::fs::write(&path,vec![b' ';2*1024*1024+1]).unwrap();assert!(FileSettings::load(&root.0,"Development",Some(&path)).is_err());
        assert!(FileSettings::load(&root.0,"../Production",None).is_err());
    }
}
