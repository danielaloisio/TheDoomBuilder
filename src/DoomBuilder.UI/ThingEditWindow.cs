using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using CodeImp.DoomBuilder;
using CodeImp.DoomBuilder.Config;
using CodeImp.DoomBuilder.Geometry;
using CodeImp.DoomBuilder.Map;
using CodeImp.DoomBuilder.Types;
using CodeImp.DoomBuilder.Windows;

namespace DoomBuilder.UI;

/// <summary>
/// UDB's thing dialog (Doom/Hexen and UDMF): type, position, angle, flags, action with its arguments and tag; in UDMF also the custom
/// fields. Position, angle and type show on the map as they are edited. The UDMF fields UDB gives their own widgets (roll, scale,
/// health, alpha, ...) are in the custom fields tab for now, and the actor's user variables are not listed yet.
/// </summary>
public sealed class ThingEditWindow : EditDialogBase
{
    private readonly ThingEditModel model;
    private readonly ThingBrowser type = new ThingBrowser();
    private readonly FlagList flags = new FlagList();
    private readonly TextBlock flagWarning = new TextBlock { Foreground = Brushes.DarkRed, TextWrapping = TextWrapping.Wrap, IsVisible = false };
    private readonly NumberBox posX = new NumberBox { AllowNegative = true, AllowRelative = true, AllowExpressions = true, ButtonStepsUseModifierKeys = true };
    private readonly NumberBox posY = new NumberBox { AllowNegative = true, AllowRelative = true, AllowExpressions = true, ButtonStepsUseModifierKeys = true };
    private readonly NumberBox posZ = new NumberBox { AllowNegative = true, AllowRelative = true, AllowExpressions = true, ButtonStepsUseModifierKeys = true };
    private readonly TextBlock zLabel = new TextBlock { VerticalAlignment = VerticalAlignment.Center, Width = 100 };
    private readonly CheckBox absoluteHeight = new CheckBox { Content = "Absolute height" };
    private readonly NumberBox angle = new NumberBox { AllowNegative = true, AllowRelative = true, AllowExpressions = true, ButtonStep = 45 };
    private readonly AngleDial dial = new AngleDial();
    private readonly CheckBox randomAngle = new CheckBox { Content = "Random angle" };
    private readonly ActionSelector action = new ActionSelector();
    private readonly ArgumentsPanel args = new ArgumentsPanel();
    private readonly TagSelector tag = new TagSelector();
    private readonly FieldsEditor fields = new FieldsEditor();
    private ThingTypeInfo thinginfo;
    private bool preventchanges;
    private bool preventmapchange;

    public event EventHandler ValuesChanged;

    public ThingBrowser TypeBrowser => type;
    public FlagList Flags => flags;
    public NumberBox PosX => posX;
    public NumberBox PosY => posY;
    public NumberBox PosZ => posZ;
    public NumberBox Angle => angle;
    public AngleDial Dial => dial;
    public CheckBox RandomAngle => randomAngle;
    public CheckBox AbsoluteHeight => absoluteHeight;
    public ActionSelector Action => action;
    public ArgumentsPanel Args => args;
    public TagSelector Tag => tag;
    public FieldsEditor Fields => fields;
    public string FlagWarningText => flagWarning.IsVisible ? flagWarning.Text : null;

    internal ThingEditModel Model => model;

    public ThingEditWindow(ICollection<Thing> things) : this(new ThingEditModel(things)) { }

    internal ThingEditWindow(ThingEditModel model) : base(model.UDMF ? "thingeditformudmf" : "thingeditform", model.Title, 780, 660)
    {
        this.model = model;
        model.Arguments = args.Model;
        model.ValuesChanged += (s, e) => ValuesChanged?.Invoke(this, EventArgs.Empty);

        preventchanges = true;

        // ---- fill in (handlers that change the things are wired afterwards or guarded by preventchanges)
        flags.Setup(model.Flags);
        action.GeneralizedCategories = General.Map.Config.GenActionCategories;
        action.AddInfo(General.Map.Config.SortedLinedefActions.ToArray());
        type.UseMultiSelection = model.Things.Count > 1;
        type.TypeChanged += OnTypeChanged;
        action.ValueChanges += (s, e) => OnActionChanged();

        if (model.AllowDecimals) posX.AllowDecimal = posY.AllowDecimal = posZ.AllowDecimal = true;
        dial.DoomAngleClamping = model.DoomAngleClamping;

        if (model.Type.HasValue) type.SelectType(model.Type.Value);
        else type.SelectMultipleTypes(model.Types);
        thinginfo = model.TypeInfo;

        angle.Text = model.Angle;
        absoluteHeight.IsChecked = model.UseAbsoluteHeight;
        zLabel.Text = model.HeightLabel;
        posX.Text = model.X;
        posY.Text = model.Y;
        posZ.Text = model.Z;
        posX.ButtonStep = posY.ButtonStep = posZ.ButtonStep = General.Map.Grid.GridSize;

        action.Value = model.Action;
        action.Empty = model.ActionEmpty;
        if (model.HasTag)
        {
            tag.Setup(UniversalType.ThingTag);
            if (model.TagsDiffer) tag.ClearTag(); else tag.SetTag(model.Tag);
        }

        bool first = true;
        foreach (Thing t in model.Things) { args.SetValue(t, first); first = false; }
        dial.Angle = angle.Input.GetResult(AngleDial.NoAngle);
        preventchanges = false;

        args.UpdateScriptControls();
        UpdateFlagWarnings();

        // ---- live changes
        posX.WhenTextChanged += (s, e) => { if (!preventchanges) model.SetX(posX.Input); };
        posY.WhenTextChanged += (s, e) => { if (!preventchanges) model.SetY(posY.Input); };
        posZ.WhenTextChanged += (s, e) => { if (!preventchanges) model.SetZ(posZ.Input); };
        angle.WhenTextChanged += (s, e) =>
        {
            if (preventchanges) return;
            preventchanges = true;
            dial.Angle = angle.Input.GetResult(AngleDial.NoAngle);
            preventchanges = false;
            if (!preventmapchange) model.SetAngle(angle.Input);
        };
        dial.AngleChanged += (s, e) =>
        {
            if (preventchanges) return;
            angle.Text = dial.Angle.ToString();     // raises the box's change, which applies it
        };
        absoluteHeight.IsCheckedChanged += (s, e) =>
        {
            if (preventchanges) return;
            preventchanges = true;
            zLabel.Text = absoluteHeight.IsChecked == true ? "Z:" : "Height:";
            posZ.Text = model.SetAbsoluteHeight(absoluteHeight.IsChecked == true);
            preventchanges = false;
        };
        randomAngle.IsCheckedChanged += (s, e) => { angle.IsEnabled = dial.IsEnabled = randomAngle.IsChecked != true; };
        flags.FlagChanged += (key, value) => UpdateFlagWarnings();

        // ---- layout
        AddTab("Type", type);
        AddTab("Settings", new ScrollViewer { Content = BuildSettings() });
        if (model.UDMF)
        {
            fields.Setup("thing", showManaged: true);
            fields.ListFixedFields(General.Map.Config.ThingFields);
            bool firstThing = true;
            foreach (Thing t in model.Things) { fields.SetValues(t.Fields, firstThing); firstThing = false; }
            AddTab("Custom", fields);
        }

        type.TypeDoubleClicked += () => OkButton.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
        Opened += (s, e) => type.FocusFilter();
    }

