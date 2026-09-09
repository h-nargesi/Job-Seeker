namespace Photon.JobSeeker.Tests;

public class GetTextContentTests
{
    [Fact]
    public void Extracts_leaf_text_nodes_joined_with_spaces()
    {
        var content = JobEligibilityHelper.GetTextContent("<p>Hello</p><p>World</p>");

        Assert.Equal(" Hello World", content);
    }

    [Fact]
    public void Collapses_blank_lines_inside_a_text_node()
    {
        var content = JobEligibilityHelper.GetTextContent("<pre>Line1\n\n\n   Line2</pre>");

        Assert.Equal(" Line1\n\nLine2", content);
    }

    [Fact]
    public void Excludes_children_of_script_style_and_head()
    {
        var html = "<html><head><style>.x{color:red}</style><script>var a = 1;</script>headnoise</head>" +
                   "<body><p>Visible</p></body></html>";

        var content = JobEligibilityHelper.GetTextContent(html);

        Assert.Equal(" Visible", content);
    }

    [Fact]
    public void Includes_nested_noscript_text_today_3_15_pinned()
    {
        var html = "<body><noscript><p>Enable JavaScript</p></noscript><p>Real content</p></body>";

        var content = JobEligibilityHelper.GetTextContent(html);

        Assert.Equal(" Enable JavaScript Real content", content);
    }
}
