using Translator.Core.Ast;
using Translator.Core.Lexer;
using Translator.Core.Parser;
using Xunit;

namespace Translator.Tests;

/// <summary>
/// Набор тестов синтаксического анализатора.
/// Проверяет, что парсер строит AST в соответствии с EBNF-грамматикой
/// поддерживаемого ST/LTL-подмножества и корректно сообщает о синтаксических ошибках.
/// </summary>
public class ParserTests
{
    /// <summary>
    /// Выполняет полный лексико-синтаксический разбор входной программы.
    /// </summary>
    private static ProgramNode ParseProgram(string input)
    {
        var tokenizer = new Tokenizer(input);
        var parser = new Parser(tokenizer.Tokenize());
        return parser.Parse();
    }

    /// <summary>
    /// Выполняет разбор программы и возвращает ожидаемую синтаксическую ошибку.
    /// </summary>
    private static SyntaxException ParseThrows(string input)
    {
        return Assert.Throws<SyntaxException>(() => ParseProgram(input));
    }

    /// <summary>
    /// Возвращает единственную инструкцию программы и проверяет ее тип.
    /// </summary>
    private static TNode SingleStatement<TNode>(string input)
        where TNode : StatementNode
    {
        ProgramNode program = ParseProgram(input);
        return Assert.IsType<TNode>(Assert.Single(program.Statements));
    }

    /// <summary>
    /// Проверяет идентификатор и возвращает его узел.
    /// </summary>
    private static IdentifierNode AssertIdentifier(ExpressionNode node, string expectedName)
    {
        var identifier = Assert.IsType<IdentifierNode>(node);
        Assert.Equal(expectedName, identifier.Name);
        return identifier;
    }

    /// <summary>
    /// Проверяет булеву константу и возвращает ее узел.
    /// </summary>
    private static BooleanNode AssertBoolean(ExpressionNode node, bool expectedValue)
    {
        var boolean = Assert.IsType<BooleanNode>(node);
        Assert.Equal(expectedValue, boolean.Value);
        return boolean;
    }

    /// <summary>
    /// Проверяет унарный оператор NOT и возвращает его узел.
    /// </summary>
    private static UnaryOperationNode AssertNot(ExpressionNode node)
    {
        var unary = Assert.IsType<UnaryOperationNode>(node);
        Assert.Equal(TokenType.NOT, unary.Operator);
        return unary;
    }

    /// <summary>
    /// Проверяет бинарный оператор и возвращает его узел.
    /// </summary>
    private static BinaryOperationNode AssertBinary(ExpressionNode node, TokenType expectedOperator)
    {
        var binary = Assert.IsType<BinaryOperationNode>(node);
        Assert.Equal(expectedOperator, binary.Operator);
        return binary;
    }

    #region Program Structure

    /// <summary>
    /// Проверяет правило грамматики <program> ::= { <statement> } для пустой программы.
    /// </summary>
    [Fact]
    public void Parse_EmptyProgram_ReturnsProgramWithoutStatements()
    {
        ProgramNode program = ParseProgram(string.Empty);

        Assert.Empty(program.Statements);
    }

    /// <summary>
    /// Проверяет, что программа может состоять из нескольких инструкций разных типов.
    /// </summary>
    [Fact]
    public void Parse_MultipleStatements_ReturnsProgramWithAllStatements()
    {
        const string input = """
            Var1 := TRUE;
            IF Var1 THEN Var2 := FALSE; END_IF;
            HtrTmr.IN := Var1 AND NOT Var2;
            """;

        ProgramNode program = ParseProgram(input);

        Assert.Equal(3, program.Statements.Count);
        Assert.IsType<AssignmentNode>(program.Statements[0]);
        Assert.IsType<IfStatementNode>(program.Statements[1]);
        Assert.IsType<AssignmentNode>(program.Statements[2]);
    }

    #endregion

    #region Assignments

