using System.Text;
using Translator.Core.Ast;
using Translator.Core.Lexer;

namespace Translator.Core.CodeGen;

/// <summary>
/// Генератор LD-кода (Ladder Diagram) в формате экспорта CoDeSys 2.3 (*.exp).
/// Поддерживает логические присваивания и условия (IF/ELSIF) в виде Set/Reset катушек.
/// </summary>
public class LdCodeGenerator : IAstVisitor, ICodeGenerator
{
    private readonly StringBuilder _sb = new();
    private int _networkCount = 0;

    /// <summary>
    /// Запускает процесс генерации кода для переданного абстрактного синтаксического дерева программы.
    /// </summary>
    public string Generate(ProgramNode program)
    {
        _sb.Clear();

        _networkCount = CalculateNetworksCount(program);

        Emit(ProgramExportConstants.HeaderNestedComments);
        Emit(ProgramExportConstants.HeaderPath);
        Emit(ProgramExportConstants.HeaderObjectFlags);
        Emit(ProgramExportConstants.HeaderSymFileFlags);

        Emit(LdConstants.ProgramStart);
        Emit(ProgramExportConstants.VarStart);
        Emit(ProgramExportConstants.VarEnd);
        Emit(ProgramExportConstants.EndDeclaration);

        Emit(LdConstants.LdBody);
        Emit($"{LdConstants.NetworksCount} {_networkCount}");

        program.Accept(this);

        Emit(ProgramExportConstants.ProgramEnd);

        return _sb.ToString();
    }

    /// <summary>
    /// Вычисляет общее количество релейных цепей (Networks), которые будут сгенерированы.
    /// </summary>
    private int CalculateNetworksCount(ProgramNode program)
    {
        int count = 0;
        foreach (var statement in program.Statements)
        {
            if (statement is AssignmentNode) count++;
            else if (statement is IfStatementNode ifNode) count += ifNode.ElsifBranch != null ? 2 : 1;
        }
        return count;
    }

    /// <summary>
    /// Добавляет строку в итоговый буфер генерации.
    /// </summary>
    private void Emit(string text) => _sb.AppendLine(text);

    /// <summary>
    /// Точка входа для обхода дерева программы.
    /// </summary>
    public void Visit(ProgramNode node)
    {
        foreach (var statement in node.Statements) statement.Accept(this);
    }

    /// <summary>
    /// Генерирует стандартную цепь LD для операции присваивания (обычная катушка).
    /// </summary>
    public void Visit(AssignmentNode node)
    {
        Emit(LdConstants.Network);
        Emit("");
        Emit(LdConstants.Comment);
        Emit(LdConstants.EmptyString);
        Emit(LdConstants.EndComment);
        Emit(LdConstants.LdAssign);

        CompileExpression(node.Expression);

        Emit(LdConstants.Expression);
        Emit(LdConstants.Positiv);

        Emit(LdConstants.EnableList);
        Emit(LdConstants.EnableListEnd);
        Emit($"{LdConstants.OutputsCount} 1");
        Emit(LdConstants.Output);
        Emit(LdConstants.Positiv);
        Emit(LdConstants.NoSet);
        Emit(node.Identifier);
    }

    /// <summary>
    /// Генерирует цепи Set/Reset катушек на основе условных конструкций IF/ELSIF.
    /// </summary>
    public void Visit(IfStatementNode node)
    {
        string targetVar = node.ThenAssignment.Identifier;
        bool thenVal = ((BooleanNode)node.ThenAssignment.Expression).Value;

        GenerateTriggerNetwork(node.Condition, targetVar, thenVal);

        if (node.ElsifBranch != null)
        {
            bool elsifVal = ((BooleanNode)node.ElsifBranch.Assignment.Expression).Value;
            GenerateTriggerNetwork(node.ElsifBranch.Condition, targetVar, elsifVal);
        }
    }

