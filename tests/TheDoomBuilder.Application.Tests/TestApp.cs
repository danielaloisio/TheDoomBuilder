using Avalonia;
using Avalonia.Headless;
using TheDoomBuilder.Application.Tests;

// Runs [AvaloniaFact] tests on Avalonia's headless platform: a real UI thread, real windows and input routing, no screen or GPU.
[assembly: AvaloniaTestApplication(typeof(TestAppBuilder))]

namespace TheDoomBuilder.Application.Tests;

public class TestAppBuilder
{
    public static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<TheDoomBuilder.Application.App>()
        .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = true });
}
