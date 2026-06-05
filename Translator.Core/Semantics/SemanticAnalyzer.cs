using System.Collections.Generic;
using Translator.Core.Ast;
using Translator.Core.Lexer;

namespace Translator.Core.Semantics;

/// <summary>
/// Семантический анализатор. Проходит по AST-дереву и проверяет логические правила программы.
/// В частности, проверяет правило единственности присваивания и отсутствие мгновенных циклических зависимостей.
/// </summary>
public class SemanticAnalyzer : IAstVisitor
{
    private enum VariableDefinitionKind
    {
        Latch,
        Function,
        Pseudo
    }

    private HashSet<string> _assignedVariables = new();
    private readonly HashSet<string> _programDefinedVariables = new();
    private readonly HashSet<string> _definedVariables = new();
    private readonly HashSet<string> _latchVariables = new();
    private readonly HashSet<string> _pseudoAssignments = new();
    private readonly Dictionary<string, (int Line, int Column)> _definitionLocations = new();
    private readonly Dictionary<string, HashSet<string>> _dependencies = new();
    private readonly Dictionary<string, VariableDefinitionKind> _definitionKinds = new();
    private string? _currentAssignee = null;
    private bool _pseudoSectionStarted = false;

    /// <summary>
    /// Точка входа для запуска семантического анализа.
    /// Обходит дерево для сбора данных, а затем выполняет проверку на наличие циклов.
    /// </summary>
    public void Analyze(ProgramNode program)
    {
        _assignedVariables.Clear();
        _programDefinedVariables.Clear();
        _definedVariables.Clear();
        _latchVariables.Clear();
        _pseudoAssignments.Clear();
        _definitionLocations.Clear();
        _dependencies.Clear();
        _definitionKinds.Clear();
        _currentAssignee = null;
        _pseudoSectionStarted = false;

        CollectProgramDefinedVariables(program);
        program.Accept(this);
        CheckLatchPseudoAssignments();
        CheckForCycles();
    }

    /// <summary>
    /// Собирает все переменные, которым программа задает значение.
    /// Остальные идентификаторы мягко считаются входными сигналами.
    /// </summary>
    private void CollectProgramDefinedVariables(ProgramNode program)
    {
        foreach (var statement in program.Statements)
        {
            switch (statement)
            {
                case AssignmentNode assignment:
                    _programDefinedVariables.Add(assignment.Identifier);
                    break;
                case IfStatementNode ifStatement:
                    _programDefinedVariables.Add(ifStatement.ThenAssignment.Identifier);
                    break;
            }
        }
    }

    /// <summary>
    /// Возвращает множество зависимостей для заданной переменной, создавая его при необходимости.
    /// </summary>
    private HashSet<string> GetOrCreateDependencies(string variable)
    {
        if (!_dependencies.TryGetValue(variable, out var dependencies))
        {
            dependencies = new HashSet<string>();
            _dependencies[variable] = dependencies;
        }

        return dependencies;
    }

    /// <summary>
    /// Временнно назначает целевую переменную для сбора зависимостей из выражения.
    /// </summary>
    private void CollectDependencies(string assignee, ExpressionNode expression)
    {
        string? previousAssignee = _currentAssignee;
        _currentAssignee = assignee;
        expression.Accept(this);
        _currentAssignee = previousAssignee;
    }

    /// <summary>
    /// Регистрирует форму задания поведения переменной и запрещает смешивать разные формы.
    /// </summary>
    private void RegisterDefinitionKind(string variable, VariableDefinitionKind kind, int line, int column)
    {
        if (_definitionKinds.TryGetValue(variable, out var existingKind) && existingKind != kind)
        {
            throw new SemanticException(
                SemanticErrorCode.MixedVariableDefinition,
                variable,
                $"{existingKind} и {kind}",
                line,
                column);
        }

        _definitionKinds[variable] = kind;
    }

    /// <summary>
    /// Проверяет псевдооператорное присваивание вида _V := V.
    /// </summary>
    private static bool IsValidPseudoAssignment(AssignmentNode node)
    {
        if (!node.Identifier.StartsWith("_")) return false;

        string originalVariable = node.Identifier[1..];
        return !string.IsNullOrWhiteSpace(originalVariable)
               && node.Expression is IdentifierNode id
               && id.Name == originalVariable;
    }

    /// <summary>
    /// Для каждой state-переменной проверяет наличие финального _V := V.
    /// </summary>
    private void CheckLatchPseudoAssignments()
    {
        foreach (string variable in _latchVariables)
        {
            if (!_pseudoAssignments.Contains(variable))
            {
                (int line, int column) = _definitionLocations.GetValueOrDefault(variable);
                throw new SemanticException(
                    SemanticErrorCode.MissingPseudoAssignment,
                    variable,
                    line: line == 0 ? null : line,
                    column: column == 0 ? null : column);
            }
        }
    }

    /// <summary>
    /// Возвращает булево значение, присваиваемое в ветке LTL-защелки.
    /// </summary>
    private static bool GetLtlAssignedValue(AssignmentNode node)
    {
        return ((BooleanNode)node.Expression).Value;
    }

