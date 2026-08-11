using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;

namespace EapWorkAssistant.Helpers;

/// <summary>
/// 将 AI 返回的 Markdown 子集转换为 WPF FlowDocument，用于报告预览渲染。
/// 支持的语法与 DocxReportService 的解析保持一致：
/// 标题(#/##/###)、无序列表(-/*/•)、有序列表(1. / 1、 / 1))、
/// 加粗(**)、斜体(*)、行内代码(`)、代码块(```)、分隔线(---)、表格(| a | b |)。
/// </summary>
public static class MarkdownToFlowDocument
{
    private static Brush GetBrush(string key, Brush fallback)
    {
        if (Application.Current.Resources[key] is Brush b) return b;
        return fallback;
    }

    public static FlowDocument Build(string? markdown)
    {
        var doc = new FlowDocument
        {
            PagePadding = new Thickness(0),
            FontFamily = (FontFamily)(Application.Current.Resources["ContentFont"] ?? new FontFamily("Microsoft YaHei")),
            FontSize = 13,
            Foreground = GetBrush("TextPrimaryBrush", Brushes.Black),
            LineHeight = 1.6
        };

        if (string.IsNullOrWhiteSpace(markdown)) return doc;

        // 清理整体代码块围栏（AI 有时会用 ```markdown ... ``` 包裹整段）
        var text = Regex.Replace(markdown, @"^```(?:markdown|md)?\s*", "", RegexOptions.Multiline);
        text = Regex.Replace(text, @"^```\s*$", "", RegexOptions.Multiline);

        var lines = text.Replace("\r\n", "\n").Split('\n');
        var listItems = new List<(string Text, bool Ordered, int Level)>();
        var tableRows = new List<string>();

        void FlushList()
        {
            if (listItems.Count == 0) return;
            var list = new List { MarkerStyle = listItems[0].Ordered ? TextMarkerStyle.Decimal : TextMarkerStyle.Disc };
            foreach (var it in listItems)
            {
                var li = new ListItem(NewParagraph(BuildInlines(it.Text)))
                {
                    Margin = new Thickness(it.Level * 16, 0, 0, 2),
                    Padding = new Thickness(4, 0, 0, 0)
                };
                list.ListItems.Add(li);
            }
            doc.Blocks.Add(list);
            listItems.Clear();
        }

        void FlushTable()
        {
            if (tableRows.Count == 0) return;
            var firstCols = ParseTableRow(tableRows[0]);
            if (firstCols.Count > 1)
            {
                var table = new Table { CellSpacing = 0, Margin = new Thickness(0, 4, 0, 8) };
                var cols = firstCols.Count;
                for (int c = 0; c < cols; c++)
                    table.Columns.Add(new TableColumn());
                var headerGroup = new TableRowGroup();
                var bodyGroup = new TableRowGroup();
                bool isHeader = true;
                foreach (var row in tableRows)
                {
                    if (Regex.IsMatch(row.Trim(), @"^\|[\s\-:|]+\|$")) continue; // 分隔行
                    var cells = ParseTableRow(row);
                    if (cells.Count == 0) continue;
                    var tr = new TableRow();
                    for (int c = 0; c < cols; c++)
                    {
                        var cellText = c < cells.Count ? cells[c] : "";
                        var p = NewParagraph(BuildInlines(cellText));
                        p.Margin = new Thickness(6, 4, 6, 4);
                        p.FontSize = 12;
                        var cell = new TableCell(p);
                        if (isHeader)
                        {
                            cell.Background = GetBrush("PrimaryLightBrush", Brushes.LightGray);
                            p.FontWeight = FontWeights.SemiBold;
                        }
                        tr.Cells.Add(cell);
                    }
                    if (isHeader) headerGroup.Rows.Add(tr);
                    else bodyGroup.Rows.Add(tr);
                    isHeader = false;
                }
                if (headerGroup.Rows.Count > 0) table.RowGroups.Add(headerGroup);
                if (bodyGroup.Rows.Count > 0) table.RowGroups.Add(bodyGroup);
                doc.Blocks.Add(table);
            }
            else
            {
                foreach (var row in tableRows)
                {
                    var cells = ParseTableRow(row);
                    if (cells.Count > 0)
                        doc.Blocks.Add(NewParagraph(BuildInlines(cells[0]), new Thickness(0, 0, 0, 4)));
                }
            }
            tableRows.Clear();
        }

        int i = 0;
        while (i < lines.Length)
        {
            var line = lines[i].TrimEnd('\r');
            var trimmed = line.Trim();

            if (string.IsNullOrWhiteSpace(trimmed))
            {
                FlushList();
                FlushTable();
                i++;
                continue;
            }

            // 代码块
            if (trimmed.StartsWith("```"))
            {
                FlushList();
                FlushTable();
                var code = new List<string>();
                i++; // 跳过起始围栏
                while (i < lines.Length && !lines[i].Trim().StartsWith("```"))
                {
                    code.Add(lines[i]);
                    i++;
                }
                i++; // 跳过结束围栏
                var codeText = string.Join("\n", code);
                var para = new Paragraph(new Run(codeText))
                {
                    FontFamily = (FontFamily)(Application.Current.Resources["MonoFont"] ?? new FontFamily("Consolas")),
                    FontSize = 12,
                    Background = GetBrush("SurfaceAltBrush", Brushes.WhiteSmoke),
                    Padding = new Thickness(10),
                    Margin = new Thickness(0, 2, 0, 8)
                };
                doc.Blocks.Add(para);
                continue;
            }

            // 标题
            if (trimmed.StartsWith("### "))
            {
                FlushList(); FlushTable();
                doc.Blocks.Add(MakeHeading(trimmed.Substring(4).Trim(), 14, FontWeights.Bold, 6, 2));
                i++; continue;
            }
            if (trimmed.StartsWith("## "))
            {
                FlushList(); FlushTable();
                doc.Blocks.Add(MakeHeading(trimmed.Substring(3).Trim(), 16, FontWeights.Bold, 10, 4));
                i++; continue;
            }
            if (trimmed.StartsWith("# "))
            {
                FlushList(); FlushTable();
                doc.Blocks.Add(MakeHeading(trimmed.Substring(2).Trim(), 19, FontWeights.Bold, 12, 6));
                i++; continue;
            }

            // 分隔线
            if (Regex.IsMatch(trimmed, @"^(\-{3,}|\*{3,}|_{3,})$"))
            {
                FlushList(); FlushTable();
                var hr = new Paragraph { Margin = new Thickness(0, 4, 0, 4) };
                hr.Inlines.Add(new InlineUIContainer(
                    new System.Windows.Controls.Border
                    {
                        BorderBrush = GetBrush("BorderBrush", Brushes.LightGray),
                        BorderThickness = new Thickness(0, 1, 0, 0),
                        Height = 1
                    }));
                doc.Blocks.Add(hr);
                i++; continue;
            }

            // 表格行
            if (trimmed.StartsWith("|") && trimmed.EndsWith("|"))
            {
                FlushList();
                tableRows.Add(trimmed);
                i++; continue;
            }

            FlushTable();

            // 无序列表（- / * / • ），支持前导空格缩进
            var bulletMatch = Regex.Match(trimmed, @"^(\s*)[-*\u2022]\s+(.+)$");
            if (bulletMatch.Success)
            {
                var level = bulletMatch.Groups[1].Value.Length / 2;
                listItems.Add((bulletMatch.Groups[2].Value.Trim(), false, level));
                i++; continue;
            }

            // 有序列表（1. / 1、 / 1) ）
            var numMatch = Regex.Match(trimmed, @"^(\s*)(\d+)[.、．)]\s+(.+)$");
            if (numMatch.Success)
            {
                var level = numMatch.Groups[1].Value.Length / 2;
                listItems.Add((numMatch.Groups[3].Value.Trim(), true, level));
                i++; continue;
            }

            // 普通段落
            FlushList();
            doc.Blocks.Add(NewParagraph(BuildInlines(trimmed), new Thickness(0, 0, 0, 6)));
            i++;
        }

        FlushList();
        FlushTable();
        return doc;
    }

