using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Translator.Core.CodeGen;
using Translator.Core.Lexer;
using Translator.Core.Parser;
using Xunit;

namespace Translator.Tests;

/// <summary>
/// Набор тестов генератора IL-кода.
/// Проверяет, что AST поддерживаемого ST/LTL-подмножества переводится в корректные инструкции IL
/// без изменения структуры логических выражений и без применения преобразований де Моргана.
/// </summary>
public class IlCodeGeneratorTests
{
    /// <summary>
    /// Выполняет полный путь от ST-текста до IL-кода через лексер, парсер и генератор.
    /// </summary>
    private static string GenerateIl(string input)
    {
        var tokenizer = new Tokenizer(input);
        var parser = new Parser(tokenizer.Tokenize());
        var program = parser.Parse();

        var generator = new IlCodeGenerator();
        return generator.Generate(program);
    }

    /// <summary>
    /// Нормализует IL-код: убирает комментарии, лишние пробелы и пустые строки.
    /// </summary>
    private static string NormalizeIl(string code)
    {
        return string.Join(
            "\n",
            code.Replace("\r", string.Empty)
                .Split('\n')
                .Select(RemoveInlineComments)
                .Select(line => Regex.Replace(line, @"\s+", " ").Trim())
                .Where(line => !string.IsNullOrWhiteSpace(line)));
    }

    /// <summary>
    /// Убирает однострочные комментарии формата CoDeSys `(* ... *)`.
    /// </summary>
    private static string RemoveInlineComments(string line)
    {
        return Regex.Replace(line, @"\(\*.*?\*\)", string.Empty);
    }

    /// <summary>
    /// Возвращает только исполняемые IL-инструкции без служебной экспортной оболочки программы.
    /// </summary>
    private static string[] GetBodyLines(string code)
    {
        string[] serviceLines =
        [
            IlKeywords.ProgramStart,
            ProgramExportConstants.VarStart,
            ProgramExportConstants.VarEnd,
            ProgramExportConstants.ProgramEnd
        ];

        return NormalizeIl(code)
            .Split('\n')
            .Where(line => !serviceLines.Contains(line))
            .ToArray();
    }

    /// <summary>
    /// Проверяет точное соответствие исполняемой части IL-кода ожидаемым инструкциям.
    /// </summary>
    private static void AssertIlBody(string input, params string[] expectedLines)
    {
        string actual = GenerateIl(input);
        Assert.Equal(expectedLines, GetBodyLines(actual));
    }

    /// <summary>
    /// Проверяет, что нормализованный IL-код содержит указанные фрагменты в заданном порядке.
    /// </summary>
    private static void AssertIlContainsInOrder(string actual, params string[] fragments)
    {
        string normalizedActual = NormalizeIl(actual);
        int currentIndex = 0;

        foreach (string fragment in fragments)
        {
            string normalizedFragment = NormalizeIl(fragment);
            int foundIndex = normalizedActual.IndexOf(normalizedFragment, currentIndex, StringComparison.Ordinal);
            Assert.True(
                foundIndex >= 0,
                $"Expected IL fragment was not found in order:{Environment.NewLine}{normalizedFragment}{Environment.NewLine}{Environment.NewLine}Actual:{Environment.NewLine}{normalizedActual}");
            currentIndex = foundIndex + normalizedFragment.Length;
        }
    }

    /// <summary>
    /// Набор выражений покрывает базис выражений и рекурсивные комбинации NOT, AND, OR и скобок.
    /// </summary>
    public static IEnumerable<object[]> ExpressionCases()
    {
        yield return new object[]
        {
            "Var:=A;",
            new[] { "LD A", "ST Var" }
        };

        yield return new object[]
        {
            "Var:=Timer.Q;",
            new[] { "LD Timer.Q", "ST Var" }
        };

        yield return new object[]
        {
            "Var:=TRUE;",
            new[] { "LD TRUE", "ST Var" }
        };

        yield return new object[]
        {
            "Var:=FALSE;",
            new[] { "LD FALSE", "ST Var" }
        };

        yield return new object[]
        {
            "Var:=NOT A;",
            new[] { "LDN A", "ST Var" }
        };

        yield return new object[]
        {
            "Var:=A AND B;",
            new[] { "LD A", "AND B", "ST Var" }
        };

        yield return new object[]
        {
            "Var:=A OR B;",
            new[] { "LD A", "OR B", "ST Var" }
        };

        yield return new object[]
        {
            "Var:=A OR B AND C;",
            new[] { "LD A", "OR (B", "AND C", ")", "ST Var" }
        };

        yield return new object[]
        {
            "Var:=(A OR B) AND C;",
            new[] { "LD A", "OR B", "AND C", "ST Var" }
        };

        yield return new object[]
        {
            "Var:=NOT A AND B OR NOT C;",
            new[] { "LDN A", "AND B", "ORN C", "ST Var" }
        };

        yield return new object[]
        {
            "Var:=NOT (A AND B);",
            new[] { "LD A", "AND B", "NOT", "ST Var" }
        };

        yield return new object[]
        {
            "Var:=NOT (A OR B);",
            new[] { "LD A", "OR B", "NOT", "ST Var" }
        };

        yield return new object[]
        {
            "Var:=A AND NOT (B OR C);",
            new[] { "LD A", "ANDN (B", "OR C", ")", "ST Var" }
        };

        yield return new object[]
        {
            "Var:=((A OR B) AND C) OR D;",
            new[] { "LD A", "OR B", "AND C", "OR D", "ST Var" }
        };

        yield return new object[]
        {
            "Var:=(A AND NOT (B OR FALSE)) OR (NOT (C AND D) AND TRUE);",
            new[] { "LD A", "ANDN (B", "OR FALSE", ")", "ORN (C", "AND D", ")", "AND TRUE", ")", "ST Var" }
        };
    }

