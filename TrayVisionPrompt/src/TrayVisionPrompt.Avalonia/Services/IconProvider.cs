using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Avalonia.Controls;

namespace TrayVisionPrompt.Avalonia.Services;

public static class IconProvider
{
    private const string DefaultIconAsset = "ollama-companion.ico";

    public static WindowIcon? LoadWindowIcon(string? iconAsset)
    {
        var effective = string.IsNullOrWhiteSpace(iconAsset) ? DefaultIconAsset : iconAsset;
        // Try embedded resources first (compiled into the exe)
        foreach (var stream in EnumerateEmbeddedStreams(effective))
        {
            try
            {
                return new WindowIcon(stream);
            }
            catch
            {
                // ignore and continue
            }
            finally
            {
                stream.Dispose();
            }
        }

        // Fallback to probing files in the published folder
        foreach (var path in EnumerateCandidatePaths(effective))
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
        // iconAsset is guaranteed non-empty by caller; still guard to be safe
        if (string.IsNullOrWhiteSpace(iconAsset)) yield break;

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

    private static IEnumerable<Stream> EnumerateEmbeddedStreams(string iconAsset)
    {
        var asm = Assembly.GetExecutingAssembly();
        var baseName = asm.GetName().Name ?? "TrayVisionPrompt.Avalonia";
        var candidates = new[]
        {
            iconAsset,
            iconAsset.EndsWith(".ico", StringComparison.OrdinalIgnoreCase) ? iconAsset : iconAsset + ".ico",
            iconAsset.EndsWith(".png", StringComparison.OrdinalIgnoreCase) ? iconAsset : iconAsset + ".png",
        }
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToArray();

        foreach (var name in asm.GetManifestResourceNames())
        {
            foreach (var cand in candidates)
            {
                // Resources typically look like: Namespace.Assets.filename.ext
                if (name.EndsWith("." + cand.Replace('/', '.').Replace('\\', '.'), StringComparison.OrdinalIgnoreCase))
                {
                    var s = asm.GetManifestResourceStream(name);
                    if (s != null)
                    {
                        yield return s;
                    }
                }
            }
        }
    }
}
