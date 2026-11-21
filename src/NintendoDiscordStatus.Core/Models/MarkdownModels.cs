using System.Collections.Generic;

namespace NintendoDiscordStatus.Core.Models
{
    public enum MarkdownBlockKind
    {
        Paragraph,
        Heading1,
        Heading2,
        Heading3,
        Bullet
    }

    public enum MarkdownSpanKind
    {
        Plain,
        Bold,
        Code
    }

    public class MarkdownSpan
    {
        public MarkdownSpanKind Kind { get; set; }
        public string Text { get; set; } = string.Empty;
    }

    public class MarkdownBlock
    {
        public MarkdownBlockKind Kind { get; set; }
        public List<MarkdownSpan> Spans { get; set; } = new List<MarkdownSpan>();
    }
}
