using Translator.Core.Ast;
using Translator.Core.Lexer;

namespace Translator.Core.CodeGen;

/// <summary>
/// Генератор IL-кода (Instruction List).
/// Реализует паттерн Visitor для обхода AST-дерева и трансляции его в инструкции стандарта МЭК 61131-3.
/// </summary>
public class IlCodeGenerator : IAstVisitor, ICodeGenerator
{
    private readonly List<IlInstruction> _instructions = new();

    /// <summary>
    /// Точка входа. Запускает генерацию и возвращает готовый IL-код в виде текста.
    /// </summary>
    public string Generate(ProgramNode program)
    {
        _instructions.Clear();

        EmitProgramHeader();
        program.Accept(this);
        Emit(IlOpCode.NONE, ProgramExportConstants.ProgramEnd);

        return string.Join(Environment.NewLine, _instructions.Select(i => i.ToString()));
    }

    /// <summary>
    /// Записывает служебную шапку экспортируемой IL-программы.
    /// </summary>
    private void EmitProgramHeader()
    {
        Emit(IlOpCode.NONE, ProgramExportConstants.HeaderNestedComments);
        Emit(IlOpCode.NONE, ProgramExportConstants.HeaderPath);
        Emit(IlOpCode.NONE, ProgramExportConstants.HeaderObjectFlags);
        Emit(IlOpCode.NONE, ProgramExportConstants.HeaderSymFileFlags);
        Emit(IlOpCode.NONE, IlKeywords.ProgramStart);
        Emit(IlOpCode.NONE, ProgramExportConstants.VarStart);
        Emit(IlOpCode.NONE, ProgramExportConstants.VarEnd);
        Emit(IlOpCode.NONE, ProgramExportConstants.EndDeclaration);
    }

    /// <summary>
    /// Создает новую IL-инструкцию и добавляет ее в список генерации.
    /// </summary>
    private void Emit(IlOpCode opCode, string operand = "", string comment = "")
    {
        _instructions.Add(new IlInstruction(opCode, operand, comment));
    }

    /// <summary>
    /// Обходит корневой узел программы, запуская трансляцию всех вложенных инструкций.
    /// </summary>
    public void Visit(ProgramNode node)
    {
        foreach (var statement in node.Statements) statement.Accept(this);
    }

    /// <summary>
    /// Обрабатывает условную конструкцию IF.
    /// Генерирует оптимизированные LTL-инструкции Set (S) и Reset (R).
    /// </summary>
    public void Visit(IfStatementNode node)
    {
        string targetVar = node.ThenAssignment.Identifier;
        bool thenValue = ((BooleanNode)node.ThenAssignment.Expression).Value;

        Emit(IlOpCode.NONE, string.Empty, IlKeywords.IfStart);
        CompileExpression(node.Condition, IlOpCode.LD);
        Emit(thenValue ? IlOpCode.S : IlOpCode.R, targetVar, $"{targetVar}:={(thenValue ? 1 : 0)}");

        if (node.ElsifBranch != null)
        {
            bool elsifValue = ((BooleanNode)node.ElsifBranch.Assignment.Expression).Value;

            Emit(IlOpCode.NONE, string.Empty, IlKeywords.ElsifStart);
            CompileExpression(node.ElsifBranch.Condition, IlOpCode.LD);
            Emit(elsifValue ? IlOpCode.S : IlOpCode.R, targetVar, $"{targetVar}:={(elsifValue ? 1 : 0)}");
        }

        Emit(IlOpCode.NONE, string.Empty, IlKeywords.EndIf);
    }

    /// <summary>
    /// Обрабатывает инструкцию присваивания (A := B). Вычисляет выражение и сохраняет в переменную командой ST.
    /// </summary>
    public void Visit(AssignmentNode node)
    {
        CompileExpression(node.Expression, IlOpCode.LD);
        Emit(IlOpCode.ST, node.Identifier);
    }

