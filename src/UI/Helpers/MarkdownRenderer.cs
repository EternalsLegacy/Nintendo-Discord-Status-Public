using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Layout;
using Avalonia.Media;
using NintendoDiscordStatus.Core.Constants;
using NintendoDiscordStatus.Core.Helpers;
using NintendoDiscordStatus.Core.Models;

namespace NintendoDiscordStatus.UI.Helpers;

public static class MarkdownRenderer
{
    public static StackPanel RenderReleaseNotes(string? MarkdownText)
    {
        StackPanel Container = new StackPanel
        {
            Spacing = 6,
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        List<MarkdownBlock> Blocks = MarkdownParser.Parse(MarkdownText);

        foreach (MarkdownBlock Block in Blocks)
        {
            Container.Children.Add(RenderBlock(Block));
        }

        return Container;
    }

    private static Control RenderBlock(MarkdownBlock Block)
    {
        switch (Block.Kind)
        {
            case MarkdownBlockKind.Heading1:
                return BuildFormattedTextBlock(Block.Spans, 17, FontWeight.Bold, ThemeConstants.TextWhiteHex, new Thickness(0, 8, 0, 3));

            case MarkdownBlockKind.Heading2:
                return BuildFormattedTextBlock(Block.Spans, 15, FontWeight.Bold, ThemeConstants.TextWhiteHex, new Thickness(0, 8, 0, 2));

            case MarkdownBlockKind.Heading3:
                return BuildFormattedTextBlock(Block.Spans, 13.5, FontWeight.SemiBold, ThemeConstants.SubHeadingHex, new Thickness(0, 6, 0, 2));

            case MarkdownBlockKind.Bullet:
                Grid BulletRow = new Grid
                {
                    ColumnDefinitions = new ColumnDefinitions("Auto, *"),
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    Margin = new Thickness(4, 2, 0, 2)
                };

                TextBlock BulletGlyph = new TextBlock
                {
                    Text = "•",
                    FontSize = 13,
                    Foreground = UiStyleHelper.ParseBrush(ThemeConstants.TextGrayHex),
                    Margin = new Thickness(0, 0, 8, 0),
                    VerticalAlignment = VerticalAlignment.Top
                };

                TextBlock BulletContent = BuildFormattedTextBlock(Block.Spans, 12.5, FontWeight.Normal, "#B8BAC2", new Thickness(0), 19);
                Grid.SetColumn(BulletContent, 1);

                BulletRow.Children.Add(BulletGlyph);
                BulletRow.Children.Add(BulletContent);
                return BulletRow;

            default:
                return BuildFormattedTextBlock(Block.Spans, 12.5, FontWeight.Normal, "#B8BAC2", new Thickness(0, 2), 19);
        }
    }

    private static TextBlock BuildFormattedTextBlock(
        List<MarkdownSpan> Spans,
        double FontSize,
        FontWeight BaseWeight,
        string BaseColorHex,
        Thickness Margin,
        double LineHeight = double.NaN)
    {
        TextBlock BlockControl = new TextBlock
        {
            FontSize = FontSize,
            FontWeight = BaseWeight,
            Foreground = UiStyleHelper.ParseBrush(BaseColorHex),
            TextWrapping = TextWrapping.Wrap,
            LineHeight = LineHeight,
            Margin = Margin
        };

        BlockControl.Inlines ??= new InlineCollection();

        foreach (MarkdownSpan SpanItem in Spans)
        {
            Run RunControl = new Run(SpanItem.Text);

            switch (SpanItem.Kind)
            {
                case MarkdownSpanKind.Bold:
                    RunControl.FontWeight = FontWeight.Bold;
                    RunControl.Foreground = UiStyleHelper.ParseBrush(ThemeConstants.TextWhiteHex);
                    break;

                case MarkdownSpanKind.Code:
                    RunControl.FontFamily = new FontFamily("Consolas, Courier New, monospace");
                    RunControl.Foreground = UiStyleHelper.ParseBrush(ThemeConstants.CodeHighlightHex);
                    break;
            }

            BlockControl.Inlines.Add(RunControl);
        }

        return BlockControl;
    }
}
