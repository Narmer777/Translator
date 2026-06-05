using Translator.Core.Ast;

namespace Translator.Core.CodeGen;

/// <summary>
/// Общий интерфейс для всех генераторов целевого кода.
/// </summary>
public interface ICodeGenerator
{
    string Generate(ProgramNode program);
}
