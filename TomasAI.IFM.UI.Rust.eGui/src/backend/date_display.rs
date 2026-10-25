//! Windows short-date formatting of an explicitly supplied backend date. This
//! uses the user's display locale and never reads the machine's current clock.
pub fn short(day: chrono::NaiveDate) -> Result<String, String> {
    use chrono::Datelike;
    if !(1..=9999).contains(&day.year()) {
        return Err("Date outside backend range".into());
    }
    #[cfg(windows)]
    {
        #[repr(C)]
        struct SystemTime {
            year: u16,
            month: u16,
            weekday: u16,
            day: u16,
            hour: u16,
            minute: u16,
            second: u16,
            milliseconds: u16,
        }
        #[link(name = "kernel32")]
        unsafe extern "system" {
            fn GetDateFormatEx(
                locale: *const u16,
                flags: u32,
                date: *const SystemTime,
                format: *const u16,
                output: *mut u16,
                capacity: i32,
                calendar: *const u16,
            ) -> i32;
        }
        let date = SystemTime {
            year: day.year() as u16,
            month: day.month() as u16,
            weekday: 0,
            day: day.day() as u16,
            hour: 0,
            minute: 0,
            second: 0,
            milliseconds: 0,
        };
        let mut output = [0u16; 512];
        // All pointers refer to stack-owned documented Win32 buffers. Null locale
        // means user-default locale, matching the managed client's current culture.
        let length = unsafe {
            GetDateFormatEx(
                std::ptr::null(),
                1,
                &date,
                std::ptr::null(),
                output.as_mut_ptr(),
                output.len() as i32,
                std::ptr::null(),
            )
        };
        if length <= 0 || length as usize > output.len() {
            return Err("Windows date formatting failed".into());
        }
        return String::from_utf16(&output[..length as usize - 1])
            .map_err(|_| "Invalid Windows date text".into());
    }
    #[cfg(not(windows))]
    {
        Ok(day.format("%m/%d/%Y").to_string())
    }
}
/// Managed DateTime's general display uses the supplied wall date/time and the
/// user's short-date/long-time patterns. No local clock or zone conversion.
pub fn general(text:&str)->Result<String,String>{
    use chrono::{Datelike,Timelike};
    let instant=chrono::DateTime::parse_from_rfc3339(text).map_err(|_|"Invalid evidence timestamp")?;
    let date=short(instant.date_naive())?;
    #[cfg(windows)] {
        #[repr(C)] struct SystemTime{year:u16,month:u16,weekday:u16,day:u16,hour:u16,minute:u16,second:u16,milliseconds:u16}
        #[link(name="kernel32")] unsafe extern "system" {
            fn GetTimeFormatEx(locale:*const u16,flags:u32,time:*const SystemTime,format:*const u16,output:*mut u16,capacity:i32)->i32;
        }
        let time=SystemTime{year:instant.year() as u16,month:instant.month() as u16,weekday:0,day:instant.day() as u16,hour:instant.hour() as u16,minute:instant.minute() as u16,second:instant.second() as u16,milliseconds:0};
        let mut output=[0u16;512];
        // Win32 only formats this explicitly supplied instant; buffers remain
        // stack-owned for the synchronous call.
        let length=unsafe{GetTimeFormatEx(std::ptr::null(),0,&time,std::ptr::null(),output.as_mut_ptr(),output.len() as i32)};
        if length<=0||length as usize>output.len(){return Err("Windows time formatting failed".into())}
        return Ok(format!("{date} {}",String::from_utf16(&output[..length as usize-1]).map_err(|_|"Invalid Windows time text")?));
    }
    #[cfg(not(windows))] {Ok(format!("{date} {}",instant.format("%H:%M:%S")))}
}
