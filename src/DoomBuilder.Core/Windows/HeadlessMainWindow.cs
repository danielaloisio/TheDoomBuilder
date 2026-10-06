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
	internal class HeadlessMainWindow : IMainWindow
	{
		private readonly Font font = new Font("sans-serif", 9f);
		private StatusInfo status = new StatusInfo(StatusType.Ready, "Ready.");
		private int processingcount;

		public virtual IntPtr Handle { get { return IntPtr.Zero; } }
		public virtual Font Font { get { return font; } }
		public virtual StatusInfo Status { get { return status; } }

		public virtual bool AltState { get { return false; } }
		public virtual bool CtrlState { get { return false; } }
		public virtual bool ShiftState { get { return false; } }
		public virtual bool MouseInDisplay { get { return false; } }
		public virtual bool AutoMerge { get { return false; } }
		public virtual bool SnapToGrid { get { return false; } }
		public virtual bool MouseExclusive { get { return false; } }
		public virtual MouseButtons MouseButtons { get { return MouseButtons.None; } }
		public virtual bool IsActiveWindow { get { return false; } }
		public virtual string ActiveDockerTabName { get { return string.Empty; } }
		public virtual RenderTargetControl Display { get { return null; } }
		public virtual int ProcessingCount { get { return processingcount; } }
		public event EventHandler OnEditFormValuesChanged;

		/// <summary>An edit dialog changed its elements: the current mode may handle it, else the map is updated and redrawn.</summary>
		protected void EditFormValuesChanged(object sender, EventArgs e)
		{
			if(OnEditFormValuesChanged != null)
			{
				OnEditFormValuesChanged(sender, e);
			}
			else
			{
				General.Map.Map.Update();
				RedrawDisplay();
			}
		}

		public virtual void DisplayReady() { status = new StatusInfo(StatusType.Ready, "Ready."); }
		public virtual void DisplayStatus(StatusType type, string message) { status = new StatusInfo(type, message); }
		public virtual void DisplayStatus(StatusInfo newstatus) { status = newstatus; }
		public virtual void RedrawDisplay() { }

		public virtual DialogResult ShowEditVertices(ICollection<Vertex> vertices) { return ShowEditVertices(vertices, true); }
		public virtual DialogResult ShowEditVertices(ICollection<Vertex> vertices, bool allowPositionChange) { return ShowEditDialog(() => General.Dialogs.ShowEditVertices(vertices, allowPositionChange, EditFormValuesChanged)); }
		public virtual DialogResult ShowEditLinedefs(ICollection<Linedef> lines) { return ShowEditLinedefs(lines, false, false); }
		public virtual DialogResult ShowEditLinedefs(ICollection<Linedef> lines, bool selectfront, bool selectback) { return ShowEditDialog(() => General.Dialogs.ShowEditLinedefs(lines, selectfront, selectback, EditFormValuesChanged)); }
		public virtual DialogResult ShowEditSectors(ICollection<Sector> sectors) { return ShowEditDialog(() => General.Dialogs.ShowEditSectors(sectors, EditFormValuesChanged)); }
		public virtual DialogResult ShowEditThings(ICollection<Thing> things) { return ShowEditDialog(() => General.Dialogs.ShowEditThings(things, EditFormValuesChanged)); }

		// Edit dialogs run with the editing input paused, as in UDB
		private DialogResult ShowEditDialog(Func<DialogResult> show)
		{
			DisableProcessing();
			BreakExclusiveMouseInput();
			try { return show(); }
			finally { ResumeExclusiveMouseInput(); EnableProcessing(); }
		}
		#region ================== Info panel

		private object lastinfoobject;

		/// <summary>The info of the element under the mouse (or aimed at) changed; null when there is none. Raised on the UI thread.</summary>
		public event Action<ElementInfo> InfoChanged;

		/// <summary>The info panel is open (it can be collapsed to leave more room for the map).</summary>
		public bool IsInfoPanelExpanded { get; set; } = true;

		/// <summary>The element whose info is shown now.</summary>
		public object InfoObject { get { return lastinfoobject; } }

		public virtual void ShowLinedefInfo(Linedef l) { ShowLinedefInfo(l, null); }
		public virtual void ShowLinedefInfo(Linedef l, Sidedef highlightside)
		{
			if(l.IsDisposed) { HideInfo(); return; }
			lastinfoobject = l;
			if(IsInfoPanelExpanded) InfoChanged?.Invoke(ElementInfoBuilder.ForLinedef(l, highlightside));
			General.Plugins?.OnHighlightLinedef(l);
		}

		public virtual void ShowSectorInfo(Sector s) { ShowSectorInfo(s, false, false); }
		public virtual void ShowSectorInfo(Sector s, bool highlightceiling, bool highlightfloor)
		{
			if(s.IsDisposed) { HideInfo(); return; }
			lastinfoobject = s;
			if(IsInfoPanelExpanded) InfoChanged?.Invoke(ElementInfoBuilder.ForSector(s, highlightceiling, highlightfloor));
			General.Plugins?.OnHighlightSector(s);
		}

		public virtual void ShowThingInfo(Thing t)
		{
			if(t.IsDisposed) { HideInfo(); return; }
			lastinfoobject = t;
			if(IsInfoPanelExpanded) InfoChanged?.Invoke(ElementInfoBuilder.ForThing(t));
			General.Plugins?.OnHighlightThing(t);
		}

		public virtual void ShowVertexInfo(Vertex v)
		{
			if(v.IsDisposed) { HideInfo(); return; }
			lastinfoobject = v;
			if(IsInfoPanelExpanded) InfoChanged?.Invoke(ElementInfoBuilder.ForVertex(v));
			General.Plugins?.OnHighlightVertex(v);
		}

		public virtual void HideInfo()
		{
			lastinfoobject = null;
			InfoChanged?.Invoke(null);
			General.Plugins?.OnHighlightLost();
		}

		public virtual void RefreshInfo()
		{
			if(lastinfoobject is Vertex) ShowVertexInfo((Vertex)lastinfoobject);
			else if(lastinfoobject is Linedef) ShowLinedefInfo((Linedef)lastinfoobject);
			else if(lastinfoobject is Sector) ShowSectorInfo((Sector)lastinfoobject);
			else if(lastinfoobject is Thing) ShowThingInfo((Thing)lastinfoobject);
			General.Plugins?.OnHighlightRefreshed(lastinfoobject);
		}

		#endregion

		public virtual void ShowHints(string hints) { }
		public virtual void ClearHints() { }
		public virtual void UpdateCoordinates(Vector2D coords) { }
		public virtual void UpdateCoordinates(Vector2D coords, bool snaptogrid) { }
		public virtual bool Focus() { return false; }
		public virtual bool FocusDisplay() { return false; }
		public virtual void EnableProcessing() { processingcount = Math.Max(0, processingcount - 1); }
		public virtual void DisableProcessing() { processingcount++; }
		public virtual void StartExclusiveMouseInput() { }
		public virtual void StopExclusiveMouseInput() { }
		public virtual void BreakExclusiveMouseInput() { }
		public virtual void ResumeExclusiveMouseInput() { }
		public virtual void SetCursor(Cursor cursor) { }
		public virtual void MessageBeep(MessageBeepType type) { }

		public virtual SizeF MeasureString(string text, Font f) { return MeasureString(text, f, int.MaxValue, null); }
		public virtual SizeF MeasureString(string text, Font f, int width, StringFormat format)
		{
			using(Bitmap bmp = new Bitmap(1, 1))
			using(Graphics g = Graphics.FromImage(bmp))
				return g.MeasureString(text, f);
		}

		public virtual int BrowseLinedefActions(IWin32Window owner, int initialvalue) { return initialvalue; }
		public virtual int BrowseLinedefActions(IWin32Window owner, int initialvalue, bool addanyaction) { return initialvalue; }
		public virtual int BrowseSectorEffect(IWin32Window owner, int initialvalue) { return initialvalue; }
		public virtual int BrowseSectorEffect(IWin32Window owner, int initialvalue, bool addanyeffect) { return initialvalue; }
		public virtual string BrowseTexture(IWin32Window owner, string initialvalue) { return General.Dialogs.BrowseImage(initialvalue, false); }
		public virtual string BrowseFlat(IWin32Window owner, string initialvalue) { return General.Dialogs.BrowseImage(initialvalue, true); }
		public virtual int BrowseThingType(IWin32Window owner, int initialvalue) { return initialvalue; }

		public virtual void AddMenu(ToolStripItem menu) { }
		public virtual void AddMenu(ToolStripItem menu, MenuSection section) { }
		public virtual void AddModesMenu(ToolStripItem menu, string group) { }
		public virtual void RemoveMenu(ToolStripItem menu) { }
		public virtual void InvokeTaggedAction(object sender, EventArgs e) { }
		public virtual void AddButton(ToolStripItem button) { }
		public virtual void AddButton(ToolStripItem button, ToolbarSection section) { }
		public virtual void AddModesButton(ToolStripItem toolbarButton, string group) { }
		public virtual void RemoveButton(ToolStripItem button) { }
		public virtual void BeginToolbarUpdate() { }
		public virtual void EndToolbarUpdate() { }
		public virtual void AddDocker(Docker d) { }
		public virtual void AddDocker(Docker d, bool notify) { }
		public virtual bool RemoveDocker(Docker d) { return false; }
		public virtual bool SelectDocker(Docker d) { return false; }
		public virtual void SelectPreviousDocker() { }

		// IMainWindow
		public virtual CodeImp.DoomBuilder.Rendering.IRenderBackend CreateRenderBackend() { return new CodeImp.DoomBuilder.Rendering.NullRenderBackend(); }
		public virtual void SetupInterface() { }
		public virtual void UpdateInterface() { }
		public virtual void UpdateStatus() { }
		public virtual void UpdateThingsFilters() { }
		public virtual void UpdateMapChangedStatus() { }
		public virtual void UpdateGZDoomPanel() { }
		public virtual void UpdateLinedefColorPresets() { }
		public virtual void UpdateZoom(float scale) { }
		public virtual void UpdateGrid(double gridsize) { }
		public virtual void ReflectThingsFilter() { }
		public virtual void EditModeChanged() { }
		public virtual void CheckEditModeButton(string modeclassname) { }
		public virtual void AddEditModeButton(EditModeInfo modeinfo) { }
		public virtual void AddEditModeSeperator(string group) { }
		public virtual void RemoveEditModeButtons() { }
		public virtual void ApplyShortcutKeys() { }
		public virtual void AddRecentFile(string filename) { }
		public virtual void AddHintsDocker() { }
		public virtual void RemoveHintsDocker() { }
		public virtual void ShowSplashDisplay() { }
		public virtual void ClearDisplay() { }
		public virtual void SetWarningsCount(int count, bool blink) { }
		public virtual void ResetClock() { }
		public virtual void DisableDynamicGridResize() { }
		public virtual void StopProcessing() { }
		public virtual void ShowErrors() { }
		public virtual void ShowConfiguration() { }
		public virtual void ShowConfigurationPage(int pageindex) { }
		public virtual void PerformAutoMapLoading() { General.PerformAutoMapLoading(); }
		public virtual void ProcessQueuedUIActions() { }
		public virtual void RunOnUIThread(Action action) { action(); }
		/// <summary>Redraws soon (several requests close together make one redraw). The base redraws at once.</summary>
		public virtual void DelayedRedraw() { RedrawDisplay(); }

		// Images load on a background thread. The display draws the flats and sprites it has, and must draw them again as they arrive
		public virtual void SpriteDataLoaded(string spritename)
		{
			RunOnUIThread(() =>
			{
				if(General.Map != null && General.Map.Data != null)
				{
					ImageData img = General.Map.Data.GetSpriteImage(spritename);
					if(img != null && img.UsedInMap && !img.IsDisposed) DelayedRedraw();
				}
			});
		}

		public virtual void ImageDataLoaded(string imagename)
		{
			RunOnUIThread(() =>
			{
				if(General.Map != null && General.Map.Data != null) ImageDataLoaded(General.Map.Data.GetFlatImage(imagename));
			});
		}

		// Called when an image was loaded for the first time or changed size: the sectors that use it as a floor or ceiling update their
		// surface (the fill of the 2D view) and the display is redrawn
		public virtual void ImageDataLoaded(ImageData img)
		{
			if(img == null || !img.UsedInMap || img.IsDisposed || General.Map == null || !General.Map.Map.IsSafeToAccess) return;

			bool updated = false;
			long imgshorthash = General.Map.Data.GetShortLongFlatName(img.LongName);
			foreach(Sector s in General.Map.Map.Sectors)
			{
				if(s.LongFloorTexture == img.LongName || s.LongFloorTexture == imgshorthash) { s.UpdateFloorSurface(); updated = true; }
				if(s.LongCeilTexture == img.LongName || s.LongCeilTexture == imgshorthash) { s.UpdateCeilingSurface(); updated = true; }
			}
			if(updated) DelayedRedraw();
		}
		public virtual void Show() { }
		public virtual void Update() { }
		public virtual void Close() { }
		public virtual void Dispose() { font.Dispose(); }
	}
}
