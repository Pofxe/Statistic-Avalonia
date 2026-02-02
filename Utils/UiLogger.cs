using System;
using System.Collections.ObjectModel;

namespace StepikAnalyticsDesktop.Utils;

public sealed class UiLogger
{
    public ObservableCollection<string> Lines { get; } = new();
    public int MaxLines { get; set; } = 400;

    public void Info(string message) => Write("INFO", message);
    public void Warn(string message) => Write("WARN", message);
    public void Error(Exception exception, string message) => Write("ERROR", $"{message} :: {exception.Message}");

    private void Write(string level, string message)
    {
        var line = $"{DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss} [{level}] {message}";
        Lines.Insert(0, line);
        while (Lines.Count > MaxLines)
        {
            Lines.RemoveAt(Lines.Count - 1);
        }
    }
}
