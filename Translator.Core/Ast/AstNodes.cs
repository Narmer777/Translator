using Translator.Core.Lexer;

namespace Translator.Core.Ast;

public abstract record AstNode(int Line, int Column)
{
    /// <summary>
    /// Точка входа для паттерна Visitor.
    /// </summary>
    public abstract void Accept(IAstVisitor visitor);
}
public abstract record StatementNode(int Line, int Column) : AstNode(Line, Column);
public abstract record ExpressionNode(int Line, int Column) : AstNode(Line, Column);

/// <summary>
/// Корень дерева. Содержит список всех инструкций программы.
/// </summary>
public record ProgramNode(List<StatementNode> Statements) : AstNode(0, 0)
{
    public override void Accept(IAstVisitor visitor)
    {
        visitor.Visit(this);
    }
}

/// <summary>
/// Узел присваивания.
/// </summary>
public record AssignmentNode(string Identifier, ExpressionNode Expression, int Line, int Column)
    : StatementNode(Line, Column)
{
    public override void Accept(IAstVisitor visitor)
    {
        visitor.Visit(this);
    }
}

/// <summary>
/// Вспомогательный узел для ветки ELSIF.
/// Содержит ровно одно условие и ровно одно присваивание.
/// </summary>
public record ElsifNode(ExpressionNode Condition, AssignmentNode Assignment)
    : AstNode(Condition.Line, Condition.Column)
{
    public override void Accept(IAstVisitor visitor)
    {
        visitor.Visit(this);
    }
}

/// <summary>
/// Узел условной конструкции.
/// </summary>
public record IfStatementNode(
    ExpressionNode Condition,
    AssignmentNode ThenAssignment,
    ElsifNode? ElsifBranch,
    int Line,
    int Column
) : StatementNode(Line, Column)
{
    public override void Accept(IAstVisitor visitor)
    {
        visitor.Visit(this);
    }
}

/// <summary>
/// Бинарная логическая операция (AND, OR).
/// </summary>
public record BinaryOperationNode(
    ExpressionNode Left,
    TokenType Operator,
    ExpressionNode Right,
    int Line,
    int Column
) : ExpressionNode(Line, Column)
{
    public override void Accept(IAstVisitor visitor)
    {
        visitor.Visit(this);
    }
}

/// <summary>
/// Унарная операция (NOT).
/// </summary>
public record UnaryOperationNode(TokenType Operator, ExpressionNode Operand, int Line, int Column)
    : ExpressionNode(Line, Column)
{
    public override void Accept(IAstVisitor visitor)
    {
        visitor.Visit(this);
    }
}

/// <summary>
/// Переменная.
/// </summary>
public record IdentifierNode(string Name, int Line, int Column) : ExpressionNode(Line, Column)
{
    public override void Accept(IAstVisitor visitor)
    {
        visitor.Visit(this);
    }
}

/// <summary>
/// Булева константа.
/// </summary>
public record BooleanNode(bool Value, int Line, int Column) : ExpressionNode(Line, Column)
{
    public override void Accept(IAstVisitor visitor)
    {
        visitor.Visit(this);
    }
}
