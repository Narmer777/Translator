namespace Translator.Core.Lexer
{

    /// <summary>
    /// Хранилище всех лексических констант и ключевых слов языка.
    /// </summary>
    public static class LexerConstants
    {
        public const char Underscore = '_';
        public const char Dot = '.';
        public const char Colon = ':';
        public const char Equal = '=';
        public const char LParen = '(';
        public const char RParen = ')';
        public const char Asterisk = '*';
        public const char SemiColon = ';';
        public const char NewLine = '\n';
        public const string Assign = ":=";
        public const string TrueLiteral = "1";
        public const string FalseLiteral = "0";

        public static readonly Dictionary<string, TokenType> Keywords = new(StringComparer.OrdinalIgnoreCase)
        {
            { "IF", TokenType.IF },
            { "THEN", TokenType.THEN },
            { "ELSIF", TokenType.ELSIF },
            { "END_IF", TokenType.ENDIF },
            { "AND", TokenType.AND },
            { "OR", TokenType.OR },
            { "NOT", TokenType.NOT },
            { "TRUE", TokenType.TRUE },
            { "FALSE", TokenType.FALSE }
        };
    }
}