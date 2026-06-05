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
/// Набор тестов генератора LD-кода.
/// Проверяет, что AST поддерживаемого ST/LTL-подмножества переводится в корректные LD-сети
/// формата экспорта CoDeSys 2.3.
/// </summary>
public class LdCodeGeneratorTests
{
    /// <summary>
    /// Выполняет полный путь от ST-текста до LD-кода через лексер, парсер и генератор.
    /// </summary>
    private static string GenerateLd(string input)
    {
        var tokenizer = new Tokenizer(input);
        var parser = new Parser(tokenizer.Tokenize());
        var program = parser.Parse();

        var generator = new LdCodeGenerator();
        return generator.Generate(program);
    }

    /// <summary>
    /// Нормализует LD-код: убирает комментарии, лишние пробелы и пустые строки.
    /// </summary>
    private static string NormalizeLd(string code, bool ignoreVarContents = false)
    {
        code = code.Replace("\r", string.Empty);

        var rawLines = code
            .Split('\n')
            .Select(RemoveInlineComments)
            .Select(line => Regex.Replace(line, @"\s+", " ").Trim())
            .Where(line => !string.IsNullOrWhiteSpace(line));

        if (!ignoreVarContents)
        {
            return string.Join("\n", rawLines);
        }

        var normalized = new List<string>();
        bool inVarBlock = false;

        foreach (string line in rawLines)
        {
            if (line == ProgramExportConstants.VarStart)
            {
                inVarBlock = true;
                normalized.Add(line);
                continue;
            }

            if (line == ProgramExportConstants.VarEnd)
            {
                inVarBlock = false;
                normalized.Add(line);
                continue;
            }

            if (!inVarBlock)
            {
                normalized.Add(line);
            }
        }

        return string.Join("\n", normalized);
    }

    /// <summary>
    /// Убирает однострочные комментарии формата CoDeSys `(* ... *)`.
    /// </summary>
    private static string RemoveInlineComments(string line)
    {
        return Regex.Replace(line, @"\(\*.*?\*\)", string.Empty);
    }

    /// <summary>
    /// Проверяет, что нормализованный LD-код содержит указанные фрагменты в заданном порядке.
    /// </summary>
    private static void AssertLdContainsInOrder(string actual, params string[] fragments)
    {
        string normalizedActual = NormalizeLd(actual, ignoreVarContents: true);
        int currentIndex = 0;

        foreach (string fragment in fragments)
        {
            string normalizedFragment = NormalizeLd(fragment, ignoreVarContents: true);
            int foundIndex = normalizedActual.IndexOf(normalizedFragment, currentIndex, StringComparison.Ordinal);
            Assert.True(
                foundIndex >= 0,
                $"Expected LD fragment was not found in order:{Environment.NewLine}{normalizedFragment}{Environment.NewLine}{Environment.NewLine}Actual:{Environment.NewLine}{normalizedActual}");
            currentIndex = foundIndex + normalizedFragment.Length;
        }
    }

    /// <summary>
    /// Проверяет количество сгенерированных LD-сетей.
    /// </summary>
    private static void AssertNetworkCount(string actual, int expectedCount)
    {
        string normalized = NormalizeLd(actual, ignoreVarContents: true);

        Assert.Contains($"{LdConstants.NetworksCount} {expectedCount}", normalized);
        Assert.Equal(expectedCount, Regex.Matches(normalized, @"(^|\n)_NETWORK(?=\n|$)").Count);
    }

