using System.IO;
using System.Text.RegularExpressions;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using Microsoft.Win32;

namespace EapWorkAssistant.Services;

/// <summary>
/// 使用 Open XML SDK 将 AI 返回的 Markdown 内容生成带样式的 Word 文档
/// </summary>
public class DocxReportService
{
    // 颜色常量
    private const string PrimaryColor = "4338CA";
    private const string TextColor = "0F172A";
    private const string SecondaryTextColor = "475569";
    private const string TableHeaderBg = "4472C4";
    private const string TableAltRowBg = "F2F7FB";

    /// <summary>
    /// 生成 DOCX 文件，返回临时文件路径
    /// </summary>
    /// <param name="title">报告标题</param>
    /// <param name="dateRange">报告周期文本</param>
    /// <param name="aiContent">AI 返回的 Markdown 格式内容</param>
    public string GenerateDocx(string title, string dateRange, string aiContent)
    {
        var tempPath = Path.Combine(Path.GetTempPath(),
            $"AI工作总结_{DateTime.Now:yyyyMMdd_HHmmss}.docx");

        using var doc = WordprocessingDocument.Create(tempPath, WordprocessingDocumentType.Document);
        var mainPart = doc.AddMainDocumentPart();

        // 初始化文档
        mainPart.Document = new Document();
        var body = mainPart.Document.AppendChild(new Body());

        // 添加样式定义
        AddDocumentStyles(mainPart);

        // 添加页边距
        AddSectionProperties(body);

        // 添加标题
        body.AppendChild(CreateHeading(title, "Heading1"));

        // 添加副标题
        body.AppendChild(CreateSubtitle($"报告周期：{dateRange}    生成时间：{DateTime.Now:yyyy-MM-dd HH:mm}"));

        // 添加分隔线（用底部边框模拟）
        body.AppendChild(CreateDivider());

        // 解析并添加 AI 内容
        ParseAndAddContent(body, aiContent);

        return tempPath;
    }

    /// <summary>
    /// 弹出保存对话框，将 DOCX 文件复制到用户选择的位置
    /// </summary>
    public static bool SaveDocxFile(string sourcePath, string defaultName = "AI工作总结")
    {
        var dialog = new SaveFileDialog
        {
            Filter = "Word 文档|*.docx|所有文件|*.*",
            FileName = $"{defaultName}_{DateTime.Now:yyyyMMdd}.docx",
            DefaultExt = ".docx"
        };

        if (dialog.ShowDialog() == true)
        {
            try
            {
                File.Copy(sourcePath, dialog.FileName, overwrite: true);
                return true;
            }
            catch
            {
                return false;
            }
        }
        return false;
    }

    #region 样式定义

