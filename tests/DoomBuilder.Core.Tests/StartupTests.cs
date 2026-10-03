using System;
using System.IO;
using CodeImp.DoomBuilder;
using CodeImp.DoomBuilder.Windows;
using Xunit;

namespace DoomBuilder.Core.Tests;

/// <summary>End-to-end smoke test of the real startup sequence with a UI-less main window.</summary>
[Collection("General static state")]
public class StartupTests : IDisposable
{
    private readonly string settingsdir = Path.Combine(Path.GetTempPath(), "udb-startup-" + Guid.NewGuid().ToString("N"));
    private readonly string appdir = TestAssets.CreateAppDirectory();

    public void Dispose()
    {
        General.ShutdownHeadless();
        if (Directory.Exists(settingsdir)) Directory.Delete(settingsdir, true);
        if (Directory.Exists(appdir)) Directory.Delete(appdir, true);
    }

    [Fact]
    public void Startup_loads_configurations_and_creates_the_core_services()
    {
        Directory.CreateDirectory(settingsdir);

        bool ok = General.Startup(Array.Empty<string>(), () => new HeadlessMainWindow(), appdir, settingsdir);

        Assert.True(ok);
        Assert.NotNull(General.Settings);
        Assert.NotNull(General.Actions);
        Assert.NotNull(General.Types);
        Assert.NotNull(General.Colors);
        Assert.NotNull(General.Editing);
        Assert.NotEmpty(General.Configs);
        Assert.Contains(General.Configs, c => c.Name.Contains("Doom"));
        Assert.True(File.Exists(Path.Combine(settingsdir, "UDBuilder.log")) || Directory.GetFiles(settingsdir).Length > 0);
    }
}
