using System.Collections.Generic;
using System.Text.RegularExpressions;
using NintendoDiscordStatus.Core.Models;

namespace NintendoDiscordStatus.Core.Helpers;

public static class MarkdownParser
{
    private static readonly Regex InlineTokenRegex = new Regex(
        @"(\*\*[^*]+?\*\*|`[^`]+?`)",
        RegexOptions.Compiled);

    public static List<MarkdownBlock> Parse(string? MarkdownText)
    {
        List<MarkdownBlock> Blocks = new List<MarkdownBlock>();

        if (string.IsNullOrWhiteSpace(MarkdownText))
        {
            return Blocks;
        }

        string NormalizedText = MarkdownText.Replace("\r\n", "\n");
        string[] Lines = NormalizedText.Split('\n');

        foreach (string RawLine in Lines)
        {
            string TrimmedLine = RawLine.Trim();
            if (string.IsNullOrWhiteSpace(TrimmedLine))
            {
                continue;
            }

            (MarkdownBlockKind Kind, string ContentText) = ClassifyLine(TrimmedLine);

            Blocks.Add(new MarkdownBlock
            {
                Kind = Kind,
                Spans = ParseInlineSpans(ContentText)
            });
        }

        return Blocks;
    }

    public static List<MarkdownSpan> ParseInlineSpans(string? LineText)
    {
        List<MarkdownSpan> Spans = new List<MarkdownSpan>();

        if (string.IsNullOrEmpty(LineText))
        {
            return Spans;
        }

        MatchCollection Matches = InlineTokenRegex.Matches(LineText);
        int CurrentIndex = 0;

        foreach (Match MatchItem in Matches)
        {
            if (MatchItem.Index > CurrentIndex)
            {
                Spans.Add(new MarkdownSpan
                {
                    Kind = MarkdownSpanKind.Plain,
                    Text = LineText.Substring(CurrentIndex, MatchItem.Index - CurrentIndex)
                });
            }

            string Token = MatchItem.Value;
            if (Token.StartsWith("**"))
            {
                Spans.Add(new MarkdownSpan
                {
                    Kind = MarkdownSpanKind.Bold,
                    Text = Token.Substring(2, Token.Length - 4)
                });
            }
            else
            {
                Spans.Add(new MarkdownSpan
                {
                    Kind = MarkdownSpanKind.Code,
                    Text = Token.Substring(1, Token.Length - 2)
                });
            }

            CurrentIndex = MatchItem.Index + MatchItem.Length;
        }

        if (CurrentIndex < LineText.Length)
        {
            Spans.Add(new MarkdownSpan
            {
                Kind = MarkdownSpanKind.Plain,
                Text = LineText.Substring(CurrentIndex)
            });
        }

        return Spans;
    }

    private static (MarkdownBlockKind Kind, string ContentText) ClassifyLine(string TrimmedLine)
    {
        if (TrimmedLine.StartsWith("### "))
        {
            return (MarkdownBlockKind.Heading3, TrimmedLine.Substring(4).Trim());
        }

        if (TrimmedLine.StartsWith("## "))
        {
            return (MarkdownBlockKind.Heading2, TrimmedLine.Substring(3).Trim());
        }

        if (TrimmedLine.StartsWith("# "))
        {
            return (MarkdownBlockKind.Heading1, TrimmedLine.Substring(2).Trim());
        }

        if (TrimmedLine.StartsWith("- ") || TrimmedLine.StartsWith("* "))
        {
            return (MarkdownBlockKind.Bullet, TrimmedLine.Substring(2).Trim());
        }

        return (MarkdownBlockKind.Paragraph, TrimmedLine);
    }
}
