using System;
using System.Collections.Concurrent;
using System.Numerics;
using System.Threading;
using Silk.NET.Maths;
using Silk.NET.OpenGL;
using Silk.NET.Windowing;

namespace DoomBuilder.Rendering.Tests;

/// <summary>
/// A real OpenGL 3.3 core context for tests, on a hidden GLFW window. GL contexts belong to one thread, so the host
/// owns a dedicated thread and tests marshal their work onto it. When no display/driver is available the host reports
/// why and the GL tests skip (set UDB_REQUIRE_GL=1 to turn that into a failure, e.g. on CI with a virtual display).
/// </summary>
public sealed class GlHost : IDisposable
{
    private readonly BlockingCollection<Action> work = new BlockingCollection<Action>();
    private readonly Thread thread;
    private readonly ManualResetEventSlim ready = new ManualResetEventSlim();
    private IWindow window;

    public GL Gl { get; private set; }
    public string UnavailableReason { get; private set; }
    public bool Available { get { return Gl != null && UnavailableReason == null; } }

    public GlHost()
    {
        thread = new Thread(Loop) { IsBackground = true, Name = "GL test thread" };

        // GLFW on macOS (Cocoa) only works from the process's main thread, which a test runner does not give us:
        // creating the window from another thread blocks forever (this hung the macOS CI job). Skip instead.
        if (OperatingSystem.IsMacOS())
        {
            UnavailableReason = "GLFW needs the main thread on macOS, so the GL tests cannot create a window here";
            ready.Set();
        }
        else
        {
            thread.Start();

            // Never let a missing display or a driver problem hang the test run
            if (!ready.Wait(TimeSpan.FromSeconds(20)))
                UnavailableReason = "Timed out creating an OpenGL window";
        }

        if (!Available && Environment.GetEnvironmentVariable("UDB_REQUIRE_GL") == "1")
            throw new InvalidOperationException("OpenGL required but unavailable: " + UnavailableReason);
    }

    private void Loop()
    {
        try
        {
            Window.PrioritizeGlfw();
            var options = WindowOptions.Default with
            {
                IsVisible = false,
                Size = new Vector2D<int>(64, 64),
                API = new GraphicsAPI(ContextAPI.OpenGL, ContextProfile.Core, ContextFlags.ForwardCompatible, new APIVersion(3, 3)),
            };
            window = Window.Create(options);
            window.Initialize();
            window.MakeCurrent();
            Gl = GL.GetApi(window);
        }
        catch (Exception e)
        {
            UnavailableReason = e.GetType().Name + ": " + e.Message;
        }
        finally
        {
            ready.Set();
        }

        foreach (Action a in work.GetConsumingEnumerable()) a();
        window?.Dispose();
    }

    /// <summary>Runs <paramref name="action"/> on the GL thread and rethrows its exception here.</summary>
    public void Invoke(Action action)
    {
        Exception error = null;
        using var done = new ManualResetEventSlim();
        work.Add(() =>
        {
            try { action(); } catch (Exception e) { error = e; }
            done.Set();
        });
        if (!done.Wait(TimeSpan.FromSeconds(60))) throw new TimeoutException("The GL thread did not finish the work in 60 s");
        if (error != null) throw new AggregateException(error);
    }

    public void Dispose()
    {
        work.CompleteAdding();
        if (thread.IsAlive) thread.Join(2000);
    }
}

public sealed class GlFixture : IDisposable
{
    public GlHost Host { get; } = new GlHost();
    public void Dispose() => Host.Dispose();
}

[Xunit.CollectionDefinition("OpenGL")]
public class GlCollection : Xunit.ICollectionFixture<GlFixture> { }
