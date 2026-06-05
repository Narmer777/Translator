namespace Translator.Core.Lexer
{
    public enum TokenType
    {
        IF,         // IF
        THEN,       // THEN
        ELSIF,      // ELSIF
        ENDIF,      // END_IF

        ASSIGN,     // :=
        AND,        // AND
        OR,         // OR
        NOT,        // NOT

        LPAREN,     // (
        RPAREN,     // )
        SEMI,       // ;
        ID,         // Идентификатор

        TRUE,       // TRUE (и '1')
        FALSE,      // FALSE (и '0')

        EOF,        // Конец файла
        ERROR       // Ошибка лексера
    }
}
