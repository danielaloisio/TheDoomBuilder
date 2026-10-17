using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using CodeImp.DoomBuilder;
using CodeImp.DoomBuilder.Map;
using CodeImp.DoomBuilder.Types;
using CodeImp.DoomBuilder.Windows;

namespace DoomBuilder.UI;

/// <summary>
/// UDB's sector dialog (Doom/Hexen and UDMF): heights, flats, brightness, effect and tag; in UDMF also the flags, more tags and the
/// custom fields. Changes show on the map as they are made. The UDMF fields that UDB gives their own widgets (offsets, light, colors,
/// damage...) are in the custom fields tab for now.
/// </summary>
public sealed class SectorEditWindow : EditDialogBase
{
    private readonly SectorEditModel model;
    private readonly NumberBox floorHeight = new NumberBox { AllowNegative = true, AllowRelative = true, AllowExpressions = true, ButtonStepsUseModifierKeys = true, ButtonStep = 8 };
    private readonly NumberBox ceilingHeight = new NumberBox { AllowNegative = true, AllowRelative = true, AllowExpressions = true, ButtonStepsUseModifierKeys = true, ButtonStep = 8 };
    private readonly NumberBox heightOffset = new NumberBox { AllowNegative = true, AllowRelative = true, AllowExpressions = true, ButtonStep = 8 };
    private readonly TextBlock sectorHeight = new TextBlock { VerticalAlignment = VerticalAlignment.Center };
    private readonly NumberBox brightness = new NumberBox { AllowNegative = true, AllowRelative = true, AllowExpressions = true, ButtonStepsUseModifierKeys = true };
    private readonly FlatSelector floorTexture = new FlatSelector { Width = 120, Height = 150 };
    private readonly FlatSelector ceilingTexture = new FlatSelector { Width = 120, Height = 150 };
    private readonly ActionSelector effect = new ActionSelector();
    private readonly TagSelector tag = new TagSelector();
    private readonly TextBox moreTags = new TextBox { Watermark = "e.g. 2, 3" };
    private readonly FlagList flags = new FlagList();
    private readonly FieldsEditor fields = new FieldsEditor();

    public event EventHandler ValuesChanged;

    public NumberBox FloorHeightBox => floorHeight;
    public NumberBox CeilingHeightBox => ceilingHeight;
    public NumberBox HeightOffsetBox => heightOffset;
    public NumberBox BrightnessBox => brightness;
    public FlatSelector FloorTexture => floorTexture;
    public FlatSelector CeilingTexture => ceilingTexture;
    public ActionSelector Effect => effect;
    public TagSelector Tag => tag;
    public FlagList Flags => flags;
    public FieldsEditor Fields => fields;
    public string SectorHeightText => sectorHeight.Text;

    internal SectorEditModel Model => model;

    public SectorEditWindow(ICollection<Sector> sectors) : this(new SectorEditModel(sectors)) { }

