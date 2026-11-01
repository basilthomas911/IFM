//! Validate editable timezone identifiers against the same Windows identifiers
//! accepted by the hybrid, plus IANA names. This does not determine business time.


/// Checks is known against the current time zone state.
pub fn is_known(name: &str) -> bool {
    if name.parse::<chrono_tz::Tz>().is_ok() {
        return true;
    }
    #[cfg(windows)]
    {
        windows::find(name).is_some()
    }
    #[cfg(not(windows))]
    {
        false
    }
}


/// Exchange-local calendar date of an API/provider instant. This is timezone
/// conversion only; the API remains the sole source of current/business time.
pub fn local_date(
    name: &str,
    instant: chrono::DateTime<chrono::Utc>,
) -> Result<chrono::NaiveDate, String> {
    if let Ok(zone) = name.parse::<chrono_tz::Tz>() {
        return Ok(instant.with_timezone(&zone).date_naive());
    }
    #[cfg(windows)]
    {
        return windows::local_date(name, instant);
    }
    #[cfg(not(windows))]
    {
        Err("Unknown exchange time zone".into())
    }
}
#[cfg(windows)]
mod windows {
    use chrono::{Datelike, Timelike};

    /// System Time used by the time zone component.
    #[repr(C)]
    #[derive(Default)]
    struct SystemTime {
        /// Year associated with this system time.
        year: u16,
        /// Month associated with this system time.
        month: u16,
        /// Weekday associated with this system time.
        weekday: u16,
        /// Day associated with this system time.
        day: u16,
        /// Hour associated with this system time.
        hour: u16,
        /// Minute associated with this system time.
        minute: u16,
        /// Second associated with this system time.
        second: u16,
        /// Milliseconds associated with this system time.
        milliseconds: u16,
    }

    /// Dynamic Zone used by the time zone component.
    #[repr(C)]
    pub(super) struct DynamicZone {
        /// Bias associated with this dynamic zone.
        bias: i32,
        /// Standard name associated with this dynamic zone.
        standard_name: [u16; 32],
        /// Standard date associated with this dynamic zone.
        standard_date: SystemTime,
        /// Standard bias associated with this dynamic zone.
        standard_bias: i32,
        /// Daylight name associated with this dynamic zone.
        daylight_name: [u16; 32],
        /// Daylight date associated with this dynamic zone.
        daylight_date: SystemTime,
        /// Daylight bias associated with this dynamic zone.
        daylight_bias: i32,
        /// Key associated with this dynamic zone.
        key: [u16; 128],
        /// Dynamic disabled associated with this dynamic zone.
        dynamic_disabled: u8,
    }
    #[link(name = "kernel32")]
    unsafe extern "system" {
        /// Reads a Windows timezone definition by its enumeration index.
        fn EnumDynamicTimeZoneInformation(index: u32, information: *mut DynamicZone) -> u32;


        /// Converts a UTC SYSTEMTIME using a Windows timezone's historical rules.
        fn SystemTimeToTzSpecificLocalTimeEx(
            zone: *const DynamicZone,
            utc: *const SystemTime,
            local: *mut SystemTime,
        ) -> i32;
    }


    /// Finds time zone for time zone.
    pub(super) fn find(name: &str) -> Option<DynamicZone> {
        for index in 0..1024 {
            // Windows fills the documented C buffer on success; no returned
            // pointers are retained. ERROR_NO_MORE_ITEMS stops enumeration.
            let mut zone: DynamicZone = unsafe { std::mem::zeroed() };
            if unsafe { EnumDynamicTimeZoneInformation(index, &mut zone) } != 0 {
                return None;
            }
            let length = zone
                .key
                .iter()
                .position(|c| *c == 0)
                .unwrap_or(zone.key.len());
            if String::from_utf16_lossy(&zone.key[..length]).eq_ignore_ascii_case(name) {
                return Some(zone);
            }
        }
        None
    }


    /// Converts the supplied instant to a calendar date in the configured timezone.
    pub(super) fn local_date(
        name: &str,
        time: chrono::DateTime<chrono::Utc>,
    ) -> Result<chrono::NaiveDate, String> {
        let zone = find(name).ok_or("Unknown exchange time zone")?;
        let utc = SystemTime {
            year: u16::try_from(time.year()).map_err(|_| "Invalid expiry year")?,
            month: time.month() as u16,
            day: time.day() as u16,
            hour: time.hour() as u16,
            minute: time.minute() as u16,
            second: time.second() as u16,
            milliseconds: time.timestamp_subsec_millis() as u16,
            ..Default::default()
        };
        let mut local = SystemTime::default();
        // The OS applies this named zone's historical/dynamic rules for the
        // supplied instant. This does not read or modify machine local time.
        if unsafe { SystemTimeToTzSpecificLocalTimeEx(&zone, &utc, &mut local) } == 0 {
            return Err("Exchange timezone conversion failed".into());
        }
        chrono::NaiveDate::from_ymd_opt(local.year as i32, local.month as u32, local.day as u32)
            .ok_or("Invalid exchange-local expiry".into())
    }
}
