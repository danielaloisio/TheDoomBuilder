using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using CodeImp.DoomBuilder;
using CodeImp.DoomBuilder.Config;
using CodeImp.DoomBuilder.Map;
using CodeImp.DoomBuilder.Types;
using CodeImp.DoomBuilder.Windows;

namespace DoomBuilder.UI;

/// <summary>
/// UDB's linedef dialog (Doom/Hexen and UDMF): flags, activation, action with arguments and tag, and the front and back sides with
/// their sector, textures and offsets. Flags, textures and offsets show on the map as they are edited. In UDMF the sidedef flags and the
/// custom fields of the line and of each side have their own tabs; the fields UDB gives dedicated widgets (scales, light, alpha,
/// per-part offsets...) are listed there too.
/// </summary>
public sealed class LinedefEditWindow : EditDialogBase
{
    /// <summary>The controls of one side.</summary>
    public sealed class SideControls
    {
        public readonly CheckBox Exists = new CheckBox { IsThreeState = true, Content = "Side" };
        public readonly NumberBox Sector = new NumberBox { MinWidth = 70 };
        public readonly TextureSelector High = new TextureSelector { Width = 100, Height = 130 };
        public readonly TextureSelector Middle = new TextureSelector { Width = 100, Height = 130 };
        public readonly TextureSelector Low = new TextureSelector { Width = 100, Height = 130 };
        public readonly NumberBox OffsetX = new NumberBox { AllowNegative = true, AllowRelative = true, AllowExpressions = true, ButtonStepsUseModifierKeys = true };
        public readonly NumberBox OffsetY = new NumberBox { AllowNegative = true, AllowRelative = true, AllowExpressions = true, ButtonStepsUseModifierKeys = true };
        public readonly FlagList Flags = new FlagList();
        public readonly FieldsEditor Fields = new FieldsEditor();
        public readonly StackPanel Group = new StackPanel();
        public TextureSelector Part(SidePart part) => part == SidePart.High ? High : part == SidePart.Middle ? Middle : Low;
    }

    private readonly LinedefEditModel model;
    private readonly FlagList flags = new FlagList();
    private readonly ComboBox activation = new ComboBox { HorizontalAlignment = HorizontalAlignment.Stretch };
    private readonly ActionSelector action = new ActionSelector();
    private readonly ArgumentsPanel args = new ArgumentsPanel();
    private readonly TagSelector tag = new TagSelector();
    private readonly TextBox moreTags = new TextBox { Watermark = "e.g. 2, 3" };
    private readonly FieldsEditor fields = new FieldsEditor();
    private readonly SideControls front = new SideControls();
    private readonly SideControls back = new SideControls();
    private bool preventchanges;

    public event EventHandler ValuesChanged;

    public FlagList Flags => flags;
    public ComboBox Activation => activation;
    public ActionSelector Action => action;
    public ArgumentsPanel Args => args;
    public TagSelector Tag => tag;
    public FieldsEditor Fields => fields;
    public SideControls Front => front;
    public SideControls Back => back;

    internal LinedefEditModel Model => model;

    public LinedefEditWindow(ICollection<Linedef> lines, bool selectfront = false, bool selectback = false)
        : this(new LinedefEditModel(lines), selectfront, selectback) { }