    private void AddDocumentStyles(MainDocumentPart mainPart)
    {
        var styles = new Styles();

        // Normal 样式
        var normalStyle = new Style
        {
            Type = StyleValues.Paragraph,
            StyleId = "Normal",
            Default = true
        };
        normalStyle.AppendChild(new StyleName { Val = "Normal" });
        normalStyle.AppendChild(new StyleRunProperties(
            new RunFonts { Ascii = "Microsoft YaHei", HighAnsi = "Microsoft YaHei", EastAsia = "Microsoft YaHei" },
            new FontSize { Val = "21" }, // 10.5pt
            new Color { Val = TextColor }
        ));
        normalStyle.AppendChild(new StyleParagraphProperties(
            new SpacingBetweenLines { After = "120", Line = "360", LineRule = LineSpacingRuleValues.Auto }
        ));
        styles.AppendChild(normalStyle);

        // Heading1 样式
        var h1Style = new Style
        {
            Type = StyleValues.Paragraph,
            StyleId = "Heading1"
        };
        h1Style.AppendChild(new StyleName { Val = "heading 1" });
        h1Style.AppendChild(new BasedOn { Val = "Normal" });
        h1Style.AppendChild(new StyleRunProperties(
            new RunFonts { Ascii = "Microsoft YaHei", HighAnsi = "Microsoft YaHei", EastAsia = "Microsoft YaHei" },
            new Bold(),
            new FontSize { Val = "44" }, // 22pt
            new Color { Val = PrimaryColor }
        ));
        h1Style.AppendChild(new StyleParagraphProperties(
            new Justification { Val = JustificationValues.Center },
            new SpacingBetweenLines { Before = "240", After = "120" }
        ));
        styles.AppendChild(h1Style);

        // Heading2 样式
        var h2Style = new Style
        {
            Type = StyleValues.Paragraph,
            StyleId = "Heading2"
        };
        h2Style.AppendChild(new StyleName { Val = "heading 2" });
        h2Style.AppendChild(new BasedOn { Val = "Normal" });
        h2Style.AppendChild(new StyleRunProperties(
            new RunFonts { Ascii = "Microsoft YaHei", HighAnsi = "Microsoft YaHei", EastAsia = "Microsoft YaHei" },
            new Bold(),
            new FontSize { Val = "28" }, // 14pt
            new Color { Val = "2E4057" }
        ));
        h2Style.AppendChild(new StyleParagraphProperties(
            new SpacingBetweenLines { Before = "360", After = "120" }
        ));
        styles.AppendChild(h2Style);

        // Heading3 样式
        var h3Style = new Style
        {
            Type = StyleValues.Paragraph,
            StyleId = "Heading3"
        };
        h3Style.AppendChild(new StyleName { Val = "heading 3" });
        h3Style.AppendChild(new BasedOn { Val = "Normal" });
        h3Style.AppendChild(new StyleRunProperties(
            new RunFonts { Ascii = "Microsoft YaHei", HighAnsi = "Microsoft YaHei", EastAsia = "Microsoft YaHei" },
            new Bold(),
            new FontSize { Val = "24" }, // 12pt
            new Color { Val = "2E4057" }
        ));
        h3Style.AppendChild(new StyleParagraphProperties(
            new SpacingBetweenLines { Before = "240", After = "80" }
        ));
        styles.AppendChild(h3Style);

        var styleDefsPart = mainPart.AddNewPart<StyleDefinitionsPart>();
        styleDefsPart.Styles = styles;
    }

    private void AddSectionProperties(Body body)
    {
        var sectionProps = new SectionProperties();
        sectionProps.AppendChild(new PageSize { Width = 11906, Height = 16838 }); // A4
        sectionProps.AppendChild(new PageMargin
        {
            Top = 1440, Bottom = 1440, Left = 1440, Right = 1440
        });
        body.AppendChild(sectionProps);
    }

    #endregion

    #region 内容创建辅助方法

    private Paragraph CreateHeading(string text, string styleId)
    {
        return new Paragraph(
            new ParagraphProperties(new ParagraphStyleId { Val = styleId }),
            new Run(new Text(text))
        );
    }

    private Paragraph CreateSubtitle(string text)
    {
        return new Paragraph(
            new ParagraphProperties(
                new Justification { Val = JustificationValues.Center },
                new SpacingBetweenLines { After = "200" }
            ),
            new Run(
                new RunProperties(
                    new FontSize { Val = "22" },
                    new Color { Val = SecondaryTextColor }
                ),
                new Text(text)
            )
        );
    }

    private Paragraph CreateDivider()
    {
        return new Paragraph(
            new ParagraphProperties(
                new ParagraphBorders(
                    new BottomBorder
                    {
                        Val = BorderValues.Single,
                        Size = 12,
                        Color = PrimaryColor,
                        Space = 1
                    }
                ),
                new SpacingBetweenLines { After = "240" }
            )
        );
    }

    private Paragraph CreateNormalParagraph(string text, bool indent = false)
    {
        var p = new Paragraph(
            new Run(
                new RunProperties(
                    new FontSize { Val = "21" },
                    new Color { Val = TextColor }
                ),
                new Text(text) { Space = SpaceProcessingModeValues.Preserve }
            )
        );
        if (indent)
        {
            p.InsertAt(new ParagraphProperties(
                new Indentation { FirstLine = "480" }
            ), 0);
        }
        return p;
    }

