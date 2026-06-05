using System;
using System.Collections.Generic;
using System.Text;

namespace Translator.Core.Lexer;

/// <summary>
/// Лексический анализатор (токенизатор) для языка ST. 
/// Преобразует исходный текст программы в последовательность токенов.
/// </summary>
public class Tokenizer
{
    private string Input { get; init; }
    private int Position { get; set; } = 0;
    private int Line { get; set; } = 1;
    private int Column { get; set; } = 1;

    /// <summary>
    /// Инициализирует новый экземпляр токенизатора с заданным исходным кодом.
    /// </summary>
    /// <param name="input">Исходный код программы на языке ST для анализа.</param>
    public Tokenizer(string input)
    {
        Input = input ?? string.Empty;
    }

    /// <summary>
    /// Выполняет лексический анализ входного текста.
    /// </summary>
    /// <returns>Список токенов, извлеченных из исходного кода. Последним токеном всегда является EOF.</returns>
    public List<Token> Tokenize()
    {
        var tokens = new List<Token>();

        while (!IsAtEnd())
        {
            char c = Peek();

            if (char.IsWhiteSpace(c))
            {
                Advance();
                continue;
            }

            if (c == LexerConstants.LParen && PeekNext() == LexerConstants.Asterisk)
            {
                SkipComment(Line, Column);
                continue;
            }

            int startLine = Line;
            int startColumn = Column;

            if (char.IsLetter(c) || c == LexerConstants.Underscore)
            {
                tokens.Add(ReadIdentifierOrKeyword(startLine, startColumn));
            }
            else if (char.IsDigit(c))
            {
                tokens.Add(ReadNumber(startLine, startColumn));
            }
            else
            {
                tokens.Add(ReadSymbol(startLine, startColumn));
            }
        }

        tokens.Add(new Token(TokenType.EOF, string.Empty, Line, Column));
        return tokens;
    }

    /// <summary>
    /// Считывает последовательность букв, цифр, подчеркиваний и точек. 
    /// Определяет, является ли прочитанное слово ключевым словом или обычным идентификатором.
    /// </summary>
    /// <param name="startLine">Строка, на которой начался токен.</param>
    /// <param name="startColumn">Столбец, на котором начался токен.</param>
    /// <returns>Токен ключевого слова или идентификатора(ID).</returns>
    private Token ReadIdentifierOrKeyword(int startLine, int startColumn)
    {
        var sb = new StringBuilder();

        while (!IsAtEnd() && (char.IsLetterOrDigit(Peek()) || Peek() == LexerConstants.Underscore || Peek() == LexerConstants.Dot))
        {
            sb.Append(Advance());
        }

        string value = sb.ToString();

        if (LexerConstants.Keywords.TryGetValue(value, out TokenType keywordType))
        {
            return new Token(keywordType, value, startLine, startColumn);
        }

        return new Token(TokenType.ID, value, startLine, startColumn);
    }

    /// <summary>
    /// Считывает числа. Разрешены только 0 (как FALSE) и 1 (как TRUE).
    /// </summary>
    /// <param name="startLine">Строка, на которой начался токен.</param>
    /// <param name="startColumn">Столбец, на котором начался токен.</param>
    /// <returns>Токен TRUE или FALSE.</returns>
    private Token ReadNumber(int startLine, int startColumn)
    {
        var sb = new StringBuilder();
        while (!IsAtEnd() && char.IsDigit(Peek()))
        {
            sb.Append(Advance());
        }

        string value = sb.ToString();

        if (value == LexerConstants.TrueLiteral) return new Token(TokenType.TRUE, value, startLine, startColumn);
        if (value == LexerConstants.FalseLiteral) return new Token(TokenType.FALSE, value, startLine, startColumn);

        throw new LexerException(LexerErrorCode.UnknownSymbol, startLine, startColumn, $"Числа ({value}) запрещены. Используйте только {LexerConstants.FalseLiteral}, {LexerConstants.TrueLiteral}, TRUE или FALSE.");
    }

    /// <summary>
    /// Считывает специальные символы и операторы (например, :=, (), ;).
    /// </summary>
    /// <param name="startLine">Строка, на которой начался токен.</param>
    /// <param name="startColumn">Столбец, на котором начался токен.</param>
    /// <returns>Токен соответствующего символа/оператора или выбрасывается исключение, если символ не распознан.</returns>
    private Token ReadSymbol(int startLine, int startColumn)
    {
        char c = Advance();

        switch (c)
        {
            case LexerConstants.Colon:
                if (Match(LexerConstants.Equal)) return new Token(TokenType.ASSIGN, LexerConstants.Assign, startLine, startColumn);
                throw new LexerException(LexerErrorCode.IncompleteAssign, startLine, startColumn);
            case LexerConstants.LParen: return new Token(TokenType.LPAREN, c.ToString(), startLine, startColumn);
            case LexerConstants.RParen: return new Token(TokenType.RPAREN, c.ToString(), startLine, startColumn);
            case LexerConstants.SemiColon: return new Token(TokenType.SEMI, c.ToString(), startLine, startColumn);
            default:
                throw new LexerException(LexerErrorCode.UnknownSymbol, startLine, startColumn, c.ToString());
        }
    }

    /// <summary>
    /// Пропускает многострочные комментарии в формате (* ... *).
    /// </summary>
    /// <param name="startLine">Строка, на которой начался комментарий.</param>
    /// <param name="startColumn">Столбец, на котором начался комментарий.</param>
    private void SkipComment(int startLine, int startColumn)
    {
        Advance();
        Advance();

        while (!IsAtEnd())
        {
            if (Peek() == LexerConstants.Asterisk && PeekNext() == LexerConstants.RParen)
            {
                Advance();
                Advance();
                return;
            }
            Advance();
        }

        throw new LexerException(LexerErrorCode.UnclosedComment, startLine, startColumn);
    }

    /// <summary>
    /// Проверяет, достигнут ли конец входного текста.
    /// </summary>
    /// <returns>True, если достигнут конец (или превышен), иначе False.</returns>
    private bool IsAtEnd()
    {
        return Position >= Input.Length;
    }

    /// <summary>
    /// Возвращает текущий символ в потоке без сдвига позиции (заглядывание вперед).
    /// </summary>
    /// <returns>Текущий символ или '\0', если достигнут конец текста.</returns>
    private char Peek()
    {
        return IsAtEnd() ? '\0' : Input[Position];
    }

    /// <summary>
    /// Возвращает следующий символ в потоке без сдвига позиции (заглядывание на 2 шага вперед).
    /// </summary>
    /// <returns>Следующий символ или '\0', если достигнут конец текста.</returns>
    private char PeekNext()
    {
        return Position + 1 >= Input.Length ? '\0' : Input[Position + 1];
    }

    /// <summary>
    /// Считывает текущий символ, сдвигает позицию на один шаг вперед и обновляет счетчики строк и столбцов.
    /// </summary>
    /// <returns>Считанный символ.</returns>
    private char Advance()
    {
        char c = Input[Position];
        Position++;

        if (c == LexerConstants.NewLine)
        {
            Line++;
            Column = 1;
        }
        else
        {
            Column++;
        }

        return c;
    }

    /// <summary>
    /// Проверяет, совпадает ли текущий символ с ожидаемым. Если совпадает, позиция сдвигается вперед.
    /// Используется для распознавания составных операторов (например, :=).
    /// </summary>
    /// <param name="expected">Ожидаемый символ.</param>
    /// <returns>True, если символ совпал и был считан, иначе False.</returns>
    private bool Match(char expected)
    {
        if (IsAtEnd() || Input[Position] != expected) return false;
        Advance();
        return true;
    }
}