    /// <summary>
    /// Набор выражений покрывает контакты, булевы литералы, NOT, AND, OR, скобки и де Моргана для LD.
    /// </summary>
    public static IEnumerable<object[]> ExpressionCases()
    {
        yield return new object[]
        {
            "Var:=A;",
            new[]
            {
                "_LD_CONTACT\nA\n_EXPRESSION\n_POSITIV",
                "_NO_SET\nVar"
            }
        };

        yield return new object[]
        {
            "Var:=Timer.Q;",
            new[]
            {
                "_LD_CONTACT\nTimer.Q\n_EXPRESSION\n_POSITIV",
                "_NO_SET\nVar"
            }
        };

        yield return new object[]
        {
            "Var:=TRUE;",
            new[]
            {
                "_LD_CONTACT\nTRUE\n_EXPRESSION\n_POSITIV",
                "_NO_SET\nVar"
            }
        };

        yield return new object[]
        {
            "Var:=FALSE;",
            new[]
            {
                "_LD_CONTACT\nFALSE\n_EXPRESSION\n_POSITIV",
                "_NO_SET\nVar"
            }
        };

        yield return new object[]
        {
            "Var:=NOT A;",
            new[]
            {
                "_LD_CONTACT\nA\n_EXPRESSION\n_NEGATIV",
                "_NO_SET\nVar"
            }
        };

        yield return new object[]
        {
            "Var:=A AND B;",
            new[]
            {
                "_LD_AND\n_LD_OPERATOR : 2",
                "_LD_CONTACT\nA\n_EXPRESSION\n_POSITIV",
                "_LD_CONTACT\nB\n_EXPRESSION\n_POSITIV",
                "_NO_SET\nVar"
            }
        };

        yield return new object[]
        {
            "Var:=A AND B AND NOT C;",
            new[]
            {
                "_LD_AND\n_LD_OPERATOR : 3",
                "_LD_CONTACT\nA\n_EXPRESSION\n_POSITIV",
                "_LD_CONTACT\nB\n_EXPRESSION\n_POSITIV",
                "_LD_CONTACT\nC\n_EXPRESSION\n_NEGATIV",
                "_NO_SET\nVar"
            }
        };

        yield return new object[]
        {
            "Var:=A OR B;",
            new[]
            {
                "_LD_OR\n_LD_OPERATOR : 2",
                "_LD_CONTACT\nA\n_EXPRESSION\n_POSITIV",
                "_LD_CONTACT\nB\n_EXPRESSION\n_POSITIV",
                "_NO_SET\nVar"
            }
        };

        yield return new object[]
        {
            "Var:=A OR B AND C;",
            new[]
            {
                "_LD_OR\n_LD_OPERATOR : 2",
                "_LD_CONTACT\nA\n_EXPRESSION\n_POSITIV",
                "_LD_AND\n_LD_OPERATOR : 2",
                "_LD_CONTACT\nB\n_EXPRESSION\n_POSITIV",
                "_LD_CONTACT\nC\n_EXPRESSION\n_POSITIV",
                "_NO_SET\nVar"
            }
        };

        yield return new object[]
        {
            "Var:=(A OR B) AND C;",
            new[]
            {
                "_LD_AND\n_LD_OPERATOR : 2",
                "_LD_OR\n_LD_OPERATOR : 2",
                "_LD_CONTACT\nA\n_EXPRESSION\n_POSITIV",
                "_LD_CONTACT\nB\n_EXPRESSION\n_POSITIV",
                "_LD_CONTACT\nC\n_EXPRESSION\n_POSITIV",
                "_NO_SET\nVar"
            }
        };

        yield return new object[]
        {
            "Var:=NOT A AND B OR NOT C;",
            new[]
            {
                "_LD_OR\n_LD_OPERATOR : 2",
                "_LD_AND\n_LD_OPERATOR : 2",
                "_LD_CONTACT\nA\n_EXPRESSION\n_NEGATIV",
                "_LD_CONTACT\nB\n_EXPRESSION\n_POSITIV",
                "_LD_CONTACT\nC\n_EXPRESSION\n_NEGATIV",
                "_NO_SET\nVar"
            }
        };

        yield return new object[]
        {
            "Var:=NOT (A AND B);",
            new[]
            {
                "_LD_OR\n_LD_OPERATOR : 2",
                "_LD_CONTACT\nA\n_EXPRESSION\n_NEGATIV",
                "_LD_CONTACT\nB\n_EXPRESSION\n_NEGATIV",
                "_NO_SET\nVar"
            }
        };

        yield return new object[]
        {
            "Var:=NOT (A OR B);",
            new[]
            {
                "_LD_AND\n_LD_OPERATOR : 2",
                "_LD_CONTACT\nA\n_EXPRESSION\n_NEGATIV",
                "_LD_CONTACT\nB\n_EXPRESSION\n_NEGATIV",
                "_NO_SET\nVar"
            }
        };

        yield return new object[]
        {
            "Var:=A AND NOT (B OR C);",
            new[]
            {
                "_LD_AND\n_LD_OPERATOR : 2",
                "_LD_CONTACT\nA\n_EXPRESSION\n_POSITIV",
                "_LD_AND\n_LD_OPERATOR : 2",
                "_LD_CONTACT\nB\n_EXPRESSION\n_NEGATIV",
                "_LD_CONTACT\nC\n_EXPRESSION\n_NEGATIV",
                "_NO_SET\nVar"
            }
        };

        yield return new object[]
        {
            "Var:=(A AND NOT (B OR FALSE)) OR (NOT (C AND D) AND TRUE);",
            new[]
            {
                "_LD_OR\n_LD_OPERATOR : 2",
                "_LD_AND\n_LD_OPERATOR : 2",
                "_LD_CONTACT\nA\n_EXPRESSION\n_POSITIV",
                "_LD_AND\n_LD_OPERATOR : 2",
                "_LD_CONTACT\nB\n_EXPRESSION\n_NEGATIV",
                "_LD_CONTACT\nFALSE\n_EXPRESSION\n_NEGATIV",
                "_LD_AND\n_LD_OPERATOR : 2",
                "_LD_OR\n_LD_OPERATOR : 2",
                "_LD_CONTACT\nC\n_EXPRESSION\n_NEGATIV",
                "_LD_CONTACT\nD\n_EXPRESSION\n_NEGATIV",
                "_LD_CONTACT\nTRUE\n_EXPRESSION\n_POSITIV",
                "_NO_SET\nVar"
            }
        };
    }

