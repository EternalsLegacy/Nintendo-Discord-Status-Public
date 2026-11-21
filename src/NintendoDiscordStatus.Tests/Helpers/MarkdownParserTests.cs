using System.Collections.Generic;
using NintendoDiscordStatus.Core.Helpers;
using NintendoDiscordStatus.Core.Models;
using Xunit;

namespace NintendoDiscordStatus.Tests.Helpers;

public class MarkdownParserTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   \r\n  ")]
    public void Parse_NullOrWhitespace_ReturnsEmptyList(string? MarkdownText)
    {
        List<MarkdownBlock> Blocks = MarkdownParser.Parse(MarkdownText);

        Assert.Empty(Blocks);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void ParseInlineSpans_NullOrEmpty_ReturnsEmptyList(string? LineText)
    {
        List<MarkdownSpan> Spans = MarkdownParser.ParseInlineSpans(LineText);

        Assert.Empty(Spans);
    }

    [Fact]
    public void Parse_HeadingsBulletsBoldAndBackticks_ParsesAllCorrectly()
    {
        string Markdown =
            "# Release Title\r\n" +
            "\r\n" +
            "## What's New\r\n" +
            "### Bugfixes\r\n" +
            "- Fixed **Auto Update** and `MainWindow.cs` crash\r\n" +
            "* Added `SettingsService` singleton\r\n" +
            "**Important:** Edit `game_lib.json` cleanly without breaking.";

        List<MarkdownBlock> Blocks = MarkdownParser.Parse(Markdown);

        Assert.Equal(6, Blocks.Count);

        Assert.Equal(MarkdownBlockKind.Heading1, Blocks[0].Kind);
        Assert.Single(Blocks[0].Spans);
        Assert.Equal("Release Title", Blocks[0].Spans[0].Text);

        Assert.Equal(MarkdownBlockKind.Heading2, Blocks[1].Kind);
        Assert.Equal("What's New", Blocks[1].Spans[0].Text);

        Assert.Equal(MarkdownBlockKind.Heading3, Blocks[2].Kind);
        Assert.Equal("Bugfixes", Blocks[2].Spans[0].Text);

        Assert.Equal(MarkdownBlockKind.Bullet, Blocks[3].Kind);
        Assert.Collection(
            Blocks[3].Spans,
            Span => AssertSpan(Span, MarkdownSpanKind.Plain, "Fixed "),
            Span => AssertSpan(Span, MarkdownSpanKind.Bold, "Auto Update"),
            Span => AssertSpan(Span, MarkdownSpanKind.Plain, " and "),
            Span => AssertSpan(Span, MarkdownSpanKind.Code, "MainWindow.cs"),
            Span => AssertSpan(Span, MarkdownSpanKind.Plain, " crash"));

        Assert.Equal(MarkdownBlockKind.Bullet, Blocks[4].Kind);
        Assert.Collection(
            Blocks[4].Spans,
            Span => AssertSpan(Span, MarkdownSpanKind.Plain, "Added "),
            Span => AssertSpan(Span, MarkdownSpanKind.Code, "SettingsService"),
            Span => AssertSpan(Span, MarkdownSpanKind.Plain, " singleton"));

        Assert.Equal(MarkdownBlockKind.Paragraph, Blocks[5].Kind);
        Assert.Collection(
            Blocks[5].Spans,
            Span => AssertSpan(Span, MarkdownSpanKind.Bold, "Important:"),
            Span => AssertSpan(Span, MarkdownSpanKind.Plain, " Edit "),
            Span => AssertSpan(Span, MarkdownSpanKind.Code, "game_lib.json"),
            Span => AssertSpan(Span, MarkdownSpanKind.Plain, " cleanly without breaking."));
    }

    private static void AssertSpan(MarkdownSpan Span, MarkdownSpanKind ExpectedKind, string ExpectedText)
    {
        Assert.Equal(ExpectedKind, Span.Kind);
        Assert.Equal(ExpectedText, Span.Text);
    }
}