    /// <summary>
    /// Проверяет правило <assignment> ::= <identifier> ":=" <expression> ";"
    /// на простом присваивании булевой константы.
    /// </summary>
    [Theory]
    [InlineData("TRUE", true)]
    [InlineData("FALSE", false)]
    [InlineData("1", true)]
    [InlineData("0", false)]
    public void Parse_AssignmentWithBooleanLiteral_ReturnsBooleanNode(string literal, bool expectedValue)
    {
        AssignmentNode assignment = SingleStatement<AssignmentNode>($"Flag := {literal};");

        Assert.Equal("Flag", assignment.Identifier);
        AssertBoolean(assignment.Expression, expectedValue);
    }

    /// <summary>
    /// Проверяет, что идентификатор с точкой допустим в левой части присваивания.
    /// Это нужно для входов таймеров и похожих внешних сущностей.
    /// </summary>
    [Fact]
    public void Parse_AssignmentWithDottedIdentifier_ReturnsAssignmentNode()
    {
        AssignmentNode assignment = SingleStatement<AssignmentNode>("HtrTmr.IN := SysOn;");

        Assert.Equal("HtrTmr.IN", assignment.Identifier);
        AssertIdentifier(assignment.Expression, "SysOn");
    }

    /// <summary>
    /// Проверяет функциональную переменную с составным логическим выражением.
    /// </summary>
    [Fact]
    public void Parse_AssignmentWithExpression_ReturnsExpressionTree()
    {
        AssignmentNode assignment = SingleStatement<AssignmentNode>("Finish := PBStop OR HtrErr OR ProdFail;");

        Assert.Equal("Finish", assignment.Identifier);

        BinaryOperationNode rootOr = AssertBinary(assignment.Expression, TokenType.OR);
        BinaryOperationNode leftOr = AssertBinary(rootOr.Left, TokenType.OR);
        AssertIdentifier(leftOr.Left, "PBStop");
        AssertIdentifier(leftOr.Right, "HtrErr");
        AssertIdentifier(rootOr.Right, "ProdFail");
    }

    #endregion

    #region Boolean Expressions

    /// <summary>
    /// Проверяет разбор одиночного идентификатора как первичного выражения.
    /// </summary>
    [Fact]
    public void Parse_ExpressionIdentifier_ReturnsIdentifierNode()
    {
        AssignmentNode assignment = SingleStatement<AssignmentNode>("Result := SysOn;");

        AssertIdentifier(assignment.Expression, "SysOn");
    }

    /// <summary>
    /// Проверяет рекурсивное правило <not_expression> ::= "NOT" <not_expression>.
    /// </summary>
    [Fact]
    public void Parse_NestedNotExpression_ReturnsRecursiveUnaryNodes()
    {
        AssignmentNode assignment = SingleStatement<AssignmentNode>("Result := NOT NOT Ready;");

        UnaryOperationNode outerNot = AssertNot(assignment.Expression);
        UnaryOperationNode innerNot = AssertNot(outerNot.Operand);
        AssertIdentifier(innerNot.Operand, "Ready");
    }

    /// <summary>
    /// Проверяет базовый разбор бинарных операций AND и OR.
    /// </summary>
    [Theory]
    [InlineData("A AND B", TokenType.AND)]
    [InlineData("A OR B", TokenType.OR)]
    public void Parse_BinaryExpression_ReturnsBinaryOperationNode(string expression, TokenType expectedOperator)
    {
        AssignmentNode assignment = SingleStatement<AssignmentNode>($"Result := {expression};");

        BinaryOperationNode binary = AssertBinary(assignment.Expression, expectedOperator);
        AssertIdentifier(binary.Left, "A");
        AssertIdentifier(binary.Right, "B");
    }

    #endregion

    #region Operator Precedence

    /// <summary>
    /// Проверяет, что AND имеет более высокий приоритет, чем OR:
    /// выражение A OR B AND C разбирается как A OR (B AND C).
    /// </summary>
    [Fact]
    public void Parse_Precedence_AndBindsStrongerThanOr()
    {
        AssignmentNode assignment = SingleStatement<AssignmentNode>("Result := A OR B AND C;");

        BinaryOperationNode orNode = AssertBinary(assignment.Expression, TokenType.OR);
        AssertIdentifier(orNode.Left, "A");

        BinaryOperationNode andNode = AssertBinary(orNode.Right, TokenType.AND);
        AssertIdentifier(andNode.Left, "B");
        AssertIdentifier(andNode.Right, "C");
    }

