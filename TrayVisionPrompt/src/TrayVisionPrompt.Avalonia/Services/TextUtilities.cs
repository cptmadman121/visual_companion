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
            if (ch == '\n' || ch == '\r')
            {
                len -= 1;
                continue;
            }
            break;
        }
        return len == s.Length ? s : s.Substring(0, len);
    }

    // Preserve multi-line formatting while stripping simple labels and code fences
    public static string SanitizeTranslationResponse(string? response, string? original)
    {
        var text = response ?? string.Empty;
        if (string.IsNullOrWhiteSpace(text)) return string.Empty;

        text = text.Replace("\r\n", "\n").Replace('\r', '\n');

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

        var rawLines = text.Split('\n');
        var cleaned = new System.Collections.Generic.List<string>(rawLines.Length);
        foreach (var line in rawLines)
        {
            var l = line.TrimEnd();
            var ls = l.TrimStart();
            ls = TrimPrefix(ls, "German:");
            ls = TrimPrefix(ls, "Deutsch:");
            ls = TrimPrefix(ls, "Translation:");
            ls = TrimPrefix(ls, "Uebersetzung:");
            ls = TrimPrefix(ls, "Übersetzung:");
            if (l.Length > 0 && char.IsWhiteSpace(l[0]) && ls.Length > 0)
            {
                ls = " " + ls.TrimStart();
            }
            cleaned.Add(ls);
        }

        if (!string.IsNullOrWhiteSpace(original))
        {
            for (int i = 0; i < cleaned.Count; i++)
            {
                if (cleaned[i].Length == 0) continue;
                if (NormalizeForCompare(cleaned[i]) == NormalizeForCompare(original!))
                {
                    cleaned.RemoveAt(i);
                }
                break;
            }
        }

        var result = string.Join("\n", cleaned);
        return TrimTrailingNewlines(result);
    }

    // Stricter variant that removes meta lines but preserves formatting
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

        var parts = text.Split('\n').Select(s => s).ToList();
        for (int i = 0; i < parts.Count; i++)
        {
            var s = parts[i].TrimEnd();
            var ss = s.TrimStart();
            ss = TrimPrefix(ss, "German:");
            ss = TrimPrefix(ss, "Deutsch:");
            ss = TrimPrefix(ss, "Translation:");
            ss = TrimPrefix(ss, "Uebersetzung:");
            ss = TrimPrefix(ss, "Übersetzung:");
            ss = TrimPrefix(ss, "German translation:");
            if (s.Length > 0 && char.IsWhiteSpace(s[0]) && ss.Length > 0)
            {
                ss = " " + ss.TrimStart();
            }
            parts[i] = ss;
        }

        parts = parts.Where(s =>
        {
            var r = s.Trim().ToLowerInvariant();
            if (r.Length == 0) return true; // keep blank lines
            if (r.Contains("this confirms i understand")) return false;
            if (r.Contains("await your text")) return false;
            if (r.Contains("i will now await")) return false;
            if (r.Contains("i will now translate")) return false;
            if (r.Contains("as requested") && r.Contains("performed")) return false;
            return true;
        }).ToList();

        if (!string.IsNullOrWhiteSpace(original))
        {
            for (int i = 0; i < parts.Count; i++)
            {
                if (parts[i].Trim().Length == 0) continue;
                if (NormalizeForCompare(parts[i]) == NormalizeForCompare(original!))
                {
                    parts.RemoveAt(i);
                }
                break;
            }
        }

        var result = string.Join("\n", parts);
        return TrimTrailingNewlines(result);
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