    internal SectorEditWindow(SectorEditModel model) : base(model.UDMF ? "sectoreditformudmf" : "sectoreditform", model.Title, 560, model.UDMF ? 560 : 520)
    {
        this.model = model;
        model.ValuesChanged += (s, e) => ValuesChanged?.Invoke(this, EventArgs.Empty);

        // ---- fill in (the handlers are wired afterwards: showing the values is not an edit)
        effect.GeneralizedOptions = General.Map.Config.GenEffectOptions;
        effect.AddInfo(General.Map.Config.SortedSectorEffects.ToArray());
        floorTexture.Initialize();
        ceilingTexture.Initialize();
        brightness.StepValues = General.Map.Config.BrightnessLevels;

        heightOffset.Text = "0";
        effect.Value = model.Effect;
        effect.Empty = model.EffectEmpty;
        brightness.Text = model.Brightness;
        floorHeight.Text = model.FloorHeight;
        ceilingHeight.Text = model.CeilingHeight;
        floorTexture.MultipleTextures = model.FloorTexturesDiffer;
        ceilingTexture.MultipleTextures = model.CeilingTexturesDiffer;
        floorTexture.TextureName = model.FloorTexture;
        ceilingTexture.TextureName = model.CeilingTexture;
        tag.Setup(UniversalType.SectorTag);
        if (model.TagsDiffer) tag.ClearTag(); else tag.SetTag(model.Tag);
        moreTags.Text = model.MoreTags;
        UpdateSectorHeight();

        floorHeight.WhenTextChanged += (s, e) => { model.SetFloorHeight(floorHeight.Input, heightOffset.Input); UpdateSectorHeight(); };
        ceilingHeight.WhenTextChanged += (s, e) => { model.SetCeilingHeight(ceilingHeight.Input, heightOffset.Input); UpdateSectorHeight(); };
        heightOffset.WhenTextChanged += (s, e) => { model.SetHeightOffset(floorHeight.Input, ceilingHeight.Input, heightOffset.Input); UpdateSectorHeight(); };
        brightness.WhenTextChanged += (s, e) => model.SetBrightness(brightness.Input);
        floorTexture.ValueChanged += (s, e) => model.SetFloorTexture(floorTexture.GetResult(null));
        ceilingTexture.ValueChanged += (s, e) => model.SetCeilingTexture(ceilingTexture.GetResult(null));

        // ---- layout
        var heights = new StackPanel { Margin = new Thickness(8) };
        heights.Children.Add(Section("Heights"));
        heights.Children.Add(Labeled("Ceiling height", ceilingHeight, 120));
        heights.Children.Add(Labeled("Floor height", floorHeight, 120));
        heights.Children.Add(Labeled("Height offset", heightOffset, 120));
        heights.Children.Add(Labeled("Sector height", sectorHeight, 120));

        var textures = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 24, Margin = new Thickness(0, 8, 0, 0) };
        textures.Children.Add(Captioned("Ceiling", ceilingTexture));
        textures.Children.Add(Captioned("Floor", floorTexture));

        var props = new StackPanel { Margin = new Thickness(8, 0) };
        props.Children.Add(Section("Properties"));
        props.Children.Add(Labeled("Brightness", brightness, 120));
        props.Children.Add(Labeled("Effect", effect, 120));
        props.Children.Add(Labeled("Tag", tag, 120));
        if (model.UDMF) props.Children.Add(Labeled("More tags", moreTags, 120));

        var general = new StackPanel();
        general.Children.Add(heights);
        general.Children.Add(new Border { Child = textures, Margin = new Thickness(8, 0) });
        general.Children.Add(new Border { Child = props, Margin = new Thickness(0, 8, 0, 0) });
        AddTab("General", new ScrollViewer { Content = general });

        if (model.UDMF)
        {
            flags.Setup(model.Flags);
            flags.FlagChanged += (key, value) => model.SetFlag(key, value);
            AddTab("Flags", flags);

            fields.Setup("sector", showManaged: true);
            fields.ListFixedFields(General.Map.Config.SectorFields);
            bool first = true;
            foreach (Sector s in model.Sectors) { fields.SetValues(s.Fields, first); first = false; }
            AddTab("Custom", fields);
        }
    }

    private static Control Section(string text) => new TextBlock { Text = text, FontWeight = Avalonia.Media.FontWeight.SemiBold, Margin = new Thickness(0, 0, 0, 4) };

    private static Control Captioned(string caption, Control control)
    {
        var stack = new StackPanel { Spacing = 4 };
        stack.Children.Add(new TextBlock { Text = caption });
        stack.Children.Add(control);
        return stack;
    }

    // The height of the sectors, shown only when they are all the same
    private void UpdateSectorHeight()
    {
        string text = model.SectorHeight(floorHeight.Input, ceilingHeight.Input);
        sectorHeight.Text = text ?? "";
        if (sectorHeight.Parent is Control row && row.Parent is Control host) row.IsVisible = text != null;
    }

    protected override bool OnAccept()
    {
        string problem = model.Validate(tag.Model, effect.Value, effect.Empty);
        if (problem != null)
        {
            General.ShowWarningMessage(problem, System.Windows.Forms.MessageBoxButtons.OK);
            return false;
        }
        model.Apply(tag.Model, effect.Value, effect.Empty, model.UDMF ? moreTags.Text : null, model.UDMF ? fields.Model : null);
        return true;
    }

    protected override void OnCancel()
    {
        floorTexture.StopUpdate();
        ceilingTexture.StopUpdate();
        model.Cancel();
    }
}
