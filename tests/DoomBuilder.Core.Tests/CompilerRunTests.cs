using System;
using System.IO;
using System.Linq;
using CodeImp.DoomBuilder;
using CodeImp.DoomBuilder.Compilers;
using CodeImp.DoomBuilder.Config;
using CodeImp.DoomBuilder.IO;
using Xunit;

namespace DoomBuilder.Core.Tests;

/// <summary>Running the external compilers (node builders) with a stand-in program, the way they run on Linux and macOS.</summary>
[Collection("General static state")]
public class CompilerRunTests : IDisposable
{
    private readonly string dir = Path.Combine(Path.GetTempPath(), "udb-comp-" + Guid.NewGuid().ToString("N"));

    public CompilerRunTests()
    {
        Directory.CreateDirectory(dir);
        General.InitializeHeadless(TestAssets.DefaultSettings);
    }

    public void Dispose()
    {
        General.ShutdownHeadless();
        if (Directory.Exists(dir)) Directory.Delete(dir, true);
    }

    private string WriteProgram(string folder, string name, string body)
    {
        Directory.CreateDirectory(folder);
        string file = Path.Combine(folder, name);
        File.WriteAllText(file, "#!/bin/sh\n" + body + "\n");
        File.SetUnixFileMode(file, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        return file;
    }

    private CompilerInfo InfoFor(string folder, string program)
    {
        var cfg = new Configuration();
        Assert.True(cfg.InputConfiguration("compilers { fakebsp { interface = \"NodesCompiler\"; program = \"" + program + "\"; } }"));
        return new CompilerInfo("fake.cfg", "fakebsp", folder, cfg);
    }

    private NodesCompiler Prepare(CompilerInfo info, string output)
    {
        var compiler = new NodesCompiler(info);
        compiler.Parameters = "-o%FO %FI";
        compiler.InputFile = Path.Combine(dir, "in.wad");
        compiler.OutputFile = output;
        File.WriteAllText(compiler.InputFile, "wad");
        return compiler;
    }

    [Fact]
    public void A_program_in_the_compilers_folder_is_run_with_the_input_and_output_files()
    {
        if (OperatingSystem.IsWindows()) return;
        string tools = Path.Combine(dir, "tools");
        WriteProgram(tools, "fakebsp", "for a in \"$@\"; do case \"$a\" in -o*) out=\"${a#-o}\";; *) in=\"$a\";; esac; done\ncp \"$in\" \"$out\"");
        string output = Path.Combine(dir, "out.wad");

        using (NodesCompiler compiler = Prepare(InfoFor(tools, "fakebsp"), output))
        {
            Assert.True(compiler.Run());
            Assert.Empty(compiler.Errors);
        }
        Assert.Equal("wad", File.ReadAllText(output));
    }

    [Fact]
    public void A_program_that_reports_an_error_fails_the_run_and_keeps_its_message()
    {
        if (OperatingSystem.IsWindows()) return;
        string tools = Path.Combine(dir, "tools");
        WriteProgram(tools, "fakebsp", "echo 'Fatal error: the map is broken'\nexit 1");

        using (NodesCompiler compiler = Prepare(InfoFor(tools, "fakebsp"), Path.Combine(dir, "out.wad")))
        {
            Assert.False(compiler.Run());
            Assert.Contains("map is broken", compiler.Errors[0].description);
        }
    }

    [Fact]
    public void A_program_missing_from_the_folder_is_found_on_the_PATH()
    {
        if (OperatingSystem.IsWindows()) return;
        string onpath = Path.Combine(dir, "bin");
        WriteProgram(onpath, "pathbsp", "echo ok > \"${1#-o}\"");
        string oldpath = Environment.GetEnvironmentVariable("PATH");
        Environment.SetEnvironmentVariable("PATH", onpath + Path.PathSeparator + oldpath);
        try
        {
            CompilerInfo info = InfoFor(Path.Combine(dir, "nothing-here"), "pathbsp.exe");     // the Windows name of the program
            Assert.Equal(Path.Combine(onpath, "pathbsp"), info.ProgramPath);

            string output = Path.Combine(dir, "out.wad");
            using (NodesCompiler compiler = Prepare(info, output))
                Assert.True(compiler.Run());
            Assert.True(File.Exists(output));
        }
        finally { Environment.SetEnvironmentVariable("PATH", oldpath); }
    }

    [Fact]
    public void A_missing_program_is_reported_with_the_place_it_was_expected()
    {
        string folder = Path.Combine(dir, "nothing-here");
        CompilerInfo info = InfoFor(folder, "doesnotexist_bsp");
        Assert.Equal(Path.Combine(folder, "doesnotexist_bsp"), info.ProgramPath);
    }

    [Fact]
    public void A_program_without_the_execute_permission_gets_it_before_running()
    {
        if (OperatingSystem.IsWindows()) return;
        string tools = Path.Combine(dir, "tools");
        string program = WriteProgram(tools, "fakebsp", "echo ok > \"${1#-o}\"");
        File.SetUnixFileMode(program, UnixFileMode.UserRead | UnixFileMode.UserWrite);        // as unpacked from an archive

        string output = Path.Combine(dir, "out.wad");
        using (NodesCompiler compiler = Prepare(InfoFor(tools, "fakebsp"), output))
            Assert.True(compiler.Run());
        Assert.True(File.Exists(output));
    }
}

/// <summary>The runner behind the "external command" steps (pre/post test commands).</summary>
public class ExternalCommandRunnerTests
{
    private static System.Diagnostics.ProcessStartInfo Sh(string script)
    {
        var info = new System.Diagnostics.ProcessStartInfo { FileName = "/bin/sh" };
        info.ArgumentList.Add("-c");
        info.ArgumentList.Add(script);
        return info;
    }

