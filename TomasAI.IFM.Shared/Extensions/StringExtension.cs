using System;
using System.Collections.Generic;
using System.Globalization;

namespace TomasAI.IFM.Shared.Extensions;

/// <summary>
/// Provides extension methods for string manipulation and evaluation.
/// </summary>
/// <remarks>The StringExtension class contains static methods that extend the functionality of the string type,
/// enabling additional operations such as checking for empty or whitespace-only strings and converting text to camel
/// case. These methods are intended to simplify common string-related tasks and can be called directly on string
/// instances using extension method syntax.</remarks>
public static class StringExtension
{
    public static bool IsEmpty(this string s) => string.IsNullOrWhiteSpace(s);

    /// <summary>
    /// Converts the specified string to camel case, removing spaces and underscores and ensuring the first character is
    /// lowercase.
    /// </summary>
    /// <remarks>This method uses the invariant culture to ensure consistent casing regardless of the system
    /// locale. For example, the input "welcome to_the maze" becomes "welcomeToTheMaze".</remarks>
    /// <param name="text">The input string to convert to camel case. Can be null or empty.</param>
    /// <returns>A camel case version of the input string with spaces and underscores removed. Returns the original string if it
    /// is null or empty.</returns>
    public static string ToCamelCase(this string text)
    {
        if (string.IsNullOrEmpty(text))
            return text;

        var words = text.Split([' ', '_'], StringSplitOptions.RemoveEmptyEntries);
        if (words.Length == 0)
            return string.Empty;

        var result = new List<string>(words.Length);

        for (int i = 0; i < words.Length; i++)
        {
            var word = words[i];
            if (word.Length == 0)
                continue;

            if (i == 0)
            {
                // First word: lowercase the first character, keep the rest as-is
                result.Add(char.ToLower(word[0], CultureInfo.InvariantCulture) + word[1..]);
            }
            else
            {
                // Subsequent words: uppercase the first character, lowercase the rest
                result.Add(char.ToUpper(word[0], CultureInfo.InvariantCulture) + word[1..].ToLower(CultureInfo.InvariantCulture));
            }
        }

        return string.Concat(result);
    }

}

