using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

namespace EapWorkAssistant.Helpers;

/// <summary>
/// 附加属性：在 TextBlock 中实现搜索关键词高亮。
/// 用法：local:Highlight.Text="{Binding Content}" local:Highlight.Keyword="{Binding Keyword}"
/// </summary>
public static class Highlight
{
    public static readonly DependencyProperty TextProperty =
        DependencyProperty.RegisterAttached("Text", typeof(string), typeof(Highlight),
            new PropertyMetadata(string.Empty, OnPropertyChanged));

    public static readonly DependencyProperty KeywordProperty =
        DependencyProperty.RegisterAttached("Keyword", typeof(string), typeof(Highlight),
            new PropertyMetadata(string.Empty, OnPropertyChanged));

    public static void SetText(DependencyObject obj, string value) => obj.SetValue(TextProperty, value);
    public static string GetText(DependencyObject obj) => (string)obj.GetValue(TextProperty);

    public static void SetKeyword(DependencyObject obj, string value) => obj.SetValue(KeywordProperty, value);
    public static string GetKeyword(DependencyObject obj) => (string)obj.GetValue(KeywordProperty);

    private static void OnPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not TextBlock textBlock) return;
        textBlock.Inlines.Clear();

        var text = GetText(d);
        var keyword = GetKeyword(d);

        if (string.IsNullOrEmpty(text))
        {
            textBlock.Text = "";
            return;
        }

        if (string.IsNullOrEmpty(keyword))
        {
            textBlock.Text = text;
            return;
        }

        var highlightBrush = Application.Current.TryFindResource("PrimaryBrush") as Brush
            ?? Brushes.Orange;

        var lowerText = text.ToLower();
        var lowerKw = keyword.ToLower();
        var lastIndex = 0;

        while (lastIndex < text.Length)
        {
            var matchIndex = lowerText.IndexOf(lowerKw, lastIndex, StringComparison.Ordinal);
            if (matchIndex < 0) break;

            if (matchIndex > lastIndex)
                textBlock.Inlines.Add(new Run(text[lastIndex..matchIndex]));

            textBlock.Inlines.Add(new Run(text[matchIndex..(matchIndex + keyword.Length)])
            {
                Foreground = highlightBrush,
                FontWeight = FontWeights.SemiBold
            });

            lastIndex = matchIndex + keyword.Length;
        }

        if (lastIndex < text.Length)
            textBlock.Inlines.Add(new Run(text[lastIndex..]));
    }
}