    /// <summary>
    /// Проверяет, что AND сохраняет высокий приоритет и в левой части OR:
    /// выражение A AND B OR C разбирается как (A AND B) OR C.
    /// </summary>
    [Fact]
    public void Parse_Precedence_AndBeforeOrOnLeftSide()
    {
        AssignmentNode assignment = SingleStatement<AssignmentNode>("Result := A AND B OR C;");

        BinaryOperationNode orNode = AssertBinary(assignment.Expression, TokenType.OR);
        BinaryOperationNode andNode = AssertBinary(orNode.Left, TokenType.AND);
        AssertIdentifier(andNode.Left, "A");
        AssertIdentifier(andNode.Right, "B");
        AssertIdentifier(orNode.Right, "C");
    }

    /// <summary>
    /// Проверяет, что NOT имеет более высокий приоритет, чем AND.
    /// </summary>
    [Fact]
    public void Parse_Precedence_NotBindsStrongerThanAnd()
    {
        AssignmentNode assignment = SingleStatement<AssignmentNode>("Result := NOT A AND B;");

        BinaryOperationNode andNode = AssertBinary(assignment.Expression, TokenType.AND);
        UnaryOperationNode notNode = AssertNot(andNode.Left);
        AssertIdentifier(notNode.Operand, "A");
        AssertIdentifier(andNode.Right, "B");
    }

    /// <summary>
    /// Проверяет, что NOT имеет более высокий приоритет, чем OR.
    /// </summary>
    [Fact]
    public void Parse_Precedence_NotBindsStrongerThanOr()
    {
        AssignmentNode assignment = SingleStatement<AssignmentNode>("Result := NOT A OR B;");

        BinaryOperationNode orNode = AssertBinary(assignment.Expression, TokenType.OR);
        UnaryOperationNode notNode = AssertNot(orNode.Left);
        AssertIdentifier(notNode.Operand, "A");
        AssertIdentifier(orNode.Right, "B");
    }

    /// <summary>
    /// Проверяет левую ассоциативность цепочки AND:
    /// выражение A AND B AND C разбирается как (A AND B) AND C.
    /// </summary>
    [Fact]
    public void Parse_Precedence_AndChainIsLeftAssociative()
    {
        AssignmentNode assignment = SingleStatement<AssignmentNode>("Result := A AND B AND C;");

        BinaryOperationNode rootAnd = AssertBinary(assignment.Expression, TokenType.AND);
        BinaryOperationNode leftAnd = AssertBinary(rootAnd.Left, TokenType.AND);
        AssertIdentifier(leftAnd.Left, "A");
        AssertIdentifier(leftAnd.Right, "B");
        AssertIdentifier(rootAnd.Right, "C");
    }

    /// <summary>
    /// Проверяет левую ассоциативность цепочки OR:
    /// выражение A OR B OR C разбирается как (A OR B) OR C.
    /// </summary>
    [Fact]
    public void Parse_Precedence_OrChainIsLeftAssociative()
    {
        AssignmentNode assignment = SingleStatement<AssignmentNode>("Result := A OR B OR C;");

        BinaryOperationNode rootOr = AssertBinary(assignment.Expression, TokenType.OR);
        BinaryOperationNode leftOr = AssertBinary(rootOr.Left, TokenType.OR);
        AssertIdentifier(leftOr.Left, "A");
        AssertIdentifier(leftOr.Right, "B");
        AssertIdentifier(rootOr.Right, "C");
    }

    /// <summary>
    /// Проверяет комбинированное выражение с OR, AND и NOT:
    /// A OR B AND NOT C разбирается как A OR (B AND (NOT C)).
    /// </summary>
    [Fact]
    public void Parse_Precedence_ComplexExpressionBuildsExpectedTree()
    {
        AssignmentNode assignment = SingleStatement<AssignmentNode>("Result := A OR B AND NOT _C2InMx;");

        BinaryOperationNode orNode = AssertBinary(assignment.Expression, TokenType.OR);
        AssertIdentifier(orNode.Left, "A");

        BinaryOperationNode andNode = AssertBinary(orNode.Right, TokenType.AND);
        AssertIdentifier(andNode.Left, "B");

        UnaryOperationNode notNode = AssertNot(andNode.Right);
        AssertIdentifier(notNode.Operand, "_C2InMx");
    }

