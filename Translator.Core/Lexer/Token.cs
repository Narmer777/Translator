namespace Translator.Core.Lexer
{
    public record Token(TokenType Type, string Value, int Line, int Column);
}
