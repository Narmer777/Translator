using System.Collections.Generic;

namespace Translator;

public enum UiLanguage
{
    Russian,
    English
}

public static class UiLocalization
{
    private static readonly Dictionary<string, string> Russian = new()
    {
        ["Window.Title"] = "Транслятор",
        ["App.Title"] = "ТРАНСЛЯТОР",
        ["Menu.File"] = "Файл",
        ["Menu.Open"] = "Открыть файл...",
        ["Menu.Save"] = "Сохранить как...",
        ["Menu.Exit"] = "Выход",
        ["Menu.Settings"] = "Настройки",
        ["Menu.Language"] = "Язык",
        ["Menu.Language.Russian"] = "Русский",
        ["Menu.Language.English"] = "English",
        ["Menu.SemanticAnalyzer"] = "Семантический анализатор",
        ["Menu.Help"] = "Справка",
        ["Menu.About"] = "О программе...",
        ["TargetLanguage.Label"] = "Целевой язык:",
        ["Panel.Input"] = "ИСХОДНЫЙ КОД (ST)",
        ["Panel.Output"] = "РЕЗУЛЬТАТ ТРАНСЛЯЦИИ",
        ["Panel.Console"] = "Консоль",
        ["Button.Translate"] = "ТРАНСЛИРОВАТЬ",
        ["Tooltip.Translate"] = "Транслировать",
        ["Tooltip.CopyInput"] = "Скопировать",
        ["Tooltip.Clear"] = "Очистить",
        ["Tooltip.ToggleInput"] = "Развернуть",
        ["Tooltip.CopyOutput"] = "Скопировать",
        ["Tooltip.ToggleOutput"] = "Развернуть",
        ["Dialog.Open.Title"] = "Выберите файл с исходным кодом ST",
        ["Dialog.Open.Filter"] = "Файлы ST/экспорта (*.exp;*.txt)|*.exp;*.txt|Файлы экспорта CoDeSys (*.exp)|*.exp|Текстовые файлы (*.txt)|*.txt",
        ["Dialog.Save.Title"] = "Сохранить результат трансляции",
        ["Dialog.Save.Filter"] = "Файлы экспорта CoDeSys (*.exp)|*.exp|Текстовые файлы (*.txt)|*.txt|Все файлы (*.*)|*.*",
        ["Log.Ready"] = "Готов.",
        ["Log.EmptyInput"] = "Пустой вход.",
        ["Log.Copied"] = "Скопировано.",
        ["Log.FileLoaded"] = "Открыт: {0}",
        ["Log.FileSaved"] = "Сохранено: {0}",
        ["Log.FileError"] = "Ошибка файла: {0}",
        ["Log.InvalidOpenExtension"] = "Недопустимый формат файла. Разрешены только .txt и .exp.",
        ["Log.Warning.NoDataToSave"] = "Нет данных для сохранения.",
        ["Log.NotImplemented"] = "Ограничение: {0}",
        ["Log.CriticalError"] = "Критический сбой: {0}",
        ["Log.Cleared"] = "Поля очищены.",
        ["Log.BuildStart"] = "Старт -> {0}",
        ["Log.Success"] = "Готово -> {0} за {1:F3} мс",
        ["Stage.Normalize"] = "Подготовка входа...",
        ["Stage.Lexer"] = "Лексический анализ...",
        ["Stage.Parser"] = "Синтаксический анализ...",
        ["Stage.Generator.IL"] = "Генерация IL...",
        ["Stage.Generator.LD"] = "Генерация LD...",
        ["Timing.Normalize"] = "Нормализация",
        ["Timing.Lexer"] = "Лексер",
        ["Timing.Parser"] = "Парсер",
        ["Timing.Semantic"] = "Семантика",
        ["Timing.Semantic.Disabled"] = "отключено",
        ["Timing.Generator"] = "Генератор",
        ["Timing.Tokens"] = "токенов: {0}",
        ["Timing.Statements"] = "инструкций AST: {0}",
        ["Timing.Target"] = "цель: {0}",
        ["Language.Changed"] = "Язык интерфейса: {0}",
        ["Semantic.Enabled"] = "Семантический анализатор: включен",
        ["Semantic.Disabled"] = "Семантический анализатор: отключен"
    };

