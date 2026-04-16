using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

namespace Clipster.App.Controls;

/// <summary>
/// A RichTextBox-based control that renders a subset of Markdown as formatted WPF elements.
/// Supports bold, italic, inline code, fenced code blocks, headings, bullet lists,
/// numbered lists, and links.
/// </summary>
public class MarkdownTextBlock : RichTextBox
{
    // Fonts
    private static readonly FontFamily MonospaceFont = new("Cascadia Code, Consolas, Courier New");
    private static readonly FontFamily DefaultFont = new("Segoe UI");

    // Code block styling
    private static readonly SolidColorBrush CodeBlockBackground = new(Color.FromRgb(0x1A, 0x1A, 0x2E));
    private static readonly SolidColorBrush CodeBlockForeground = new(Color.FromRgb(0xE0, 0xE0, 0xF0));

    // Inline code styling
    private static readonly SolidColorBrush InlineCodeBackground = new(Color.FromRgb(0x2A, 0x2A, 0x40));

    public static readonly DependencyProperty MarkdownTextProperty = DependencyProperty.Register(
        nameof(MarkdownText),
        typeof(string),
        typeof(MarkdownTextBlock),
        new PropertyMetadata(string.Empty, OnMarkdownTextChanged));

    public string MarkdownText
    {
        get => (string)GetValue(MarkdownTextProperty);
        set => SetValue(MarkdownTextProperty, value);
    }

    public MarkdownTextBlock()
    {
        IsReadOnly = true;
        IsDocumentEnabled = true;
        BorderThickness = new Thickness(0);
        Background = Brushes.Transparent;
        Padding = new Thickness(0);
        Document = new FlowDocument { FontFamily = DefaultFont };

        // Freeze brushes for performance
        CodeBlockBackground.Freeze();
        CodeBlockForeground.Freeze();
        InlineCodeBackground.Freeze();
    }

