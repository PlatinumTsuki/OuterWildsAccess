using System;
using System.Text;

namespace OuterWildsAccess
{
    /// <summary>
    /// Shared text-cleaning utilities for screen reader announcements.
    ///
    /// Centralizes tag stripping and whitespace normalization so every handler
    /// announces clean, TTS-friendly text without duplicating logic.
    /// </summary>
    public static class TextUtils
    {
        /// <summary>
        /// Removes all angle-bracket tags from text (HTML / TextMeshPro rich text).
        /// Examples: "&lt;color=red&gt;", "&lt;sprite=0&gt;", "&lt;CMD&gt;".
        /// Returns null if the input is null or whitespace after stripping.
        /// </summary>
        public static string StripTags(string text)
        {
            if (string.IsNullOrEmpty(text)) return null;

            var sb = new StringBuilder(text.Length);
            int i  = 0;
            while (i < text.Length)
            {
                if (text[i] == '<')
                {
                    int end = text.IndexOf('>', i);
                    if (end >= 0) { i = end + 1; continue; }
                }
                sb.Append(text[i++]);
            }

            string result = sb.ToString();
            return string.IsNullOrWhiteSpace(result) ? null : result;
        }

        /// <summary>
        /// Collapses all whitespace sequences (spaces, tabs, newlines) into a single space.
        /// Returns null if the input is null or entirely whitespace.
        /// </summary>
        public static string NormalizeWhitespace(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return null;
            return string.Join(" ", text.Split(new char[0], StringSplitOptions.RemoveEmptyEntries));
        }

        /// <summary>
        /// Strips rich-text tags then normalizes whitespace.
        /// Use this before passing any UI text to ScreenReader.Say().
        /// Returns null if the result is empty.
        /// </summary>
        public static string CleanText(string text)
        {
            return NormalizeWhitespace(StripTags(text));
        }
    }
}
