using System;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using CodeImp.DoomBuilder;

namespace TheDoomBuilder.Application;

public partial class App : Avalonia.Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
        TheDoomBuilder.Application.Shell.TreeTranslator.Install();
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