    private Control BuildSettings()
    {
        var panel = new StackPanel { Margin = new Thickness(8), Spacing = 6 };

        var position = new StackPanel();
        position.Children.Add(Section("Position"));
        position.Children.Add(Labeled("X", posX, 100));
        position.Children.Add(Labeled("Y", posY, 100));
        if (model.HasHeight)
        {
            var zrow = new DockPanel { Margin = new Thickness(0, 3) };
            DockPanel.SetDock(zLabel, Dock.Left);
            zrow.Children.Add(zLabel);
            zrow.Children.Add(posZ);
            position.Children.Add(zrow);
            position.Children.Add(absoluteHeight);
        }
        panel.Children.Add(position);

        var rotation = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12 };
        var anglebox = new StackPanel { Width = 200 };
        anglebox.Children.Add(Section("Angle"));
        anglebox.Children.Add(angle);
        anglebox.Children.Add(randomAngle);
        rotation.Children.Add(anglebox);
        rotation.Children.Add(dial);
        panel.Children.Add(rotation);

        var flagsBox = new StackPanel();
        flagsBox.Children.Add(Section("Flags"));
        flagsBox.Children.Add(new Border { Child = flags, Height = 150 });
        flagsBox.Children.Add(flagWarning);
        panel.Children.Add(flagsBox);

        if (model.HasAction)
        {
            var actionBox = new StackPanel();
            actionBox.Children.Add(Section("Action"));
            actionBox.Children.Add(action);
            actionBox.Children.Add(new Border { Child = args, Margin = new Thickness(0, 4) });
            panel.Children.Add(actionBox);
        }

        if (model.HasTag)
        {
            var idBox = new StackPanel();
            idBox.Children.Add(Section("Identification"));
            idBox.Children.Add(Labeled("Tag", tag, 100));
            panel.Children.Add(idBox);
        }
        return panel;
    }

    private static Control Section(string text) => new TextBlock { Text = text, FontWeight = FontWeight.SemiBold, Margin = new Thickness(0, 4, 0, 2) };

    // The arguments follow the action, or the thing type when the action is not known
    private void OnActionChanged()
    {
        int showaction = General.Map.Config.LinedefActions.ContainsKey(action.Value) ? action.Value : 0;
        args.UpdateAction(showaction, preventchanges, action.Empty ? null : thinginfo);
        if (!preventchanges) args.UpdateScriptControls();
    }

    private void OnTypeChanged(ThingTypeInfo value)
    {
        thinginfo = value;
        OnActionChanged();

        int typed = type.GetResult(0);
        if (preventchanges || (!string.IsNullOrEmpty(type.TypeStringValue) && !ThingEditModel.IsValidType(typed))) return;
        model.SetTypes(t => type.GetResult(t));
    }

    private void UpdateFlagWarnings()
    {
        List<string> warnings = model.FlagWarnings();
        flagWarning.IsVisible = warnings.Count > 0;
        flagWarning.Text = string.Join(Environment.NewLine, warnings);
    }

    protected override bool OnAccept()
    {
        string problem = model.Validate(tag.Model, type.TypeStringValue, type.GetResult(0), action.Value);
        if (problem != null)
        {
            General.ShowWarningMessage(problem, System.Windows.Forms.MessageBoxButtons.OK);
            return false;
        }

        int defaulttype = type.GetResult(General.Settings.DefaultThingType);
        int defaultangle = angle.GetResult((int)Angle2D.RadToDeg(General.Settings.DefaultThingAngle) - 90) + 90;
        model.Apply(tag.Model, action.Value, action.Empty, randomAngle.IsChecked == true, defaulttype, defaultangle, model.UDMF ? fields.Model : null);
        return true;
    }

    protected override void OnCancel() => model.Cancel();
}
