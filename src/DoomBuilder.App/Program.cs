using Avalonia;
using System;

namespace DoomBuilder.App;

class Program
{
    /// <summary>Command-line arguments, in the UDB syntax (map.wad -map MAP01 -cfg Doom_DoomDoom.cfg ...).</summary>
    public static string[] Arguments { get; set; } = Array.Empty<string>();

    /// <summary>Folder with the editor's assets (configurations, scripts...). Defaults to the application's folder; tests point it elsewhere.</summary>
    public static string ApplicationDirectory { get; set; }

    /// <summary>Where settings and the log go. Defaults to the user's application data folder; tests use a temporary one.</summary>
    public static string SettingsDirectory { get; set; }

    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
    [STAThread]
    public static void Main(string[] args)
    {
        Arguments = args;
        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
#if DEBUG
            .WithDeveloperTools()
#endif
            .WithInterFont()
            .LogToTrace();
}
