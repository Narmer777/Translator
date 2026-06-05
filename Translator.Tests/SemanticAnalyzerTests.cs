using Translator.Core.Lexer;
using Translator.Core.Parser;
using Translator.Core.Semantics;
using Xunit;

namespace Translator.Tests;

/// <summary>
/// Набор тестов семантического анализатора.
/// Проверяет LTL-ориентированные правила корректности ST-программы после построения AST:
/// единственность задания переменных, форму state-защелок, функциональные уравнения,
/// псевдооператорный раздел, порядок спецификации и диагностические координаты.
/// </summary>
public class SemanticAnalyzerTests
{
    /// <summary>
    /// Выполняет полный лексико-синтаксический и семантический анализ программы.
    /// </summary>
    private static void AnalyzeCode(string input)
    {
        var tokenizer = new Tokenizer(input);
        var parser = new Parser(tokenizer.Tokenize());
        var program = parser.Parse();

        var analyzer = new SemanticAnalyzer();
        analyzer.Analyze(program);
    }

    /// <summary>
    /// Выполняет анализ программы и возвращает ожидаемую семантическую ошибку.
    /// </summary>
    private static SemanticException AnalyzeThrows(string input)
    {
        return Assert.Throws<SemanticException>(() => AnalyzeCode(input));
    }

    /// <summary>
    /// Проверяет, что программа проходит семантический анализ без исключений.
    /// </summary>
    private static void AssertValidProgram(string input)
    {
        Exception? exception = Record.Exception(() => AnalyzeCode(input));

        Assert.Null(exception);
    }

    /// <summary>
    /// Проверяет код ошибки и имя переменной в семантическом исключении.
    /// </summary>
    private static SemanticException AssertSemanticError(
        string input,
        SemanticErrorCode expectedErrorCode,
        string expectedVariable)
    {
        SemanticException exception = AnalyzeThrows(input);

        Assert.Equal(expectedErrorCode, exception.ErrorCode);
        Assert.Equal(expectedVariable, exception.VariableName);

        return exception;
    }

    #region Valid Programs

    /// <summary>
    /// Проверяет, что пустая программа семантически допустима.
    /// </summary>
    [Fact]
    public void Analyze_EmptyProgram_DoesNotThrow()
    {
        AssertValidProgram(string.Empty);
    }

    /// <summary>
    /// Проверяет корректную цепочку функциональных переменных,
    /// где каждая зависимость уже определена выше по тексту.
    /// </summary>
    [Fact]
    public void Analyze_ValidFunctionChain_DoesNotThrow()
    {
        const string input = """
            HtrErr := PBStop AND WTS;
            Fin := PBStop OR HtrErr;
            SysReady := Fin OR ManualReady;
            """;

        AssertValidProgram(input);
    }

    /// <summary>
    /// Проверяет полный корректный LTL-фрагмент: функциональные переменные,
    /// state-защелки и финальный псевдооператорный раздел.
    /// </summary>
    [Fact]
    public void Analyze_ValidComplexLtlFragment_DoesNotThrow()
    {
        const string input = """
            HtrErr := Alarm OR BtnStop;
            Fin := BtnStop OR HtrErr;

            IF NOT _SysOn AND BtnStart AND NOT Fin THEN SysOn := TRUE;
            ELSIF _SysOn AND Fin THEN SysOn := FALSE;
            END_IF;

            IF NOT _Htr AND SysOn AND NOT TempHi THEN Htr := TRUE;
            ELSIF _Htr AND (NOT SysOn OR TempHi) THEN Htr := FALSE;
            END_IF;

            _SysOn := SysOn;
            _Htr := Htr;
            """;

        AssertValidProgram(input);
    }

    #endregion

    #region Single Definition And Definition Kind

    /// <summary>
    /// Проверяет запрет повторного прямого присваивания одной переменной.
    /// </summary>
    [Fact]
    public void Analyze_DuplicateFunctionAssignment_ThrowsMultipleAssignments()
    {
        const string input = """
            SysOn := TRUE;
            SysOn := FALSE;
            """;

        AssertSemanticError(input, SemanticErrorCode.MultipleAssignments, "SysOn");
    }