    /// <summary>
    /// Проверяет, что скобки переопределяют стандартный приоритет:
    /// (A OR B) AND C разбирается как AND с OR в левой ветке.
    /// </summary>
    [Fact]
    public void Parse_Precedence_ParenthesesOverrideDefaultPriority()
    {
        AssignmentNode assignment = SingleStatement<AssignmentNode>("Result := (A OR B) AND C;");

        BinaryOperationNode andNode = AssertBinary(assignment.Expression, TokenType.AND);
        BinaryOperationNode orNode = AssertBinary(andNode.Left, TokenType.OR);
        AssertIdentifier(orNode.Left, "A");
        AssertIdentifier(orNode.Right, "B");
        AssertIdentifier(andNode.Right, "C");
    }

    /// <summary>
    /// Проверяет, что NOT перед скобками применяется ко всему вложенному выражению.
    /// </summary>
    [Fact]
    public void Parse_Precedence_NotBeforeParenthesesWrapsWholeExpression()
    {
        AssignmentNode assignment = SingleStatement<AssignmentNode>("Result := NOT (A OR B) AND C;");

        BinaryOperationNode andNode = AssertBinary(assignment.Expression, TokenType.AND);
        UnaryOperationNode notNode = AssertNot(andNode.Left);
        BinaryOperationNode orNode = AssertBinary(notNode.Operand, TokenType.OR);
        AssertIdentifier(orNode.Left, "A");
        AssertIdentifier(orNode.Right, "B");
        AssertIdentifier(andNode.Right, "C");
    }

    /// <summary>
    /// Проверяет вложенные скобки и смешанные операторы в выражении,
    /// близком к реальным условиям LTL-спецификаций.
    /// </summary>
    [Fact]
    public void Parse_Precedence_NestedParenthesesBuildExpectedTree()
    {
        AssignmentNode assignment = SingleStatement<AssignmentNode>("Result := (A AND (B OR NOT C)) OR D;");

        BinaryOperationNode rootOr = AssertBinary(assignment.Expression, TokenType.OR);
        BinaryOperationNode leftAnd = AssertBinary(rootOr.Left, TokenType.AND);
        AssertIdentifier(leftAnd.Left, "A");

        BinaryOperationNode nestedOr = AssertBinary(leftAnd.Right, TokenType.OR);
        AssertIdentifier(nestedOr.Left, "B");
        UnaryOperationNode notNode = AssertNot(nestedOr.Right);
        AssertIdentifier(notNode.Operand, "C");
        AssertIdentifier(rootOr.Right, "D");
    }

    #endregion

    #region If Statements

    /// <summary>
    /// Проверяет условную конструкцию без ELSIF.
    /// </summary>
    [Fact]
    public void Parse_IfStatementWithoutElsif_ReturnsIfNodeWithoutElsifBranch()
    {
        IfStatementNode ifNode = SingleStatement<IfStatementNode>("IF NOT Alarm THEN Lamp := TRUE; END_IF;");

        UnaryOperationNode condition = AssertNot(ifNode.Condition);
        AssertIdentifier(condition.Operand, "Alarm");

        Assert.Equal("Lamp", ifNode.ThenAssignment.Identifier);
        AssertBoolean(ifNode.ThenAssignment.Expression, true);
        Assert.Null(ifNode.ElsifBranch);
    }

