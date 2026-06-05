using System.Collections.Generic;
using Translator.Core.Lexer;
using Xunit;

namespace Translator.Tests;

/// <summary>
/// Набор тестов лексического анализатора.
/// Проверяет, что исходный ST-текст корректно разбивается на токены,
/// незначимые элементы пропускаются, а лексические ошибки получают точные координаты.
/// </summary>
public class TokenizerTests
{
    /// <summary>
    /// Выполняет лексический анализ строки и возвращает полный список токенов, включая EOF.
    /// </summary>
    private static List<Token> Tokenize(string input)
    {
        var tokenizer = new Tokenizer(input);
        return tokenizer.Tokenize();
    }

    /// <summary>
    /// Проверяет тип, значение и при необходимости координаты одного токена.
    /// </summary>
    private static void AssertToken(
        Token token,
        TokenType expectedType,
        string expectedValue,
        int? expectedLine = null,
        int? expectedColumn = null)
    {
        Assert.Equal(expectedType, token.Type);
        Assert.Equal(expectedValue, token.Value);

        if (expectedLine.HasValue)
        {
            Assert.Equal(expectedLine.Value, token.Line);
        }

        if (expectedColumn.HasValue)
        {
            Assert.Equal(expectedColumn.Value, token.Column);
        }
    }

    /// <summary>
    /// Проверяет последовательность значимых токенов и наличие EOF в конце.
    /// </summary>
    private static void AssertTokenSequence(
        string input,
        params (TokenType Type, string Value)[] expectedTokens)
    {
        List<Token> actualTokens = Tokenize(input);

        Assert.Equal(expectedTokens.Length + 1, actualTokens.Count);

        for (int i = 0; i < expectedTokens.Length; i++)
        {
            AssertToken(actualTokens[i], expectedTokens[i].Type, expectedTokens[i].Value);
        }

        AssertToken(actualTokens[^1], TokenType.EOF, string.Empty);
    }

    #region Базовый поток

    /// <summary>
    /// Доказывает, что пустой ввод является корректным лексическим потоком,
    /// состоящим только из признака конца файла.
    /// </summary>
    [Fact]
    public void Tokenize_EmptyInput_ReturnsOnlyEof()
    {
        List<Token> tokens = Tokenize(string.Empty);

        Assert.Single(tokens);
        AssertToken(tokens[0], TokenType.EOF, string.Empty, 1, 1);
    }

    /// <summary>
    /// Проверяет, что пробелы, переводы строк и комментарии не создают токены
    /// и не нарушают порядок значимых элементов программы.
    /// </summary>
    [Fact]
    public void Tokenize_WhitespaceAndComments_SkipsNonSignificantText()
    {
        const string input = """
            (* header *)

              (* inner comment *)
              SysOn := TRUE;
            """;

        AssertTokenSequence(
            input,
            (TokenType.ID, "SysOn"),
            (TokenType.ASSIGN, ":="),
            (TokenType.TRUE, "TRUE"),
            (TokenType.SEMI, ";"));
    }

    /// <summary>
    /// Проверяет, что многострочный комментарий полностью пропускается
    /// и следующий токен получает координаты после окончания комментария.
    /// </summary>
    [Fact]
    public void Tokenize_MultilineComment_SkipsUntilClosingMarker()
    {
        const string input = "(* line 1\n   line 2 *)\nSysOn := TRUE;";

        List<Token> tokens = Tokenize(input);

        AssertToken(tokens[0], TokenType.ID, "SysOn", 3, 1);
        AssertToken(tokens[1], TokenType.ASSIGN, ":=", 3, 7);
        AssertToken(tokens[2], TokenType.TRUE, "TRUE", 3, 10);
        AssertToken(tokens[3], TokenType.SEMI, ";", 3, 14);
        AssertToken(tokens[4], TokenType.EOF, string.Empty, 3, 15);
    }

    #endregion

    #region Ключевые слова