    internal LinedefEditWindow(LinedefEditModel model, bool selectfront, bool selectback)
        : base(model.UDMF ? "linedefeditformudmf" : "linedefeditform", model.Title, 720, 680)
    {
        this.model = model;
        model.Arguments = args.Model;
        model.ValuesChanged += (s, e) => ValuesChanged?.Invoke(this, EventArgs.Empty);

        preventchanges = true;
        flags.Setup(model.Flags);
        action.GeneralizedCategories = General.Map.Config.GenActionCategories;
        action.AddInfo(General.Map.Config.SortedLinedefActions.ToArray());
        action.ValueChanges += (s, e) => OnActionChanged();

        activation.ItemsSource = model.Activations.Select(a => a.Title).ToList();
        activation.SelectedIndex = model.Activation == null ? -1 : model.Activations.IndexOf(model.Activation);

        // The action control must be set up even when its value is the default one
        if (action.Value != model.Action) action.Value = model.Action; else OnActionChanged();
        action.Empty = model.ActionEmpty;
        if (model.HasTag)
        {
            tag.Setup(UniversalType.LinedefTag);
            if (model.TagsDiffer) tag.ClearTag(); else tag.SetTag(model.Tag);
        }
        moreTags.Text = model.MoreTags;

        bool first = true;
        foreach (Linedef l in model.Lines) { args.SetValue(l, first); first = false; }

        FillSide(front, model.Front);
        FillSide(back, model.Back);
        preventchanges = false;
        args.UpdateScriptControls();

        // ---- live changes
        flags.FlagChanged += (key, value) => { if (!preventchanges) model.SetFlag(key, value); };
        WireSide(front, true);
        WireSide(back, false);

        // ---- layout
        AddTab("Linedef", new ScrollViewer { Content = BuildGeneral() });
        TabItem fronttab = AddTab("Front", new ScrollViewer { Content = BuildSide(front, model.Front, "Front side") });
        TabItem backtab = AddTab("Back", new ScrollViewer { Content = BuildSide(back, model.Back, "Back side") });
        if (model.UDMF)
        {
            fields.Setup("linedef", showManaged: true);
            fields.ListFixedFields(General.Map.Config.LinedefFields);
            bool firstline = true;
            foreach (Linedef l in model.Lines) { fields.SetValues(l.Fields, firstline); firstline = false; }
            AddTab("Custom", fields);

            AddSideFields(front, true, "Front custom");
            AddSideFields(back, false, "Back custom");
        }

        Opened += (s, e) =>
        {
            if (selectfront) Tabs.SelectedItem = fronttab;
            else if (selectback) Tabs.SelectedItem = backtab;
        };
    }

    #region ================== Layout

    private static Control Section(string text) => new TextBlock { Text = text, FontWeight = FontWeight.SemiBold, Margin = new Thickness(0, 6, 0, 2) };

    private Control BuildGeneral()
    {
        var panel = new StackPanel { Margin = new Thickness(8), Spacing = 4 };
        panel.Children.Add(Section("Flags"));
        panel.Children.Add(new Border { Child = flags, Height = 160 });

        panel.Children.Add(Section("Action"));
        if (model.HasPresetActivations) panel.Children.Add(Labeled("Activation", activation, 100));
        panel.Children.Add(action);
        if (model.HasActionArgs) panel.Children.Add(new Border { Child = args, Margin = new Thickness(0, 4) });

        if (model.HasTag)
        {
            panel.Children.Add(Section("Identification"));
            panel.Children.Add(Labeled("Tag", tag, 100));
            if (model.UDMF) panel.Children.Add(Labeled("More tags", moreTags, 100));
        }
        return panel;
    }

