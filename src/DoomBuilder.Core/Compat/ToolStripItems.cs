// The toolbar and menu items that plugins hand to the shell (General.Interface.AddButton / AddMenu). In UDB these were WinForms
// ToolStrip items; here they are plain objects that hold the state (text, checked, enabled, value...) and raise events, and the
// Avalonia shell draws them. Same names and namespaces as the originals, so the plugin sources keep compiling.
using System;
using System.Collections.Generic;
using System.Drawing;

namespace System.Windows.Forms
{
    public enum ToolStripItemDisplayStyle { None, Text, Image, ImageAndText }

    public class ToolStripItem
    {
        private string text, tooltip;
        private Image image;
        private bool enabled = true, visible = true;

        public ToolStripItem() { }
        public ToolStripItem(string text) { this.text = text; }
        public ToolStripItem(string text, Image image, EventHandler onClick, string name) { this.text = text; this.image = image; if (onClick != null) Click += onClick; Name = name; }

        public string Name { get; set; }
        public object Tag { get; set; }
        public ToolStripItemDisplayStyle DisplayStyle { get; set; } = ToolStripItemDisplayStyle.Image;
        public string ShortcutKeyDisplayString { get; set; }

        public virtual string Text { get { return text; } set { if (text != value) { text = value; Changed(); } } }
        public virtual string ToolTipText { get { return tooltip; } set { if (tooltip != value) { tooltip = value; Changed(); } } }
        public virtual Image Image { get { return image; } set { if (image != value) { image = value; Changed(); } } }
        public virtual bool Enabled { get { return enabled; } set { if (enabled != value) { enabled = value; Changed(); } } }
        public virtual bool Visible { get { return visible; } set { if (visible != value) { visible = value; Changed(); } } }

        public event EventHandler Click;
        /// <summary>Raised when what the shell shows changed (text, image, enabled, visible, checked, value...).</summary>
        public event EventHandler StateChanged;

        public virtual void PerformClick() { OnClick(EventArgs.Empty); }
        protected virtual void OnClick(EventArgs e) { Click?.Invoke(this, e); }
        protected void Changed() { StateChanged?.Invoke(this, EventArgs.Empty); }
        public override string ToString() { return Text ?? Name ?? GetType().Name; }
    }

    public class ToolStripSeparator : ToolStripItem { }

    public class ToolStripLabel : ToolStripItem
    {
        public ToolStripLabel() { }
        public ToolStripLabel(string text) : base(text) { }
    }

    public class ToolStripButton : ToolStripItem
    {
        private bool isChecked;

        public ToolStripButton() { }
        public ToolStripButton(string text) : base(text) { }
        public ToolStripButton(string text, Image image) : base(text) { Image = image; }
        public ToolStripButton(string text, Image image, EventHandler onClick) : base(text, image, onClick, null) { }
        public ToolStripButton(string text, Image image, EventHandler onClick, string name) : base(text, image, onClick, name) { }

        public bool CheckOnClick { get; set; }
        public bool Checked
        {
            get { return isChecked; }
            set { if (isChecked != value) { isChecked = value; Changed(); CheckedChanged?.Invoke(this, EventArgs.Empty); } }
        }
        public event EventHandler CheckedChanged;

        protected override void OnClick(EventArgs e)
        {
            if (CheckOnClick) Checked = !Checked;
            base.OnClick(e);
        }
    }

    /// <summary>A menu entry. Menus nest: the entries below it are in <see cref="DropDownItems"/>.</summary>
    public class ToolStripMenuItem : ToolStripButton
    {
        public ToolStripMenuItem() { }
        public ToolStripMenuItem(string text) : base(text) { }
        public ToolStripMenuItem(string text, Image image, EventHandler onClick) : base(text, image, onClick) { }
        public ToolStripItemCollection DropDownItems { get; } = new ToolStripItemCollection();
        /// <summary>The shell raises this just before it shows the entries below (UDB filled them lazily, e.g. the list of things).</summary>
        public event EventHandler DropDownOpening;
        public void RaiseDropDownOpening() { DropDownOpening?.Invoke(this, EventArgs.Empty); }
    }

    public class ToolStripDropDownButton : ToolStripButton
    {
        public ToolStripItemCollection DropDownItems { get; } = new ToolStripItemCollection();
    }

    public class ToolStripSplitButton : ToolStripDropDownButton { }

    public class ToolStripComboBox : ToolStripItem
    {
        private int selectedindex = -1;

