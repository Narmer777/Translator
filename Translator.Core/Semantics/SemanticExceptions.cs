using System;

namespace Translator.Core.Semantics;

public enum SemanticErrorCode
{
    MultipleAssignments,
    CyclicDependency,
    InvalidLtlAssignmentValue,
    InvalidLtlTargetMismatch,
    InvalidLtlStateGuard,
    InvalidLtlBranchValues,
    MixedVariableDefinition,
    InvalidPseudoAssignment,
    InvalidPseudoSectionOrder,
    UseBeforeDefinition,
    MissingPseudoAssignment
}

/// <summary>
/// Исключение, выбрасываемое при нарушении семантических правил языка или логики спецификации.
/// </summary>
public class SemanticException : Exception
{
    public SemanticErrorCode ErrorCode { get; }
    public string VariableName { get; }
    public string ExtraInfo { get; }
    public int? Line { get; }
    public int? Column { get; }

    public SemanticException(
        SemanticErrorCode errorCode,
        string variableName,
        string extraInfo = "",
        int? line = null,
        int? column = null)
        : base(FormatMessage(errorCode, variableName, extraInfo, line, column))
    {
        ErrorCode = errorCode;
        VariableName = variableName;
        ExtraInfo = extraInfo;
        Line = line;
        Column = column;
    }

    private static string FormatMessage(
        SemanticErrorCode errorCode,
        string variable,
        string extraInfo,
        int? line,
        int? column)
    {
        string message = errorCode switch
        {
            SemanticErrorCode.MultipleAssignments =>
                $"Переменная '{variable}' имеет множественное присваивание в одном потоке выполнения.",
            SemanticErrorCode.CyclicDependency =>
                $"Обнаружена мгновенная циклическая зависимость для переменной '{variable}'.",
            SemanticErrorCode.InvalidLtlAssignmentValue =>
                $"Нарушение LTL-шаблона: в условной конструкции переменной '{variable}' должна присваиваться константа TRUE или FALSE (1 или 0).",
            SemanticErrorCode.InvalidLtlTargetMismatch =>
                $"Нарушение LTL-шаблона: ветка ELSIF переключает переменную '{variable}', хотя главная ветка IF переключает '{extraInfo}'. Обе ветки должны управлять одной переменной.",
            SemanticErrorCode.InvalidLtlStateGuard =>
                $"Нарушение LTL-шаблона: условие для переменной '{variable}' должно содержать корректную псевдопеременную состояния {extraInfo}.",
            SemanticErrorCode.InvalidLtlBranchValues =>
                $"Нарушение LTL-шаблона: ветки IF/ELSIF для переменной '{variable}' должны задавать противоположные значения TRUE и FALSE.",
            SemanticErrorCode.MixedVariableDefinition =>
                $"Переменная '{variable}' смешивает разные формы задания поведения: {extraInfo}.",
            SemanticErrorCode.InvalidPseudoAssignment =>
                $"Нарушение псевдооператорного присваивания: переменная '{variable}' должна задаваться в форме _V := V.",
            SemanticErrorCode.InvalidPseudoSectionOrder =>
                $"Нарушение псевдооператорного раздела: после присваивания '{variable}' допускаются только присваивания вида _V := V.",
            SemanticErrorCode.UseBeforeDefinition =>
                $"Нарушение порядка спецификации: вычисляемая переменная '{variable}' используется до своего определения.",
            SemanticErrorCode.MissingPseudoAssignment =>
                $"Нарушение LTL-шаблона: для state-переменной '{variable}' в конце программы должно быть присваивание _{variable} := {variable}.",
            _ => $"Неизвестная семантическая ошибка для '{variable}'."
        };

        return line.HasValue && column.HasValue
            ? $"Строка {line}, Столбец {column}: {message}"
            : message;
    }
}