    /// <summary>
    /// Проверяет полное множество ключевых слов текущей грамматики ST.
    /// </summary>
    [Fact]
    public void Tokenize_Keywords_RecognizesAllReservedWords()
    {
        AssertTokenSequence(
            "IF THEN ELSIF END_IF AND OR NOT TRUE FALSE",
            (TokenType.IF, "IF"),
            (TokenType.THEN, "THEN"),
            (TokenType.ELSIF, "ELSIF"),
            (TokenType.ENDIF, "END_IF"),
            (TokenType.AND, "AND"),
            (TokenType.OR, "OR"),
            (TokenType.NOT, "NOT"),
            (TokenType.TRUE, "TRUE"),
            (TokenType.FALSE, "FALSE"));
    }

    /// <summary>
    /// Фиксирует, что ключевые слова распознаются без учета регистра,
    /// как это ожидается для ST-кода в среде CoDeSys.
    /// </summary>
    [Fact]
    public void Tokenize_Keywords_AreCaseInsensitive()
    {
        AssertTokenSequence(
            "if Then elsif end_if aNd Or nOt true false",
            (TokenType.IF, "if"),
            (TokenType.THEN, "Then"),
            (TokenType.ELSIF, "elsif"),
            (TokenType.ENDIF, "end_if"),
            (TokenType.AND, "aNd"),
            (TokenType.OR, "Or"),
            (TokenType.NOT, "nOt"),
            (TokenType.TRUE, "true"),
            (TokenType.FALSE, "false"));
    }

    /// <summary>
    /// Проверяет, что идентификаторы, содержащие ключевые слова как часть имени,
    /// не ошибочно классифицируются как служебные слова.
    /// </summary>
    [Fact]
    public void Tokenize_KeywordFragments_RemainIdentifiers()
    {
        AssertTokenSequence(
            "IFx ThenFlag END_IF_State TRUEVALUE NOT_READY",
            (TokenType.ID, "IFx"),
            (TokenType.ID, "ThenFlag"),
            (TokenType.ID, "END_IF_State"),
            (TokenType.ID, "TRUEVALUE"),
            (TokenType.ID, "NOT_READY"));
    }

    #endregion

    #region Идентификаторы и литералы

    /// <summary>
    /// Проверяет основные допустимые формы идентификаторов:
    /// обычные переменные, псевдооператорные переменные, имена с цифрами и имена таймеров с точкой.
    /// </summary>
    [Fact]
    public void Tokenize_Identifiers_RecognizesPlainPseudoNumericAndDottedNames()
    {
        AssertTokenSequence(
            "SysOn _HtrErr A1 TankP3 HtrTmr.Q",
            (TokenType.ID, "SysOn"),
            (TokenType.ID, "_HtrErr"),
            (TokenType.ID, "A1"),
            (TokenType.ID, "TankP3"),
            (TokenType.ID, "HtrTmr.Q"));
    }

    /// <summary>
    /// Проверяет обе формы булевых литералов: текстовую и числовую.
    /// В рамках LTL-подмножества числа 1 и 0 трактуются как TRUE и FALSE.
    /// </summary>
    [Fact]
    public void Tokenize_BooleanLiterals_RecognizesKeywordAndNumericForms()
    {
        AssertTokenSequence(
            "TRUE FALSE 1 0",
            (TokenType.TRUE, "TRUE"),
            (TokenType.FALSE, "FALSE"),
            (TokenType.TRUE, "1"),
            (TokenType.FALSE, "0"));
    }

    #endregion

    #region Символы и выражения

    /// <summary>
    /// Проверяет все одиночные и составные служебные символы,
    /// которые используются парсером: присваивание, скобки и точку с запятой.
    /// </summary>
    [Fact]
    public void Tokenize_PrimarySymbols_RecognizesAssignmentParenthesesAndSemicolon()
    {
        AssertTokenSequence(
            "X := (A);",
            (TokenType.ID, "X"),
            (TokenType.ASSIGN, ":="),
            (TokenType.LPAREN, "("),
            (TokenType.ID, "A"),
            (TokenType.RPAREN, ")"),
            (TokenType.SEMI, ";"));
    }

