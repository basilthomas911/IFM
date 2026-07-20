using System.Globalization;

namespace TomasAI.IFM.Framework.Storage.Extensions;

/// <summary>
/// Provides extension methods for parsing segments of a <see cref="ReadOnlySpan{char}"/> into various data types.
/// </summary>
/// <remarks>This class includes methods for parsing segments of a <see cref="ReadOnlySpan{char}"/> into common
/// types such as <see cref="int"/>, <see cref="float"/>, <see cref="bool"/>, and others. Each method advances the
/// provided <c>start</c> index to the position after the parsed value, making it suitable for sequential parsing of
/// delimited data.</remarks>
public static class ReadOnlySpanExtension
{

    /// <summary>
    /// Parses the next segment from the <see cref="ReadOnlySpan{char}"/> as an <see cref="int"/>.
    /// Advances the <paramref name="start"/> index to the position after the parsed value.
    /// Returns the parsed integer, or 0 if parsing fails.
    /// </summary>
    public static int GetInt(this ReadOnlySpan<char> charBuffer, ref int start)
        => int.TryParse(charBuffer.ParseSpan(ref start), out var value) ? value : default;

    /// <summary>
    /// Parses the next segment from the <see cref="ReadOnlySpan{char}"/> as a <see cref="float"/>.
    /// Advances the <paramref name="start"/> index to the position after the parsed value.
    /// Returns the parsed float, or 0 if parsing fails.
    /// </summary>
    public static float GetFloat(this ReadOnlySpan<char> charBuffer, ref int start)
        => float.TryParse(charBuffer.ParseSpan(ref start), NumberStyles.Float, CultureInfo.InvariantCulture, out var value) ? value : default;

    /// <summary>
    /// Parses the next segment from the <see cref="ReadOnlySpan{char}"/> as a <see cref="double"/>.
    /// Advances the <paramref name="start"/> index to the position after the parsed value.
    /// Returns the parsed double, or 0 if parsing fails.
    /// </summary>
    public static double GetDouble(this ReadOnlySpan<char> charBuffer, ref int start)
        => double.TryParse(charBuffer.ParseSpan(ref start), NumberStyles.Float, CultureInfo.InvariantCulture, out var value) ? value : default;

    /// <summary>
    /// Parses the next segment from the <see cref="ReadOnlySpan{char}"/> as a <see cref="decimal"/>.
    /// Advances the <paramref name="start"/> index to the position after the parsed value.
    /// Returns the parsed decimal, or 0 if parsing fails.
    /// </summary>
    public static decimal GetDecimal(this ReadOnlySpan<char> charBuffer, ref int start)
        => decimal.TryParse(charBuffer.ParseSpan(ref start), NumberStyles.Float, CultureInfo.InvariantCulture, out var value) ? value : default;

    /// <summary>
    /// Parses the next segment from the <see cref="ReadOnlySpan{char}"/> as a <see cref="bool"/>.
    /// Advances the <paramref name="start"/> index to the position after the parsed value.
    /// Returns the parsed boolean, or false if parsing fails.
    /// </summary>
    public static bool GetBool(this ReadOnlySpan<char> charBuffer, ref int start)
        =>  bool.TryParse(charBuffer.ParseSpan(ref start), out var value) && value;


    /// <summary>
    /// Parses the next segment from the <see cref="ReadOnlySpan{char}"/> as a <see cref="long"/>.
    /// Advances the <paramref name="start"/> index to the position after the parsed value.
    /// Returns the parsed long, or 0 if parsing fails.
    /// </summary>
    public static long GetLong(this ReadOnlySpan<char> charBuffer, ref int start)
        => long.TryParse(charBuffer.ParseSpan(ref start), out var value) ? value : default;

