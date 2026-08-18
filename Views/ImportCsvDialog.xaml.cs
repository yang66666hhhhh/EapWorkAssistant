using System.Collections.Generic;
using System.Windows;
using EapWorkAssistant.Helpers;
using EapWorkAssistant.Services;

namespace EapWorkAssistant.Views;

public partial class ImportCsvDialog : Window
{
    /// <summary>用户选择的导入模式；取消时为默认值（不会被读取）。</summary>
    public ImportMode SelectedMode { get; private set; } = ImportMode.SkipDuplicate;

    public ImportCsvDialog(ImportCsvDialogModel model)
    {
        DataContext = model;
        InitializeComponent();

        SummaryText.Text = $"共解析 {model.TotalParsed} 条，有效 {model.ValidCount} 条"
                           + (model.SkippedReasons.Count > 0
                               ? $"，将跳过 {model.SkippedReasons.Count} 条异常记录"
                               : "");

        var reasons = model.SkippedReasons;
        SkippedText.Text = reasons.Count == 0
            ? "无异常记录"
            : string.Join("\n", reasons.Take(5))
              + (reasons.Count > 5 ? $"\n...及其他 {reasons.Count - 5} 条" : "");

        ConfigWarningText.Text = model.ConfigWarnings.Count == 0
            ? ""
            : string.Join("\n", model.ConfigWarnings);

        Loaded += (_, _) => ConfirmButton.Focus();
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        if (Owner is Window owner)
        {
            Width = owner.ActualWidth;
            Height = owner.ActualHeight;
            Left = owner.Left;
            Top = owner.Top;
        }
    }

    private void Mode_Checked(object sender, RoutedEventArgs e)
    {
        if (sender is System.Windows.Controls.RadioButton rb && rb.Tag is string tag)
        {
            SelectedMode = tag switch
            {
                "Overwrite" => ImportMode.Overwrite,
                "Append" => ImportMode.Append,
                _ => ImportMode.SkipDuplicate
            };
        }
    }

    private void ConfirmButton_Click(object sender, RoutedEventArgs e) => DialogResult = true;

    private void CancelButton_Click(object sender, RoutedEventArgs e) => DialogResult = false;
}
