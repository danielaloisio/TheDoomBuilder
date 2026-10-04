using System.Linq;
using CodeImp.DoomBuilder.Controls;
using CodeImp.DoomBuilder.Windows;
using Xunit;

namespace DoomBuilder.Core.Tests;

public class DockerModelTests
{
    private static Docker D(string name) => new Docker(name, name.ToUpperInvariant(), new System.Windows.Forms.Panel());

    [Fact]
    public void The_first_docker_added_is_selected_and_a_repeated_one_is_ignored()
    {
        var model = new DockerModel();
        Assert.Equal("None", model.SelectedTitle);
        var a = D("a");

        model.Add(a, false, "builder");
        model.Add(a, false, "builder");

        Assert.Single(model.Dockers);
        Assert.Same(a, model.Selected);
        Assert.Equal("A", model.SelectedTitle);
        Assert.Equal("builder_a", a.FullName);
    }

    [Fact]
    public void Selecting_remembers_the_previous_one_and_removing_the_selected_falls_back_to_it()
    {
        var model = new DockerModel();
        Docker a = D("a"), b = D("b"), c = D("c");
        model.Add(a, false); model.Add(b, false); model.Add(c, false);

        model.Select(c);
        model.Select(b);                 // previous is now c
        Assert.True(model.Remove(b));

        Assert.Same(c, model.Selected);
        Assert.DoesNotContain(b, model.Dockers);
    }

    [Fact]
    public void Removing_the_only_docker_leaves_nothing_selected_and_unknown_dockers_are_refused()
    {
        var model = new DockerModel();
        var a = D("a");
        model.Add(a, false);

        Assert.False(model.Remove(D("zzz")));
        Assert.False(model.Select(D("zzz")));
        Assert.True(model.Remove(a));
        Assert.Null(model.Selected);
        Assert.Equal("None", model.SelectedTitle);
    }

    [Fact]
    public void Selecting_the_previous_without_history_picks_the_first()
    {
        var model = new DockerModel();
        Docker a = D("a"), b = D("b");
        model.Add(a, false); model.Add(b, false);
        model.Select(b);                 // previous: a
        model.Select(a);                 // previous: b
        model.SelectPrevious();
        Assert.Same(b, model.Selected);

        var fresh = new DockerModel();
        fresh.Add(a, false); fresh.Add(b, false);
        fresh.SelectPrevious();
        Assert.Same(a, fresh.Selected);
    }

    [Fact]
    public void Changes_and_notifications_are_reported_and_tabs_follow_the_saved_order()
    {
        var model = new DockerModel();
        int changes = 0; Docker notified = null;
        model.Changed += () => changes++;
        model.NotifyRequested += d => notified = d;
        Docker a = D("a"), b = D("b"), c = D("c");

        model.Add(a, false, "p"); model.Add(b, true, "p"); model.Add(c, false, "p");
        Assert.Same(b, notified);
        Assert.True(changes >= 3);

        model.SortBy(new[] { "p_c", "p_a" });
        Assert.Equal(new[] { c, a, b }, model.Dockers);
    }
}
