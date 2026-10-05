using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using CodeImp.DoomBuilder;
using CodeImp.DoomBuilder.Map;
using CodeImp.DoomBuilder.Windows;

namespace DoomBuilder.UI;

/// <summary>UDB's "Edit Vertex" dialog: position, the height offsets (UDMF) and the custom fields. Changes show on the map as they are made.</summary>
public sealed class VertexEditWindow : EditDialogBase
{
    private readonly VertexEditModel model;
    private readonly NumberBox x = new NumberBox { AllowNegative = true, AllowRelative = true, AllowExpressions = true, ButtonStepsUseModifierKeys = true };
    private readonly NumberBox y = new NumberBox { AllowNegative = true, AllowRelative = true, AllowExpressions = true, ButtonStepsUseModifierKeys = true };
    private readonly NumberBox zCeiling = new NumberBox { AllowNegative = true, AllowRelative = true, AllowExpressions = true };
    private readonly NumberBox zFloor = new NumberBox { AllowNegative = true, AllowRelative = true, AllowExpressions = true };
    private readonly FieldsEditor fields = new FieldsEditor();

    /// <summary>Raised when the vertices changed (the map must be updated and redrawn).</summary>
    public event EventHandler ValuesChanged;

    public NumberBox XBox => x;
    public NumberBox YBox => y;
    public NumberBox ZCeilingBox => zCeiling;
    public NumberBox ZFloorBox => zFloor;
    public FieldsEditor Fields => fields;

    internal VertexEditModel Model => model;

    public VertexEditWindow(ICollection<Vertex> vertices, bool allowPositionChange)
        : this(new VertexEditModel(vertices, allowPositionChange)) { }

    internal VertexEditWindow(VertexEditModel model) : base("vertexeditform", model.Title, 460, 380)
    {
        this.model = model;
        model.ValuesChanged += (s, e) => ValuesChanged?.Invoke(this, EventArgs.Empty);

        if (model.AllowDecimals)
        {
            x.AllowDecimal = y.AllowDecimal = zCeiling.AllowDecimal = zFloor.AllowDecimal = true;
            x.ButtonStepSmall = y.ButtonStepSmall = 0.1f;
        }

        x.Text = model.X;
        y.Text = model.Y;
        x.IsEnabled = y.IsEnabled = model.AllowPositionChange;
        zCeiling.Text = model.ZCeiling;
        zFloor.Text = model.ZFloor;

        // Wired after the boxes are filled in: showing the values is not an edit
        x.WhenTextChanged += (s, e) => model.SetX(x.Input);
        y.WhenTextChanged += (s, e) => model.SetY(y.Input);
        zCeiling.WhenTextChanged += (s, e) => model.SetZCeiling(zCeiling.Input);
        zFloor.WhenTextChanged += (s, e) => model.SetZFloor(zFloor.Input);

        var general = new StackPanel { Margin = new Thickness(8) };
        general.Children.Add(Labeled("Position X", x));
        general.Children.Add(Labeled("Position Y", y));

        if (model.HasCustomFields)
        {
            var clearC = new Button { Content = "Unused" };
            clearC.Click += (s, e) => zCeiling.Text = VertexEditModel.ClearValue;
            var clearF = new Button { Content = "Unused" };
            clearF.Click += (s, e) => zFloor.Text = VertexEditModel.ClearValue;
            var heights = new StackPanel { IsEnabled = model.HeightSupported, Margin = new Thickness(0, 12, 0, 0) };
            heights.Children.Add(new TextBlock { Text = "Height offsets", FontWeight = Avalonia.Media.FontWeight.SemiBold });
            heights.Children.Add(Labeled("Ceiling", WithButton(zCeiling, clearC)));
            heights.Children.Add(Labeled("Floor", WithButton(zFloor, clearF)));
            general.Children.Add(heights);
        }
        AddTab("General", general);

        if (model.HasCustomFields)
        {
            fields.Setup("vertex");
            fields.ListFixedFields(General.Map.Config.VertexFields);
            bool first = true;
            foreach (Vertex v in model.Vertices) { fields.SetValues(v.Fields, first); first = false; }
            AddTab("Custom", fields);
        }
    }

    private static Control WithButton(Control box, Button button)
    {
        var row = new DockPanel();
        DockPanel.SetDock(button, Dock.Right);
        row.Children.Add(button);
        row.Children.Add(box);
        return row;
    }

    protected override bool OnAccept()
    {
        model.Apply(fields.Model);
        return true;
    }

    protected override void OnCancel() => model.Cancel();
}