    /// <summary>
    /// Проверяет запрет двух отдельных state-защелок для одной переменной.
    /// </summary>
    [Fact]
    public void Analyze_DuplicateLatchDefinition_ThrowsMultipleAssignments()
    {
        const string input = """
            IF NOT _SysOn AND PBStart THEN SysOn := TRUE; END_IF;
            IF _SysOn AND PBStop THEN SysOn := FALSE; END_IF;
            _SysOn := SysOn;
            """;

        AssertSemanticError(input, SemanticErrorCode.MultipleAssignments, "SysOn");
    }

    /// <summary>
    /// Проверяет запрет смешивания state-защелки и функционального уравнения для одной переменной.
    /// </summary>
    [Fact]
    public void Analyze_LatchThenFunctionForSameVariable_ThrowsMixedVariableDefinition()
    {
        const string input = """
            IF NOT _SysOn AND PBStart THEN SysOn := TRUE; END_IF;
            SysOn := PBStart;
            _SysOn := SysOn;
            """;

        AssertSemanticError(input, SemanticErrorCode.MixedVariableDefinition, "SysOn");
    }

    #endregion

    #region LTL State Formulas

    /// <summary>
    /// Проверяет корректную положительную конструктивную формулу V+:
    /// для присваивания V := TRUE условие содержит NOT _V.
    /// </summary>
    [Fact]
    public void Analyze_PositiveSingleFormula_DoesNotThrow()
    {
        const string input = """
            IF NOT _SysOn AND PBStart THEN SysOn := TRUE; END_IF;
            _SysOn := SysOn;
            """;

        AssertValidProgram(input);
    }

    /// <summary>
    /// Проверяет корректную отрицательную конструктивную формулу V-:
    /// для присваивания V := FALSE условие содержит _V.
    /// </summary>
    [Fact]
    public void Analyze_NegativeSingleFormula_DoesNotThrow()
    {
        const string input = """
            IF _SysOn AND PBStop THEN SysOn := FALSE; END_IF;
            _SysOn := SysOn;
            """;

        AssertValidProgram(input);
    }

    /// <summary>
    /// Проверяет, что порядок операндов внутри условия не важен:
    /// guard NOT _V может находиться не в начале выражения.
    /// </summary>
    [Fact]
    public void Analyze_PositiveFormulaWithGuardNotAtStart_DoesNotThrow()
    {
        const string input = """
            IF PBStart AND NOT _SysOn THEN SysOn := TRUE; END_IF;
            _SysOn := SysOn;
            """;

        AssertValidProgram(input);
    }

    /// <summary>
    /// Проверяет, что ветки V- и V+ могут идти в обратном порядке,
    /// если каждая ветка содержит правильную псевдопеременную состояния.
    /// </summary>
    [Fact]
    public void Analyze_ReversedPositiveAndNegativeBranches_DoesNotThrow()
    {
        const string input = """
            IF _SysOn AND PBStop THEN SysOn := FALSE;
            ELSIF PBStart AND NOT _SysOn THEN SysOn := TRUE;
            END_IF;
            _SysOn := SysOn;
            """;

        AssertValidProgram(input);
    }

    /// <summary>
    /// Проверяет запрет положительной state-формулы без guard NOT _V.
    /// </summary>
    [Fact]
    public void Analyze_PositiveFormulaWithoutPseudoGuard_ThrowsInvalidLtlStateGuard()
    {
        const string input = """
            IF PBStart THEN SysOn := TRUE; END_IF;
            """;

        AssertSemanticError(input, SemanticErrorCode.InvalidLtlStateGuard, "SysOn");
    }