    /// <summary>
    /// Проверяет условную конструкцию с одной веткой ELSIF.
    /// </summary>
    [Fact]
    public void Parse_IfStatementWithElsif_ReturnsIfNodeWithSingleElsifBranch()
    {
        const string input = "IF NOT _SysOn AND PBStrt THEN SysOn := 1; ELSIF _SysOn AND Fin THEN SysOn := 0; END_IF;";

        IfStatementNode ifNode = SingleStatement<IfStatementNode>(input);

        BinaryOperationNode condition = AssertBinary(ifNode.Condition, TokenType.AND);
        UnaryOperationNode notNode = AssertNot(condition.Left);
        AssertIdentifier(notNode.Operand, "_SysOn");
        AssertIdentifier(condition.Right, "PBStrt");

        Assert.Equal("SysOn", ifNode.ThenAssignment.Identifier);
        AssertBoolean(ifNode.ThenAssignment.Expression, true);

        ElsifNode elsifBranch = Assert.IsType<ElsifNode>(ifNode.ElsifBranch);
        BinaryOperationNode elsifCondition = AssertBinary(elsifBranch.Condition, TokenType.AND);
        AssertIdentifier(elsifCondition.Left, "_SysOn");
        AssertIdentifier(elsifCondition.Right, "Fin");

        Assert.Equal("SysOn", elsifBranch.Assignment.Identifier);
        AssertBoolean(elsifBranch.Assignment.Expression, false);
    }

    #endregion

    #region Real LTL/ST Fragments

    /// <summary>
    /// Проверяет разбор реального LTL-фрагмента: защелка состояния,
    /// функциональная переменная и псевдооператорное присваивание.
    /// </summary>
    [Fact]
    public void Parse_RealLtlFragment_ReturnsExpectedStatementKinds()
    {
        const string input = """
            IF NOT _SysOn AND PBStrt THEN SysOn := TRUE;
            ELSIF _SysOn AND Fin THEN SysOn := FALSE;
            END_IF;
            Fin := PBStop OR HtrErr;
            _SysOn := SysOn;
            """;

        ProgramNode program = ParseProgram(input);

        Assert.Equal(3, program.Statements.Count);
        Assert.IsType<IfStatementNode>(program.Statements[0]);
        Assert.IsType<AssignmentNode>(program.Statements[1]);
        Assert.IsType<AssignmentNode>(program.Statements[2]);

        var finAssignment = Assert.IsType<AssignmentNode>(program.Statements[1]);
        Assert.Equal("Fin", finAssignment.Identifier);

        var pseudoAssignment = Assert.IsType<AssignmentNode>(program.Statements[2]);
        Assert.Equal("_SysOn", pseudoAssignment.Identifier);
        AssertIdentifier(pseudoAssignment.Expression, "SysOn");
    }

    #endregion

    #region Source Locations

    /// <summary>
    /// Проверяет, что парсер переносит координаты токенов в AST-узлы.
    /// </summary>
    [Fact]
    public void Parse_SourceLocations_AreStoredInAstNodes()
    {
        const string input = """
            A := TRUE;
              IF A THEN B := FALSE; END_IF;
            """;

        ProgramNode program = ParseProgram(input);

        var assignment = Assert.IsType<AssignmentNode>(program.Statements[0]);
        Assert.Equal(1, assignment.Line);
        Assert.Equal(1, assignment.Column);
        Assert.Equal(1, assignment.Expression.Line);
        Assert.Equal(6, assignment.Expression.Column);

        var ifNode = Assert.IsType<IfStatementNode>(program.Statements[1]);
        Assert.Equal(2, ifNode.Line);
        Assert.Equal(3, ifNode.Column);
        Assert.Equal(2, ifNode.ThenAssignment.Line);
        Assert.Equal(13, ifNode.ThenAssignment.Column);
    }

    #endregion

    #region Syntax Errors

    /// <summary>
    /// Проверяет ошибку, когда инструкция начинается с недопустимого токена.
    /// </summary>
    [Fact]
    public void Parse_InvalidStatementStart_ThrowsExpectedStatement()
    {
        SyntaxException exception = ParseThrows("AND SysOn := TRUE;");

        Assert.Equal(SyntaxErrorCode.ExpectedStatement, exception.ErrorCode);
    }

    /// <summary>
    /// Проверяет ошибку отсутствующего оператора присваивания после идентификатора.
    /// </summary>
    [Fact]
    public void Parse_MissingAssignAfterIdentifier_ThrowsExpectedAssignAfterId()
    {
        SyntaxException exception = ParseThrows("SysOn TRUE;");

        Assert.Equal(SyntaxErrorCode.ExpectedAssignAfterId, exception.ErrorCode);
    }