    #region Export Envelope

    /// <summary>
    /// Проверяет, что генератор добавляет экспортную оболочку LD-программы для CodeSys.
    /// </summary>
    [Fact]
    public void Generate_Assignment_ProducesCodesysLdProgramEnvelope()
    {
        string actual = GenerateLd("X:=A;");
        string normalized = NormalizeLd(actual, ignoreVarContents: true);

        Assert.Contains(ProgramExportConstants.HeaderNestedComments, actual);
        Assert.Contains(ProgramExportConstants.HeaderPath, actual);
        Assert.Contains(ProgramExportConstants.HeaderObjectFlags, actual);
        Assert.Contains(ProgramExportConstants.HeaderSymFileFlags, actual);
        Assert.Contains(LdConstants.ProgramStart, normalized);
        Assert.Contains(ProgramExportConstants.VarStart, normalized);
        Assert.Contains(ProgramExportConstants.VarEnd, normalized);
        Assert.Contains(LdConstants.LdBody, normalized);
        Assert.Contains(ProgramExportConstants.ProgramEnd, normalized);
    }

    /// <summary>
    /// Проверяет полную структуру простой LD-сети для присваивания одному контакту.
    /// </summary>
    [Fact]
    public void Generate_AssignmentWithIdentifier_ProducesSingleContactNetwork()
    {
        const string input = "X:=A;";
        const string expectedFragment = """
            _NETWORK
            _COMMENT
            ''
            _END_COMMENT
            _LD_ASSIGN
            _LD_CONTACT
            A
            _EXPRESSION
            _POSITIV
            _EXPRESSION
            _POSITIV
            ENABLELIST : 0
            ENABLELIST_END
            _OUTPUTS : 1
            _OUTPUT
            _POSITIV
            _NO_SET
            X
            """;

        string actual = GenerateLd(input);

        AssertNetworkCount(actual, 1);
        AssertLdContainsInOrder(actual, expectedFragment);
    }

    #endregion

    #region Expressions

