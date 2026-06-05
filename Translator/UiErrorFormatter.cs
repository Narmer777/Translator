using Translator.Core.CodeGen;
using Translator.Core.Lexer;
using Translator.Core.Parser;
using Translator.Core.Semantics;

namespace Translator;

public static class UiErrorFormatter
{
    public static string Format(LexerException exception)
    {
        bool ru = UiLocalization.CurrentLanguage == UiLanguage.Russian;
        string message = exception.ErrorCode switch
        {
            LexerErrorCode.UnknownSymbol => ru
                ? $"Неизвестный символ '{exception.Details}'."
                : $"Unknown symbol '{exception.Details}'.",
            LexerErrorCode.IncompleteAssign => ru
                ? "Неожиданный символ ':'. Возможно, вы имели в виду ':='?"
                : "Unexpected ':' character. Did you mean ':='?",
            LexerErrorCode.UnclosedComment => ru
                ? "Незакрытый комментарий."
                : "Unclosed comment.",
            _ => ru ? "Лексическая ошибка." : "Lexical error."
        };

        return WithLocation(message, exception.Line, exception.Column, ru);
    }

    public static string Format(SyntaxException exception)
    {
        bool ru = UiLocalization.CurrentLanguage == UiLanguage.Russian;
        string message = exception.ErrorCode switch
        {
            SyntaxErrorCode.ExpectedStatement => ru
                ? "Ожидалась инструкция: IF или присваивание."
                : "Expected a statement: IF or assignment.",
            SyntaxErrorCode.ExpectedIdentifier => ru
                ? "Ожидался идентификатор."
                : "Expected an identifier.",
            SyntaxErrorCode.ExpectedAssignAfterId => ru
                ? "Ожидалось ':=' после идентификатора."
                : "Expected ':=' after identifier.",
            SyntaxErrorCode.ExpectedSemiAfterAssignment => ru
                ? "Ожидалась ';' после выражения присваивания."
                : "Expected ';' after assignment expression.",
            SyntaxErrorCode.ExpectedThenAfterIf => ru
                ? "Ожидалось 'THEN' после условия IF."
                : "Expected 'THEN' after IF condition.",
            SyntaxErrorCode.ExpectedThenAfterElsif => ru
                ? "Ожидалось 'THEN' после условия ELSIF."
                : "Expected 'THEN' after ELSIF condition.",
            SyntaxErrorCode.ExpectedEndIf => ru
                ? "Ожидалось 'END_IF' для завершения условной конструкции."
                : "Expected 'END_IF' to close the conditional statement.",
            SyntaxErrorCode.ExpectedSemiAfterEndIf => ru
                ? "Ожидалась ';' после 'END_IF'."
                : "Expected ';' after 'END_IF'.",
            SyntaxErrorCode.ExpectedRightParen => ru
                ? "Ожидалась закрывающая скобка ')'."
                : "Expected closing parenthesis ')'.",
            SyntaxErrorCode.ExpectedExpression => ru
                ? "Ожидалось выражение."
                : "Expected an expression.",
            _ => ru ? "Синтаксическая ошибка." : "Syntax error."
        };

        if (exception.ActualToken != null && exception.ActualToken.Type != TokenType.EOF)
        {
            message += ru
                ? $" Встречено: '{exception.ActualToken.Value}'."
                : $" Found: '{exception.ActualToken.Value}'.";
        }

        return WithLocation(message, exception.ErrorToken.Line, exception.ErrorToken.Column, ru);
    }