    /// <summary>
    /// Проверяет запрет положительной state-формулы с неправильной полярностью guard.
    /// </summary>
    [Fact]
    public void Analyze_PositiveFormulaWithWrongPseudoPolarity_ThrowsInvalidLtlStateGuard()
    {
        const string input = """
            IF _SysOn AND PBStart THEN SysOn := TRUE; END_IF;
            """;

        AssertSemanticError(input, SemanticErrorCode.InvalidLtlStateGuard, "SysOn");
    }

    /// <summary>
    /// Проверяет запрет отрицательной state-формулы с неправильной полярностью guard.
    /// </summary>
    [Fact]
    public void Analyze_NegativeFormulaWithWrongPseudoPolarity_ThrowsInvalidLtlStateGuard()
    {
        const string input = """
            IF NOT _SysOn AND PBStop THEN SysOn := FALSE; END_IF;
            """;

        AssertSemanticError(input, SemanticErrorCode.InvalidLtlStateGuard, "SysOn");
    }

    #endregion

    #region If Elsif Consistency

    /// <summary>
    /// Проверяет корректную LTL-защелку с ветками IF и ELSIF.
    /// </summary>
    [Fact]
    public void Analyze_ValidIfElsifLtlPattern_DoesNotThrow()
    {
        const string input = """
            IF NOT _SysOn AND PBStart THEN SysOn := TRUE;
            ELSIF _SysOn AND Fin THEN SysOn := FALSE;
            END_IF;
            _SysOn := SysOn;
            """;

        AssertValidProgram(input);
    }

    /// <summary>
    /// Проверяет, что ветки IF и ELSIF должны присваивать одной и той же переменной.
    /// </summary>
    [Fact]
    public void Analyze_DifferentTargetsInIfAndElsif_ThrowsInvalidLtlTargetMismatch()
    {
        const string input = """
            IF NOT _SysOn THEN SysOn := TRUE;
            ELSIF _SysOn THEN Alarm := FALSE;
            END_IF;
            """;

        AssertSemanticError(input, SemanticErrorCode.InvalidLtlTargetMismatch, "Alarm");
    }

    /// <summary>
    /// Проверяет, что ветки IF и ELSIF должны задавать противоположные булевы значения.
    /// </summary>
    [Fact]
    public void Analyze_ElsifWithSameBranchValues_ThrowsInvalidLtlBranchValues()
    {
        const string input = """
            IF NOT _SysOn AND PBStart THEN SysOn := TRUE;
            ELSIF NOT _SysOn AND PBStop THEN SysOn := TRUE;
            END_IF;
            """;

        AssertSemanticError(input, SemanticErrorCode.InvalidLtlBranchValues, "SysOn");
    }

    /// <summary>
    /// Проверяет, что в THEN-ветке state-защелки можно присваивать только TRUE или FALSE.
    /// </summary>
    [Fact]
    public void Analyze_InvalidLtlValueInThenBranch_ThrowsInvalidLtlAssignmentValue()
    {
        const string input = """
            IF NOT _SysOn THEN SysOn := PBStart; END_IF;
            """;

        AssertSemanticError(input, SemanticErrorCode.InvalidLtlAssignmentValue, "SysOn");
    }

    /// <summary>
    /// Проверяет, что в ELSIF-ветке state-защелки можно присваивать только TRUE или FALSE.
    /// </summary>
    [Fact]
    public void Analyze_InvalidLtlValueInElsifBranch_ThrowsInvalidLtlAssignmentValue()
    {
        const string input = """
            IF NOT _SysOn THEN SysOn := TRUE;
            ELSIF _SysOn THEN SysOn := PBStop;
            END_IF;
            """;

        AssertSemanticError(input, SemanticErrorCode.InvalidLtlAssignmentValue, "SysOn");
    }

    #endregion

    #region Pseudo Operator Section

    /// <summary>
    /// Проверяет корректный финальный псевдооператорный раздел из нескольких присваиваний.
    /// </summary>
    [Fact]
    public void Analyze_ValidPseudoOperatorSection_DoesNotThrow()
    {
        const string input = """
            IF NOT _SysOn THEN SysOn := TRUE; END_IF;
            Fin := PBStop OR HtrErr;
            _SysOn := SysOn;
            _Fin := Fin;
            """;

        AssertValidProgram(input);
    }