    /// <summary>
    /// Проверяет поток токенов для составного логического выражения
    /// с NOT, AND, OR и вложенными скобками.
    /// </summary>
    [Fact]
    public void Tokenize_ComplexExpression_PreservesOperatorAndOperandOrder()
    {
        AssertTokenSequence(
            "NOT _SysOn AND (PBStart OR Alarm)",
            (TokenType.NOT, "NOT"),
            (TokenType.ID, "_SysOn"),
            (TokenType.AND, "AND"),
            (TokenType.LPAREN, "("),
            (TokenType.ID, "PBStart"),
            (TokenType.OR, "OR"),
            (TokenType.ID, "Alarm"),
            (TokenType.RPAREN, ")"));
    }

    /// <summary>
    /// Проверяет, что перевод строки внутри инструкции не влияет
    /// на порядок токенов условной ветки.
    /// </summary>
    [Fact]
    public void Tokenize_LineBreakInsideStatement_PreservesTokenOrder()
    {
        const string input = """
            ELSIF _SysOn AND Fin
            THEN SysOn:=0;
            """;

        AssertTokenSequence(
            input,
            (TokenType.ELSIF, "ELSIF"),
            (TokenType.ID, "_SysOn"),
            (TokenType.AND, "AND"),
            (TokenType.ID, "Fin"),
            (TokenType.THEN, "THEN"),
            (TokenType.ID, "SysOn"),
            (TokenType.ASSIGN, ":="),
            (TokenType.FALSE, "0"),
            (TokenType.SEMI, ";"));
    }

    #endregion

    #region Реальные ST-фрагменты

    /// <summary>
    /// Проверяет полный поток токенов для LTL-защелки с ветками IF и ELSIF.
    /// Такой пример доказывает совместимость лексера с последующими этапами парсинга и семантики.
    /// </summary>
    [Fact]
    public void Tokenize_FullIfElsifLtlStatement_MatchesGrammarTokenSequence()
    {
        const string input = """
            IF NOT _C2InMx AND LS0 AND (_Vlv2 OR _TS2) THEN C2InMx:=1;
            ELSIF _C2InMx AND NOT LS0 THEN C2InMx:=0;
            END_IF;
            """;

        AssertTokenSequence(
            input,
            (TokenType.IF, "IF"),
            (TokenType.NOT, "NOT"),
            (TokenType.ID, "_C2InMx"),
            (TokenType.AND, "AND"),
            (TokenType.ID, "LS0"),
            (TokenType.AND, "AND"),
            (TokenType.LPAREN, "("),
            (TokenType.ID, "_Vlv2"),
            (TokenType.OR, "OR"),
            (TokenType.ID, "_TS2"),
            (TokenType.RPAREN, ")"),
            (TokenType.THEN, "THEN"),
            (TokenType.ID, "C2InMx"),
            (TokenType.ASSIGN, ":="),
            (TokenType.TRUE, "1"),
            (TokenType.SEMI, ";"),
            (TokenType.ELSIF, "ELSIF"),
            (TokenType.ID, "_C2InMx"),
            (TokenType.AND, "AND"),
            (TokenType.NOT, "NOT"),
            (TokenType.ID, "LS0"),
            (TokenType.THEN, "THEN"),
            (TokenType.ID, "C2InMx"),
            (TokenType.ASSIGN, ":="),
            (TokenType.FALSE, "0"),
            (TokenType.SEMI, ";"),
            (TokenType.ENDIF, "END_IF"),
            (TokenType.SEMI, ";"));
    }

    /// <summary>
    /// Проверяет реальный фрагмент псевдооператорного раздела,
    /// где несколько переменных прошлого цикла обновляются подряд.
    /// </summary>
    [Fact]
    public void Tokenize_PseudoOperatorSection_RecognizesRepeatedAssignments()
    {
        const string input = """
            _SysOn := SysOn;
            _Htr := Htr;
            _WTS := WTS;
            """;

        AssertTokenSequence(
            input,
            (TokenType.ID, "_SysOn"),
            (TokenType.ASSIGN, ":="),
            (TokenType.ID, "SysOn"),
            (TokenType.SEMI, ";"),
            (TokenType.ID, "_Htr"),
            (TokenType.ASSIGN, ":="),
            (TokenType.ID, "Htr"),
            (TokenType.SEMI, ";"),
            (TokenType.ID, "_WTS"),
            (TokenType.ASSIGN, ":="),
            (TokenType.ID, "WTS"),
            (TokenType.SEMI, ";"));
    }