    private static Paragraph NewParagraph(IEnumerable<Inline> inlines, Thickness margin)
    {
        var p = new Paragraph { Margin = margin };
        p.Inlines.AddRange(inlines);
        return p;
    }

    private static Paragraph NewParagraph(IEnumerable<Inline> inlines)
        => NewParagraph(inlines, new Thickness(0, 0, 0, 6));

    private static Paragraph MakeHeading(string text, double size, FontWeight weight, double before, double after)
    {
        var p = NewParagraph(BuildInlines(text));
        p.FontSize = size;
        p.FontWeight = weight;
        p.Foreground = GetBrush("TextPrimaryBrush", Brushes.Black);
        p.Margin = new Thickness(0, before, 0, after);
        return p;
    }

    /// <summary>
    /// 将行内文本拆分为带加粗/斜体/行内代码样式的 Inline 集合。
    /// </summary>
    private static IEnumerable<Inline> BuildInlines(string text)
    {
        var inlines = new List<Inline>();
        var pattern = @"(\*\*.+?\*\*)|(\*.+?\*)|(`[^`]+?`)";
        var matches = Regex.Matches(text, pattern);
        int lastIndex = 0;
        foreach (Match m in matches)
        {
            if (m.Index > lastIndex)
                inlines.Add(new Run(text.Substring(lastIndex, m.Index - lastIndex)));

            var val = m.Value;
            if (m.Groups[1].Success) // 加粗
            {
                inlines.Add(new Run(val.Substring(2, val.Length - 4)) { FontWeight = FontWeights.Bold });
            }
            else if (m.Groups[2].Success) // 斜体
            {
                inlines.Add(new Run(val.Substring(1, val.Length - 2)) { FontStyle = FontStyles.Italic });
            }
            else if (m.Groups[3].Success) // 行内代码
            {
                inlines.Add(new Run(val.Substring(1, val.Length - 2))
                {
                    FontFamily = (FontFamily)(Application.Current.Resources["MonoFont"] ?? new FontFamily("Consolas")),
                    Background = GetBrush("SurfaceAltBrush", Brushes.WhiteSmoke),
                    FontSize = 12
                });
            }
            lastIndex = m.Index + val.Length;
        }
        if (lastIndex < text.Length)
            inlines.Add(new Run(text.Substring(lastIndex)));

        return inlines;
    }

    private static List<string> ParseTableRow(string row)
    {
        var inner = row.Trim().Trim('|');
        return inner.Split('|').Select(c => c.Trim()).ToList();
    }
}
