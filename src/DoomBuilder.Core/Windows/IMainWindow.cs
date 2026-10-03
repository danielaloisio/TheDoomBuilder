using System;
using System.Drawing;
using CodeImp.DoomBuilder.Data;
using CodeImp.DoomBuilder.Editing;

namespace CodeImp.DoomBuilder.Windows
{
	/// <summary>
	/// What the Core needs from the application shell beyond the plugin-facing <see cref="IMainForm"/>.
	/// In UDB these were internal members of the concrete WinForms MainForm; the Avalonia shell implements this.
	/// </summary>
	internal interface IMainWindow : IMainForm
	{
		Font Font { get; }
		StatusInfo Status { get; }

		void SetupInterface();
		void UpdateCoordinates(CodeImp.DoomBuilder.Geometry.Vector2D coords, bool snaptogrid);
		void UpdateInterface();
		void UpdateStatus();
		void UpdateThingsFilters();
		void UpdateMapChangedStatus();
		void UpdateGZDoomPanel();
		void UpdateLinedefColorPresets();
		void UpdateZoom(float scale);
		void UpdateGrid(double gridsize);
		void ReflectThingsFilter();
		void EditModeChanged();
		void CheckEditModeButton(string modeclassname);
		void AddEditModeButton(EditModeInfo modeinfo);
		void AddEditModeSeperator(string group);
		void RemoveEditModeButtons();
		void ApplyShortcutKeys();
		void AddRecentFile(string filename);
		void AddHintsDocker();
		void RemoveHintsDocker();
		void ShowSplashDisplay();
		void ClearDisplay();
		void SetWarningsCount(int count, bool blink);
		void ResetClock();
		void DisableDynamicGridResize();
		void StopProcessing();
		void ShowErrors();
		void ShowConfiguration();
		void ShowConfigurationPage(int pageindex);
		void PerformAutoMapLoading();
		void ProcessQueuedUIActions();
		void RunOnUIThread(Action action);
		void SpriteDataLoaded(string spritename);
		void ImageDataLoaded(string imagename);
		void ImageDataLoaded(ImageData img);
		void Show();
		void Update();
		void Close();
		void Dispose();
	}
}
