using System;
using System.Collections.Generic;
using Microsoft.Win32;
using System.IO;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;
using System.Xml;
using ICSharpCode.AvalonEdit.Highlighting;
using ICSharpCode.AvalonEdit.Highlighting.Xshd;
using Translator.Core.Lexer;
using Translator.Core.Parser;
using Translator.Core.CodeGen;
using Translator.Core.Ast;
using Translator.Core.Semantics;

namespace Translator
{
    public enum TargetLanguage
    {
        IL,
        LD,
    }

    public partial class MainWindow : Window
    {
        private sealed record TranslationRunResult(
            string CompiledCode,
            int TokenCount,
            int StatementCount,
            TimeSpan NormalizeTime,
            TimeSpan LexerTime,
            TimeSpan ParserTime,
            TimeSpan SemanticTime,
            TimeSpan GeneratorTime);

        private const string StRulesFileName = "ST.xshd";
        private const string IlRulesFileName = "IL.xshd";
        private bool _isInputMaximized = false;
        private bool _isOutputMaximized = false;
        private GridLength _originalConsoleHeight = new GridLength(220);
        private GridLength _originalInputWidth = new GridLength(1, GridUnitType.Star);
        private GridLength _originalOutputWidth = new GridLength(1, GridUnitType.Star);
        private readonly string MaximizeIconData = "M4,4H20V20H4V4M6,8V18H18V8H6Z";
        private readonly string RestoreIconData = "M4,8H8V4H20V16H16V20H4V8M16,8V14H18V6H10V8H16M6,10V18H14V10H6Z";
        private bool _semanticAnalysisEnabled = true;
        private readonly UiLogger _logger;

        public MainWindow()
        {
            InitializeComponent();
            LoadSyntaxHighlighting();

            _logger = new UiLogger(LogConsole);
            SetUiLanguage(UiLanguage.Russian, logChange: false);
            _logger.LogReady();
        }

        private async void TranslateButton_Click(object sender, RoutedEventArgs e)
        {
            OutputEditor.Clear();

            string sourceCode = InputEditor.Text;

            if (string.IsNullOrWhiteSpace(sourceCode))
            {
                _logger.LogEmptyInput();
                return;
            }

            TargetLanguage targetLang = GetSelectedTargetLanguage();
            string targetName = targetLang == TargetLanguage.IL ? "IL" : "LD";

            TranslateButton.IsEnabled = false;
            _logger.LogBuildStart(targetName);
            await Task.Yield();

            try
            {
                await RunTranslationPipelineAsync(sourceCode, targetLang);
            }
            finally
            {
                TranslateButton.IsEnabled = true;
            }
        }

        private TargetLanguage GetSelectedTargetLanguage()
        {
            if (RbLdTarget.IsChecked == true) return TargetLanguage.LD;
            return TargetLanguage.IL;
        }

        private async Task RunTranslationPipelineAsync(string sourceCode, TargetLanguage target)
        {
            try
            {
                string targetName = target == TargetLanguage.IL ? "IL" : "LD";
                DateTime startedAt = DateTime.UtcNow;
                var progress = new Progress<string>(message => _logger.LogStage(message));

                bool semanticAnalysisEnabled = _semanticAnalysisEnabled;
                TranslationRunResult result = await Task.Run(() => TranslateSourceCode(sourceCode, target, semanticAnalysisEnabled, progress));
                OutputEditor.Text = result.CompiledCode;
                _logger.LogSuccess(targetName, DateTime.UtcNow - startedAt);
            }
            catch (LexerException lexEx) { _logger.LogLexerError(UiErrorFormatter.Format(lexEx)); }
            catch (SyntaxException synEx) { _logger.LogSyntaxError(UiErrorFormatter.Format(synEx)); }
            catch (SemanticException semEx) { _logger.LogSemanticError(UiErrorFormatter.Format(semEx)); }
            catch (CodeGenException codeGenEx) { _logger.LogCodeGenError(UiErrorFormatter.Format(codeGenEx)); }
            catch (NotImplementedException notImplEx) { _logger.LogNotImplemented(notImplEx.Message); }
            catch (Exception ex) { _logger.LogCriticalError(ex); }
        }

