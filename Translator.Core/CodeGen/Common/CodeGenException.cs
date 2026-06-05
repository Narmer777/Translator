namespace Translator.Core.CodeGen;

public enum CodeGenErrorCode
{
    UnsupportedNode,
    InvalidLtlStructure
}

/// <summary>
/// Общие исключение для всех генераторов кода.
/// </summary>
public class CodeGenException : Exception
{
    public CodeGenErrorCode ErrorCode { get; }

    public CodeGenException(CodeGenErrorCode errorCode, string message)
        : base(message)
    {
        ErrorCode = errorCode;
    }
}