    /// <summary>
    /// Проверяет, что псевдооператорная переменная может использоваться до финального _V := V,
    /// потому что она обозначает значение прошлого цикла.
    /// </summary>
    [Fact]
    public void Analyze_PseudoVariableUsedBeforePseudoAssignment_DoesNotThrow()
    {
        const string input = """
            IF NOT _SysOn AND PBStart THEN SysOn := TRUE; END_IF;
            _SysOn := SysOn;
            """;

        AssertValidProgram(input);
    }

    /// <summary>
    /// Проверяет обязательность финального псевдоприсваивания для каждой state-переменной.
    /// </summary>
    [Fact]
    public void Analyze_LatchWithoutFinalPseudoAssignment_ThrowsMissingPseudoAssignment()
    {
        const string input = """
            IF NOT _SysOn AND PBStart THEN SysOn := TRUE; END_IF;
            """;

        AssertSemanticError(input, SemanticErrorCode.MissingPseudoAssignment, "SysOn");
    }

    /// <summary>
    /// Проверяет точную форму псевдооператорного присваивания: _V := V.
    /// </summary>
    [Fact]
    public void Analyze_InvalidPseudoAssignment_ThrowsInvalidPseudoAssignment()
    {
        const string input = """
            _SysOn := OtherVar;
            """;

        AssertSemanticError(input, SemanticErrorCode.InvalidPseudoAssignment, "_SysOn");
    }

    /// <summary>
    /// Проверяет запрет функциональных присваиваний после начала псевдооператорного раздела.
    /// </summary>
    [Fact]
    public void Analyze_FunctionAfterPseudoOperatorSection_ThrowsInvalidPseudoSectionOrder()
    {
        const string input = """
            _SysOn := SysOn;
            SysReady := _SysOn;
            """;

        AssertSemanticError(input, SemanticErrorCode.InvalidPseudoSectionOrder, "SysReady");
    }

    /// <summary>
    /// Проверяет запрет state-защелок после начала псевдооператорного раздела.
    /// </summary>
    [Fact]
    public void Analyze_LatchAfterPseudoOperatorSection_ThrowsInvalidPseudoSectionOrder()
    {
        const string input = """
            _SysOn := SysOn;
            IF NOT _Alarm AND PBStop THEN Alarm := TRUE; END_IF;
            """;

        AssertSemanticError(input, SemanticErrorCode.InvalidPseudoSectionOrder, "Alarm");
    }

    #endregion

    #region Specification Order And Dependencies

    /// <summary>
    /// Проверяет, что программно определяемую переменную можно использовать после ее определения.
    /// </summary>
    [Fact]
    public void Analyze_FunctionVariableUsedAfterDefinition_DoesNotThrow()
    {
        const string input = """
            HtrErr := PBStop AND WTS;
            Fin := PBStop OR HtrErr;
            """;

        AssertValidProgram(input);
    }

    /// <summary>
    /// Проверяет, что идентификаторы без присваивания считаются внешними входами
    /// и могут использоваться без предварительного определения.
    /// </summary>
    [Fact]
    public void Analyze_InputVariablesWithoutAssignments_DoesNotThrow()
    {
        const string input = """
            Fin := PBStop OR HtrErr OR ExternalAlarm;
            """;

        AssertValidProgram(input);
    }

    /// <summary>
    /// Проверяет мягкое правило порядка спецификации:
    /// переменная, определяемая программой ниже, не может использоваться выше.
    /// </summary>
    [Fact]
    public void Analyze_FunctionVariableUsedBeforeDefinition_ThrowsUseBeforeDefinition()
    {
        const string input = """
            Fin := PBStop OR HtrErr;
            HtrErr := PBStop AND WTS;
            """;

        AssertSemanticError(input, SemanticErrorCode.UseBeforeDefinition, "HtrErr");
    }

