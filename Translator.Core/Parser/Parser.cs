using Translator.Core.Ast;
using Translator.Core.Lexer;

namespace Translator.Core.Parser;

public class Parser
{
    private readonly List<Token> _tokens;
    private int _position = 0;

    public Parser(List<Token> tokens)
    {
        _tokens = tokens;
    }

    #region Базовые методы навигации по токенам.

    /// <summary>
    /// Смотрит текущий токен без сдвига позиции.
    /// </summary>
    private Token Peek()
    {
        return _tokens[_position];
    }

    /// <summary>
    /// Смотрит предыдущий токен.
    /// </summary>
    private Token Previous()
    {
        return _tokens[_position - 1];
    }

    /// <summary>
    /// Проверяет, не достигли ли мы конца файла.
    /// </summary>
    private bool IsAtEnd()
    {
        return Peek().Type == TokenType.EOF;
    }

    /// <summary>
    /// Проверяет, совпадает ли тип текущего токена с ожидаемым.
    /// </summary>
    private bool Check(TokenType type)
    {
        if (IsAtEnd()) return false;
        return Peek().Type == type;
    }

    /// <summary>
    /// Сдвигает позицию вперед и возвращает предыдущий токен.
    /// </summary>
    private Token Advance()
    {
        if (!IsAtEnd()) _position++;
        return Previous();
    }

