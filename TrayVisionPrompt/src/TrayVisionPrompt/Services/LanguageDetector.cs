using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace TrayVisionPrompt.Services;

public static class LanguageDetector
{
    private static readonly string[] GermanHints = new[]
    {
        "der","die","das","und","ist","nicht","es","ich","du","er","sie","wir","ihr","sind","ein","eine","einer","einem","einen","dem","den","des","zu","mit","auf","für","von","im","als","auch","bei","oder","aber","wie","wenn","dann","man","sein","haben","werden"
    };

    private static readonly string[] EnglishHints = new[]
    {
        "the","and","is","are","not","it","i","you","he","she","we","they","this","that","to","of","in","on","for","with","as","at","from","by","or","but","if","then","be","have","will","would","can","should","must","do"
    };

    public static string? Detect(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        var sample = text.Length > 4000 ? text.Substring(0, 4000) : text;

        // Quick character hint
        int germanCharScore = 0;
        foreach (var ch in sample)
        {
            if ("äöüÄÖÜß".IndexOf(ch) >= 0) germanCharScore += 2;
        }

        // Word hint scoring
        int germanScore = germanCharScore;
        int englishScore = 0;
        var lower = sample.ToLowerInvariant();

        foreach (var w in GermanHints)
        {
            germanScore += CountWord(lower, w);
        }
        foreach (var w in EnglishHints)
        {
            englishScore += CountWord(lower, w);
        }

        if (germanScore >= englishScore + 2) return "German";
        if (englishScore >= germanScore + 2) return "English";

        // Tiebreaker by presence of Umlauts
        if (germanCharScore > 0) return "German";

        return null;
    }

    private static int CountWord(string text, string word)
    {
        var m = Regex.Matches(text, $"\\b{Regex.Escape(word)}\\b", RegexOptions.CultureInvariant);
        return m.Count;
    }
}

