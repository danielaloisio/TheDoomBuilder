using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using CodeImp.DoomBuilder.Controls;
using CodeImp.DoomBuilder.Data;
using CodeImp.DoomBuilder.Editing;
using CodeImp.DoomBuilder.Geometry;
using CodeImp.DoomBuilder.Map;

namespace CodeImp.DoomBuilder.Windows
{
	/// <summary>
	/// IMainWindow implementation with no UI. Used by tests and command-line tools that run the Core
	/// without the Avalonia shell. Every UI request is a no-op; dialogs answer "cancel".
	/// </summary>
	internal sealed class HeadlessMainWindow : IMainWindow
	{
		private readonly Font font = new Font("sans-serif", 9f);
		private StatusInfo status = new StatusInfo(StatusType.Ready, "Ready.");
		private int processingcount;

		public IntPtr Handle { get { return IntPtr.Zero; } }
		public Font Font { get { return font; } }
		public StatusInfo Status { get { return status; } }

		public bool AltState { get { return false; } }
		public bool CtrlState { get { return false; } }
		public bool ShiftState { get { return false; } }
		public bool MouseInDisplay { get { return false; } }
		public bool AutoMerge { get { return false; } }
		public bool SnapToGrid { get { return false; } }
		public bool MouseExclusive { get { return false; } }
		public MouseButtons MouseButtons { get { return MouseButtons.None; } }
		public bool IsActiveWindow { get { return false; } }
		public string ActiveDockerTabName { get { return string.Empty; } }
		public RenderTargetControl Display { get { return null; } }
		public int ProcessingCount { get { return processingcount; } }
		public event EventHandler OnEditFormValuesChanged { add { } remove { } }

		public void DisplayReady() { status = new StatusInfo(StatusType.Ready, "Ready."); }
		public void DisplayStatus(StatusType type, string message) { status = new StatusInfo(type, message); }
		public void DisplayStatus(StatusInfo newstatus) { status = newstatus; }
		public void RedrawDisplay() { }

		public DialogResult ShowEditVertices(ICollection<Vertex> vertices) { return DialogResult.Cancel; }
		public DialogResult ShowEditVertices(ICollection<Vertex> vertices, bool allowPositionChange) { return DialogResult.Cancel; }
		public DialogResult ShowEditLinedefs(ICollection<Linedef> lines) { return DialogResult.Cancel; }
		public DialogResult ShowEditLinedefs(ICollection<Linedef> lines, bool selectfront, bool selectback) { return DialogResult.Cancel; }
		public DialogResult ShowEditSectors(ICollection<Sector> sectors) { return DialogResult.Cancel; }
		public DialogResult ShowEditThings(ICollection<Thing> things) { return DialogResult.Cancel; }
		public void ShowLinedefInfo(Linedef l) { }
		public void ShowLinedefInfo(Linedef l, Sidedef highlightside) { }
		public void ShowSectorInfo(Sector s) { }
		public void ShowSectorInfo(Sector s, bool highlightceiling, bool highlightfloor) { }
		public void ShowThingInfo(Thing t) { }
		public void ShowVertexInfo(Vertex v) { }
		public void HideInfo() { }
		public void ShowHints(string hints) { }
		public void ClearHints() { }
		public void RefreshInfo() { }
		public void UpdateCoordinates(Vector2D coords) { }
		public void UpdateCoordinates(Vector2D coords, bool snaptogrid) { }
		public bool Focus() { return false; }
		public bool FocusDisplay() { return false; }
		public void EnableProcessing() { processingcount = Math.Max(0, processingcount - 1); }
		public void DisableProcessing() { processingcount++; }
		public void StartExclusiveMouseInput() { }
		public void StopExclusiveMouseInput() { }
		public void BreakExclusiveMouseInput() { }
		public void ResumeExclusiveMouseInput() { }
		public void SetCursor(Cursor cursor) { }
		public void MessageBeep(MessageBeepType type) { }

		public SizeF MeasureString(string text, Font f) { return MeasureString(text, f, int.MaxValue, null); }
		public SizeF MeasureString(string text, Font f, int width, StringFormat format)
		{
			using(Bitmap bmp = new Bitmap(1, 1))
			using(Graphics g = Graphics.FromImage(bmp))
				return g.MeasureString(text, f);
		}

		public int BrowseLinedefActions(IWin32Window owner, int initialvalue) { return initialvalue; }
		public int BrowseLinedefActions(IWin32Window owner, int initialvalue, bool addanyaction) { return initialvalue; }
		public int BrowseSectorEffect(IWin32Window owner, int initialvalue) { return initialvalue; }
		public int BrowseSectorEffect(IWin32Window owner, int initialvalue, bool addanyeffect) { return initialvalue; }
		public string BrowseTexture(IWin32Window owner, string initialvalue) { return initialvalue; }
		public string BrowseFlat(IWin32Window owner, string initialvalue) { return initialvalue; }
		public int BrowseThingType(IWin32Window owner, int initialvalue) { return initialvalue; }

		public void AddMenu(ToolStripItem menu) { }
		public void AddMenu(ToolStripItem menu, MenuSection section) { }
		public void AddModesMenu(ToolStripItem menu, string group) { }
		public void RemoveMenu(ToolStripItem menu) { }
		public void InvokeTaggedAction(object sender, EventArgs e) { }
		public void AddButton(ToolStripItem button) { }
		public void AddButton(ToolStripItem button, ToolbarSection section) { }
		public void AddModesButton(ToolStripItem toolbarButton, string group) { }
		public void RemoveButton(ToolStripItem button) { }
		public void BeginToolbarUpdate() { }
		public void EndToolbarUpdate() { }
		public void AddDocker(Docker d) { }
		public void AddDocker(Docker d, bool notify) { }
		public bool RemoveDocker(Docker d) { return false; }
		public bool SelectDocker(Docker d) { return false; }
		public void SelectPreviousDocker() { }

		// IMainWindow
		public void SetupInterface() { }
		public void UpdateInterface() { }
		public void UpdateStatus() { }
		public void UpdateThingsFilters() { }
		public void UpdateMapChangedStatus() { }
		public void UpdateGZDoomPanel() { }
		public void UpdateLinedefColorPresets() { }
		public void UpdateZoom(float scale) { }
		public void UpdateGrid(double gridsize) { }
		public void ReflectThingsFilter() { }
		public void EditModeChanged() { }
		public void CheckEditModeButton(string modeclassname) { }
		public void AddEditModeButton(EditModeInfo modeinfo) { }
		public void AddEditModeSeperator(string group) { }
		public void RemoveEditModeButtons() { }
		public void ApplyShortcutKeys() { }
		public void AddRecentFile(string filename) { }
		public void AddHintsDocker() { }
		public void RemoveHintsDocker() { }
		public void ShowSplashDisplay() { }
		public void ClearDisplay() { }
		public void SetWarningsCount(int count, bool blink) { }
		public void ResetClock() { }
		public void DisableDynamicGridResize() { }
		public void StopProcessing() { }
		public void ShowErrors() { }
		public void ShowConfiguration() { }
		public void ShowConfigurationPage(int pageindex) { }
		public void PerformAutoMapLoading() { }
		public void ProcessQueuedUIActions() { }
		public void RunOnUIThread(Action action) { action(); }
		public void SpriteDataLoaded(string spritename) { }
		public void ImageDataLoaded(string imagename) { }
		public void ImageDataLoaded(ImageData img) { }
		public void Show() { }
		public void Update() { }
		public void Close() { }
		public void Dispose() { font.Dispose(); }
	}
}
