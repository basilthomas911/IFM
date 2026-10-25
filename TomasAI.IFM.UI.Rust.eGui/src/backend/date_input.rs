//! Date-only user inputs must fit the backend's DateOnly range. Parsing a
//! broader Chrono date must never wrap or create an unsupported wire value.
pub fn parse(text: &str) -> Result<chrono::NaiveDate, String> {
    use chrono::Datelike;
    let value = chrono::NaiveDate::parse_from_str(text, "%Y-%m-%d")
        .map_err(|_| "Enter a date as yyyy-MM-dd")?;
    if !(1..=9999).contains(&value.year()) {
        return Err("Date must be between 0001-01-01 and 9999-12-31".into());
    }
    Ok(value)
}