    /// <summary>
    /// Проверяет самоссылку в функциональном уравнении.
    /// Она перехватывается правилом порядка спецификации как использование до определения.
    /// </summary>
    [Fact]
    public void Analyze_SelfAssignment_ThrowsUseBeforeDefinition()
    {
        const string input = """
            VarA := VarA AND TRUE;
            """;

        AssertSemanticError(input, SemanticErrorCode.UseBeforeDefinition, "VarA");
    }

    /// <summary>
    /// Проверяет прямой цикл зависимостей. В текущем наборе правил он перехватывается
    /// более ранней проверкой порядка спецификации.
    /// </summary>
    [Fact]
    public void Analyze_DirectCycle_ThrowsUseBeforeDefinition()
    {
        const string input = """
            VarA := VarB AND TRUE;
            VarB := VarA OR FALSE;
            """;

        AssertSemanticError(input, SemanticErrorCode.UseBeforeDefinition, "VarB");
    }

    /// <summary>
    /// Проверяет косвенный цикл зависимостей. В текущем наборе правил он также
    /// перехватывается до графовой проверки циклов.
    /// </summary>
    [Fact]
    public void Analyze_IndirectCycle_ThrowsUseBeforeDefinition()
    {
        const string input = """
            VarA := VarB;
            VarB := VarC;
            VarC := VarA;
            """;

        AssertSemanticError(input, SemanticErrorCode.UseBeforeDefinition, "VarB");
    }

    /// <summary>
    /// Проверяет использование будущей программной переменной в условии IF.
    /// </summary>
    [Fact]
    public void Analyze_IfConditionUsesVariableBeforeDefinition_ThrowsUseBeforeDefinition()
    {
        const string input = """
            IF NOT _X AND Y THEN X := TRUE; END_IF;
            Y := X;
            _X := X;
            """;

        AssertSemanticError(input, SemanticErrorCode.UseBeforeDefinition, "Y");
    }

    /// <summary>
    /// Проверяет использование будущей программной переменной в условии ELSIF.
    /// </summary>
    [Fact]
    public void Analyze_ElsifConditionUsesVariableBeforeDefinition_ThrowsUseBeforeDefinition()
    {
        const string input = """
            IF NOT _X AND A THEN X := TRUE;
            ELSIF _X AND Y THEN X := FALSE;
            END_IF;
            Y := X;
            _X := X;
            """;

        AssertSemanticError(input, SemanticErrorCode.UseBeforeDefinition, "Y");
    }

    #endregion

    #region Source Locations

    /// <summary>
    /// Проверяет, что семантическая ошибка содержит координаты идентификатора,
    /// на котором обнаружено нарушение.
    /// </summary>
    [Fact]
    public void Analyze_SemanticExceptionIncludesSourceLocation()
    {
        const string input = """
            Fin := PBStop OR HtrErr;
            HtrErr := PBStop AND WTS;
            """;

        SemanticException exception = AssertSemanticError(
            input,
            SemanticErrorCode.UseBeforeDefinition,
            "HtrErr");

        Assert.Equal(1, exception.Line);
        Assert.Equal(18, exception.Column);
        Assert.Contains("Строка 1, колонка 18", exception.Message);
    }

    /// <summary>
    /// Проверяет, что ошибка отсутствующего псевдооператорного присваивания
    /// привязана к месту определения state-переменной.
    /// </summary>
    [Fact]
    public void Analyze_MissingPseudoAssignmentIncludesLatchLocation()
    {
        const string input = """
            IF NOT _SysOn THEN SysOn := TRUE; END_IF;
            """;

        SemanticException exception = AssertSemanticError(
            input,
            SemanticErrorCode.MissingPseudoAssignment,
            "SysOn");

        Assert.Equal(1, exception.Line);
        Assert.Equal(20, exception.Column);
    }

    #endregion
}