    private Paragraph CreateBulletItem(string text)
    {
        return new Paragraph(
            new ParagraphProperties(
                new Indentation { Left = "720", Hanging = "360" },
                new SpacingBetweenLines { Before = "40", After = "40" }
            ),
            new Run(
                new RunProperties(
                    new FontSize { Val = "21" },
                    new Color { Val = TextColor }
                ),
                new Text("• " + text) { Space = SpaceProcessingModeValues.Preserve }
            )
        );
    }

    private Paragraph CreateNumberedItem(string number, string text)
    {
        return new Paragraph(
            new ParagraphProperties(
                new Indentation { Left = "720", Hanging = "360" },
                new SpacingBetweenLines { Before = "40", After = "40" }
            ),
            new Run(
                new RunProperties(
                    new FontSize { Val = "21" },
                    new Color { Val = TextColor }
                ),
                new Text($"{number}. {text}") { Space = SpaceProcessingModeValues.Preserve }
            )
        );
    }

    private Paragraph CreateCellParagraph(string text, RunProperties runProps, bool isHeader)
    {
        var p = new Paragraph(
            new Run(runProps, new Text(text.Trim()) { Space = SpaceProcessingModeValues.Preserve })
        );
        if (isHeader)
        {
            p.InsertAt(new ParagraphProperties(
                new Justification { Val = JustificationValues.Center }
            ), 0);
        }
        return p;
    }

    #endregion

    #region Markdown 解析

    /// <summary>
    /// 解析 AI 返回的 Markdown 内容，转换为 OpenXML 元素
    /// </summary>
    private void ParseAndAddContent(Body body, string content)
    {
        if (string.IsNullOrWhiteSpace(content))
            return;

        // 清理 Markdown 代码块标记（AI 有时会用 ```markdown ... ```）
        content = Regex.Replace(content, @"^```(?:markdown|md)?\s*", "", RegexOptions.Multiline);
        content = Regex.Replace(content, @"^```\s*$", "", RegexOptions.Multiline);

        var lines = content.Split('\n');
        var i = 0;
        var tableRows = new List<string>();

        while (i < lines.Length)
        {
            var line = lines[i].TrimEnd('\r');
            var trimmed = line.Trim();

            // 空行跳过
            if (string.IsNullOrWhiteSpace(trimmed))
            {
                FlushTable(body, tableRows);
                tableRows.Clear();
                i++;
                continue;
            }

            // ## 标题 (H2)
            if (trimmed.StartsWith("## "))
            {
                FlushTable(body, tableRows);
                tableRows.Clear();
                body.AppendChild(CreateHeading(trimmed[3..].Trim(), "Heading2"));
                i++;
                continue;
            }

            // ### 标题 (H3)
            if (trimmed.StartsWith("### "))
            {
                FlushTable(body, tableRows);
                tableRows.Clear();
                body.AppendChild(CreateHeading(trimmed[4..].Trim(), "Heading3"));
                i++;
                continue;
            }

            // 表格行 (| col1 | col2 |)
            if (trimmed.StartsWith("|") && trimmed.EndsWith("|"))
            {
                // 跳过分隔行 (|---|---|)
                if (Regex.IsMatch(trimmed, @"^\|[\s\-:|]+\|$"))
                {
                    i++;
                    continue;
                }
                tableRows.Add(trimmed);
                i++;
                continue;
            }

            // 非表格行，先刷新表格
            FlushTable(body, tableRows);
            tableRows.Clear();

            // 无序列表 (- item / * item / • item)
            if (Regex.IsMatch(trimmed, @"^[-*•]\s+"))
            {
                var itemText = Regex.Replace(trimmed, @"^[-*•]\s+", "");
                body.AppendChild(CreateBulletItem(CleanBoldMarkers(itemText)));
                i++;
                continue;
            }

            // 有序列表 (1. item / 1、item)
            var numMatch = Regex.Match(trimmed, @"^(\d+)[.、．)]\s*(.+)");
            if (numMatch.Success)
            {
                body.AppendChild(CreateNumberedItem(numMatch.Groups[1].Value, CleanBoldMarkers(numMatch.Groups[2].Value)));
                i++;
                continue;
            }

            // 普通段落（清除 Markdown 加粗标记）
            body.AppendChild(CreateNormalParagraph(CleanBoldMarkers(trimmed), indent: true));
            i++;
        }

        // 刷新末尾的表格
        FlushTable(body, tableRows);
    }