    /// <summary>
    /// Проверяет трансляцию всех базовых и составных вариантов выражений в LD-контакты и блоки.
    /// </summary>
    [Theory]
    [MemberData(nameof(ExpressionCases))]
    public void Generate_AssignmentExpressions_ProduceExpectedLdStructure(string input, string[] expectedFragments)
    {
        string actual = GenerateLd(input);

        AssertNetworkCount(actual, 1);
        AssertLdContainsInOrder(actual, expectedFragments);
    }

    /// <summary>
    /// Проверяет, что цепочка одинаковых AND-операторов сплющивается в один LD-блок с тремя операндами.
    /// </summary>
    [Fact]
    public void Generate_FlattenedAndExpression_ProducesSingleAndBlock()
    {
        string actual = GenerateLd("X:=A AND B AND NOT C;");

        AssertLdContainsInOrder(
            actual,
            """
            _LD_AND
            _LD_OPERATOR : 3
            _LD_CONTACT
            A
            _EXPRESSION
            _POSITIV
            _LD_CONTACT
            B
            _EXPRESSION
            _POSITIV
            _LD_CONTACT
            C
            _EXPRESSION
            _NEGATIV
            """);
    }

    /// <summary>
    /// Проверяет LD-особенность: отрицание OR-группы переводится по де Моргану в AND отрицательных контактов.
    /// </summary>
    [Fact]
    public void Generate_NegatedOrExpression_AppliesDeMorganForLdContacts()
    {
        string actual = GenerateLd("X:=NOT (A OR B);");

        AssertLdContainsInOrder(
            actual,
            """
            _LD_AND
            _LD_OPERATOR : 2
            _LD_CONTACT
            A
            _EXPRESSION
            _NEGATIV
            _LD_CONTACT
            B
            _EXPRESSION
            _NEGATIV
            """);
    }

    /// <summary>
    /// Проверяет LD-особенность: отрицание AND-группы переводится по де Моргану в OR отрицательных контактов.
    /// </summary>
    [Fact]
    public void Generate_NegatedAndExpression_AppliesDeMorganForLdContacts()
    {
        string actual = GenerateLd("X:=NOT (A AND B);");

        AssertLdContainsInOrder(
            actual,
            """
            _LD_OR
            _LD_OPERATOR : 2
            _LD_CONTACT
            A
            _EXPRESSION
            _NEGATIV
            _LD_CONTACT
            B
            _EXPRESSION
            _NEGATIV
            """);
    }

    #endregion

    #region Networks And Coils

    /// <summary>
    /// Проверяет, что обычное присваивание создает обычную катушку `_NO_SET`.
    /// </summary>
    [Fact]
    public void Generate_Assignment_ProducesNoSetCoil()
    {
        string actual = GenerateLd("X:=A;");

        AssertLdContainsInOrder(
            actual,
            """
            _OUTPUT
            _POSITIV
            _NO_SET
            X
            """);
    }

    /// <summary>
    /// Проверяет, что ветка `V := TRUE` внутри IF создает Set-катушку с положительным выходом.
    /// </summary>
    [Fact]
    public void Generate_IfThenTrue_ProducesSetNetwork()
    {
        string actual = GenerateLd("IF A THEN X:=TRUE; END_IF;");

        AssertNetworkCount(actual, 1);
        AssertLdContainsInOrder(
            actual,
            """
            _LD_CONTACT
            A
            _EXPRESSION
            _POSITIV
            """,
            """
            _OUTPUT
            _POSITIV
            _SET
            X
            """);
    }

    /// <summary>
    /// Проверяет, что ветка `V := FALSE` внутри IF создает Reset-поведение через отрицательный выход `_SET`.
    /// </summary>
    [Fact]
    public void Generate_IfThenFalse_ProducesResetNetwork()
    {
        string actual = GenerateLd("IF A THEN X:=FALSE; END_IF;");

        AssertNetworkCount(actual, 1);
        AssertLdContainsInOrder(
            actual,
            """
            _LD_CONTACT
            A
            _EXPRESSION
            _POSITIV
            """,
            """
            _OUTPUT
            _NEGATIV
            _SET
            X
            """);
    }

