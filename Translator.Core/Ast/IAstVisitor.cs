namespace Translator.Core.Ast;

/// <summary>
/// Базовый интерфейс для паттерна Visitor. 
/// Определяет методы для обхода каждого конкретного типа узла AST.
/// </summary>
public interface IAstVisitor
{
    void Visit(ProgramNode node);
    void Visit(AssignmentNode node);
    void Visit(IfStatementNode node);
    void Visit(ElsifNode node);
    void Visit(BinaryOperationNode node);
    void Visit(UnaryOperationNode node);
    void Visit(IdentifierNode node);
    void Visit(BooleanNode node);
}