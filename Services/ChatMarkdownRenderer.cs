using System.Text.RegularExpressions;
using Ganss.Xss;
using Markdig;

namespace NorthrailCRM.Services;

public sealed class ChatMarkdownRenderer
{
    private static readonly Regex SandboxFileLink = new(
        @"(?<prefix>\]\()(?:sandbox:)?<?(?<path>/mnt/data/[^)\s>]+)>?(?<suffix>\))",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder()
        .UsePipeTables()
        .DisableHtml()
        .Build();

    private static readonly string[] AllowedTags =
    [
        "a", "blockquote", "br", "code", "del", "em", "h1", "h2", "h3", "h4", "h5", "h6",
        "hr", "li", "ol", "p", "pre", "s", "strong", "table", "tbody", "td", "th", "thead", "tr", "ul"
    ];

    public string ToSafeHtml(string markdown, IReadOnlyList<AgentChatAttachment>? attachments = null)
    {
        markdown = RewriteSandboxFileLinks(markdown ?? string.Empty, attachments);
        var sanitizer = new HtmlSanitizer();
        sanitizer.AllowedTags.Clear();
        sanitizer.AllowedTags.UnionWith(AllowedTags);
        sanitizer.AllowedAttributes.Clear();
        sanitizer.AllowedAttributes.UnionWith(["align", "class", "href", "start", "title"]);
        sanitizer.AllowedSchemes.Clear();
        sanitizer.AllowedSchemes.UnionWith(["http", "https", "mailto"]);

        return sanitizer.Sanitize(Markdown.ToHtml(markdown, Pipeline));
    }

    private static string RewriteSandboxFileLinks(string markdown, IReadOnlyList<AgentChatAttachment>? attachments)
    {
        if (attachments is not { Count: > 0 })
        {
            return markdown;
        }

        return SandboxFileLink.Replace(markdown, match =>
        {
            var linkedPath = Uri.UnescapeDataString(match.Groups["path"].Value);
            var attachment = attachments.FirstOrDefault(file =>
                linkedPath.EndsWith(file.FileName, StringComparison.OrdinalIgnoreCase));

            return attachment is null
                ? match.Value
                : $"{match.Groups["prefix"].Value}{attachment.DownloadUrl}{match.Groups["suffix"].Value}";
        });
    }
}