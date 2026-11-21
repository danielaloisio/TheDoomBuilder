#region ================== Namespaces

using System;
using System.Windows.Forms;
using CodeImp.DoomBuilder.Geometry;

#endregion

namespace CodeImp.DoomBuilder.Actions
{
	/// <summary>
	/// Turns raw keyboard and mouse events into what the editor expects: key presses for the <see cref="ActionManager"/>,
	/// and mouse/key events for the active edit mode and the plugins. It is the input half of the WinForms MainForm,
	/// without any UI types, so every toolkit shell (and the tests) share it.
	/// Key data uses the WinForms <see cref="Keys"/> values plus the Shift/Control/Alt modifier bits: that is how
	/// shortcuts are stored in the user's settings.
	/// </summary>
	public sealed class InputDispatcher
	{
		#region ================== Variables

		private readonly IInputHost host;

		private bool alt, shift, ctrl;
		private MouseButtons mousebuttons;
		private bool mouseinside;

		// Exclusive mouse mode (3D view)
		private bool mouseexclusive;
		private int mouseexclusivebreaklevel;
		private IMouseCapture capture;

		// Periodic processing
		private int processingcount;
		private long lastupdatetime;

		#endregion

		#region ================== Constructor

		public InputDispatcher(IInputHost host)
		{
			this.host = host;
		}

		#endregion

		#region ================== Properties

		public bool AltState { get { return alt; } }
		public bool CtrlState { get { return ctrl; } }
		public bool ShiftState { get { return shift; } }
		public MouseButtons MouseButtons { get { return mousebuttons; } }
		public bool MouseInDisplay { get { return mouseinside; } }
		public bool MouseExclusive { get { return mouseexclusive; } }
		public int ProcessingCount { get { return processingcount; } }

		private static bool MapIsOpen { get { return (General.Map != null) && (General.Editing != null) && (General.Editing.Mode != null); } }

		private int Modifiers
		{
			get
			{
				int mod = 0;
				if(alt) mod |= (int)Keys.Alt;
				if(shift) mod |= (int)Keys.Shift;
				if(ctrl) mod |= (int)Keys.Control;
				return mod;
			}
		}

		#endregion

		#region ================== Keyboard

		/// <summary>A key went down. Returns true when it was used (the toolkit should not process it further).</summary>
		/// <param name="keyData">Key code with the modifier bits.</param>
		public bool KeyDown(Keys keyData)
		{
			KeyEventArgs e = new KeyEventArgs(keyData);

			// Keep key modifiers
			alt = e.Alt;
			shift = e.Shift;
			ctrl = e.Control;

			bool handled = false;
			if(e.KeyData != Keys.None)
			{
				// Invoke any actions associated with this key
				General.Actions.UpdateModifiers(Modifiers);
				handled = General.Actions.KeyPressed((int)e.KeyData);
				e.Handled = handled;

				// Invoke on editing mode
				if(MapIsOpen)
				{
					if(General.Plugins != null) General.Plugins.OnEditKeyDown(e);
					General.Editing.Mode.OnKeyDown(e);
					handled = e.Handled;
				}
			}

			// F1 pressed without an action bound to it: help for the mode (or the main help when no map is open)
			if((e.KeyCode == Keys.F1) && (e.Modifiers == Keys.None))
			{
				if(General.Actions.GetActionsByKey((int)e.KeyData).Length == 0)
				{
					if(MapIsOpen) General.Editing.Mode.OnHelp();
					else General.ShowHelp("introduction.html");
				}
			}

			// F10 would open the menu bar of the toolkit
			if((e.KeyCode == Keys.F10) && (General.Actions.GetActionsByKey((int)e.KeyData).Length > 0))
				handled = true;

			return handled;
		}

