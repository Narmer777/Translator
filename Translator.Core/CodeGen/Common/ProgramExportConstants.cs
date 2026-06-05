namespace Translator.Core.CodeGen;

/// <summary>
/// Общие константы для файлового экспорта программ и распознавания служебной оболочки.
/// </summary>
public static class ProgramExportConstants
{
    public const string HeaderNestedComments = "(* @NESTEDCOMMENTS := 'Yes' *)";
    public const string HeaderPath = "(* @PATH := '' *)";
    public const string HeaderObjectFlags = "(* @OBJECTFLAGS := '0, 8' *)";
    public const string HeaderSymFileFlags = "(* @SYMFILEFLAGS := '2048' *)";
    public const string ProgramKeyword = "PROGRAM ";
    public const string ProgramEnd = "END_PROGRAM";
    public const string VarStart = "VAR";
    public const string VarEnd = "END_VAR";
    public const string EndDeclaration = "(* @END_DECLARATION := '0' *)";
}
