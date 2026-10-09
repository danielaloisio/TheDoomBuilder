using System.IO;
using CodeImp.DoomBuilder;
using Xunit;

namespace DoomBuilder.Core.Tests;

/// <summary>The reference manual is HTML pages in the Help folder (the CHM of UDB was converted by tools/chm_to_html.py).</summary>
[Collection("General static state")]
public class HelpTests : System.IDisposable
{
    public HelpTests() { General.InitializeHeadless(TestAssets.DefaultSettings); }
    public void Dispose() { General.ShutdownHeadless(); }

    [Fact]
    public void A_help_page_is_found_by_name_and_keeps_its_anchor_separate()
    {
        string page = General.FindHelpPage("w_scripteditor.html#find");
        Assert.NotNull(page);
        Assert.Equal("w_scripteditor.html", Path.GetFileName(page));
        Assert.True(File.Exists(page));
    }

    [Fact]
    public void An_unknown_or_empty_page_opens_the_table_of_contents()
    {
        Assert.Equal("index.html", Path.GetFileName(General.FindHelpPage("no_such_page.html")));
        Assert.Equal("index.html", Path.GetFileName(General.FindHelpPage(null)));
    }

    [Fact]
    public void Every_page_in_the_table_of_contents_exists()
    {
        string index = General.FindHelpPage(null);
        string text = File.ReadAllText(index);
        foreach(System.Text.RegularExpressions.Match m in System.Text.RegularExpressions.Regex.Matches(text, "href=\"([^\"#]+)"))
            Assert.True(File.Exists(Path.Combine(Path.GetDirectoryName(index), m.Groups[1].Value)), m.Groups[1].Value);
    }
}