    #region Export Envelope

    /// <summary>
    /// Проверяет, что генератор добавляет экспортную оболочку IL-программы для CodeSys.
    /// </summary>
    [Fact]
    public void Generate_Assignment_ProducesCodesysProgramEnvelope()
    {
        string actual = GenerateIl("X:=A;");

        AssertIlContainsInOrder(
            actual,
            "(* @NESTEDCOMMENTS := 'Yes' *)",
            "(* @PATH := '' *)",
            "(* @OBJECTFLAGS := '0, 8' *)",
            "(* @SYMFILEFLAGS := '2048' *)",
            "PROGRAM PLC_IL_PRG_TR",
            "VAR",
            "END_VAR",
            "(* @END_DECLARATION := '0' *)",
            "LD A",
            "ST X",
            "END_PROGRAM");
    }

    #endregion

    #region Assignments And Expressions

    /// <summary>
    /// Проверяет трансляцию всех базовых и составных вариантов выражений.
    /// </summary>
    [Theory]
    [MemberData(nameof(ExpressionCases))]
    public void Generate_AssignmentExpressions_ProduceExpectedInstructionSequence(string input, string[] expectedLines)
    {
        AssertIlBody(input, expectedLines);
    }

    /// <summary>
    /// Проверяет, что отрицание составного выражения сохраняется как вычисление выражения с последующей командой NOT.
    /// </summary>
    [Fact]
    public void Generate_NotOverBinaryExpression_DoesNotApplyDeMorganTransformation()
    {
        AssertIlBody(
            "X:=NOT (A AND B);",
            "LD A",
            "AND B",
            "NOT",
            "ST X");
    }

    /// <summary>
    /// Проверяет, что вложенное отрицание в правой части AND оформляется скобочной IL-группой.
    /// </summary>
    [Fact]
    public void Generate_AndWithNegatedGroup_UsesNegatedAndGroup()
    {
        AssertIlBody(
            "X:=A AND NOT (B OR C);",
            "LD A",
            "ANDN (B",
            "OR C",
            ")",
            "ST X");
    }

    #endregion

    #region If Statements

    /// <summary>
    /// Проверяет, что ветка `V := TRUE` внутри IF транслируется в IL-команду установки S.
    /// </summary>
    [Fact]
    public void Generate_IfThenTrue_ProducesSetInstruction()
    {
        AssertIlBody(
            "IF NOT _X AND A THEN X:=TRUE; END_IF;",
            "LDN _X",
            "AND A",
            "S X");
    }

    /// <summary>
    /// Проверяет, что ветка `V := FALSE` внутри IF транслируется в IL-команду сброса R.
    /// </summary>
    [Fact]
    public void Generate_IfThenFalse_ProducesResetInstruction()
    {
        AssertIlBody(
            "IF _X AND B THEN X:=FALSE; END_IF;",
            "LD _X",
            "AND B",
            "R X");
    }

    /// <summary>
    /// Проверяет, что конструкция IF/ELSIF создает две последовательные проверки с командами S и R.
    /// </summary>
    [Fact]
    public void Generate_IfWithElsif_ProducesSetAndResetInstructions()
    {
        AssertIlBody(
            "IF NOT _X AND A THEN X:=1; ELSIF _X AND B THEN X:=0; END_IF;",
            "LDN _X",
            "AND A",
            "S X",
            "LD _X",
            "AND B",
            "R X");
    }

    #endregion

    #region Ltl Fragments

    /// <summary>
    /// Проверяет псевдооператорное присваивание `_V := V` как обычную IL-запись через LD/ST.
    /// </summary>
    [Fact]
    public void Generate_PseudoOperatorAssignment_ProducesLoadAndStore()
    {
        AssertIlBody(
            "_X:=X;",
            "LD X",
            "ST _X");
    }

    /// <summary>
    /// Проверяет реальный LTL/ST-фрагмент: state-защелку, функциональное присваивание и псевдооператорный раздел.
    /// </summary>
    [Fact]
    public void Generate_RealLtlFragment_ProducesExpectedIlStructure()
    {
        const string input = """
            IF NOT _HtrErr AND NOT WTS AND (HtrTmr.Q OR _Htr AND _WTS) THEN HtrErr:=TRUE;
            ELSIF _HtrErr AND PBStop THEN HtrErr:=FALSE;
            END_IF;
            Fin:=HtrErr OR PBStop;
            _HtrErr:=HtrErr;
            _WTS:=WTS;
            """;

        string actual = GenerateIl(input);

        AssertIlContainsInOrder(
            actual,
            """
            LDN _HtrErr
            ANDN WTS
            AND (HtrTmr.Q
            OR (_Htr
            AND _WTS
            )
            )
            S HtrErr
            """,
            """
            LD _HtrErr
            AND PBStop
            R HtrErr
            """,
            """
            LD HtrErr
            OR PBStop
            ST Fin
            """,
            """
            LD HtrErr
            ST _HtrErr
            """,
            """
            LD WTS
            ST _WTS
            """);
    }

    #endregion
}
