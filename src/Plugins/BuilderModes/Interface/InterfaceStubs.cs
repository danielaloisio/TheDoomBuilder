// TEMPORARY STUBS for the WinForms dialogs and docker panels of BuilderModes' Interface folder that are not ported yet. The
// dialogs answer "cancelled", so the editing command that opens one simply does nothing. Each is replaced by a real Avalonia
// implementation (see PLAN-phase5-status.md); the namespaces are the ones the UDB sources use, so the plugin code compiles unchanged.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using CodeImp.DoomBuilder.Geometry;
using CodeImp.DoomBuilder.Map;

namespace CodeImp.DoomBuilder.BuilderModes
{
    /// <summary>A modal dialog that is not ported: it is never shown and counts as cancelled.</summary>
    public abstract class StubDialog : IDisposable, IWin32Window
    {
        public IntPtr Handle { get { return IntPtr.Zero; } }
        public DialogResult ShowDialog() { return DialogResult.Cancel; }
        public DialogResult ShowDialog(IWin32Window owner) { return DialogResult.Cancel; }
        public virtual void Dispose() { }
    }

    // The parameters of the "fit textures" dialog (visual mode): a plain data structure, kept as it is
    internal struct FitTextureOptions
    {
        public double HorizontalRepeat;
        public double VerticalRepeat;
        public int PatternWidth;
        public int PatternHeight;
        public bool FitWidth;
        public bool FitHeight;
        public bool FitAcrossSurfaces;
        public bool AutoWidth;
        public bool AutoHeight;
        public Rectangle GlobalBounds;
        public Rectangle Bounds;

        // Initial texture coordinates
        public double InitialOffsetX;
        public double InitialOffsetY;
        public double ControlSideOffsetX;
        public double ControlSideOffsetY;
        public double InitialScaleX;
        public double InitialScaleY;
    }


    internal class FitTexturesForm : StubDialog { public bool Setup(params object[] args) { return false; } }
    public class FindReplaceForm : StubDialog { }
    public class ErrorCheckForm : StubDialog { }

    // The docker panels. They are controls in the shim (a Docker holds one); the real ones come with the dockers' content.
}

namespace CodeImp.DoomBuilder.BuilderModes.Interface
{
    internal class BridgeModeForm : StubDialog { }
    internal class SlopeArchForm : StubDialog { public SlopeArchForm(params object[] args) { } public event EventHandler UpdateChangedObjects; }
}