        private TranslationRunResult TranslateSourceCode(string sourceCode, TargetLanguage target, bool semanticAnalysisEnabled, IProgress<string> progress)
        {
            var stopwatch = new Stopwatch();

            stopwatch.Start();
            sourceCode = NormalizeImportedSourceCode(sourceCode);
            stopwatch.Stop();
            TimeSpan normalizeTime = stopwatch.Elapsed;
            progress.Report($"{UiLocalization.Get("Timing.Normalize")}: {normalizeTime.TotalMilliseconds:F3} ms");

            stopwatch.Restart();
            var tokenizer = new Tokenizer(sourceCode);
            var tokens = tokenizer.Tokenize();
            stopwatch.Stop();
            TimeSpan lexerTime = stopwatch.Elapsed;
            progress.Report($"{UiLocalization.Get("Timing.Lexer")}: {lexerTime.TotalMilliseconds:F3} ms | {UiLocalization.Format("Timing.Tokens", tokens.Count)}");

            stopwatch.Restart();
            var parser = new Parser(tokens);
            ProgramNode program = parser.Parse();
            stopwatch.Stop();
            TimeSpan parserTime = stopwatch.Elapsed;
            progress.Report($"{UiLocalization.Get("Timing.Parser")}: {parserTime.TotalMilliseconds:F3} ms | {UiLocalization.Format("Timing.Statements", program.Statements.Count)}");

            stopwatch.Restart();
            if (semanticAnalysisEnabled)
            {
                var analyzer = new SemanticAnalyzer();
                analyzer.Analyze(program);
            }
            stopwatch.Stop();
            TimeSpan semanticTime = stopwatch.Elapsed;
            string semanticStatus = semanticAnalysisEnabled
                ? $"{semanticTime.TotalMilliseconds:F3} ms"
                : UiLocalization.Get("Timing.Semantic.Disabled");
            progress.Report($"{UiLocalization.Get("Timing.Semantic")}: {semanticStatus}");

            stopwatch.Restart();
            string compiledCode = GenerateTargetCode(program, target);
            stopwatch.Stop();
            TimeSpan generatorTime = stopwatch.Elapsed;
            progress.Report($"{UiLocalization.Get("Timing.Generator")}: {generatorTime.TotalMilliseconds:F3} ms | {UiLocalization.Format("Timing.Target", target == TargetLanguage.IL ? "IL" : "LD")}");

            return new TranslationRunResult(
                compiledCode,
                tokens.Count,
                program.Statements.Count,
                normalizeTime,
                lexerTime,
                parserTime,
                semanticTime,
                generatorTime);
        }

        private string GenerateTargetCode(ProgramNode program, TargetLanguage target)
        {
            ICodeGenerator generator = CodeGeneratorFactory(target);
            return generator.Generate(program);
        }

        private ICodeGenerator CodeGeneratorFactory(TargetLanguage target)
        {
            return target switch
            {
                TargetLanguage.IL => new IlCodeGenerator(),
                TargetLanguage.LD => new LdCodeGenerator(),
                _ => throw new ArgumentOutOfRangeException(nameof(target), "Unknown target language.")
            };
        }

        private void LoadSyntaxHighlighting()
        {
            try
            {
                var assembly = Assembly.GetExecutingAssembly();
                var resourceNames = assembly.GetManifestResourceNames();

                string? stResourceName = resourceNames.FirstOrDefault(r => r.EndsWith(StRulesFileName));
                string? ilResourceName = resourceNames.FirstOrDefault(r => r.EndsWith(IlRulesFileName));

                if (stResourceName != null)
                {
                    using Stream? s = assembly.GetManifestResourceStream(stResourceName);
                    using XmlReader reader = new XmlTextReader(s!);
                    InputEditor.SyntaxHighlighting = HighlightingLoader.Load(reader, HighlightingManager.Instance);
                }

                if (ilResourceName != null)
                {
                    using Stream? s = assembly.GetManifestResourceStream(ilResourceName);
                    using XmlReader reader = new XmlTextReader(s!);
                    OutputEditor.SyntaxHighlighting = HighlightingLoader.Load(reader, HighlightingManager.Instance);
                }
            }
            catch { }
        }