    /// <summary>
    /// Если текущий токен совпадает с любым из переданных типов, сдвигает позицию и возвращает true.
    /// Используется для проверки операторов.
    /// </summary>
    private bool Match(params TokenType[] types)
    {
        foreach (var type in types)
        {
            if (Check(type))
            {
                Advance();
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// Требует, чтобы текущий токен был определенного типа. 
    /// Если это так — сдвигается вперед. Если нет — выбрасывает исключение.
    /// </summary>
    private Token Consume(TokenType type, SyntaxErrorCode errorCode)
    {
        if (Check(type)) return Advance();
        Token targetToken = _position > 0 ? Previous() : Peek();
        throw new SyntaxException(targetToken, errorCode, Peek());
    }
    #endregion

    #region Методы рекурсивного спуска

    /// <summary>
    /// Точка входа в парсер. Читает все инструкции до конца файла.
    /// Соответствует правилу: <program> ::= { <statement> }
    /// </summary>
    public ProgramNode Parse()
    {
        var statements = new List<StatementNode>();

        while (!IsAtEnd())
        {
            statements.Add(ParseStatement());
        }

        return new ProgramNode(statements);
    }

    /// <summary>
    /// Разбирает одну инструкцию.
    /// Соответствует правилу: <statement> ::= <assignment> | <if-statement>
    /// </summary>
    private StatementNode ParseStatement()
    {
        if (Match(TokenType.IF))
        {
            return ParseIfStatement();
        }

        if (Check(TokenType.ID))
        {
            return ParseAssignment();
        }

        throw new SyntaxException(Peek(), SyntaxErrorCode.ExpectedStatement);
    }

    /// <summary>
    /// Разбирает операцию присваивания.
    /// Соответствует правилу: <assignment> ::= <identifier> ":=" <expression> ";"
    /// </summary>
    private AssignmentNode ParseAssignment()
    {
        Token idToken = Consume(TokenType.ID, SyntaxErrorCode.ExpectedIdentifier);

        Consume(TokenType.ASSIGN, SyntaxErrorCode.ExpectedAssignAfterId);
        ExpressionNode value = ParseExpression();
        Consume(TokenType.SEMI, SyntaxErrorCode.ExpectedSemiAfterAssignment);

        return new AssignmentNode(idToken.Value, value, idToken.Line, idToken.Column);
    }

    /// <summary>
    /// Разбирает условную конструкцию.
    /// Соответствует правилу: <if-statement> ::= "IF" <expression> "THEN" <assignment> [ "ELSIF" <expression> "THEN" <assignment> ] "END_IF" ";"
    /// Предполагается, что токен 'IF' уже был считан.
    /// </summary>
    private StatementNode ParseIfStatement()
    {
        Token ifToken = Previous();
        ExpressionNode condition = ParseExpression();

        Consume(TokenType.THEN, SyntaxErrorCode.ExpectedThenAfterIf);

        AssignmentNode thenAssign = ParseAssignment();

        ElsifNode? elsifBranch = null;

        if (Match(TokenType.ELSIF))
        {
            ExpressionNode elsifCondition = ParseExpression();
            Consume(TokenType.THEN, SyntaxErrorCode.ExpectedThenAfterElsif);

            AssignmentNode elsifAssign = ParseAssignment();

            elsifBranch = new ElsifNode(elsifCondition, elsifAssign);
        }

        Consume(TokenType.ENDIF, SyntaxErrorCode.ExpectedEndIf);
        Consume(TokenType.SEMI, SyntaxErrorCode.ExpectedSemiAfterEndIf);

        return new IfStatementNode(condition, thenAssign, elsifBranch, ifToken.Line, ifToken.Column);
    }

    /// <summary>
    /// Разбирает общее логическое выражение. Является точкой входа для парсинга выражений.
    /// Соответствует правилу: <expression> ::= <or_expression>
    /// </summary>
    private ExpressionNode ParseExpression()
    {
        return ParseOrExpression();
    }

    /// <summary>
    /// Разбирает цепочку операций логического ИЛИ. Имеет самый низкий приоритет.
    /// Соответствует правилу: <or_expression> ::= <and_expression> { "OR" <and_expression> }
    /// </summary>
    private ExpressionNode ParseOrExpression()
    {
        ExpressionNode expr = ParseAndExpression();

        while (Match(TokenType.OR))
        {
            Token operatorToken = Previous();
            ExpressionNode right = ParseAndExpression();
            expr = new BinaryOperationNode(expr, operatorToken.Type, right, operatorToken.Line, operatorToken.Column);
        }

        return expr;
    }

    /// <summary>
    /// Разбирает цепочку операций логического И. Имеет более высокий приоритет, чем OR.
    /// Соответствует правилу: <and_expression> ::= <not_expression> { "AND" <not_expression> }
    /// </summary>
    private ExpressionNode ParseAndExpression()
    {
        ExpressionNode expr = ParseNotExpression();

        while (Match(TokenType.AND))
        {
            Token operatorToken = Previous();
            ExpressionNode right = ParseNotExpression();
            expr = new BinaryOperationNode(expr, operatorToken.Type, right, operatorToken.Line, operatorToken.Column);
        }

        return expr;
    }

    /// <summary>
    /// Разбирает унарные операции, такие как логическое отрицание NOT.
    /// Соответствует правилу: <not_expression> ::= "NOT" <not_expression> | <primary>
    /// </summary>
    private ExpressionNode ParseNotExpression()
    {
        if (Match(TokenType.NOT))
        {
            Token operatorToken = Previous();
            ExpressionNode right = ParseNotExpression();
            return new UnaryOperationNode(operatorToken.Type, right, operatorToken.Line, operatorToken.Column);
        }

        return ParsePrimary();
    }

    /// <summary>
    /// Разбирает базовые элементы выражения: переменные, логические константы и скобки. Имеет наивысший приоритет.
    /// Соответствует правилу: <primary> ::= <identifier> | <bool_literal> | "(" <expression> ")"
    /// </summary>
    private ExpressionNode ParsePrimary()
    {
        if (Match(TokenType.FALSE))
        {
            Token token = Previous();
            return new BooleanNode(false, token.Line, token.Column);
        }

        if (Match(TokenType.TRUE))
        {
            Token token = Previous();
            return new BooleanNode(true, token.Line, token.Column);
        }

        if (Match(TokenType.ID))
        {
            Token token = Previous();
            return new IdentifierNode(token.Value, token.Line, token.Column);
        }

        if (Match(TokenType.LPAREN))
        {
            ExpressionNode expr = ParseExpression();
            Consume(TokenType.RPAREN, SyntaxErrorCode.ExpectedRightParen);
            return expr;
        }

        throw new SyntaxException(Peek(), SyntaxErrorCode.ExpectedExpression);
    }
    #endregion
}