    /// <summary>
    /// Проверяет, что условие ветки содержит _V в нужной полярности.
    /// Для V := TRUE требуется NOT _V, для V := FALSE требуется _V.
    /// </summary>
    private static void ValidateLtlStateGuard(string variable, bool assignedValue, ExpressionNode condition)
    {
        if (variable.StartsWith("_"))
        {
            throw new SemanticException(
                SemanticErrorCode.InvalidLtlStateGuard,
                variable,
                $"для обычной переменной '{variable}'",
                condition.Line,
                condition.Column);
        }

        string pseudoVariable = "_" + variable;
        bool expectedNegated = assignedValue;

        if (!ContainsIdentifierWithPolarity(condition, pseudoVariable, expectedNegated))
        {
            string expected = expectedNegated ? $"NOT {pseudoVariable}" : pseudoVariable;
            throw new SemanticException(
                SemanticErrorCode.InvalidLtlStateGuard,
                variable,
                expected,
                condition.Line,
                condition.Column);
        }
    }

    /// <summary>
    /// Ищет идентификатор в выражении с учетом количества внешних NOT.
    /// </summary>
    private static bool ContainsIdentifierWithPolarity(
        ExpressionNode expression,
        string identifier,
        bool expectedNegated,
        bool currentNegated = false)
    {
        return expression switch
        {
            IdentifierNode id => id.Name == identifier && currentNegated == expectedNegated,
            UnaryOperationNode unary when unary.Operator == TokenType.NOT =>
                ContainsIdentifierWithPolarity(unary.Operand, identifier, expectedNegated, !currentNegated),
            BinaryOperationNode binary =>
                ContainsIdentifierWithPolarity(binary.Left, identifier, expectedNegated, currentNegated)
                || ContainsIdentifierWithPolarity(binary.Right, identifier, expectedNegated, currentNegated),
            _ => false
        };
    }

    /// <summary>
    /// Запускает алгоритм поиска циклов в графе зависимостей для всех записанных переменных.
    /// </summary>
    private void CheckForCycles()
    {
        var visited = new HashSet<string>();
        var recursionStack = new HashSet<string>();

        foreach (var node in _dependencies.Keys)
        {
            if (HasCycle(node, visited, recursionStack))
            {
                (int line, int column) = _definitionLocations.GetValueOrDefault(node);
                throw new SemanticException(
                    SemanticErrorCode.CyclicDependency,
                    node,
                    line: line == 0 ? null : line,
                    column: column == 0 ? null : column);
            }
        }
    }

    /// <summary>
    /// Рекурсивный метод поиска в глубину для обнаружения циклов в ориентированном графе.
    /// </summary>
    private bool HasCycle(string node, HashSet<string> visited, HashSet<string> recStack)
    {
        if (recStack.Contains(node)) return true;
        if (visited.Contains(node)) return false;

        visited.Add(node);
        recStack.Add(node);

        if (_dependencies.ContainsKey(node))
        {
            foreach (var neighbor in _dependencies[node])
            {
                if (HasCycle(neighbor, visited, recStack)) return true;
            }
        }

        recStack.Remove(node);
        return false;
    }

    /// <summary>
    /// Обходит корневой узел программы, запуская проверку всех вложенных инструкций.
    /// </summary>
    public void Visit(ProgramNode node)
    {
        foreach (var statement in node.Statements) statement.Accept(this);
    }

    /// <summary>
    /// Обрабатывает узел присваивания. Проверяет уникальность левой части 
    /// и инициирует сбор зависимостей для правой части.
    /// </summary>
    public void Visit(AssignmentNode node)
    {
        if (node.Identifier.StartsWith("_"))
        {
            _pseudoSectionStarted = true;

            if (!IsValidPseudoAssignment(node))
            {
                throw new SemanticException(
                    SemanticErrorCode.InvalidPseudoAssignment,
                    node.Identifier,
                    line: node.Line,
                    column: node.Column);
            }

            RegisterDefinitionKind(node.Identifier, VariableDefinitionKind.Pseudo, node.Line, node.Column);
            _pseudoAssignments.Add(node.Identifier[1..]);
        }
        else
        {
            if (_pseudoSectionStarted)
            {
                throw new SemanticException(
                    SemanticErrorCode.InvalidPseudoSectionOrder,
                    node.Identifier,
                    line: node.Line,
                    column: node.Column);
            }

            RegisterDefinitionKind(node.Identifier, VariableDefinitionKind.Function, node.Line, node.Column);
        }

        if (!_assignedVariables.Add(node.Identifier))
        {
            throw new SemanticException(
                SemanticErrorCode.MultipleAssignments,
                node.Identifier,
                line: node.Line,
                column: node.Column);
        }

        _definitionLocations.TryAdd(node.Identifier, (node.Line, node.Column));
        GetOrCreateDependencies(node.Identifier);
        CollectDependencies(node.Identifier, node.Expression);
        _definedVariables.Add(node.Identifier);
    }