    /// <summary>
    /// Проверяет ошибку отсутствующей точки с запятой после присваивания.
    /// </summary>
    [Fact]
    public void Parse_MissingSemicolonAfterAssignment_ThrowsExpectedSemiAfterAssignment()
    {
        SyntaxException exception = ParseThrows("SysOn := TRUE");

        Assert.Equal(SyntaxErrorCode.ExpectedSemiAfterAssignment, exception.ErrorCode);
    }

    /// <summary>
    /// Проверяет ошибку отсутствующего THEN после условия IF.
    /// </summary>
    [Fact]
    public void Parse_MissingThenAfterIf_ThrowsExpectedThenAfterIf()
    {
        SyntaxException exception = ParseThrows("IF TRUE SysOn := TRUE; END_IF;");

        Assert.Equal(SyntaxErrorCode.ExpectedThenAfterIf, exception.ErrorCode);
    }

    /// <summary>
    /// Проверяет ошибку отсутствующего THEN после условия ELSIF.
    /// </summary>
    [Fact]
    public void Parse_MissingThenAfterElsif_ThrowsExpectedThenAfterElsif()
    {
        SyntaxException exception = ParseThrows("IF TRUE THEN SysOn := TRUE; ELSIF FALSE SysOn := FALSE; END_IF;");

        Assert.Equal(SyntaxErrorCode.ExpectedThenAfterElsif, exception.ErrorCode);
    }

    /// <summary>
    /// Проверяет ошибку отсутствующего END_IF.
    /// </summary>
    [Fact]
    public void Parse_MissingEndIf_ThrowsExpectedEndIf()
    {
        SyntaxException exception = ParseThrows("IF TRUE THEN SysOn := TRUE;");

        Assert.Equal(SyntaxErrorCode.ExpectedEndIf, exception.ErrorCode);
    }

    /// <summary>
    /// Проверяет ошибку отсутствующей точки с запятой после END_IF.
    /// </summary>
    [Fact]
    public void Parse_MissingSemicolonAfterEndIf_ThrowsExpectedSemiAfterEndIf()
    {
        SyntaxException exception = ParseThrows("IF TRUE THEN SysOn := TRUE; END_IF");

        Assert.Equal(SyntaxErrorCode.ExpectedSemiAfterEndIf, exception.ErrorCode);
    }

    /// <summary>
    /// Проверяет ошибку незакрытой скобки в выражении.
    /// </summary>
    [Fact]
    public void Parse_MissingRightParenthesis_ThrowsExpectedRightParen()
    {
        SyntaxException exception = ParseThrows("Var1 := (A AND B;");

        Assert.Equal(SyntaxErrorCode.ExpectedRightParen, exception.ErrorCode);
    }

    /// <summary>
    /// Проверяет ошибку отсутствующего выражения в присваивании.
    /// </summary>
    [Fact]
    public void Parse_MissingExpressionInAssignment_ThrowsExpectedExpression()
    {
        SyntaxException exception = ParseThrows("Var1 := ;");

        Assert.Equal(SyntaxErrorCode.ExpectedExpression, exception.ErrorCode);
    }

    /// <summary>
    /// Проверяет ошибку отсутствующего выражения в условии IF.
    /// </summary>
    [Fact]
    public void Parse_MissingExpressionInIfCondition_ThrowsExpectedExpression()
    {
        SyntaxException exception = ParseThrows("IF THEN SysOn := TRUE; END_IF;");

        Assert.Equal(SyntaxErrorCode.ExpectedExpression, exception.ErrorCode);
    }

    /// <summary>
    /// Проверяет, что синтаксическая ошибка содержит координаты проблемного токена.
    /// </summary>
    [Fact]
    public void Parse_SyntaxException_ContainsSourceLocation()
    {
        SyntaxException exception = ParseThrows("SysOn TRUE;");

        Assert.Equal(1, exception.ErrorToken.Line);
        Assert.Equal(1, exception.ErrorToken.Column);
        Assert.Contains("Строка 1, Столбец 1", exception.Message);
    }

    #endregion
}