    /// <summary>
    /// Вспомогательный метод для создания цепи с катушкой типа Set(S) или Reset(R).
    /// </summary>
    private void GenerateTriggerNetwork(ExpressionNode condition, string targetVar, bool isSet)
    {
        Emit(LdConstants.Network);
        Emit("");
        Emit(LdConstants.Comment);
        Emit(LdConstants.EmptyString);
        Emit(LdConstants.EndComment);
        Emit(LdConstants.LdAssign);

        CompileExpression(condition);

        Emit(LdConstants.Expression);
        Emit(LdConstants.Positiv);

        Emit(LdConstants.EnableList);
        Emit(LdConstants.EnableListEnd);
        Emit($"{LdConstants.OutputsCount} 1");
        Emit(LdConstants.Output);
        Emit(isSet ? LdConstants.Positiv : LdConstants.Negativ);
        Emit(LdConstants.Set);
        Emit(targetVar);
    }

    /// <summary>
    /// Рекурсивно компилирует логическое выражение (ST) в набор контактов и операторов LD.
    /// </summary>
    private void CompileExpression(ExpressionNode expr, bool negate = false)
    {
        switch (expr)
        {
            case UnaryOperationNode unary when unary.Operator == TokenType.NOT:
                CompileExpression(unary.Operand, !negate);
                break;

            case IdentifierNode id:
                Emit(LdConstants.LdContact);
                Emit(id.Name);
                Emit(LdConstants.Expression);
                Emit(negate ? LdConstants.Negativ : LdConstants.Positiv);
                break;

            case BooleanNode b:
                Emit(LdConstants.LdContact);
                Emit(b.Value ? LdConstants.TrueValue : LdConstants.FalseValue);
                Emit(LdConstants.Expression);
                Emit(negate ? LdConstants.Negativ : LdConstants.Positiv);
                break;

            case BinaryOperationNode binNode:
                CompileBinaryExpression(binNode, negate);
                break;

            default:
                throw new CodeGenException(CodeGenErrorCode.UnsupportedNode, $"Неподдерживаемый узел выражения в LD: {expr.GetType().Name}");
        }
    }

    /// <summary>
    /// Вспомогательный метод для компиляции бинарных логических операторов.
    /// </summary>
    private void CompileBinaryExpression(BinaryOperationNode binNode, bool negate)
    {
        TokenType effectiveOp = binNode.Operator;

        if (negate)
        {
            if (effectiveOp == TokenType.AND) effectiveOp = TokenType.OR;
            else if (effectiveOp == TokenType.OR) effectiveOp = TokenType.AND;
        }

        List<ExpressionNode> operands = FlattenExpression(binNode, binNode.Operator);

        if (effectiveOp == TokenType.AND)
        {
            Emit(LdConstants.LdAnd);
            Emit($"{LdConstants.LdOperator} {operands.Count}");
        }
        else if (effectiveOp == TokenType.OR)
        {
            Emit(LdConstants.LdOr);
            Emit($"{LdConstants.LdOperator} {operands.Count}");
        }

        foreach (var op in operands)
        {
            CompileExpression(op, negate);
        }

        Emit(LdConstants.Expression);
        Emit(LdConstants.Positiv);
    }

    /// <summary>
    /// Сплющивает цепочки одинаковых логических операторов.
    /// </summary>
    private List<ExpressionNode> FlattenExpression(ExpressionNode node, TokenType targetOperator)
    {
        var result = new List<ExpressionNode>();

        if (node is BinaryOperationNode binNode && binNode.Operator == targetOperator)
        {
            result.AddRange(FlattenExpression(binNode.Left, targetOperator));
            result.AddRange(FlattenExpression(binNode.Right, targetOperator));
        }
        else
        {
            result.Add(node);
        }

        return result;
    }

    // --- Заглушки ---
    public void Visit(ElsifNode node) { }
    public void Visit(BinaryOperationNode node) { }
    public void Visit(UnaryOperationNode node) { }
    public void Visit(IdentifierNode node) { }
    public void Visit(BooleanNode node) { }
}
