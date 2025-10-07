using System.Text.RegularExpressions;

namespace AzDoWiCreator;

public static class MarkdownHelper
{
    /// <summary>
    /// Detects if a string contains typical markdown syntax patterns
    /// </summary>
    public static bool ContainsMarkdownSyntax(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return false;

        var text = value!;

        // Check for common markdown patterns:
        // - Headers: # Header or ## Header
        // - Bold: **text** or __text__
        // - Italic: *text* or _text_
        // - Links: [text](url)
        // - Lists: - item or * item or 1. item
        // - Code blocks: ```code``` or `code`
        // - Blockquotes: > quote
        
        var markdownPatterns = new[]
        {
            @"^#{1,6}\s+.+$",                    // Headers
            @"\*\*.+?\*\*",                       // Bold with **
            @"__.+?__",                           // Bold with __
            @"(?<!\*)\*(?!\*)(?:(?!\*).)+\*(?!\*)", // Italic with * (but not **)
            @"(?<!_)_(?!_)(?:(?!_).)+_(?!_)",    // Italic with _ (but not __)
            @"\[.+?\]\(.+?\)",                    // Links
            @"^\s*[-*+]\s+.+$",                   // Unordered lists
            @"^\s*\d+\.\s+.+$",                   // Ordered lists
            @"```[\s\S]*?```",                    // Code blocks
            @"`[^`]+`",                           // Inline code
            @"^>\s+.+$"                           // Blockquotes
        };

        foreach (var pattern in markdownPatterns)
        {
            if (Regex.IsMatch(text, pattern, RegexOptions.Multiline))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Converts markdown text to HTML for Azure DevOps rich text fields
    /// </summary>
    public static string ConvertMarkdownToHtml(string markdown)
    {
        if (string.IsNullOrWhiteSpace(markdown))
            return string.Empty;

        var html = markdown;

        // Convert headers (must be done first to avoid conflicts)
        html = Regex.Replace(html, @"^######\s+(.+)$", "<h6>$1</h6>", RegexOptions.Multiline);
        html = Regex.Replace(html, @"^#####\s+(.+)$", "<h5>$1</h5>", RegexOptions.Multiline);
        html = Regex.Replace(html, @"^####\s+(.+)$", "<h4>$1</h4>", RegexOptions.Multiline);
        html = Regex.Replace(html, @"^###\s+(.+)$", "<h3>$1</h3>", RegexOptions.Multiline);
        html = Regex.Replace(html, @"^##\s+(.+)$", "<h2>$1</h2>", RegexOptions.Multiline);
        html = Regex.Replace(html, @"^#\s+(.+)$", "<h1>$1</h1>", RegexOptions.Multiline);

        // Convert bold (** or __)
        html = Regex.Replace(html, @"\*\*(.+?)\*\*", "<strong>$1</strong>");
        html = Regex.Replace(html, @"__(.+?)__", "<strong>$1</strong>");

        // Convert italic (* or _) - must be after bold to avoid conflicts
        html = Regex.Replace(html, @"(?<!\*)\*(?!\*)(.+?)\*(?!\*)", "<em>$1</em>");
        html = Regex.Replace(html, @"(?<!_)_(?!_)(.+?)_(?!_)", "<em>$1</em>");

        // Convert links
        html = Regex.Replace(html, @"\[(.+?)\]\((.+?)\)", "<a href=\"$2\">$1</a>");

        // Convert code blocks (``` ... ```)
        html = Regex.Replace(html, @"```([\s\S]*?)```", "<pre><code>$1</code></pre>", RegexOptions.Singleline);

        // Convert inline code
        html = Regex.Replace(html, @"`([^`]+)`", "<code>$1</code>");

        // Convert unordered lists
        var lines = html.Split('\n');
        var result = new System.Text.StringBuilder();
        bool inList = false;
        string? currentListType = null;

        for (int i = 0; i < lines.Length; i++)
        {
            var line = lines[i];
            var unorderedListMatch = Regex.Match(line, @"^\s*[-*+]\s+(.+)$");
            var orderedListMatch = Regex.Match(line, @"^\s*\d+\.\s+(.+)$");
            var blockquoteMatch = Regex.Match(line, @"^>\s+(.+)$");

            if (unorderedListMatch.Success)
            {
                if (!inList || currentListType != "ul")
                {
                    if (inList && currentListType == "ol")
                    {
                        result.Append("</ol>");
                    }
                    result.Append("<ul>");
                    inList = true;
                    currentListType = "ul";
                }
                result.Append($"<li>{unorderedListMatch.Groups[1].Value}</li>");
            }
            else if (orderedListMatch.Success)
            {
                if (!inList || currentListType != "ol")
                {
                    if (inList && currentListType == "ul")
                    {
                        result.Append("</ul>");
                    }
                    result.Append("<ol>");
                    inList = true;
                    currentListType = "ol";
                }
                result.Append($"<li>{orderedListMatch.Groups[1].Value}</li>");
            }
            else if (blockquoteMatch.Success)
            {
                if (inList)
                {
                    result.Append(currentListType == "ul" ? "</ul>" : "</ol>");
                    inList = false;
                    currentListType = null;
                }
                result.Append($"<blockquote>{blockquoteMatch.Groups[1].Value}</blockquote>");
            }
            else
            {
                if (inList)
                {
                    result.Append(currentListType == "ul" ? "</ul>" : "</ol>");
                    inList = false;
                    currentListType = null;
                }
                
                // Regular line - convert newlines to <br> unless empty
                if (!string.IsNullOrWhiteSpace(line))
                {
                    result.Append($"<div>{line}</div>");
                }
                else if (i < lines.Length - 1)
                {
                    result.Append("<br/>");
                }
            }
        }

        if (inList)
        {
            result.Append(currentListType == "ul" ? "</ul>" : "</ol>");
        }

        return result.ToString();
    }
}