    /// <summary>
    /// Проверяет, что IF/ELSIF создает две отдельные LD-сети: установку и сброс state-переменной.
    /// </summary>
    [Fact]
    public void Generate_IfWithElsif_ProducesSetAndResetNetworks()
    {
        string actual = GenerateLd("IF A THEN X:=1; ELSIF B THEN X:=0; END_IF;");

        AssertNetworkCount(actual, 2);
        AssertLdContainsInOrder(
            actual,
            """
            _LD_CONTACT
            A
            _EXPRESSION
            _POSITIV
            """,
            """
            _OUTPUT
            _POSITIV
            _SET
            X
            """,
            """
            _LD_CONTACT
            B
            _EXPRESSION
            _POSITIV
            """,
            """
            _OUTPUT
            _NEGATIV
            _SET
            X
            """);
    }

    /// <summary>
    /// Проверяет расчет количества сетей для смешанной программы из присваиваний и IF/ELSIF.
    /// </summary>
    [Fact]
    public void Generate_MixedProgram_ReportsCorrectNetworksCount()
    {
        const string input = """
            X:=A;
            IF B THEN Y:=1; ELSIF C THEN Y:=0; END_IF;
            Z:=NOT D;
            """;

        string actual = GenerateLd(input);

        AssertNetworkCount(actual, 4);
    }

    #endregion

    #region Ltl Fragments

    /// <summary>
    /// Проверяет псевдооператорное присваивание `_V := V` как обычную LD-сеть с катушкой `_NO_SET`.
    /// </summary>
    [Fact]
    public void Generate_PseudoOperatorAssignment_ProducesNoSetCoilForPseudoVariable()
    {
        string actual = GenerateLd("_X:=X;");

        AssertNetworkCount(actual, 1);
        AssertLdContainsInOrder(
            actual,
            """
            _LD_CONTACT
            X
            _EXPRESSION
            _POSITIV
            """,
            """
            _NO_SET
            _X
            """);
    }

    /// <summary>
    /// Проверяет реальный LTL/ST-фрагмент: state-защелку, функциональное присваивание и псевдооператорный раздел.
    /// </summary>
    [Fact]
    public void Generate_RealLtlFragment_ProducesExpectedLdStructure()
    {
        const string input = """
            IF NOT _HtrErr AND NOT WTS AND (HtrTmr.Q OR _Htr AND _WTS) THEN HtrErr:=TRUE;
            ELSIF _HtrErr AND PBStop THEN HtrErr:=FALSE;
            END_IF;
            Fin:=HtrErr OR PBStop;
            _HtrErr:=HtrErr;
            _WTS:=WTS;
            """;

        string actual = GenerateLd(input);

        AssertNetworkCount(actual, 5);
        AssertLdContainsInOrder(
            actual,
            """
            _LD_AND
            _LD_OPERATOR : 3
            _LD_CONTACT
            _HtrErr
            _EXPRESSION
            _NEGATIV
            _LD_CONTACT
            WTS
            _EXPRESSION
            _NEGATIV
            _LD_OR
            _LD_OPERATOR : 2
            _LD_CONTACT
            HtrTmr.Q
            _EXPRESSION
            _POSITIV
            """,
            """
            _OUTPUT
            _POSITIV
            _SET
            HtrErr
            """,
            """
            _LD_CONTACT
            _HtrErr
            _EXPRESSION
            _POSITIV
            _LD_CONTACT
            PBStop
            _EXPRESSION
            _POSITIV
            """,
            """
            _OUTPUT
            _NEGATIV
            _SET
            HtrErr
            """,
            """
            _LD_OR
            _LD_OPERATOR : 2
            _LD_CONTACT
            HtrErr
            _EXPRESSION
            _POSITIV
            _LD_CONTACT
            PBStop
            _EXPRESSION
            _POSITIV
            """,
            """
            _NO_SET
            Fin
            """,
            """
            _LD_CONTACT
            HtrErr
            _EXPRESSION
            _POSITIV
            """,
            """
            _NO_SET
            _HtrErr
            """,
            """
            _LD_CONTACT
            WTS
            _EXPRESSION
            _POSITIV
            """,
            """
            _NO_SET
            _WTS
            """);
    }

    #endregion
}
