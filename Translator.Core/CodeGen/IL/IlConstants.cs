namespace Translator.Core.CodeGen;

/// <summary>
/// Строковые константы для форматирования комментариев и специальных элементов IL-кода.
/// </summary>
public static class IlKeywords
{
    public const string ProgramStart = "PROGRAM PLC_IL_PRG_TR";
    public const string IfStart = "IF        ";
    public const string ElsifStart = "ELSIF     ";
    public const string EndIf = "END_IF    ";
    public const string ParenOpen = "(";
    public const string ParenClose = ")";
    public const string TrueLiteral = "TRUE";
    public const string FalseLiteral = "FALSE";
}
