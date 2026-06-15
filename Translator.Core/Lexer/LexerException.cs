namespace Translator.Core.Lexer;

/// <summary>
/// Типы возможных лексических ошибок.
/// </summary>
public enum LexerErrorCode
{
    UnknownSymbol,
    IncompleteAssign,
    UnclosedComment,
    InvalidIdentifier
}

/// <summary>
/// Исключение, выбрасываемое при обнаружении лексических ошибок.
/// </summary>
public class LexerException : Exception
{
    public LexerErrorCode ErrorCode { get; }
    public int Line { get; }
    public int Column { get; }
    public string Details { get; }

    public LexerException(LexerErrorCode errorCode, int line, int column, string details = "")
        : base(FormatMessage(errorCode, line, column, details))
    {
        ErrorCode = errorCode;
        Line = line;
        Column = column;
        Details = details;
    }

    private static string FormatMessage(LexerErrorCode errorCode, int line, int column, string details)
    {
        return errorCode switch
        {
            LexerErrorCode.UnknownSymbol => $"Неизвестный символ '{details}' на строке {line}, столбец {column}.",
            LexerErrorCode.IncompleteAssign => $"Неожиданный символ ':' на строке {line}, столбец {column}. Возможно, вы имели в виду ':='?",
            LexerErrorCode.UnclosedComment => $"Незакрытый комментарий. Комментарий начался на строке {line}, столбец {column}.",
            LexerErrorCode.InvalidIdentifier => $"Некорректный идентификатор '{details}' на строке {line}, столбец {column}.",
            _ => $"Лексическая ошибка на строке {line}, столбец {column}."
        };
    }
}