		/// <summary>A key was released. Returns true when it was used.</summary>
		public bool KeyUp(Keys keyData)
		{
			KeyEventArgs e = new KeyEventArgs(keyData);

			alt = e.Alt;
			shift = e.Shift;
			ctrl = e.Control;

			General.Actions.UpdateModifiers(Modifiers);
			bool handled = General.Actions.KeyReleased((int)e.KeyData);
			e.Handled = handled;

			if(MapIsOpen)
			{
				if(General.Plugins != null) General.Plugins.OnEditKeyUp(e);
				General.Editing.Mode.OnKeyUp(e);
				handled = e.Handled;
			}

			if((e.KeyCode == Keys.F10) && (General.Actions.GetActionsByKey((int)e.KeyData).Length > 0))
				handled = true;

			return handled;
		}

		/// <summary>Releases every pressed key and button (the window lost focus, a dialog opened...).</summary>
		public void ReleaseAllKeys()
		{
			General.Actions.ReleaseAllKeys();
			mousebuttons = MouseButtons.None;
			shift = false;
			ctrl = false;
			alt = false;
		}

		#endregion

		#region ================== Mouse

		private static Keys ButtonKey(MouseButtons button)
		{
			switch(button)
			{
				case MouseButtons.Left: return Keys.LButton;
				case MouseButtons.Middle: return Keys.MButton;
				case MouseButtons.Right: return Keys.RButton;
				case MouseButtons.XButton1: return Keys.XButton1;
				case MouseButtons.XButton2: return Keys.XButton2;
				default: return Keys.None;
			}
		}

		public void MouseDown(MouseEventArgs e)
		{
			// Apply button
			mousebuttons |= e.Button;

			// Invoke any actions associated with this button
			General.Actions.KeyPressed((int)ButtonKey(e.Button) | Modifiers);

			if(MapIsOpen)
			{
				if(General.Plugins != null) General.Plugins.OnEditMouseDown(e);
				General.Editing.Mode.OnMouseDown(e);
			}
		}

		public void MouseUp(MouseEventArgs e)
		{
			mousebuttons &= ~e.Button;

			General.Actions.KeyReleased((int)ButtonKey(e.Button) | Modifiers);

			if(MapIsOpen)
			{
				if(General.Plugins != null) General.Plugins.OnEditMouseUp(e);
				General.Editing.Mode.OnMouseUp(e);
			}
		}

		public void MouseClick(MouseEventArgs e)
		{
			if(MapIsOpen)
			{
				if(General.Plugins != null) General.Plugins.OnEditMouseClick(e);
				General.Editing.Mode.OnMouseClick(e);
			}
		}

		public void MouseDoubleClick(MouseEventArgs e)
		{
			if(MapIsOpen)
			{
				if(General.Plugins != null) General.Plugins.OnEditMouseDoubleClick(e);
				General.Editing.Mode.OnMouseDoubleClick(e);
			}
		}

		public void MouseMove(MouseEventArgs e)
		{
			// While the mouse is captured, movement arrives through Tick() as relative input instead
			if(MapIsOpen && (capture == null))
			{
				if(General.Plugins != null) General.Plugins.OnEditMouseMove(e);
				General.Editing.Mode.OnMouseMove(e);
			}
		}

		public void MouseEnter(EventArgs e)
		{
			mouseinside = true;

			// Skip when exclusive (3D) so the mouse does not disappear when moved over another window
			if(MapIsOpen && (capture == null) && !mouseexclusive)
			{
				if(General.Plugins != null) General.Plugins.OnEditMouseEnter(e);
				General.Editing.Mode.OnMouseEnter(e);
			}
		}

		public void MouseLeave(EventArgs e)
		{
			mouseinside = false;

			if(MapIsOpen && (capture == null))
			{
				if(General.Plugins != null) General.Plugins.OnEditMouseLeave(e);
				General.Editing.Mode.OnMouseLeave(e);
			}
		}

		/// <summary>Vertical wheel: positive delta is up. Wheel steps become "scroll" keys for the action system.</summary>
		public void Wheel(int delta)
		{
			if(!host.CanProcessInput) return;

			if(delta > 0)
			{
				General.Actions.KeyPressed((int)SpecialKeys.MScrollUp | Modifiers);
				General.Actions.KeyReleased((int)SpecialKeys.MScrollUp | Modifiers);
			}
			else if(delta < 0)
			{
				General.Actions.KeyPressed((int)SpecialKeys.MScrollDown | Modifiers);
				General.Actions.KeyReleased((int)SpecialKeys.MScrollDown | Modifiers);
			}
		}

