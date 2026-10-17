using System;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using CodeImp.DoomBuilder;

namespace DoomBuilder.App;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
        DoomBuilder.App.Shell.TreeTranslator.Install();
    }

    // The native macOS app menu's "About" item (App.axaml) has no built-in command; General.Dialogs is only ready
    // once the host has started (Program.cs), which is always true by the time a person can click the menu.
    private void OnAboutMenuItemClick(object sender, EventArgs e) => General.Dialogs?.ShowAbout();

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new MainWindow();
        }

        base.OnFrameworkInitializationCompleted();
    }
}