    /// <summary>
    /// Рекурсивно компилирует логическое выражение в последовательность IL-инструкций.
    /// </summary>
    private void CompileExpression(ExpressionNode expr, IlOpCode currentOp, bool openParen = false, bool forceParenGroup = false)
    {
        switch (expr)
        {
            case UnaryOperationNode unary when unary.Operator == TokenType.NOT:
                if (unary.Operand is IdentifierNode or BooleanNode)
                {
                    if (openParen)
                    {
                        CompileExpression(unary.Operand, currentOp, openParen);
                        Emit(IlOpCode.NOT);
                        break;
                    }

                    CompileExpression(unary.Operand, InvertOpCode(currentOp), openParen);
                    break;
                }

                if (currentOp is IlOpCode.LD or IlOpCode.LDN)
                {
                    CompileExpression(unary.Operand, currentOp, openParen);
                    Emit(IlOpCode.NOT);
                    break;
                }

                CompileExpression(unary.Operand, InvertOpCode(currentOp), openParen, forceParenGroup: true);
                break;

            case IdentifierNode id:
                EmitOperand(id.Name, currentOp, openParen);
                break;

            case BinaryOperationNode bin:
                CompileBinaryExpression(bin, currentOp, openParen, forceParenGroup);
                break;

            case BooleanNode b:
                string literal = b.Value ? IlKeywords.TrueLiteral : IlKeywords.FalseLiteral;
                EmitOperand(literal, currentOp, openParen);
                break;

            default:
                throw new CodeGenException(CodeGenErrorCode.UnsupportedNode, $"Неподдерживаемый узел выражения: {expr.GetType().Name}");
        }
    }

    /// <summary>
    /// Вспомогательный метод для компиляции бинарных операций (AND, OR).
    /// Сохраняет структуру исходного выражения и расставляет скобки для вложенных групп.
    /// </summary>
    private void CompileBinaryExpression(BinaryOperationNode binaryNode, IlOpCode currentOp, bool openParen, bool forceParenGroup)
    {
        IlOpCode rightOp = ToBinaryOpCode(binaryNode.Operator);
        bool needsParenGroup = forceParenGroup || NeedsParenGroup(currentOp, rightOp, openParen);

        CompileExpression(
            binaryNode.Left,
            currentOp,
            openParen || needsParenGroup);

        CompileExpression(binaryNode.Right, rightOp);

        if (needsParenGroup)
        {
            Emit(IlOpCode.NONE, IlKeywords.ParenClose);
        }
    }

    /// <summary>
    /// Записывает простой операнд IL-выражения: идентификатор или булеву константу.
    /// </summary>
    private void EmitOperand(string operand, IlOpCode currentOp, bool openParen)
    {
        string formattedOperand = openParen ? IlKeywords.ParenOpen + operand : operand;
        Emit(currentOp, formattedOperand);
    }

    /// <summary>
    /// Определяет, нужно ли выделять текущее подвыражение в отдельную скобочную группу.
    /// </summary>
    private bool NeedsParenGroup(IlOpCode currentOp, IlOpCode rightOp, bool openParen)
    {
        return !openParen &&
               currentOp != IlOpCode.LD &&
               currentOp != IlOpCode.LDN &&
               rightOp != GetOpFamily(currentOp);
    }

    /// <summary>
    /// Переводит логический оператор AST в базовую IL-инструкцию для правой части бинарного выражения.
    /// </summary>
    private IlOpCode ToBinaryOpCode(TokenType type) => type switch
    {
        TokenType.AND => IlOpCode.AND,
        TokenType.OR => IlOpCode.OR,
        _ => IlOpCode.NONE
    };

    /// <summary>
    /// Возвращает логическое семейство IL-инструкции: AND, OR или NONE.
    /// Это нужно для сравнения контекста без учёта отрицательной формы ANDN / ORN.
    /// </summary>
    private IlOpCode GetOpFamily(IlOpCode opCode) => opCode switch
    {
        IlOpCode.AND or IlOpCode.ANDN => IlOpCode.AND,
        IlOpCode.OR or IlOpCode.ORN => IlOpCode.OR,
        _ => IlOpCode.NONE
    };

    /// <summary>
    /// Инвертирует IL-инструкцию.
    /// Пример: LD -> LDN, AND -> ANDN, OR -> ORN.
    /// </summary>
    private IlOpCode InvertOpCode(IlOpCode opCode) => opCode switch
    {
        IlOpCode.LD => IlOpCode.LDN,
        IlOpCode.AND => IlOpCode.ANDN,
        IlOpCode.OR => IlOpCode.ORN,
        IlOpCode.LDN => IlOpCode.LD,
        IlOpCode.ANDN => IlOpCode.AND,
        IlOpCode.ORN => IlOpCode.OR,
        _ => opCode
    };

    // --- Заглушки ---
    public void Visit(ElsifNode node) { }
    public void Visit(BinaryOperationNode node) { }
    public void Visit(UnaryOperationNode node) { }
    public void Visit(IdentifierNode node) { }
    public void Visit(BooleanNode node) { }
}
