namespace Dracocephalum.Fortrania.Syntax;

/// <summary>An operator that takes two operands.</summary>
public enum BinaryOperator
{
    /// <summary><c>+</c>.</summary>
    Add,

    /// <summary><c>-</c>.</summary>
    Subtract,

    /// <summary><c>*</c>.</summary>
    Multiply,

    /// <summary><c>/</c>.</summary>
    Divide,

    /// <summary><c>**</c>.</summary>
    Power,

    /// <summary><c>//</c>.</summary>
    Concatenate,

    /// <summary><c>.EQ.</c>.</summary>
    Equal,

    /// <summary><c>.NE.</c>.</summary>
    NotEqual,

    /// <summary><c>.LT.</c>.</summary>
    LessThan,

    /// <summary><c>.LE.</c>.</summary>
    LessOrEqual,

    /// <summary><c>.GT.</c>.</summary>
    GreaterThan,

    /// <summary><c>.GE.</c>.</summary>
    GreaterOrEqual,

    /// <summary><c>.AND.</c>.</summary>
    And,

    /// <summary><c>.OR.</c>.</summary>
    Or,

    /// <summary><c>.EQV.</c>.</summary>
    Equivalent,

    /// <summary><c>.NEQV.</c>.</summary>
    NotEquivalent,
}