    #endregion

    #region Координаты токенов

    /// <summary>
    /// Проверяет, что лексер корректно сохраняет строку и столбец каждого токена.
    /// Эти координаты используются затем в ошибках парсера и семантического анализатора.
    /// </summary>
    [Fact]
    public void Tokenize_TokenLocations_TrackLineAndColumn()
    {
        const string input = "A := TRUE;\n  B := FALSE;";

        List<Token> tokens = Tokenize(input);

        AssertToken(tokens[0], TokenType.ID, "A", 1, 1);
        AssertToken(tokens[1], TokenType.ASSIGN, ":=", 1, 3);
        AssertToken(tokens[2], TokenType.TRUE, "TRUE", 1, 6);
        AssertToken(tokens[3], TokenType.SEMI, ";", 1, 10);
        AssertToken(tokens[4], TokenType.ID, "B", 2, 3);
        AssertToken(tokens[5], TokenType.ASSIGN, ":=", 2, 5);
        AssertToken(tokens[6], TokenType.FALSE, "FALSE", 2, 8);
        AssertToken(tokens[7], TokenType.SEMI, ";", 2, 13);
        AssertToken(tokens[8], TokenType.EOF, string.Empty, 2, 14);
    }

    #endregion

    #region Ошибки

    /// <summary>
    /// Проверяет диагностирование символов, которые не входят в поддерживаемое ST-подмножество.
    /// </summary>
    [Fact]
    public void Tokenize_UnknownSymbol_ThrowsLexerExceptionWithLocation()
    {
        LexerException exception = Assert.Throws<LexerException>(() => Tokenize("SysOn := 1 ?;"));

        Assert.Equal(LexerErrorCode.UnknownSymbol, exception.ErrorCode);
        Assert.Equal(1, exception.Line);
        Assert.Equal(12, exception.Column);
        Assert.Equal("?", exception.Details);
    }

    /// <summary>
    /// Проверяет, что одиночное двоеточие не принимается как оператор присваивания.
    /// Для корректной программы требуется составной оператор :=.
    /// </summary>
    [Fact]
    public void Tokenize_IncompleteAssign_ThrowsLexerExceptionWithLocation()
    {
        LexerException exception = Assert.Throws<LexerException>(() => Tokenize("Var1 : 1;"));

        Assert.Equal(LexerErrorCode.IncompleteAssign, exception.ErrorCode);
        Assert.Equal(1, exception.Line);
        Assert.Equal(6, exception.Column);
    }

    /// <summary>
    /// Проверяет, что незакрытый комментарий распознается как лексическая ошибка,
    /// а координаты указывают на начало комментария.
    /// </summary>
    [Fact]
    public void Tokenize_UnclosedComment_ThrowsLexerExceptionWithStartLocation()
    {
        LexerException exception = Assert.Throws<LexerException>(() => Tokenize("SysOn := 1; (* unclosed \n SysOn := 0;"));

        Assert.Equal(LexerErrorCode.UnclosedComment, exception.ErrorCode);
        Assert.Equal(1, exception.Line);
        Assert.Equal(13, exception.Column);
    }

    /// <summary>
    /// Проверяет ограничение LTL-подмножества: числовые литералы кроме 0 и 1 запрещены.
    /// </summary>
    [Fact]
    public void Tokenize_ForbiddenNumber_ThrowsLexerExceptionWithLocation()
    {
        LexerException exception = Assert.Throws<LexerException>(() => Tokenize("Var1 := 10;"));

        Assert.Equal(LexerErrorCode.UnknownSymbol, exception.ErrorCode);
        Assert.Equal(1, exception.Line);
        Assert.Equal(9, exception.Column);
        Assert.Contains("10", exception.Details);
    }

    #endregion
}
