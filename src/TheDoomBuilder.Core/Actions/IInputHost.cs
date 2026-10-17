using CodeImp.DoomBuilder.Geometry;

namespace CodeImp.DoomBuilder.Actions
{
	/// <summary>
	/// Relative mouse movement for exclusive mouse mode (the 3D view). UDB read it from a native RawMouse (Windows) or by
	/// warping the cursor to the window center (the Unix fallback). Implementations live with the UI toolkit.
	/// </summary>
	public interface IMouseCapture : System.IDisposable
	{
		/// <summary>Pointer movement in pixels since the previous call (or since the capture started).</summary>
		Vector2D Poll();
	}

	/// <summary>What the <see cref="InputDispatcher"/> needs from the application shell.</summary>
	public interface IInputHost
	{
		/// <summary>
		/// Confines and hides the pointer inside the display and returns the capture that reports its movement.
		/// Disposing the capture gives the pointer back.
		/// </summary>
		IMouseCapture BeginMouseCapture();

		/// <summary>Turns the periodic <see cref="InputDispatcher.Tick"/> calls (about every 10 ms) on or off.</summary>
		void SetProcessing(bool enabled);

		/// <summary>False while a modal dialog is open: wheel input is then ignored.</summary>
		bool CanProcessInput { get; }
	}
}