    private static readonly Dictionary<string, string> English = new()
    {
        ["Window.Title"] = "Translator",
        ["App.Title"] = "TRANSLATOR",
        ["Menu.File"] = "File",
        ["Menu.Open"] = "Open file...",
        ["Menu.Save"] = "Save as...",
        ["Menu.Exit"] = "Exit",
        ["Menu.Settings"] = "Settings",
        ["Menu.Language"] = "Language",
        ["Menu.Language.Russian"] = "Russian",
        ["Menu.Language.English"] = "English",
        ["Menu.SemanticAnalyzer"] = "Semantic analyzer",
        ["Menu.Help"] = "Help",
        ["Menu.About"] = "About...",
        ["TargetLanguage.Label"] = "Target language:",
        ["Panel.Input"] = "SOURCE CODE (ST)",
        ["Panel.Output"] = "TRANSLATION RESULT",
        ["Panel.Console"] = "Console",
        ["Button.Translate"] = "TRANSLATE",
        ["Tooltip.Translate"] = "Translate",
        ["Tooltip.CopyInput"] = "Copy",
        ["Tooltip.Clear"] = "Clear",
        ["Tooltip.ToggleInput"] = "Expand",
        ["Tooltip.CopyOutput"] = "Copy",
        ["Tooltip.ToggleOutput"] = "Expand",
        ["Dialog.Open.Title"] = "Select a file with ST source code",
        ["Dialog.Open.Filter"] = "ST/export files (*.exp;*.txt)|*.exp;*.txt|CoDeSys export files (*.exp)|*.exp|Text files (*.txt)|*.txt",
        ["Dialog.Save.Title"] = "Save translation result",
        ["Dialog.Save.Filter"] = "CoDeSys export files (*.exp)|*.exp|Text files (*.txt)|*.txt|All files (*.*)|*.*",
        ["Log.Ready"] = "Ready.",
        ["Log.EmptyInput"] = "Empty input.",
        ["Log.Copied"] = "Copied.",
        ["Log.FileLoaded"] = "Opened: {0}",
        ["Log.FileSaved"] = "Saved: {0}",
        ["Log.FileError"] = "File error: {0}",
        ["Log.InvalidOpenExtension"] = "Unsupported file format. Only .txt and .exp are allowed.",
        ["Log.Warning.NoDataToSave"] = "No data to save.",
        ["Log.NotImplemented"] = "Limitation: {0}",
        ["Log.CriticalError"] = "Critical failure: {0}",
        ["Log.Cleared"] = "Editors cleared.",
        ["Log.BuildStart"] = "Start -> {0}",
        ["Log.Success"] = "Done -> {0} in {1:F3} ms",
        ["Stage.Normalize"] = "Preparing input...",
        ["Stage.Lexer"] = "Lexical analysis...",
        ["Stage.Parser"] = "Syntax analysis...",
        ["Stage.Generator.IL"] = "Generating IL...",
        ["Stage.Generator.LD"] = "Generating LD...",
        ["Timing.Normalize"] = "Normalization",
        ["Timing.Lexer"] = "Lexer",
        ["Timing.Parser"] = "Parser",
        ["Timing.Semantic"] = "Semantic analyzer",
        ["Timing.Semantic.Disabled"] = "disabled",
        ["Timing.Generator"] = "Generator",
        ["Timing.Tokens"] = "tokens: {0}",
        ["Timing.Statements"] = "AST statements: {0}",
        ["Timing.Target"] = "target: {0}",
        ["Language.Changed"] = "UI language: {0}",
        ["Semantic.Enabled"] = "Semantic analyzer: enabled",
        ["Semantic.Disabled"] = "Semantic analyzer: disabled"
    };

    public static UiLanguage CurrentLanguage { get; private set; } = UiLanguage.Russian;

    public static void SetLanguage(UiLanguage language)
    {
        CurrentLanguage = language;
    }

    public static string Get(string key)
    {
        var source = CurrentLanguage == UiLanguage.Russian ? Russian : English;
        return source.TryGetValue(key, out string? value) ? value : key;
    }

    public static string Format(string key, params object[] args)
    {
        return string.Format(Get(key), args);
    }
}