    /// <summary>
    /// 清除 Markdown 加粗标记 **text**，保留纯文本
    /// </summary>
    private static string CleanBoldMarkers(string text)
    {
        return Regex.Replace(text, @"\*\*(.+?)\*\*", "$1");
    }

    /// <summary>
    /// 将累积的表格行渲染为 Word 表格
    /// </summary>
    private void FlushTable(Body body, List<string> rows)
    {
        if (rows.Count == 0) return;

        // 解析列数
        var firstCols = ParseTableRow(rows[0]);
        if (firstCols.Count == 0) return;

        var colCount = firstCols.Count;
        var table = new Table();

        // 表格属性
        var tableProps = new TableProperties(
            new TableBorders(
                new TopBorder { Val = BorderValues.Single, Size = 4, Color = "BBBBBB" },
                new BottomBorder { Val = BorderValues.Single, Size = 4, Color = "BBBBBB" },
                new LeftBorder { Val = BorderValues.Single, Size = 4, Color = "BBBBBB" },
                new RightBorder { Val = BorderValues.Single, Size = 4, Color = "BBBBBB" },
                new InsideHorizontalBorder { Val = BorderValues.Single, Size = 4, Color = "BBBBBB" },
                new InsideVerticalBorder { Val = BorderValues.Single, Size = 4, Color = "BBBBBB" }
            ),
            new TableWidth { Width = "5000", Type = TableWidthUnitValues.Pct }
        );
        table.AppendChild(tableProps);

        for (var r = 0; r < rows.Count; r++)
        {
            var cols = ParseTableRow(rows[r]);
            var tableRow = new TableRow();

            for (var c = 0; c < colCount; c++)
            {
                var cellText = c < cols.Count ? cols[c] : "";
                var cell = new TableCell();

                // 单元格属性
                var cellProps = new TableCellProperties(
                    new TableCellBorders(
                        new TopBorder { Val = BorderValues.Single, Size = 4, Color = "BBBBBB" },
                        new BottomBorder { Val = BorderValues.Single, Size = 4, Color = "BBBBBB" },
                        new LeftBorder { Val = BorderValues.Single, Size = 4, Color = "BBBBBB" },
                        new RightBorder { Val = BorderValues.Single, Size = 4, Color = "BBBBBB" }
                    ),
                    new TableCellMargin(
                        new TopMargin { Width = "60", Type = TableWidthUnitValues.Dxa },
                        new BottomMargin { Width = "60", Type = TableWidthUnitValues.Dxa },
                        new LeftMargin { Width = "100", Type = TableWidthUnitValues.Dxa },
                        new RightMargin { Width = "100", Type = TableWidthUnitValues.Dxa }
                    )
                );

                // 表头行加背景色
                if (r == 0)
                {
                    cellProps.AppendChild(new Shading
                    {
                        Val = ShadingPatternValues.Clear,
                        Fill = TableHeaderBg
                    });
                }
                // 交替行背景色
                else if (r % 2 == 0)
                {
                    cellProps.AppendChild(new Shading
                    {
                        Val = ShadingPatternValues.Clear,
                        Fill = TableAltRowBg
                    });
                }

                cell.AppendChild(cellProps);

                // 单元格内容
                var runProps = r == 0
                    ? new RunProperties(new Bold(), new Color { Val = "FFFFFF" }, new FontSize { Val = "20" })
                    : new RunProperties(new FontSize { Val = "20" }, new Color { Val = TextColor });

                cell.AppendChild(CreateCellParagraph(cellText, runProps, r == 0));

                tableRow.AppendChild(cell);
            }

            table.AppendChild(tableRow);
        }

        body.AppendChild(table);
        body.AppendChild(new Paragraph()); // 表格后空行
    }

    /// <summary>
    /// 解析 Markdown 表格行的列内容
    /// </summary>
    private static List<string> ParseTableRow(string row)
    {
        // 移除首尾 |，按 | 分割
        var inner = row.Trim().Trim('|');
        return inner.Split('|').Select(c => c.Trim()).ToList();
    }

    #endregion
}