    private Control BuildSide(SideControls side, SideSettings settings, string title)
    {
        var panel = new StackPanel { Margin = new Thickness(8), Spacing = 6 };
        side.Exists.Content = title;
        panel.Children.Add(side.Exists);

        side.Group.Spacing = 6;
        side.Group.Children.Add(Labeled("Sector", side.Sector, 100));

        var textures = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 16 };
        foreach (var (caption, selector) in new[] { ("Upper", side.High), ("Middle", side.Middle), ("Lower", side.Low) })
        {
            var one = new StackPanel { Spacing = 3 };
            one.Children.Add(new TextBlock { Text = caption });
            one.Children.Add(selector);
            textures.Children.Add(one);
        }
        side.Group.Children.Add(textures);
        side.Group.Children.Add(Labeled("Offset X", side.OffsetX, 100));
        side.Group.Children.Add(Labeled("Offset Y", side.OffsetY, 100));
        panel.Children.Add(side.Group);
        return panel;
    }

    private void AddSideFields(SideControls side, bool isfront, string header)
    {
        SideSettings settings = isfront ? model.Front : model.Back;
        var panel = new DockPanel { Margin = new Thickness(8) };
        side.Flags.Setup(settings.Flags);
        side.Flags.FlagChanged += (key, value) => model.SetSideFlag(isfront, key, value);
        var flagBox = new Border { Child = side.Flags, Height = 110 };
        DockPanel.SetDock(flagBox, Dock.Top);
        panel.Children.Add(flagBox);

        side.Fields.Setup("sidedef", showManaged: true);
        side.Fields.ListFixedFields(General.Map.Config.SidedefFields);
        bool first = true;
        foreach (Linedef l in model.Lines)
        {
            Sidedef s = isfront ? l.Front : l.Back;
            if (s == null) continue;
            side.Fields.SetValues(s.Fields, first);
            first = false;
        }
        panel.Children.Add(side.Fields);
        AddTab(header, panel);
    }

    #endregion

    #region ================== Sides

    private void FillSide(SideControls side, SideSettings s)
    {
        side.Exists.IsChecked = s.Exists;
        side.Group.IsEnabled = s.Exists != false;

        foreach (SidePart part in new[] { SidePart.High, SidePart.Middle, SidePart.Low })
        {
            TextureSelector selector = side.Part(part);
            selector.Initialize();
            selector.Required = s.Required[(int)part];
            selector.MultipleTextures = s.MultipleTextures[(int)part];
            selector.TextureName = s.Textures[(int)part];
        }
        side.Sector.Text = s.Sector;
        side.OffsetX.Text = s.OffsetX;
        side.OffsetY.Text = s.OffsetY;
    }

    private void WireSide(SideControls side, bool isfront)
    {
        foreach (SidePart part in new[] { SidePart.High, SidePart.Middle, SidePart.Low })
        {
            TextureSelector selector = side.Part(part);
            selector.Refresh();
            selector.ValueChanged += (s, e) =>
            {
                if (preventchanges) return;
                model.SetTexture(isfront, part, selector.TextureName, current => selector.GetResult(current));
            };
        }
        side.OffsetX.WhenTextChanged += (s, e) => { if (!preventchanges) model.SetOffsets(isfront, side.OffsetX.Input, side.OffsetY.Input); };
        side.OffsetY.WhenTextChanged += (s, e) => { if (!preventchanges) model.SetOffsets(isfront, side.OffsetX.Input, side.OffsetY.Input); };

        bool? mixed = (isfront ? model.Front : model.Back).Exists;
        side.Exists.IsCheckedChanged += (s, e) =>
        {
            // When some lines have the side and some do not, the box only says so
            if (mixed == null && side.Exists.IsChecked != null) { side.Exists.IsChecked = null; return; }
            if (mixed != null && side.Exists.IsChecked == null) { side.Exists.IsChecked = true; return; }   // not mixed: only on or off
            side.Group.IsEnabled = side.Exists.IsChecked != false;
        };
    }

    #endregion

    private void OnActionChanged()
    {
        int showaction = General.Map.Config.LinedefActions.ContainsKey(action.Value) ? action.Value : 0;
        args.UpdateAction(showaction, preventchanges);
        if (!preventchanges) args.UpdateScriptControls();
    }

    protected override bool OnAccept()
    {
        string problem = model.Validate(tag.Model, action.Value);
        if (problem != null)
        {
            General.ShowWarningMessage(problem, System.Windows.Forms.MessageBoxButtons.OK);
            return false;
        }

        LinedefActivateInfo chosen = activation.SelectedIndex > -1 && activation.SelectedIndex < model.Activations.Count ? model.Activations[activation.SelectedIndex] : null;
        model.Apply(tag.Model, chosen, action.Value, action.Empty, model.UDMF ? moreTags.Text : null,
            front.Exists.IsChecked, front.Sector.Input, back.Exists.IsChecked, back.Sector.Input,
            model.UDMF ? fields.Model : null, model.UDMF ? front.Fields.Model : null, model.UDMF ? back.Fields.Model : null);
        return true;
    }

    protected override void OnCancel()
    {
        foreach (SideControls s in new[] { front, back })
        {
            s.High.StopUpdate();
            s.Middle.StopUpdate();
            s.Low.StopUpdate();
        }
        model.Cancel();
    }
}
