using System;
using System.IO;

namespace DoomBuilder.Rendering.Tests;

internal static class TestAssets
{
    /// <summary>Finds the repository's assets/Common folder by walking up from the test binaries.</summary>
    public static string Common
    {
        get
        {
            for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir != null; dir = dir.Parent)
            {
                string candidate = Path.Combine(dir.FullName, "assets", "Common");
                if (Directory.Exists(candidate)) return candidate;
            }
            throw new DirectoryNotFoundException("assets/Common not found above " + AppContext.BaseDirectory);
        }
    }

    /// <summary>Compiler/nodebuilder configurations (the real executables are platform specific and not needed by the tests).</summary>
    public static string Compilers => Path.Combine(Common, "..", "Linux", "Compilers");

    /// <summary>
    /// Builds an application folder the way packaging will: assets/Common overlaid with the platform folder.
    /// The caller deletes it.
    /// </summary>
    public static string CreateAppDirectory()
    {
        string target = Path.Combine(Path.GetTempPath(), "udb-app-" + Guid.NewGuid().ToString("N"));
        CopyDirectory(Common, target);
        CopyDirectory(Path.Combine(Common, "..", "Linux"), target);   // TODO: pick the folder of the current OS once the other platforms exist
        return target;
    }

    private static void CopyDirectory(string source, string target)
    {
        Directory.CreateDirectory(target);
        foreach (string file in Directory.GetFiles(source)) File.Copy(file, Path.Combine(target, Path.GetFileName(file)), true);
        foreach (string dir in Directory.GetDirectories(source)) CopyDirectory(dir, Path.Combine(target, Path.GetFileName(dir)));
    }

    public static string DefaultSettings => Path.Combine(Common, "UDBuilder.default.cfg");
}
