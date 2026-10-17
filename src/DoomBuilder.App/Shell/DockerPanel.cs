using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using CodeImp.DoomBuilder.Controls;
using CodeImp.DoomBuilder.Windows;

namespace DoomBuilder.App.Shell;

/// <summary>
/// The side panel with a tab for each docker (UDB's DockersControl). The state lives in <see cref="DockerModel"/>; this shows it:
/// one tab per docker, the selected one up front, the tab strip on the outer side.
/// </summary>
public sealed class DockerPanel : UserControl
{
    private readonly DockerModel model;
    private readonly TabControl tabs = new TabControl { FontSize = 12 };
    private bool syncing;

    public DockerPanel(DockerModel model)
    {
        this.model = model;
        Content = tabs;
        tabs.SelectionChanged += (s, e) =>
        {
            if (syncing) return;
            if (tabs.SelectedItem is TabItem { Tag: Docker docker }) model.Select(docker);
        };
        model.Changed += OnModelChanged;
        Rebuild();
    }

    /// <summary>The tab strip sits on the side the panel is not docked to.</summary>
    public void SetSide(bool right) => tabs.TabStripPlacement = right ? Dock.Right : Dock.Left;

    /// <summary>The tabs, in order (for tests).</summary>
    public IReadOnlyList<TabItem> Tabs => tabs.Items.OfType<TabItem>().ToList();

    public TabItem SelectedTab => tabs.SelectedItem as TabItem;

    private void OnModelChanged()
    {
        if (Dispatcher.UIThread.CheckAccess()) Rebuild();
        else Dispatcher.UIThread.Post(Rebuild);
    }

    private void Rebuild()
    {
        syncing = true;
        try
        {
            // Keep the tabs that are still there (so a panel does not lose its state), add and drop the others
            var existing = tabs.Items.OfType<TabItem>().ToDictionary(t => (Docker)t.Tag);
            var wanted = model.Dockers.ToList();

            foreach (var gone in existing.Keys.Where(d => !wanted.Contains(d)).ToList())
            {
                ((TabItem)existing[gone]).Content = null;
                tabs.Items.Remove(existing[gone]);
                existing.Remove(gone);
            }

            for (int i = 0; i < wanted.Count; i++)
            {
                if (!existing.TryGetValue(wanted[i], out TabItem tab))
                {
                    tab = new TabItem { Header = CodeImp.DoomBuilder.Localization.Localizer.T(wanted[i].Title), Tag = wanted[i], Content = ContentFor(wanted[i]), FontSize = 12, Padding = new Thickness(8, 4) };
                    tabs.Items.Insert(Math.Min(i, tabs.Items.Count), tab);
                }
                else if (tabs.Items.IndexOf(tab) != i)
                {
                    tabs.Items.Remove(tab);
                    tabs.Items.Insert(i, tab);
                }
            }

            tabs.SelectedItem = tabs.Items.OfType<TabItem>().FirstOrDefault(t => t.Tag == model.Selected);
        }
        finally { syncing = false; }
    }

    private static Control ContentFor(Docker docker)
        => AvaloniaDocker.ContentOf(docker) ?? new TextBlock { Text = docker.Title, Margin = new Thickness(10), Opacity = 0.6 };
}
