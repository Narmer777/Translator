namespace Translator.Core.CodeGen;

/// <summary>
/// Константы текстового формата LD-экспорта CoDeSys.
/// </summary>
public static class LdConstants
{
    public const string ProgramStart = "PROGRAM PLC_LD_PRG_TR";
    public const string LdBody = "_LD_BODY";
    public const string NetworksCount = "_NETWORKS :";

    public const string Network = "_NETWORK";
    public const string Comment = "_COMMENT";
    public const string EmptyString = "''";
    public const string EndComment = "_END_COMMENT";
    public const string LdAssign = "_LD_ASSIGN";
    public const string Expression = "_EXPRESSION";
    public const string Positiv = "_POSITIV";
    public const string Negativ = "_NEGATIV";
    public const string Empty = "_EMPTY";

    public const string EnableList = "ENABLELIST : 0";
    public const string EnableListEnd = "ENABLELIST_END";
    public const string OutputsCount = "_OUTPUTS :";
    public const string Output = "_OUTPUT";
    public const string NoSet = "_NO_SET";
    public const string Set = "_SET";

    public const string LdAnd = "_LD_AND";
    public const string LdOr = "_LD_OR";
    public const string LdOperator = "_LD_OPERATOR :";
    public const string LdContact = "_LD_CONTACT";

    public const string TrueValue = "TRUE";
    public const string FalseValue = "FALSE";
}
