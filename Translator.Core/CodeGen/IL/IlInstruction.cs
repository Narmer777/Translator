namespace Translator.Core.CodeGen;

/// <summary>
/// Все поддерживаемые стандартом МЭК 61131-3 инструкции IL.
/// </summary>
public enum IlOpCode
{
    NONE,
    LD, LDN,
    ST, S, R,
    AND, ANDN,
    OR, ORN,
    NOT
}

/// <summary>
/// Представляет одну строку сгенерированного IL-кода.
/// </summary>
public class IlInstruction
{
    public IlOpCode OpCode { get; }
    public string Operand { get; }
    public string Comment { get; }

    public IlInstruction(IlOpCode opCode, string operand = "", string comment = "")
    {
        OpCode = opCode;
        Operand = operand;
        Comment = comment;
    }

    /// <summary>
    /// Форматирует инструкцию в текст с отступами в строгом соответствии со стандартом МЭК 61131-3.
    /// </summary>
    public override string ToString()
    {
        string op = OpCode == IlOpCode.NONE ? "" : OpCode.ToString();
        string line;

        if (Operand == IlKeywords.ParenOpen)
        {
            line = $"{op + "(",-8}";
        }
        else if (Operand == IlKeywords.ParenClose)
        {
            line = $"{")",-8}";
        }
        else
        {
            line = $"{op,-8} {Operand,-15}";
        }

        if (!string.IsNullOrEmpty(Comment))
        {
            line = line.PadRight(24) + $" (* {Comment} *)";
        }

        return line.TrimEnd();
    }
}
