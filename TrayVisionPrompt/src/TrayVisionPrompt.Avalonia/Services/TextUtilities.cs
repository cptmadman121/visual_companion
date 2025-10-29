using System;
using System.Linq;

namespace TrayVisionPrompt.Avalonia.Services;

public static class TextUtilities
{
    // Trims only trailing newline characters (CR, LF, or CRLF sequences)
    public static string TrimTrailingNewlines(string? input)
    {
        if (string.IsNullOrEmpty(input))
        {
            return input ?? string.Empty;
        }

        var s = input;
        var len = s.Length;
        while (len > 0)
        {
            var ch = s[len - 1];
            if (ch == '\n')
            {
                len -= 1;
                continue;
            }
            if (ch == '\r')
            {
                len -= 1;
                continue;
            }
            break;
        }
        return len == s.Length ? s : s.Substring(0, len);
    }

    // For translation prompts: strip code fences, headers, and keep only the likely translated line.
    // Also collapses internal newlines to a single space to avoid multi-line pastes.
    public static string SanitizeTranslationResponse(string? response, string? original)
    {
        var text = response ?? string.Empty;
        if (string.IsNullOrWhiteSpace(text)) return string.Empty;

        // Normalize newlines
        text = text.Replace("\r\n", "\n").Replace('\r', '\n');

        // Strip fenced code blocks if present
        if (text.StartsWith("```"))
        {
            var idx = text.IndexOf('\n');
            if (idx >= 0) text = text[(idx + 1)..];
            if (text.EndsWith("```"))
            {
                var lastIdx = text.LastIndexOf("```", StringComparison.Ordinal);
                if (lastIdx >= 0) text = text[..lastIdx];
            }
        }

        // Split and trim lines
        var lines = text.Split('\n');
        var cleaned = new System.Collections.Generic.List<string>(lines.Length);
        foreach (var line in lines)
        {
            var l = line.Trim();
            if (l.Length == 0) continue; // drop empty lines

            // Remove language labels
            l = TrimPrefix(l, "German:");
            l = TrimPrefix(l, "Deutsch:");
            l = TrimPrefix(l, "Translation:");
            l = TrimPrefix(l, "Übersetzung:");
            cleaned.Add(l);
        }

        // If first line equals original, drop it
        if (cleaned.Count > 0 && !string.IsNullOrWhiteSpace(original))
        {
            var normA = NormalizeForCompare(cleaned[0]);
            var normB = NormalizeForCompare(original!);
            if (normA == normB)
            {
                cleaned.RemoveAt(0);
            }
        }

        if (cleaned.Count == 0) return string.Empty;

        // Heuristic: keep last line if multiple remain (often translation is last)
        var result = cleaned.Count == 1 ? cleaned[0] : cleaned[^1];
        result = result.Trim();
        // Collapse any internal newlines or multiple spaces just in case
        result = result.Replace('\n', ' ');
        while (result.Contains("  ")) result = result.Replace("  ", " ");
        return result.Trim();
    }

    // Stricter variant that additionally removes common meta-confirmations
    public static string SanitizeTranslationStrict(string? response, string? original)
    {
        if (string.IsNullOrWhiteSpace(response)) return string.Empty;
        var text = response.Replace("\r\n", "\n").Replace('\r', '\n');
        if (text.StartsWith("```"))
        {
            var idx = text.IndexOf('\n');
            if (idx >= 0) text = text[(idx + 1)..];
            if (text.EndsWith("```"))
            {
                var lastIdx = text.LastIndexOf("```", StringComparison.Ordinal);
                if (lastIdx >= 0) text = text[..lastIdx];
            }
        }

        var parts = text.Split('\n')
            .Select(s => s.Trim())
            .Where(s => s.Length > 0)
            .Select(s => TrimPrefix(TrimPrefix(TrimPrefix(TrimPrefix(TrimPrefix(s,
                "German:"), "Deutsch:"), "Translation:"), "Übersetzung:"), "German translation:"))
            .ToList();

        // Remove generic confirmations/meta
        parts = parts.Where(s =>
        {
            var r = s.ToLowerInvariant();
            if (r.Contains("this confirms i understand")) return false;
            if (r.Contains("await your text")) return false;
            if (r.Contains("i will now await")) return false;
            if (r.Contains("i will now translate")) return false;
            if (r.Contains("as requested") && r.Contains("performed")) return false;
            return true;
        }).ToList();

        if (parts.Count > 0 && !string.IsNullOrWhiteSpace(original))
        {
            if (NormalizeForCompare(parts[0]) == NormalizeForCompare(original!))
            {
                parts.RemoveAt(0);
            }
        }
        if (parts.Count == 0) return string.Empty;
        var result = parts.Count == 1 ? parts[0] : parts[^1];
        result = result.Replace('\n', ' ');
        while (result.Contains("  ")) result = result.Replace("  ", " ");
        return result.Trim();
    }

    private static string TrimPrefix(string s, string prefix)
    {
        return s.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
            ? s[prefix.Length..].TrimStart()
            : s;
    }

    private static string NormalizeForCompare(string s)
    {
        s = s.Trim().TrimEnd('.', '!', '?');
        var sb = new System.Text.StringBuilder(s.Length);
        foreach (var ch in s)
        {
            if (!char.IsWhiteSpace(ch)) sb.Append(char.ToLowerInvariant(ch));
        }
        return sb.ToString();
    }
}
