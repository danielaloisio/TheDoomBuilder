using System;
using System.Diagnostics;
using CodeImp.DoomBuilder.Config;

namespace CodeImp.DoomBuilder.Windows
{
	/// <summary>
	/// Runs an external command (the commands of a "pre/post test" or "reload resources" step) and reports its output line by line
	/// (UDB's RunExternalCommandForm without the window). Events come from background threads.
	/// </summary>
	public sealed class ExternalCommandRunner : IDisposable
	{
		private readonly ProcessStartInfo startinfo;
		private readonly ExternalCommandSettings settings;
		private readonly object lockobj = new object();
		private Process process;
		private bool running;
		private bool haserrors;
		private bool stopped;

		/// <summary>A line of output; the flag is true for the error stream.</summary>
		public event Action<string, bool> OutputReceived;

		/// <summary>The command ended (or was stopped). Once per <see cref="Start"/>.</summary>
		public event Action Finished;

		public ExternalCommandRunner(ProcessStartInfo startinfo, ExternalCommandSettings settings)
		{
			this.startinfo = startinfo;
			this.settings = settings;
		}

		public bool IsRunning { get { lock(lockobj) return running; } }

		/// <summary>True when the exit code (if it counts), the error stream (if it counts) or a failure to start made the run a failure.</summary>
		public bool HasErrors { get { lock(lockobj) return haserrors; } }

		public int ExitCode { get; private set; }

		/// <summary>Starts (or restarts) the command.</summary>
		public void Start()
		{
			lock(lockobj)
			{
				if(running) return;
				running = true;
				haserrors = false;
				stopped = false;
				ExitCode = 0;
			}

			var thread = new System.Threading.Thread(Run) { Name = "Run external command", IsBackground = true };
			thread.Start();
		}

		/// <summary>Kills the command and everything it started.</summary>
		public void Stop()
		{
			Process p;
			lock(lockobj)
			{
				if(!running) return;
				stopped = true;
				haserrors = true;
				p = process;
			}

			try { if(p != null && !p.HasExited) p.Kill(true); }
			catch(Exception) { }   // gone already
		}

		private void Run()
		{
			try
			{
				startinfo.UseShellExecute = false;
				startinfo.RedirectStandardOutput = true;
				startinfo.RedirectStandardError = true;
				startinfo.CreateNoWindow = true;

				var p = new Process { StartInfo = startinfo };
				p.OutputDataReceived += (s, e) => { if(e.Data != null) OutputReceived?.Invoke(e.Data, false); };
				p.ErrorDataReceived += (s, e) =>
				{
					if(e.Data == null) return;
					if(settings != null && settings.StdErrIsError) lock(lockobj) haserrors = true;
					OutputReceived?.Invoke(e.Data, true);
				};

				lock(lockobj) process = p;
				p.Start();
				p.BeginOutputReadLine();
				p.BeginErrorReadLine();
				p.WaitForExit();

				ExitCode = p.ExitCode;
				if(!stopped && p.ExitCode != 0 && (settings == null || settings.ExitCodeIsError)) lock(lockobj) haserrors = true;
			}
			catch(Exception e)
			{
				lock(lockobj) haserrors = true;
				ExitCode = -1;
				OutputReceived?.Invoke("Unable to run the command: " + e.Message, true);
			}
			finally
			{
				lock(lockobj) running = false;
				Finished?.Invoke();
			}
		}

		/// <summary>Runs the command and waits for it (when there is no window to show it in).</summary>
		public bool RunToEnd()
		{
			using(var done = new System.Threading.ManualResetEventSlim())
			{
				Finished += done.Set;
				Start();
				done.Wait();
			}
			return !HasErrors;
		}

		public void Dispose()
		{
			Stop();
			lock(lockobj) { process?.Dispose(); process = null; }
		}
	}
}