    public static string Format(SemanticException exception)
    {
        bool ru = UiLocalization.CurrentLanguage == UiLanguage.Russian;
        string message = exception.ErrorCode switch
        {
            SemanticErrorCode.MultipleAssignments => ru
                ? $"Переменная '{exception.VariableName}' присваивается более одного раза."
                : $"Variable '{exception.VariableName}' is assigned more than once.",
            SemanticErrorCode.CyclicDependency => ru
                ? $"Обнаружена мгновенная циклическая зависимость для переменной '{exception.VariableName}'."
                : $"Instantaneous cyclic dependency detected for variable '{exception.VariableName}'.",
            SemanticErrorCode.InvalidLtlAssignmentValue => ru
                ? $"В условной LTL-конструкции переменной '{exception.VariableName}' должна присваиваться только константа TRUE или FALSE."
                : $"In an LTL conditional statement, variable '{exception.VariableName}' must be assigned only TRUE or FALSE.",
            SemanticErrorCode.InvalidLtlTargetMismatch => ru
                ? $"Ветка ELSIF переключает '{exception.VariableName}', хотя IF переключает '{exception.ExtraInfo}'."
                : $"ELSIF branch switches '{exception.VariableName}', but IF branch switches '{exception.ExtraInfo}'.",
            SemanticErrorCode.InvalidLtlStateGuard => ru
                ? $"Условие для переменной '{exception.VariableName}' должно содержать корректную псевдопеременную состояния {exception.ExtraInfo}."
                : $"Condition for variable '{exception.VariableName}' must contain the correct state pseudo-variable {exception.ExtraInfo}.",
            SemanticErrorCode.InvalidLtlBranchValues => ru
                ? $"Ветки IF/ELSIF для переменной '{exception.VariableName}' должны задавать противоположные значения TRUE и FALSE."
                : $"IF/ELSIF branches for variable '{exception.VariableName}' must assign opposite TRUE/FALSE values.",
            SemanticErrorCode.MixedVariableDefinition => ru
                ? $"Переменная '{exception.VariableName}' смешивает разные формы задания поведения: {exception.ExtraInfo}."
                : $"Variable '{exception.VariableName}' mixes different behavior definition forms: {exception.ExtraInfo}.",
            SemanticErrorCode.InvalidPseudoAssignment => ru
                ? $"Псевдооператорная переменная '{exception.VariableName}' должна задаваться в форме _V := V."
                : $"Pseudo-operator variable '{exception.VariableName}' must be assigned as _V := V.",
            SemanticErrorCode.InvalidPseudoSectionOrder => ru
                ? $"После псевдооператорного присваивания '{exception.VariableName}' допускаются только присваивания вида _V := V."
                : $"After pseudo-operator assignment '{exception.VariableName}', only _V := V assignments are allowed.",
            SemanticErrorCode.UseBeforeDefinition => ru
                ? $"Вычисляемая переменная '{exception.VariableName}' используется до своего определения."
                : $"Program-defined variable '{exception.VariableName}' is used before its definition.",
            SemanticErrorCode.MissingPseudoAssignment => ru
                ? $"Для state-переменной '{exception.VariableName}' в конце программы должно быть присваивание _{exception.VariableName} := {exception.VariableName}."
                : $"State variable '{exception.VariableName}' must have final assignment _{exception.VariableName} := {exception.VariableName}.",
            _ => ru ? "Семантическая ошибка." : "Semantic error."
        };

        return WithLocation(message, exception.Line, exception.Column, ru);
    }

    public static string Format(CodeGenException exception)
    {
        bool ru = UiLocalization.CurrentLanguage == UiLanguage.Russian;
        return exception.ErrorCode switch
        {
            CodeGenErrorCode.UnsupportedNode => ru
                ? "Генератор кода встретил неподдерживаемый узел AST."
                : "The code generator encountered an unsupported AST node.",
            CodeGenErrorCode.InvalidLtlStructure => ru
                ? "Генератор кода получил некорректную LTL-структуру."
                : "The code generator received an invalid LTL structure.",
            _ => ru ? "Ошибка генерации кода." : "Code generation error."
        };
    }

    private static string WithLocation(string message, int? line, int? column, bool ru)
    {
        return line.HasValue && column.HasValue
            ? ru
                ? $"Строка {line}, колонка {column}: {message}"
                : $"Line {line}, column {column}: {message}"
            : message;
    }
}