        public List<object> Items { get; } = new List<object>();
        public int SelectedIndex
        {
            get { return selectedindex; }
            set { if (selectedindex != value) { selectedindex = value; Changed(); SelectedIndexChanged?.Invoke(this, EventArgs.Empty); } }
        }
        public object SelectedItem { get { return selectedindex >= 0 && selectedindex < Items.Count ? Items[selectedindex] : null; } set { SelectedIndex = Items.IndexOf(value); } }
        public override string Text { get { return SelectedItem != null ? SelectedItem.ToString() : base.Text; } set { int i = Items.FindIndex(o => o.ToString() == value); if (i >= 0) SelectedIndex = i; else base.Text = value; } }
        public event EventHandler SelectedIndexChanged;
        public event EventHandler DropDownClosed;
        public void RaiseDropDownClosed() { DropDownClosed?.Invoke(this, EventArgs.Empty); }
    }

    public class ToolStripItemCollection : List<ToolStripItem>
    {
        public void AddRange(IEnumerable<ToolStripItem> items) { base.AddRange(items); }
    }

    /// <summary>The ordered items of a toolbar or menu bar (what a designer's ToolStrip/MenuStrip held).</summary>
    public class ToolStrip
    {
        public string Name { get; set; }
        public string Text { get; set; }
        public ToolStripItemCollection Items { get; } = new ToolStripItemCollection();
    }

    public class MenuStrip : ToolStrip { }
}

namespace CodeImp.DoomBuilder.Controls
{
    using System.Windows.Forms;

    /// <summary>The base of the toolbar panels and menu holders ported from UDB (they were WinForms controls/forms there).</summary>
    public class ToolStripHost : IDisposable
    {
        public virtual void Dispose() { }
    }

    /// <summary>A button that runs the program action named in its Tag; the shell adds the action's shortcut to the tooltip.</summary>
    public class ToolStripActionButton : ToolStripButton
    {
        private string baseToolTip;

        public ToolStripActionButton() { }
        public ToolStripActionButton(string text) : base(text) { }
        public ToolStripActionButton(string text, Image image) : base(text, image) { }
        public ToolStripActionButton(string text, Image image, EventHandler onClick) : base(text, image, onClick) { }
        public ToolStripActionButton(string text, Image image, EventHandler onClick, string name) : base(text, image, onClick, name) { }

        /// <summary>The tooltip with the shortcut of the action; the shell calls this when it shows the button.</summary>
        public void UpdateToolTip()
        {
            // The action is named in the Tag, or it is the one that switches to the edit mode the button stands for
            string action = Tag as string;
            if(action == null && Tag is CodeImp.DoomBuilder.Editing.EditModeInfo mode) action = mode.SwitchAction.GetFullActionName(mode.Plugin.Assembly);
            if(string.IsNullOrEmpty(action) || General.Actions == null || !General.Actions.Exists(action)) return;
            var a = General.Actions.GetActionByName(action);
            if(baseToolTip == null) baseToolTip = string.IsNullOrWhiteSpace(ToolTipText) ? Text : ToolTipText;
            ToolTipText = baseToolTip + (a.ShortcutKey == 0 ? "" : " (" + CodeImp.DoomBuilder.Actions.Action.GetShortcutKeyDesc(a.ShortcutKey) + ")");
        }
    }

    /// <summary>A number box in a toolbar.</summary>
    public class ToolStripNumericUpDown : ToolStripItem
    {
        private decimal value, minimum = 0, maximum = 100;

        public decimal Minimum { get { return minimum; } set { minimum = value; Value = this.value; } }
        public decimal Maximum { get { return maximum; } set { maximum = value; Value = this.value; } }
        public decimal Increment { get; set; } = 1;
        public decimal Value
        {
            get { return value; }
            set
            {
                decimal v = Math.Max(minimum, Math.Min(maximum, value));
                if(this.value == v) return;
                this.value = v;
                Changed();
                ValueChanged?.Invoke(this, EventArgs.Empty);
            }
        }
        public event EventHandler ValueChanged;
    }

    /// <summary>A check box in a toolbar.</summary>
    public class ToolStripCheckBox : ToolStripItem
    {
        private bool isChecked;

        public bool Checked
        {
            get { return isChecked; }
            set { if(isChecked != value) { isChecked = value; Changed(); CheckedChanged?.Invoke(this, EventArgs.Empty); } }
        }
        public event EventHandler CheckedChanged;
    }
}
