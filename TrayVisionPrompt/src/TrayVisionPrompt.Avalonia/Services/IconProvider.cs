using System;
using System.Collections.Generic;
using System.IO;
using Avalonia.Controls;

namespace TrayVisionPrompt.Avalonia.Services;

public static class IconProvider
{
    public static WindowIcon? LoadWindowIcon(string? iconAsset)
    {
        foreach (var path in EnumerateCandidatePaths(iconAsset))
        {
            if (!File.Exists(path))
            {
                continue;
            }

            try
            {
                return new WindowIcon(path);
            }
            catch
            {
                // Ignore load failures and continue probing other candidates.
            }
        }

        return null;
    }

    private static IEnumerable<string> EnumerateCandidatePaths(string? iconAsset)
    {
        if (string.IsNullOrWhiteSpace(iconAsset))
        {
            yield break;
        }

        var hasExtension = Path.HasExtension(iconAsset);
        if (Path.IsPathRooted(iconAsset))
        {
            if (hasExtension)
            {
                yield return iconAsset;
            }
            else
            {
                yield return iconAsset + ".ico";
                yield return iconAsset + ".png";
            }

            yield break;
        }

        var baseDir = AppContext.BaseDirectory;
        if (iconAsset.Contains(Path.DirectorySeparatorChar) || iconAsset.Contains(Path.AltDirectorySeparatorChar))
        {
            var combined = Path.Combine(baseDir, iconAsset);
            if (hasExtension)
            {
                yield return combined;
            }
            else
            {
                yield return combined + ".ico";
                yield return combined + ".png";
            }
            yield break;
        }

        var assetsDir = Path.Combine(baseDir, "Assets");
        if (hasExtension)
        {
            yield return Path.Combine(assetsDir, iconAsset);
        }
        else
        {
            yield return Path.Combine(assetsDir, iconAsset + ".ico");
            yield return Path.Combine(assetsDir, iconAsset + ".png");
        }
    }
}