        private void CopyButton_Click(object sender, RoutedEventArgs e)
        {
            string textToCopy = OutputEditor.Text;

            if (!string.IsNullOrEmpty(textToCopy))
            {
                Clipboard.SetText(textToCopy);
                _logger.LogCopiedToClipboard();
            }
        }

        private void MenuOpenFile_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                OpenFileDialog openFileDialog = new OpenFileDialog
                {
                    Title = UiLocalization.Get("Dialog.Open.Title"),
                    Filter = UiLocalization.Get("Dialog.Open.Filter")
                };

                if (openFileDialog.ShowDialog() == true)
                {
                    if (!IsSupportedOpenFile(openFileDialog.FileName))
                    {
                        _logger.LogWarning(UiLocalization.Get("Log.InvalidOpenExtension"));
                        return;
                    }

                    string sourceCode = File.ReadAllText(openFileDialog.FileName);
                    InputEditor.Text = NormalizeImportedSourceCode(sourceCode);
                    _logger.LogFileLoaded(openFileDialog.FileName);
                }
            }
            catch (Exception ex)
            {
                _logger.LogFileError(ex);
            }
        }

        private static bool IsSupportedOpenFile(string fileName)
        {
            string extension = Path.GetExtension(fileName);

            return extension.Equals(".txt", StringComparison.OrdinalIgnoreCase)
                   || extension.Equals(".exp", StringComparison.OrdinalIgnoreCase);
        }

        private void MenuSaveFile_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(OutputEditor.Text))
                {
                    _logger.LogWarning(UiLocalization.Get("Log.Warning.NoDataToSave"));
                    return;
                }

                SaveFileDialog saveFileDialog = new SaveFileDialog
                {
                    Title = UiLocalization.Get("Dialog.Save.Title"),
                    Filter = UiLocalization.Get("Dialog.Save.Filter"),
                    DefaultExt = ".exp",
                    FileName = GetSelectedTargetLanguage() == TargetLanguage.LD ? "PLC_LD_PRG_TR.exp" : "PLC_IL_PRG_TR.exp"
                };

                if (saveFileDialog.ShowDialog() == true)
                {
                    File.WriteAllText(saveFileDialog.FileName, OutputEditor.Text);
                    _logger.LogFileSaved(saveFileDialog.FileName);
                }
            }
            catch (Exception ex)
            {
                _logger.LogFileError(ex);
            }
        }

        private void ClearButton_Click(object sender, RoutedEventArgs e)
        {
            InputEditor.Clear();
            OutputEditor.Clear();
            _logger.LogInputCleared();
        }

        private void MenuExit_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
        }

        private void CopyStButton_Click(object sender, RoutedEventArgs e)
        {
            string textToCopy = InputEditor.Text;

            if (!string.IsNullOrEmpty(textToCopy))
            {
                Clipboard.SetText(textToCopy);
                _logger.LogCopiedToClipboard();
            }
        }

        private void MenuLanguageRussian_Click(object sender, RoutedEventArgs e)
        {
            SetUiLanguage(UiLanguage.Russian);
        }

        private void MenuLanguageEnglish_Click(object sender, RoutedEventArgs e)
        {
            SetUiLanguage(UiLanguage.English);
        }

        private void MenuSemanticAnalyzer_Click(object sender, RoutedEventArgs e)
        {
            _semanticAnalysisEnabled = MenuSemanticAnalyzer.IsChecked == true;
            _logger.LogStage(UiLocalization.Get(_semanticAnalysisEnabled
                ? "Semantic.Enabled"
                : "Semantic.Disabled"));
        }

        private void SetUiLanguage(UiLanguage language, bool logChange = true)
        {
            UiLocalization.SetLanguage(language);
            ApplyLocalization();

            if (logChange)
            {
                string languageName = language == UiLanguage.Russian
                    ? UiLocalization.Get("Menu.Language.Russian")
                    : UiLocalization.Get("Menu.Language.English");

                _logger.LogStage(UiLocalization.Format("Language.Changed", languageName));
            }
        }

        private void ApplyLocalization()
        {
            Title = UiLocalization.Get("Window.Title");
            AppTitleText.Text = UiLocalization.Get("App.Title");

            MenuFileRoot.Header = UiLocalization.Get("Menu.File");
            MenuOpenFile.Header = UiLocalization.Get("Menu.Open");
            MenuSaveFile.Header = UiLocalization.Get("Menu.Save");
            MenuExit.Header = UiLocalization.Get("Menu.Exit");
            MenuSettingsRoot.Header = UiLocalization.Get("Menu.Settings");
            MenuLanguageRoot.Header = UiLocalization.Get("Menu.Language");
            MenuLanguageRussian.Header = UiLocalization.Get("Menu.Language.Russian");
            MenuLanguageEnglish.Header = UiLocalization.Get("Menu.Language.English");
            MenuSemanticAnalyzer.Header = UiLocalization.Get("Menu.SemanticAnalyzer");
            MenuHelpRoot.Header = UiLocalization.Get("Menu.Help");
            MenuAbout.Header = UiLocalization.Get("Menu.About");

            TargetLanguageText.Text = UiLocalization.Get("TargetLanguage.Label");
            InputHeaderText.Text = UiLocalization.Get("Panel.Input");
            OutputHeaderText.Text = UiLocalization.Get("Panel.Output");
            ConsoleHeaderText.Text = UiLocalization.Get("Panel.Console");
            TranslateButton.Content = UiLocalization.Get("Button.Translate");
            TranslateButton.ToolTip = UiLocalization.Get("Tooltip.Translate");

            CopyStButton.ToolTip = UiLocalization.Get("Tooltip.CopyInput");
            ClearButton.ToolTip = UiLocalization.Get("Tooltip.Clear");
            MaximizeInputButton.ToolTip = UiLocalization.Get("Tooltip.ToggleInput");
            CopyButton.ToolTip = UiLocalization.Get("Tooltip.CopyOutput");
            MaximizeOutputButton.ToolTip = UiLocalization.Get("Tooltip.ToggleOutput");

            MenuLanguageRussian.IsChecked = UiLocalization.CurrentLanguage == UiLanguage.Russian;
            MenuLanguageEnglish.IsChecked = UiLocalization.CurrentLanguage == UiLanguage.English;
            MenuSemanticAnalyzer.IsChecked = _semanticAnalysisEnabled;
        }

        private void MaximizeInputButton_Click(object sender, RoutedEventArgs e)
        {
            _isInputMaximized = !_isInputMaximized;

            if (_isInputMaximized)
            {
                if (OutputColumn.Width.Value > 0)
                {
                    _originalInputWidth = InputColumn.Width;
                    _originalOutputWidth = OutputColumn.Width;
                }
                OutputColumn.Width = new GridLength(0);
                EditorSplitterColumn.Width = new GridLength(0);
                HideSecondaryPanels();
                MaximizeInputIcon.Data = System.Windows.Media.Geometry.Parse(RestoreIconData);
            }
            else
            {
                InputColumn.Width = _originalInputWidth;
                OutputColumn.Width = _originalOutputWidth;
                EditorSplitterColumn.Width = new GridLength(15);
                RestoreSecondaryPanels();
                MaximizeInputIcon.Data = System.Windows.Media.Geometry.Parse(MaximizeIconData);
            }
        }

        private void MaximizeOutputButton_Click(object sender, RoutedEventArgs e)
        {
            _isOutputMaximized = !_isOutputMaximized;

            if (_isOutputMaximized)
            {
                if (InputColumn.Width.Value > 0)
                {
                    _originalInputWidth = InputColumn.Width;
                    _originalOutputWidth = OutputColumn.Width;
                }

                InputColumn.Width = new GridLength(0);
                EditorSplitterColumn.Width = new GridLength(0);
                HideSecondaryPanels();
                MaximizeOutputIcon.Data = System.Windows.Media.Geometry.Parse(RestoreIconData);
            }
            else
            {
                InputColumn.Width = _originalInputWidth;
                OutputColumn.Width = _originalOutputWidth;
                EditorSplitterColumn.Width = new GridLength(15);
                RestoreSecondaryPanels();
                MaximizeOutputIcon.Data = System.Windows.Media.Geometry.Parse(MaximizeIconData);
            }
        }

        private void HideSecondaryPanels()
        {
            MenuRow.Height = new GridLength(0);
            MenuBorder.Visibility = Visibility.Collapsed;
            SettingsRow.Height = new GridLength(0);
            SettingsBorder.Visibility = Visibility.Collapsed;
            if (ConsoleRow.Height.Value > 0)
                _originalConsoleHeight = ConsoleRow.Height;
            ConsoleSplitterRow.Height = new GridLength(0);
            ConsoleSplitter.Visibility = Visibility.Collapsed;
            ConsoleRow.Height = new GridLength(0);
            ConsoleArea.Visibility = Visibility.Collapsed;
        }

        private void RestoreSecondaryPanels()
        {
            MenuRow.Height = GridLength.Auto;
            MenuBorder.Visibility = Visibility.Visible;
            SettingsRow.Height = GridLength.Auto;
            SettingsBorder.Visibility = Visibility.Visible;
            ConsoleSplitterRow.Height = new GridLength(5);
            ConsoleSplitter.Visibility = Visibility.Visible;
            ConsoleRow.Height = _originalConsoleHeight;
            ConsoleArea.Visibility = Visibility.Visible;
        }

        private void TopBar_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (e.ClickCount == 2)
            {
                if (WindowState == WindowState.Normal)
                    WindowState = WindowState.Maximized;
                else
                    WindowState = WindowState.Normal;
            }
            else
            {
                DragMove();
            }
        }

        private void MinimizeWindow_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState.Minimized;
        }

        private void MaximizeWindow_Click(object sender, RoutedEventArgs e)
        {
            if (WindowState == WindowState.Normal)
                WindowState = WindowState.Maximized;
            else
                WindowState = WindowState.Normal;
        }

        private void CloseWindow_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
        }

        private static string NormalizeImportedSourceCode(string sourceCode)
        {
            if (string.IsNullOrWhiteSpace(sourceCode))
            {
                return string.Empty;
            }

            List<string> lines = sourceCode
                .Replace("\r\n", "\n")
                .Replace('\r', '\n')
                .Split('\n')
                .ToList();

            int start = 0;
            while (start < lines.Count && string.IsNullOrWhiteSpace(lines[start]))
            {
                start++;
            }

            while (start < lines.Count && IsMetadataLine(lines[start]))
            {
                start++;
            }

            if (start < lines.Count && lines[start].TrimStart().StartsWith(ProgramExportConstants.ProgramKeyword, StringComparison.OrdinalIgnoreCase))
            {
                start++;

                while (start < lines.Count && string.IsNullOrWhiteSpace(lines[start]))
                {
                    start++;
                }

                if (start < lines.Count && string.Equals(lines[start].Trim(), ProgramExportConstants.VarStart, StringComparison.OrdinalIgnoreCase))
                {
                    start++;
                    while (start < lines.Count && !string.Equals(lines[start].Trim(), ProgramExportConstants.VarEnd, StringComparison.OrdinalIgnoreCase))
                    {
                        start++;
                    }

                    if (start < lines.Count)
                    {
                        start++;
                    }
                }

                while (start < lines.Count &&
                       (string.IsNullOrWhiteSpace(lines[start]) ||
                        IsMetadataLine(lines[start]) ||
                        string.Equals(lines[start].Trim(), LdConstants.LdBody, StringComparison.OrdinalIgnoreCase)))
                {
                    start++;
                }
            }

            int end = lines.Count - 1;
            while (end >= start && string.IsNullOrWhiteSpace(lines[end]))
            {
                end--;
            }

            while (end >= start && IsMetadataLine(lines[end]))
            {
                end--;
            }

            if (end >= start && string.Equals(lines[end].Trim(), ProgramExportConstants.ProgramEnd, StringComparison.OrdinalIgnoreCase))
            {
                end--;
            }

            while (end >= start && string.IsNullOrWhiteSpace(lines[end]))
            {
                end--;
            }

            if (end < start)
            {
                return string.Empty;
            }

            return string.Join(Environment.NewLine, lines.Skip(start).Take(end - start + 1)).Trim();
        }

        private static bool IsMetadataLine(string line)
        {
            string trimmed = line.Trim();
            return trimmed.StartsWith("(* @", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(trimmed, ProgramExportConstants.EndDeclaration, StringComparison.OrdinalIgnoreCase);
        }
    }
}
