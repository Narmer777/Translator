using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

namespace Translator
{
    public class UiLogger
    {
        private readonly RichTextBox _console;

        public UiLogger(RichTextBox console)
        {
            _console = console;
        }

        public void Clear()
        {
            _console.Document.Blocks.Clear();
        }

        public void LogBuildStart(string targetName)
        {
            AppendText($"[{DateTime.Now:HH:mm:ss}] [RUN] {UiLocalization.Format("Log.BuildStart", targetName)}", Brushes.Gray, FontWeights.Bold);
        }

        public void LogReady()
        {
            Info(UiLocalization.Get("Log.Ready"));
        }

        public void LogStage(string message)
        {
            Info(message);
        }

        public void LogTiming(string stageName, TimeSpan elapsed, string extraInfo = "")
        {
            string suffix = string.IsNullOrWhiteSpace(extraInfo) ? string.Empty : $" | {extraInfo}";
            Info($"{stageName}: {elapsed.TotalMilliseconds:F3} ms{suffix}");
        }

        public void LogEmptyInput()
        {
            Warning(UiLocalization.Get("Log.EmptyInput"));
        }

        public void LogSuccess(string targetName, TimeSpan elapsed)
        {
            Success(UiLocalization.Format("Log.Success", targetName, elapsed.TotalMilliseconds));
        }

        public void LogLexerError(string message)
        {
            Error($"error ST0001: {message}");
        }

        public void LogSyntaxError(string message)
        {
            Error($"error ST0002: {message}");
        }

        public void LogSemanticError(string message)
        {
            Error($"error ST0003: {message}");
        }

        public void LogCodeGenError(string message)
        {
            Error($"error ST0004: {message}");
        }

        public void LogNotImplemented(string message)
        {
            Warning(UiLocalization.Format("Log.NotImplemented", message));
        }

        public void LogCriticalError(Exception ex)
        {
            Error(UiLocalization.Format("Log.CriticalError", ex.Message));
        }

        public void LogCopiedToClipboard()
        {
            Info(UiLocalization.Get("Log.Copied"));
        }

        public void LogFileLoaded(string filePath)
        {
            Info(UiLocalization.Format("Log.FileLoaded", Path.GetFileName(filePath)));
        }

        public void LogFileSaved(string filePath)
        {
            Success(UiLocalization.Format("Log.FileSaved", Path.GetFileName(filePath)));
        }

        public void LogFileError(Exception ex)
        {
            Error(UiLocalization.Format("Log.FileError", ex.Message));
        }

        public void LogWarning(string message)
        {
            Warning(message);
        }

        public void LogInputCleared()
        {
            Info(UiLocalization.Get("Log.Cleared"));
        }

        private void Info(string message)
        {
            AppendText($"[{DateTime.Now:HH:mm:ss}] [INF] {message}", Brushes.Black, FontWeights.Normal);
        }

        private void Success(string message)
        {
            AppendText($"[{DateTime.Now:HH:mm:ss}] [SUC] {message}", Brushes.DarkGreen, FontWeights.Normal);
        }

        private void Warning(string message)
        {
            AppendText($"[{DateTime.Now:HH:mm:ss}] [WRN] {message}", Brushes.DarkOrange, FontWeights.Normal);
        }

        private void Error(string message)
        {
            AppendText($"[{DateTime.Now:HH:mm:ss}] [ERR] {message}", Brushes.Red, FontWeights.Normal);
        }

        private void AppendText(string text, Brush color, FontWeight weight)
        {
            var run = new Run(text) { Foreground = color, FontWeight = weight };
            var paragraph = new Paragraph(run) { Margin = new Thickness(0, 2, 0, 2) };

            _console.Document.Blocks.Add(paragraph);
            _console.ScrollToEnd();
        }
    }
}