    /// <summary>
    /// Parses the next segment from the <see cref="ReadOnlySpan{char}"/> as a <see cref="DateTime"/>.
    /// Advances the <paramref name="start"/> index to the position after the parsed value.
    /// Returns the parsed DateTime, or <see cref="DateTime.MinValue"/> if parsing fails.
    /// </summary>
    public static DateTime GetDateTime(this ReadOnlySpan<char> charBuffer, ref int start)
        => DateTime.TryParse(charBuffer.ParseSpan(ref start), CultureInfo.InvariantCulture, DateTimeStyles.None, out var value) ? value : default;

#if NET6_0_OR_GREATER
    /// <summary>
    /// Parses a <see cref="ReadOnlySpan{T}"/> of characters to extract a <see cref="DateOnly"/> value, starting at the
    /// specified position.
    /// </summary>
    /// <remarks>This method uses <see cref="DateTime.TryParse(string, IFormatProvider, DateTimeStyles, out
    /// DateTime)"/>  with the invariant culture and no specific date/time styles. The parsed <see cref="DateTime"/> is
    /// then converted to a <see cref="DateOnly"/>.</remarks>
    /// <param name="charBuffer">The span of characters to parse.</param>
    /// <param name="start">The zero-based index in <paramref name="charBuffer"/> where parsing begins.  This value is updated to the
    /// position immediately after the parsed date if successful.</param>
    /// <returns>A <see cref="DateOnly"/> representing the parsed date. If parsing fails, the default value of <see
    /// cref="DateOnly"/> is returned.</returns>
    public static DateOnly GetDateOnly(this ReadOnlySpan<char> charBuffer, ref int start)
    {
        var segment = charBuffer.ParseSpan(ref start);
        return DateTime.TryParse(segment, CultureInfo.InvariantCulture, DateTimeStyles.None, out var value)
            ? DateOnly.FromDateTime(value) : default;
    }

    /// <summary>
    /// Parses a <see cref="ReadOnlySpan{T}"/> of characters to extract a <see cref="TimeOnly"/> value, starting at the
    /// specified position.
    /// </summary>
    /// <remarks>This method uses <see cref="DateTime.TryParse(string, IFormatProvider, DateTimeStyles, out
    /// DateTime)"/>  with the invariant culture to parse the input. Ensure the input span contains a valid time
    /// representation.</remarks>
    /// <param name="charBuffer">The span of characters to parse.</param>
    /// <param name="start">The zero-based index in <paramref name="charBuffer"/> where parsing begins.  This value is updated to reflect
    /// the position after the parsed content.</param>
    /// <returns>A <see cref="TimeOnly"/> value representing the time extracted from the input span.  If parsing fails, the
    /// default <see cref="TimeOnly"/> value is returned.</returns>
    public static TimeOnly GetTimeOnly(this ReadOnlySpan<char> charBuffer, ref int start)
    {
        var segment = charBuffer.ParseSpan(ref start);
        return DateTime.TryParse(segment, CultureInfo.InvariantCulture, DateTimeStyles.None, out var value)
            ? TimeOnly.FromDateTime(value) : default;
    }

#endif

    /// <summary>
    /// Parses the next segment from the <see cref="ReadOnlySpan{char}"/> as an enum of type <typeparamref name="T"/>.
    /// Advances the <paramref name="start"/> index to the position after the parsed value.
    /// Returns the parsed enum value, or the default value of <typeparamref name="T"/> if parsing fails.
    /// </summary>
    /// <typeparam name="T">The enum type to parse.</typeparam>
    public static T GetEnum<T>(this ReadOnlySpan<char> charBuffer, ref int start) where T : struct, Enum
        => Enum.TryParse<T>(charBuffer.ParseSpan(ref start), ignoreCase: true, out var value) ? value : default;

    /// <summary>
    /// Parses the next segment from the <see cref="ReadOnlySpan{char}"/> as a <see cref="string"/>.
    /// Advances the <paramref name="start"/> index to the position after the parsed value.
    /// Returns the parsed string, or <see cref="string.Empty"/> if parsing fails.
    /// </summary>
    public static string GetString(this ReadOnlySpan<char> charBuffer, ref int start)
        => charBuffer.Parse(ref start);

    /// <summary>
    /// Parses a ReadOnlySpan&lt;char&gt; from the given start position up to the next pipe character '|'.
    /// Returns the segment as a ReadOnlySpan&lt;char&gt; without allocating a string.
    /// </summary>
    public static ReadOnlySpan<char> ParseSpan(this ReadOnlySpan<char> charBuffer, ref int start)
    {
        if (start < 0 || start >= charBuffer.Length)
        {
            start = -1;
            return [];
        }

        int pipeIndex = charBuffer[start..].IndexOf('|');
        if (pipeIndex == -1)
        {
            var result = charBuffer[start..];
            start = -1;
            return result;
        }
        else
        {
            var result = charBuffer.Slice(start, pipeIndex);
            start = start + pipeIndex + 1;
            if (start >= charBuffer.Length)
                start = -1;
            return result;
        }
    }

    /// <summary>
    /// Parses a ReadOnlySpan&lt;char&gt; from the given start position up to the next pipe character '|'.
    /// Updates start to the position after the pipe, or -1 if past the end.
    /// Returns the parsed slice as a string.
    /// </summary>
    public static string Parse(this ReadOnlySpan<char> charBuffer, ref int start)
    {
        var span = charBuffer.ParseSpan(ref start);
        return span.Length == 0 ? string.Empty : span.ToString();
    }
}
