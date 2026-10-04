using Avalonia;
using Avalonia.Headless;
using DoomBuilder.App.Tests;

// Runs [AvaloniaFact] tests on Avalonia's headless platform: a real UI thread, real windows and input routing, no screen or GPU.
[assembly: AvaloniaTestApplication(typeof(TestAppBuilder))]

namespace DoomBuilder.App.Tests;

public class TestAppBuilder
{
    public static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<DoomBuilder.App.App>()
        .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = true });
}
