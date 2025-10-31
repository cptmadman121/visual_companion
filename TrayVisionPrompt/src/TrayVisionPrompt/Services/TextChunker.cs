using System;
using System.Collections.Generic;

namespace TrayVisionPrompt.Services;

internal static class TextChunker
{
    private const int ModelContextTokens = 8192;
    private const int ReservedInstructionTokens = 1024;
    private const double ApproxCharsPerToken = 4.0;
    private const int MinChunkChars = 512;
    private const int HardMaxChunkChars = 6000;

    public static IReadOnlyList<string> Split(string? text, int configuredMaxTokens, int reservedTokens = ReservedInstructionTokens)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return Array.Empty<string>();
        }

        var trimmed = text.Trim();
        var maxChars = CalculateMaxChunkLength(configuredMaxTokens, reservedTokens);
        if (trimmed.Length <= maxChars)
        {
            return new[] { trimmed };
        }

        var chunks = new List<string>();
        var remaining = trimmed.AsSpan();
        while (remaining.Length > maxChars)
        {
            var sliceLength = Math.Min(maxChars + 500, remaining.Length);
            var slice = remaining.Slice(0, sliceLength);
            var splitIndex = FindSplitPosition(slice, maxChars);
            if (splitIndex <= 0 || splitIndex >= remaining.Length)
            {
                splitIndex = Math.Min(maxChars, remaining.Length);
            }

            var segment = remaining.Slice(0, splitIndex).Trim();
            if (segment.Length > 0)
            {
                chunks.Add(segment.ToString());
            }

            remaining = remaining.Slice(splitIndex).TrimStart();
        }

        if (remaining.Length > 0)
        {
            chunks.Add(remaining.Trim().ToString());
        }

        return chunks;
    }

    private static int CalculateMaxChunkLength(int configuredMaxTokens, int reservedTokens)
    {
        var effectiveMaxTokens = Math.Max(reservedTokens * 2, Math.Min(ModelContextTokens, Math.Max(ReservedInstructionTokens, configuredMaxTokens)));
        var availableTokens = Math.Max(reservedTokens, effectiveMaxTokens - reservedTokens);
        var charBudget = (int)(availableTokens * ApproxCharsPerToken);
        var bounded = Math.Max(MinChunkChars, Math.Min(charBudget, HardMaxChunkChars));
        return bounded;
    }

    private static int FindSplitPosition(ReadOnlySpan<char> slice, int preferred)
    {
        var breakChars = new[] { '\n', '.', '!', '?', ';' };
        var maxIndex = Math.Min(slice.Length - 1, preferred + 300);
        for (var idx = maxIndex; idx >= Math.Max(0, preferred - 1200); idx--)
        {
            var ch = slice[idx];
            for (var i = 0; i < breakChars.Length; i++)
            {
                if (ch == breakChars[i])
                {
                    return idx + 1;
                }
            }
        }

        return Math.Min(slice.Length, preferred);
    }
}
