using EapWorkAssistant.Services;
using Xunit;

namespace EapWorkAssistant.Tests;

/// <summary>CSV 状态机解析器（ExportService.ParseCsvRows / EscapeCsv）的纯逻辑单测。</summary>
public class CsvParserTests
{
    [Fact]
    public void ParseCsvRows_SimpleRow_ReturnsFields()
    {
        var rows = ExportService.ParseCsvRows("a,b,c");
        Assert.Single(rows);
        Assert.Equal(3, rows[0].Count);
        Assert.Equal("a", rows[0][0]);
        Assert.Equal("c", rows[0][2]);
    }

    [Fact]
    public void ParseCsvRows_QuotedComma_KeepsField()
    {
        var rows = ExportService.ParseCsvRows("\"a,b\",c");
        Assert.Equal("a,b", rows[0][0]);
        Assert.Equal("c", rows[0][1]);
    }

    [Fact]
    public void ParseCsvRows_QuotedNewline_KeepsFieldOnOneRow()
    {
        var rows = ExportService.ParseCsvRows("\"line1\nline2\",x");
        Assert.Single(rows);
        Assert.Equal("line1\nline2", rows[0][0]);
        Assert.Equal("x", rows[0][1]);
    }

    [Fact]
    public void ParseCsvRows_EscapedQuote_Unescapes()
    {
        var rows = ExportService.ParseCsvRows("\"he said \"\"hi\"\"\",end");
        Assert.Equal("he said \"hi\"", rows[0][0]);
        Assert.Equal("end", rows[0][1]);
    }

    [Fact]
    public void EscapeCsv_CommasQuotesNewlines_AreQuoted()
    {
        Assert.Equal("\"a,b\"", ExportService.EscapeCsv("a,b"));
        Assert.Equal("\"a\"\"b\"", ExportService.EscapeCsv("a\"b"));
        Assert.Equal("\"a\nb\"", ExportService.EscapeCsv("a\nb"));
    }

    [Fact]
    public void EscapeCsv_SafeString_ReturnedAsIs()
    {
        Assert.Equal("hello", ExportService.EscapeCsv("hello"));
        Assert.Equal("", ExportService.EscapeCsv(""));
        Assert.Equal("", ExportService.EscapeCsv(null));
    }

    [Fact]
    public void RoundTrip_EscapeThenParse_PreservesValue()
    {
        var original = "hello, \"world\"\nline2";
        var escaped = ExportService.EscapeCsv(original);
        var rows = ExportService.ParseCsvRows(escaped);
        Assert.Equal(original, rows[0][0]);
    }
}