		/// <summary>Horizontal wheel: negative delta is left.</summary>
		public void HorizontalWheel(int delta)
		{
			if(delta < 0)
			{
				General.Actions.KeyPressed((int)SpecialKeys.MScrollLeft | Modifiers);
				General.Actions.KeyReleased((int)SpecialKeys.MScrollLeft | Modifiers);
			}
			else if(delta > 0)
			{
				General.Actions.KeyPressed((int)SpecialKeys.MScrollRight | Modifiers);
				General.Actions.KeyReleased((int)SpecialKeys.MScrollRight | Modifiers);
			}
		}

		#endregion

		#region ================== Exclusive mouse input

		// Locks the mouse in the window and starts reading relative movement
		private void StartMouseCapture()
		{
			if(capture == null) capture = host.BeginMouseCapture();
		}

		// Gives the mouse back
		private void StopMouseCapture()
		{
			if(capture != null)
			{
				capture.Dispose();
				capture = null;
			}
		}

		/// <summary>Requests exclusive mouse input (the 3D view: pointer hidden and locked, movement reported as deltas).</summary>
		public void StartExclusiveMouseInput()
		{
			if(!mouseexclusive)
			{
				General.WriteLogLine("Starting exclusive mouse input mode...");
				StartMouseCapture();
				mouseexclusive = true;
				mouseexclusivebreaklevel = 0;
			}
		}

		public void StopExclusiveMouseInput()
		{
			if(mouseexclusive)
			{
				General.WriteLogLine("Stopping exclusive mouse input mode...");
				StopMouseCapture();
				mouseexclusive = false;
				mouseexclusivebreaklevel = 0;
			}
		}

		/// <summary>Temporarily gives the mouse back (for a dialog) and counts the break level.</summary>
		public void BreakExclusiveMouseInput()
		{
			if(mouseexclusive)
			{
				StopMouseCapture();
				mouseexclusivebreaklevel++;
			}
		}

		/// <summary>Takes the mouse again once every break has been resumed.</summary>
		public void ResumeExclusiveMouseInput()
		{
			if(mouseexclusive && (mouseexclusivebreaklevel > 0))
			{
				mouseexclusivebreaklevel--;
				if(mouseexclusivebreaklevel == 0) StartMouseCapture();
			}
		}

		#endregion

		#region ================== Processing

		public void EnableProcessing()
		{
			processingcount++;

			if(processingcount == 1)
			{
				lastupdatetime = Clock.CurrentTime;
				host.SetProcessing(true);
			}
		}

		public void DisableProcessing()
		{
			processingcount--;
			if(processingcount < 0) processingcount = 0;

			if(processingcount == 0) host.SetProcessing(false);
		}

		public void StopProcessing()
		{
			processingcount = 0;
			host.SetProcessing(false);
		}

		/// <summary>The clock was reset: the next delta must not be negative.</summary>
		public void ResetClock()
		{
			lastupdatetime = 0;
		}

		/// <summary>Called by the shell's timer while processing is enabled: relative mouse input, then the mode's OnProcess.</summary>
		public void Tick()
		{
			long curtime = Clock.CurrentTime;
			long deltatime = curtime - lastupdatetime;
			lastupdatetime = curtime;

			if(MapIsOpen)
			{
				if(capture != null)
				{
					Vector2D raw = capture.Poll();

					// Calculate changes depending on sensitivity
					Vector2D deltamouse = new Vector2D(
						raw.x * General.Settings.VisualMouseSensX * General.Settings.MouseSpeed * 0.01f,
						raw.y * General.Settings.VisualMouseSensY * General.Settings.MouseSpeed * 0.01f);

					if(General.Plugins != null) General.Plugins.OnEditMouseInput(deltamouse);
					General.Editing.Mode.OnMouseInput(deltamouse);
				}

				General.Editing.Mode.OnProcess(deltatime);
			}
		}

		#endregion
	}
}
