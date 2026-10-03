using Avalonia.Controls;

namespace DoomBuilder.App;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        Viewport.InfoChanged += () =>
            InfoText.Text = Viewport.Error is { } err ? $"ERRO GL: {err}" : Viewport.GlInfo;
    }
}