    /// <summary>
    /// Обрабатывает блок условной конструкции.
    /// Проверяет LTL-ограничения (совпадение целевой переменной и константность значений), 
    /// а затем обходит условие и все внутренние ветки для сбора зависимостей.
    /// </summary>
    public void Visit(IfStatementNode node)
    {
        if (_pseudoSectionStarted)
        {
            throw new SemanticException(
                SemanticErrorCode.InvalidPseudoSectionOrder,
                node.ThenAssignment.Identifier,
                line: node.Line,
                column: node.Column);
        }

        if (node.ThenAssignment.Expression is not BooleanNode)
        {
            throw new SemanticException(
                SemanticErrorCode.InvalidLtlAssignmentValue,
                node.ThenAssignment.Identifier,
                line: node.ThenAssignment.Line,
                column: node.ThenAssignment.Column);
        }

        bool thenValue = GetLtlAssignedValue(node.ThenAssignment);
        ValidateLtlStateGuard(node.ThenAssignment.Identifier, thenValue, node.Condition);

        if (node.ElsifBranch != null)
        {
            if (node.ElsifBranch.Assignment.Expression is not BooleanNode)
            {
                throw new SemanticException(
                    SemanticErrorCode.InvalidLtlAssignmentValue,
                    node.ElsifBranch.Assignment.Identifier,
                    line: node.ElsifBranch.Assignment.Line,
                    column: node.ElsifBranch.Assignment.Column);
            }

            if (node.ElsifBranch.Assignment.Identifier != node.ThenAssignment.Identifier)
            {
                throw new SemanticException(SemanticErrorCode.InvalidLtlTargetMismatch,
                    node.ElsifBranch.Assignment.Identifier,
                    node.ThenAssignment.Identifier,
                    node.ElsifBranch.Assignment.Line,
                    node.ElsifBranch.Assignment.Column);
            }

            bool elsifValue = GetLtlAssignedValue(node.ElsifBranch.Assignment);
            if (thenValue == elsifValue)
            {
                throw new SemanticException(
                    SemanticErrorCode.InvalidLtlBranchValues,
                    node.ThenAssignment.Identifier,
                    line: node.ElsifBranch.Assignment.Line,
                    column: node.ElsifBranch.Assignment.Column);
            }

            ValidateLtlStateGuard(node.ElsifBranch.Assignment.Identifier, elsifValue, node.ElsifBranch.Condition);
        }

        string targetVar = node.ThenAssignment.Identifier;
        RegisterDefinitionKind(targetVar, VariableDefinitionKind.Latch, node.ThenAssignment.Line, node.ThenAssignment.Column);
        _latchVariables.Add(targetVar);

        if (!_assignedVariables.Add(targetVar))
        {
            throw new SemanticException(
                SemanticErrorCode.MultipleAssignments,
                targetVar,
                line: node.ThenAssignment.Line,
                column: node.ThenAssignment.Column);
        }

        _definitionLocations.TryAdd(targetVar, (node.ThenAssignment.Line, node.ThenAssignment.Column));
        GetOrCreateDependencies(targetVar);
        CollectDependencies(targetVar, node.Condition);

        if (node.ElsifBranch != null)
        {
            CollectDependencies(targetVar, node.ElsifBranch.Condition);
        }

        _definedVariables.Add(targetVar);
    }

    /// <summary>
    /// Обрабатывает ветку ELSIF внутри условной конструкции.
    /// </summary>
    public void Visit(ElsifNode node)
    {
        GetOrCreateDependencies(node.Assignment.Identifier);
        CollectDependencies(node.Assignment.Identifier, node.Condition);
        node.Assignment.Accept(this);
    }

    /// <summary>
    /// Обрабатывает бинарную операцию, рекурсивно обходя левый и правый операнды.
    /// </summary>
    public void Visit(BinaryOperationNode node)
    {
        node.Left.Accept(this);
        node.Right.Accept(this);
    }

    /// <summary>
    /// Обрабатывает унарную операцию, обходя её единственный операнд.
    /// </summary>
    public void Visit(UnaryOperationNode node)
    {
        node.Operand.Accept(this);
    }

    /// <summary>
    /// Обрабатывает встреченный идентификатор. Если он находится в правой части присваивания, 
    /// добавляет его в список зависимостей текущей переменной.
    /// </summary>
    public void Visit(IdentifierNode node)
    {
        if (_currentAssignee != null)
        {
            if (!node.Name.StartsWith("_")
                && _programDefinedVariables.Contains(node.Name)
                && !_definedVariables.Contains(node.Name))
            {
                throw new SemanticException(
                    SemanticErrorCode.UseBeforeDefinition,
                    node.Name,
                    line: node.Line,
                    column: node.Column);
            }

            if (!_currentAssignee.StartsWith("_"))
            {
                _dependencies[_currentAssignee].Add(node.Name);
            }
        }
    }

    public void Visit(BooleanNode node) { }
}
