using Translator.Core.Lexer;

namespace Translator.Core.Parser;

/// <summary>
/// Типы возможных синтаксических ошибок (нарушения грамматики языка ST).
/// </summary>
public enum SyntaxErrorCode
{
    ExpectedStatement,
    ExpectedIdentifier,
    ExpectedAssignAfterId,
    ExpectedSemiAfterAssignment,
    ExpectedThenAfterIf,
    ExpectedThenAfterElsif,
    ExpectedEndIf,
    ExpectedSemiAfterEndIf,
    ExpectedRightParen,
    ExpectedExpression
}

/// <summary>
/// Исключение, выбрасываемое при обнаружении синтаксических ошибок в исходном коде.
/// Формирует профессиональные сообщения в стиле промышленных компиляторов.
/// </summary>
public class SyntaxException : Exception
{
    public Token ErrorToken { get; }
    public Token? ActualToken { get; }
    public SyntaxErrorCode ErrorCode { get; }

    /// <summary>
    /// Инициализирует исключение с указанием токена, к которому привязана ошибка.
    /// </summary>
    /// <param name="token">Токен, к которому привязана ошибка (обычно предыдущий валидный токен).</param>
    /// <param name="errorCode">Код синтаксической ошибки.</param>
    /// <param name="actualToken">Токен, который был фактически встречен парсером вместо ожидаемого (опционально). Обрати внимание на знак вопроса.</param>
    public SyntaxException(Token token, SyntaxErrorCode errorCode, Token? actualToken = null)
        : base(FormatFullMessage(token, errorCode, actualToken))
    {
        ErrorToken = token;
        ActualToken = actualToken;
        ErrorCode = errorCode;
    }

    private static string FormatFullMessage(Token token, SyntaxErrorCode errorCode, Token? actualToken)
    {
        string baseMsg = FormatMessage(errorCode);

        if (actualToken != null && actualToken.Type != TokenType.EOF && actualToken != token)
        {
            return $"Строка {token.Line}, Столбец {token.Column}: {baseMsg} (Встречено: '{actualToken.Value}')";
        }

        return $"Строка {token.Line}, Столбец {token.Column}: {baseMsg}";
    }

    private static string FormatMessage(SyntaxErrorCode errorCode)
    {
        return errorCode switch
        {
            SyntaxErrorCode.ExpectedStatement => "Ожидалась инструкция (IF или присваивание)",
            SyntaxErrorCode.ExpectedIdentifier => "Ожидался идентификатор",
            SyntaxErrorCode.ExpectedAssignAfterId => $"Ожидалось '{LexerConstants.Assign}' после идентификатора",
            SyntaxErrorCode.ExpectedSemiAfterAssignment => "Ожидалась ';' после выражения присваивания",
            SyntaxErrorCode.ExpectedThenAfterIf => "Ожидалось 'THEN' после условия IF",
            SyntaxErrorCode.ExpectedThenAfterElsif => "Ожидалось 'THEN' после условия ELSIF",
            SyntaxErrorCode.ExpectedEndIf => "Ожидалось 'END_IF' для завершения условной конструкции",
            SyntaxErrorCode.ExpectedSemiAfterEndIf => "Ожидалась ';' после 'END_IF'",
            SyntaxErrorCode.ExpectedRightParen => "Ожидалась закрывающая скобка ')'",
            SyntaxErrorCode.ExpectedExpression => "Ожидалось выражение",
            _ => "Неизвестная синтаксическая ошибка"
        };
    }
}