    [Fact]
    public void Output_is_reported_line_by_line_and_a_clean_exit_is_a_success()
    {
        if (OperatingSystem.IsWindows()) return;
        var lines = new System.Collections.Concurrent.ConcurrentQueue<string>();
        using var runner = new CodeImp.DoomBuilder.Windows.ExternalCommandRunner(Sh("echo one; echo two"), new ExternalCommandSettings());
        runner.OutputReceived += (text, err) => lines.Enqueue((err ? "E:" : "") + text);

        Assert.True(runner.RunToEnd());
        Assert.Equal(new[] { "one", "two" }, lines.ToArray());
        Assert.Equal(0, runner.ExitCode);
    }

    [Fact]
    public void A_non_zero_exit_code_is_an_error_only_when_the_settings_say_so()
    {
        if (OperatingSystem.IsWindows()) return;
        using (var runner = new CodeImp.DoomBuilder.Windows.ExternalCommandRunner(Sh("exit 3"), new ExternalCommandSettings { ExitCodeIsError = true, StdErrIsError = false }))
        {
            Assert.False(runner.RunToEnd());
            Assert.Equal(3, runner.ExitCode);
        }
        using (var runner = new CodeImp.DoomBuilder.Windows.ExternalCommandRunner(Sh("exit 3"), new ExternalCommandSettings { ExitCodeIsError = false, StdErrIsError = false }))
            Assert.True(runner.RunToEnd());
    }

    [Fact]
    public void The_error_stream_counts_as_an_error_when_the_settings_say_so()
    {
        if (OperatingSystem.IsWindows()) return;
        var errors = new System.Collections.Concurrent.ConcurrentQueue<string>();
        using var runner = new CodeImp.DoomBuilder.Windows.ExternalCommandRunner(Sh("echo oops >&2"), new ExternalCommandSettings { ExitCodeIsError = true, StdErrIsError = true });
        runner.OutputReceived += (text, err) => { if (err) errors.Enqueue(text); };

        Assert.False(runner.RunToEnd());
        Assert.Equal(new[] { "oops" }, errors.ToArray());
    }

    [Fact]
    public void A_program_that_cannot_start_is_an_error_with_a_message()
    {
        var errors = new System.Collections.Concurrent.ConcurrentQueue<string>();
        var info = new System.Diagnostics.ProcessStartInfo { FileName = "/definitely/not/a/program" };
        using var runner = new CodeImp.DoomBuilder.Windows.ExternalCommandRunner(info, new ExternalCommandSettings());
        runner.OutputReceived += (text, err) => { if (err) errors.Enqueue(text); };

        Assert.False(runner.RunToEnd());
        Assert.Contains("Unable to run", errors.Single());
    }

    [Fact]
    public void Stopping_kills_the_command_and_counts_as_a_failure()
    {
        if (OperatingSystem.IsWindows()) return;
        using var runner = new CodeImp.DoomBuilder.Windows.ExternalCommandRunner(Sh("sleep 30"), new ExternalCommandSettings());
        using var done = new System.Threading.ManualResetEventSlim();
        runner.Finished += done.Set;
        runner.Start();
        System.Threading.Thread.Sleep(300);
        Assert.True(runner.IsRunning);

        runner.Stop();
        Assert.True(done.Wait(10000), "the command was not stopped");
        Assert.True(runner.HasErrors);
        Assert.False(runner.IsRunning);
    }
}