    private static void OnMarkdownTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is MarkdownTextBlock control)
            control.RenderMarkdown((string)e.NewValue ?? string.Empty);
    }

    private void RenderMarkdown(string markdown)
    {
        var doc = new FlowDocument { FontFamily = DefaultFont, FontSize = FontSize };
        if (string.IsNullOrEmpty(markdown))
        {
            Document = doc;
            return;
        }

        var lines = markdown.Replace("\r\n", "\n").Split('\n');
        int i = 0;

        while (i < lines.Length)
        {
            var line = lines[i];

            // Fenced code block (``` ... ```)
            if (line.TrimStart().StartsWith("```"))
            {
                i++;
                var codeLines = new List<string>();
                while (i < lines.Length && !lines[i].TrimStart().StartsWith("```"))
                {
                    codeLines.Add(lines[i]);
                    i++;
                }
                if (i < lines.Length) i++; // skip closing ```

                doc.Blocks.Add(CreateCodeBlock(string.Join("\n", codeLines)));
                continue;
            }

            // Heading (# through ######)
            var headingMatch = Regex.Match(line, @"^(#{1,6})\s+(.+)$");
            if (headingMatch.Success)
            {
                int level = headingMatch.Groups[1].Value.Length;
                string text = headingMatch.Groups[2].Value;
                doc.Blocks.Add(CreateHeading(text, level));
                i++;
                continue;
            }

            // Bullet list (- or * items)
            if (Regex.IsMatch(line, @"^\s*[-*]\s+"))
            {
                var listBlock = new List { MarkerStyle = TextMarkerStyle.Disc };
                while (i < lines.Length && Regex.IsMatch(lines[i], @"^\s*[-*]\s+"))
                {
                    var itemText = Regex.Replace(lines[i], @"^\s*[-*]\s+", "");
                    var listItem = new ListItem(CreateInlineParagraph(itemText));
                    listBlock.ListItems.Add(listItem);
                    i++;
                }
                doc.Blocks.Add(listBlock);
                continue;
            }

            // Numbered list (1. 2. etc.)
            if (Regex.IsMatch(line, @"^\s*\d+\.\s+"))
            {
                var listBlock = new List { MarkerStyle = TextMarkerStyle.Decimal };
                while (i < lines.Length && Regex.IsMatch(lines[i], @"^\s*\d+\.\s+"))
                {
                    var itemText = Regex.Replace(lines[i], @"^\s*\d+\.\s+", "");
                    var listItem = new ListItem(CreateInlineParagraph(itemText));
                    listBlock.ListItems.Add(listItem);
                    i++;
                }
                doc.Blocks.Add(listBlock);
                continue;
            }

            // Blank line
            if (string.IsNullOrWhiteSpace(line))
            {
                i++;
                continue;
            }

            // Regular paragraph
            doc.Blocks.Add(CreateInlineParagraph(line));
            i++;
        }

        Document = doc;
    }

    private static Paragraph CreateHeading(string text, int level)
    {
        double fontSize = level switch
        {
            1 => 24,
            2 => 20,
            3 => 18,
            4 => 16,
            5 => 14,
            _ => 13
        };

        var paragraph = new Paragraph
        {
            FontSize = fontSize,
            FontWeight = FontWeights.Bold,
            Margin = new Thickness(0, 4, 0, 2)
        };
        paragraph.Inlines.AddRange(ParseInlines(text));
        return paragraph;
    }

    private static Paragraph CreateInlineParagraph(string text)
    {
        var paragraph = new Paragraph { Margin = new Thickness(0, 2, 0, 2) };
        paragraph.Inlines.AddRange(ParseInlines(text));
        return paragraph;
    }

    private static BlockUIContainer CreateCodeBlock(string code)
    {
        var textBox = new TextBox
        {
            Text = code,
            FontFamily = MonospaceFont,
            FontSize = 12,
            Background = CodeBlockBackground,
            Foreground = CodeBlockForeground,
            BorderThickness = new Thickness(0),
            Padding = new Thickness(12, 8, 12, 8),
            IsReadOnly = true,
            TextWrapping = TextWrapping.Wrap,
            AcceptsReturn = true,
        };

        var border = new Border
        {
            Child = textBox,
            CornerRadius = new CornerRadius(6),
            Margin = new Thickness(0, 4, 0, 4),
            Background = CodeBlockBackground
        };

        return new BlockUIContainer(border);
    }

    /// <summary>
    /// Parses inline markdown formatting into WPF Inline elements.
    /// Handles: bold (**), italic (*), inline code (`), and links ([text](url)).
    /// </summary>
    private static IEnumerable<Inline> ParseInlines(string text)
    {
        // Pattern handles: **bold**, *italic*, `code`, [link text](url)
        // Order matters: **bold** must be matched before *italic*
        var pattern = @"(\*\*(.+?)\*\*)|(\*(.+?)\*)|(`(.+?)`)|(\[(.+?)\]\((.+?)\))";
        int lastIndex = 0;
        var inlines = new List<Inline>();

        foreach (Match match in Regex.Matches(text, pattern))
        {
            // Add any plain text before this match
            if (match.Index > lastIndex)
            {
                inlines.Add(new Run(text[lastIndex..match.Index]));
            }

            if (match.Groups[1].Success) // **bold**
            {
                inlines.Add(new Bold(new Run(match.Groups[2].Value)));
            }
            else if (match.Groups[3].Success) // *italic*
            {
                inlines.Add(new Italic(new Run(match.Groups[4].Value)));
            }
            else if (match.Groups[5].Success) // `inline code`
            {
                var codeRun = new Run(match.Groups[6].Value)
                {
                    FontFamily = MonospaceFont,
                    Background = InlineCodeBackground,
                    FontSize = 11.5
                };
                // Wrap with thin spaces for visual padding
                inlines.Add(new Run("\u2009"));
                inlines.Add(codeRun);
                inlines.Add(new Run("\u2009"));
            }
            else if (match.Groups[7].Success) // [text](url)
            {
                var linkText = match.Groups[8].Value;
                var url = match.Groups[9].Value;
                var hyperlink = new Hyperlink(new Run(linkText))
                {
                    NavigateUri = Uri.TryCreate(url, UriKind.Absolute, out var uri) ? uri : null,
                    ToolTip = url
                };
                hyperlink.RequestNavigate += (_, e) =>
                {
                    try
                    {
                        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                        {
                            FileName = e.Uri.AbsoluteUri,
                            UseShellExecute = true
                        });
                    }
                    catch { /* best-effort */ }
                    e.Handled = true;
                };
                inlines.Add(hyperlink);
            }

            lastIndex = match.Index + match.Length;
        }

        // Remaining plain text after last match
        if (lastIndex < text.Length)
        {
            inlines.Add(new Run(text[lastIndex..]));
        }

        // If nothing was parsed, return the original text as a single Run
        if (inlines.Count == 0)
        {
            inlines.Add(new Run(text));
        }

        return inlines;
    }
}
